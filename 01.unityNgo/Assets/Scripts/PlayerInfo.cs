using Unity.Netcode;
using UnityEngine;

// 플레이어 오브젝트가 어디서 어떻게 만들어지는지 Console에 출력한다.
public class PlayerInfo : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        string where = IsHost ? "Host" : IsServer ? "Server" : "Client";
        Debug.Log($"[{where}] 스폰: NetworkObjectId={NetworkObjectId}, 소유자={OwnerClientId}, " +
                  $"IsOwner={IsOwner}, IsLocalPlayer={IsLocalPlayer}");
    }

    public override void OnNetworkDespawn()
    {
        string where = IsHost ? "Host" : IsServer ? "Server" : "Client";
        Debug.Log($"[{where}] 디스폰: NetworkObjectId={NetworkObjectId}, 소유자={OwnerClientId}");
    }
}
