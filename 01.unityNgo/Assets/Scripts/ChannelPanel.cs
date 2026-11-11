using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 채널 화면: 채널 이름을 쓰고 만들기, 입장, 나가기를 누른다. 만들어진 채널과 인원 수를 보여 준다.
public class ChannelPanel : MonoBehaviour
{
    string channelName = "";
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
        if (!hub.TryGetUser(nm.LocalClientId, out UserState me)) return;    // 접속자가 아니면(Server 모드) 그리지 않는다

        GUILayout.BeginArea(new Rect(570, 175, 350, 185), GUI.skin.box);

        GUILayout.Label("내 채널: " + (me.Channel.Length == 0 ? "로비" : me.Channel.ToString()));
        channelName = GUILayout.TextField(channelName, 16);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("만들기")) { string n = channelName; pending = () => hub.CreateChannel(n); }
        if (GUILayout.Button("입장"))   { string n = channelName; pending = () => hub.JoinChannel(n); }
        if (GUILayout.Button("나가기")) pending = hub.LeaveChannel;
        GUILayout.EndHorizontal();

        var parts = new List<string>();
        foreach (KeyValuePair<string, int> channel in hub.GetChannels())
            parts.Add($"{channel.Key}({channel.Value}명)");
        GUILayout.Label(parts.Count == 0 ? "만들어진 채널이 없다" : "만들어진 채널: " + string.Join(", ", parts));

        GUILayout.EndArea();
    }
}
