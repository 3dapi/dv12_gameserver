using System.Collections.Generic;
using UnityEngine;

// 채팅 로그 창. ChatHub가 알려 주는 줄을 모아서 보여 준다.
public class ChatLogPanel : MonoBehaviour
{
    const int MaxLines = 200;                       // 이보다 많아지면 오래된 줄부터 버린다

    readonly List<string> lines = new List<string>();
    Vector2 scroll;
    bool follow;                                    // 새 줄이 오면 맨 아래로 내린다
    bool wasConnected;

    void OnEnable()  { ChatHub.OnLog += AddLine; }
    void OnDisable() { ChatHub.OnLog -= AddLine; }

    void AddLine(string line)
    {
        if (lines.Count >= MaxLines) lines.RemoveAt(0);
        lines.Add(line);
        follow = true;
    }

    void Update()
    {
        bool connected = ChatHub.Instance != null;
        if (wasConnected && !connected) lines.Clear();      // 접속이 끝나면 그 접속의 대화를 지운다
        wasConnected = connected;
    }

    void OnGUI()
    {
        UiKit.Apply();
        if (ChatHub.Instance == null) return;

        GUILayout.BeginArea(new Rect(10, 175, 550, Screen.height - 486), GUI.skin.box);
        if (follow) scroll.y = Mathf.Infinity;
        scroll = GUILayout.BeginScrollView(scroll);
        foreach (string line in lines) GUILayout.Label(line);
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        if (Event.current.type == EventType.Repaint) follow = false;
    }
}
