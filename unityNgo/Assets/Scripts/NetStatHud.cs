using Unity.Netcode;
using UnityEngine;

// 전송 계층(Unity Transport)이 재는 왕복 시간(RTT)을 화면 오른쪽 아래에 보여 주는 도구.
// 이름이 Panel로 끝나지 않으므로 ViewToggle의 '패널 숨기기'를 눌러도 남는다.
public class NetStatHud : MonoBehaviour
{
    void OnGUI()
    {
        UiKit.Apply();
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;

        NetworkTransport transport = nm.NetworkConfig.NetworkTransport;
        string text;
        if (nm.IsServer)
        {
            // 서버(호스트)는 접속한 클라이언트마다 읽는다. 호스트 자신은 서버와 같은 프로그램이라 건너뛴다.
            text = "";
            foreach (ulong id in nm.ConnectedClientsIds)
                if (!(nm.IsHost && id == nm.LocalClientId))
                    text += $"클라이언트 {id}: {transport.GetCurrentRtt(id)} ms\n";
            if (text.Length == 0) text = "접속한 클라이언트 없음";
        }
        else
        {
            text = $"서버까지: {transport.GetCurrentRtt(NetworkManager.ServerClientId)} ms";
        }

        GUILayout.BeginArea(new Rect(Screen.width - 350, Screen.height - 150, 340, 115), GUI.skin.box);
        GUILayout.Label("전송 계층 RTT");
        GUILayout.Label(text.TrimEnd());
        GUILayout.EndArea();
    }
}
