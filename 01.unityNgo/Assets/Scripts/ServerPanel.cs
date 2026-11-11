using Unity.Netcode;
using UnityEngine;

// 서버(호스트) 화면 아래쪽의 접속자 목록과 내보내기 버튼
public class ServerPanel : MonoBehaviour
{
    ulong kickId = ulong.MaxValue;         // 내보내기로 정한 클라이언트(없으면 MaxValue)

    // 접속을 끊는 일은 화면을 그리는 도중이 아니라 Update에서 한다.
    void Update()
    {
        if (kickId == ulong.MaxValue) return;
        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null && nm.IsServer && nm.ConnectedClients.ContainsKey(kickId))
            nm.DisconnectClient(kickId, "서버가 내보냈다.");
        kickId = ulong.MaxValue;
    }

    void OnGUI()
    {
        UiKit.Apply();
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        GUILayout.BeginArea(new Rect(440, Screen.height - 150, 420, 140), GUI.skin.box);
        GUILayout.Label($"접속자 {nm.ConnectedClientsIds.Count}명 (정원 {ApprovalGate.MaxPlayers}명)");
        foreach (ulong id in nm.ConnectedClientsIds)
        {
            ApprovalGate.Names.TryGetValue(id, out string playerName);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{id}: {playerName}");
            if (id != NetworkManager.ServerClientId && GUILayout.Button("내보내기", GUILayout.Width(100)))
                kickId = id;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndArea();
    }
}
