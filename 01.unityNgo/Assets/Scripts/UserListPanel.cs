using Unity.Netcode;
using UnityEngine;

// 접속자 목록 화면. NetworkList를 매 프레임 읽어서 그린다. 서버와 클라이언트가 같은 코드를 쓴다.
public class UserListPanel : MonoBehaviour
{
    void OnGUI()
    {
        UiKit.Apply();
        ChatHub hub = ChatHub.Instance;
        if (hub == null) return;

        GUILayout.BeginArea(new Rect(440, 10, 480, 160), GUI.skin.box);
        GUILayout.Label($"접속자 {hub.Users.Count}명");
        DrawRow("ID", "이름", "채널", "HP", "상태", "골드");

        ulong myId = NetworkManager.Singleton.LocalClientId;
        for (int i = 0; i < hub.Users.Count; i++)
        {
            UserState u = hub.Users[i];
            DrawRow(u.Id.ToString(),
                    u.Name.ToString() + (u.Id == myId ? " (나)" : ""),
                    u.Channel.Length == 0 ? "로비" : u.Channel.ToString(),
                    u.Hp.ToString(),
                    u.Ready ? "준비" : "대기",
                    u.Gold.ToString());
        }
        GUILayout.EndArea();
    }

    static void DrawRow(string id, string userName, string channel, string hp, string state, string gold)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(id, GUILayout.Width(30));
        GUILayout.Label(userName, GUILayout.Width(140));
        GUILayout.Label(channel, GUILayout.Width(100));
        GUILayout.Label(hp, GUILayout.Width(50));
        GUILayout.Label(state, GUILayout.Width(50));
        GUILayout.Label(gold, GUILayout.Width(50));
        GUILayout.EndHorizontal();
    }
}
