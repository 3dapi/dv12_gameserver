using Unity.Netcode;
using UnityEngine;

// NetPlayer 프리팹에 붙인다. 서버(호스트)에서만 일한다.
// 이 캡슐을 가까이 있는 접속자의 컴퓨터에만 존재하게 한다.
public class ProximityVisibility : NetworkBehaviour
{
    [SerializeField] float viewDistance = 3f;       // 이 거리(미터) 안에 있는 접속자에게만 보인다

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        NetworkObject.CheckObjectVisibility += IsNear;                 // 새 접속자나 NetworkShow가 물을 때 서버가 부른다
        NetworkManager.NetworkTickSystem.Tick += OnTick;               // 틱마다 한 번씩 검사한다
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        NetworkObject.CheckObjectVisibility -= IsNear;
        NetworkManager.NetworkTickSystem.Tick -= OnTick;
    }

    // clientId의 플레이어가 이 캡슐 가까이 있는가
    bool IsNear(ulong clientId)
    {
        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) || client.PlayerObject == null)
            return true;                                               // 플레이어가 아직 없으면 일단 보이게 한다
        return Vector3.Distance(client.PlayerObject.transform.position, transform.position) <= viewDistance;
    }

    void OnTick()
    {
        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            // 주인과 서버(호스트)에게는 항상 보인다.
            if (clientId == OwnerClientId || clientId == NetworkManager.ServerClientId) continue;

            bool near = IsNear(clientId);
            bool visible = NetworkObject.IsNetworkVisibleTo(clientId);
            if (near && !visible) NetworkObject.NetworkShow(clientId);
            else if (!near && visible) NetworkObject.NetworkHide(clientId);
        }
    }
}
