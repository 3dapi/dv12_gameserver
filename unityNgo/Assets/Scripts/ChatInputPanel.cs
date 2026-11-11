using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 채팅 입력 창: 보낼 곳(전체, 채널, 귓속말)을 고르고 한 줄을 보낸다. 서버(호스트)에는 공지 입력칸이 더 나온다.
public class ChatInputPanel : MonoBehaviour
{
    const int MaxLength = 200;
    static readonly string[] Modes = { "전체", "채널", "귓속말" };

    int mode;                               // 0 전체, 1 채널, 2 귓속말
    ulong whisperTo = ulong.MaxValue;       // 귓속말을 받을 사람의 ClientId
    string input = "";
    string announce = "";
    Action pending;                         // 눌린 버튼이 시킨 일. OnGUI는 기록만 하고 Update에서 실행한다

    void Update()
    {
        if (pending == null) return;
        Action job = pending;
        pending = null;
        job();
    }

    void OnGUI()
    {
        UiKit.Apply();
        ChatHub hub = ChatHub.Instance;
        NetworkManager nm = NetworkManager.Singleton;
        if (hub == null || nm == null) return;

        GUILayout.BeginArea(new Rect(10, Screen.height - 305, 550, 150), GUI.skin.box);

        if (hub.TryGetUser(nm.LocalClientId, out _))
            DrawChatInput(hub, nm.LocalClientId);
        else
            GUILayout.Label("이 실행은 접속자가 아니다(Server 모드). 공지만 보낼 수 있다.");

        if (nm.IsServer) DrawAnnounce(hub);
        GUILayout.EndArea();
    }

    void DrawChatInput(ChatHub hub, ulong myId)
    {
        int picked = GUILayout.Toolbar(mode, Modes);
        if (picked != mode) pending = () => mode = picked;      // 화면 구성이 달라지는 값이므로 Update에서 바꾼다
        if (mode == 2) DrawWhisperTarget(hub, myId);

        // Enter 키는 입력 칸을 그리기 전에 확인한다.
        Event e = Event.current;
        bool enter = e.type == EventType.KeyDown
                     && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                     && GUI.GetNameOfFocusedControl() == "ChatInput";

        GUILayout.BeginHorizontal();
        GUI.SetNextControlName("ChatInput");
        input = GUILayout.TextField(input, MaxLength);
        bool clicked = GUILayout.Button("보내기", GUILayout.Width(80));
        GUILayout.EndHorizontal();

        if (clicked || enter)
        {
            string text = input;
            int sendMode = mode;
            ulong target = whisperTo;
            input = "";
            pending = () => Send(hub, sendMode, target, text);
        }
    }

    // 나를 뺀 접속자의 이름을 버튼 줄로 보여 주고 한 명을 고르게 한다.
    void DrawWhisperTarget(ChatHub hub, ulong myId)
    {
        var ids = new List<ulong>();
        var names = new List<string>();
        for (int i = 0; i < hub.Users.Count; i++)
        {
            UserState u = hub.Users[i];
            if (u.Id == myId) continue;
            ids.Add(u.Id);
            names.Add(u.Name.ToString());
        }

        if (ids.Count == 0)
        {
            GUILayout.Label("귓속말을 보낼 다른 접속자가 없다");
            return;
        }

        int selected = Mathf.Max(0, ids.IndexOf(whisperTo));    // 고른 사람이 나갔으면 첫 번째 사람
        selected = GUILayout.SelectionGrid(selected, names.ToArray(), Mathf.Min(names.Count, 4));
        whisperTo = ids[selected];
    }

    void DrawAnnounce(ChatHub hub)
    {
        GUILayout.BeginHorizontal();
        announce = GUILayout.TextField(announce, MaxLength);
        if (GUILayout.Button("공지", GUILayout.Width(80)))
        {
            string text = announce;
            announce = "";
            pending = () => hub.Announce(text);
        }
        GUILayout.EndHorizontal();
    }

    static void Send(ChatHub hub, int sendMode, ulong target, string text)
    {
        if (text.Trim().Length == 0) return;
        if (sendMode == 0) hub.SendChat(text);
        else if (sendMode == 1) hub.SendChannelChat(text);
        else hub.SendWhisper(target, text);
    }
}
