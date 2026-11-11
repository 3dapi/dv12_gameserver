using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

// 접속 승인: 접속 코드, 정원, 이름 중복을 검사하고 플레이어가 생길 위치를 정한다. (서버에서 실행된다)
public class ApprovalGate : MonoBehaviour
{
    public const string AccessCode = "1234";   // 교재용 접속 코드. 실제 서비스에서는 이렇게 코드에 적어 두지 않는다.
    public const int MaxPlayers = 4;           // 호스트를 포함한 최대 접속자 수

    // 승인된 접속자의 ClientId → 이름. 서버(호스트)에서만 채워진다.
    public static readonly Dictionary<ulong, string> Names = new Dictionary<ulong, string>();

    NetworkManager nm;

    void Start()
    {
        nm = NetworkManager.Singleton;
        nm.NetworkConfig.ConnectionApproval = true;       // 서버와 클라이언트 양쪽이 켜야 한다
        nm.ConnectionApprovalCallback = Approve;          // 하나만 넣을 수 있으므로 += 가 아니라 = 이다
        nm.OnClientDisconnectCallback += OnClientDisconnected;
        nm.OnServerStopped += OnServerStopped;
    }

    void OnDestroy()
    {
        if (nm == null) return;
        nm.OnClientDisconnectCallback -= OnClientDisconnected;
        nm.OnServerStopped -= OnServerStopped;
    }

    void OnClientDisconnected(ulong clientId) { Names.Remove(clientId); }
    void OnServerStopped(bool wasClient) { Names.Clear(); }

    void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        ulong clientId = request.ClientNetworkId;

        // 클라이언트가 접속 요청에 실어 보낸 "이름|코드"를 읽는다.
        string[] parts = Encoding.UTF8.GetString(request.Payload ?? new byte[0]).Split('|');
        string playerName = parts.Length > 0 ? parts[0].Trim() : "";
        string code = parts.Length > 1 ? parts[1] : "";

        // 호스트 자신(ClientId 0)은 거절할 수 없다. 이름이 비어 있으면 기본 이름을 쓴다.
        bool isHost = clientId == NetworkManager.ServerClientId;
        if (isHost && playerName.Length == 0) playerName = $"Player {clientId}";

        string reason = isHost ? null : Validate(playerName, code);
        if (reason != null)
        {
            response.Approved = false;
            response.Reason = reason;          // 접속하려던 클라이언트의 NetworkManager.DisconnectReason으로 전달된다
            Debug.Log($"[서버] 접속 거절: '{playerName}' ({reason})");
            return;
        }

        Names[clientId] = playerName;
        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Position = SpawnPoint(clientId);
        response.Rotation = Quaternion.identity;
        Debug.Log($"[서버] 접속 승인: '{playerName}' (ClientId {clientId})");
    }

    // 거절 사유를 돌려준다. 통과하면 null이다.
    string Validate(string playerName, string code)
    {
        if (code != AccessCode) return "접속 코드가 틀렸다.";
        if (playerName.Length == 0) return "이름을 입력해야 한다.";
        if (playerName.Length > 16) return "이름은 16자까지 쓸 수 있다.";     // 입력칸은 16자로 막지만, 고친 클라이언트는 더 길게 보낼 수 있다
        if (Names.Count >= MaxPlayers) return $"정원({MaxPlayers}명)이 찼다.";
        foreach (KeyValuePair<ulong, string> pair in Names)
            if (pair.Value == playerName) return "이미 사용 중인 이름이다.";
        return null;
    }

    static Vector3 SpawnPoint(ulong clientId) => new Vector3((int)(clientId % 8) * 2f - 7f, 1f, 0f);
}
