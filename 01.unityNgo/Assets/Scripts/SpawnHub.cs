using Unity.Netcode;
using UnityEngine;

// 서버가 아이템을 만들고, 없애고, 소유자를 바꾸는 서비스. ChatHub처럼 씬에 미리 놓는 서버 소유 오브젝트이다.
public class SpawnHub : NetworkBehaviour
{
    public const int MaxItems = 8;                  // 한 번에 존재할 수 있는 아이템 수

    public static SpawnHub Instance { get; private set; }

    [SerializeField] NetworkObject itemPrefab;      // Inspector에서 Item 프리팹을 연결한다

    public override void OnNetworkSpawn()   { Instance = this; }
    public override void OnNetworkDespawn() { if (Instance == this) Instance = null; }

    // ---------- 화면이 부르는 함수 ----------

    public void RequestSpawn()   { if (IsSpawned) SpawnItemRpc(); }
    public void RequestDespawn() { if (IsSpawned) DespawnItemRpc(); }
    public void RequestPass()    { if (IsSpawned) PassItemRpc(); }

    // 시험용: 서버에 알리지 않고 이 컴퓨터에서만 Instantiate한다. 스폰하지 않았으므로 다른 컴퓨터에는 나타나지 않는다.
    public void SpawnLocalOnly()
    {
        NetworkObject local = Instantiate(itemPrefab, RandomPosition(), Quaternion.identity);
        Destroy(local.gameObject, 5f);              // 5초 뒤에 저절로 지운다
    }

    // ---------- 서버에서 실행되는 RPC 본문 ----------

    [Rpc(SendTo.Server)]
    void SpawnItemRpc(RpcParams rpcParams = default)
    {
        if (Item.All.Count >= MaxItems) return;     // 요청이 아무리 와도 서버가 정한 한도를 넘지 않는다

        ulong sender = rpcParams.Receive.SenderClientId;
        NetworkObject item = Instantiate(itemPrefab, RandomPosition(), Quaternion.identity);
        item.SpawnWithOwnership(sender);            // 서버가 스폰하면 모든 클라이언트에 복제된다. 소유자는 요청한 사람이다.
    }

    [Rpc(SendTo.Server)]
    void DespawnItemRpc(RpcParams rpcParams = default)
    {
        Item item = LastOwnedBy(rpcParams.Receive.SenderClientId);   // 요청한 사람이 소유한 아이템만 없앨 수 있다
        if (item != null) item.NetworkObject.Despawn();              // 제거도 서버만 한다
    }

    [Rpc(SendTo.Server)]
    void PassItemRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        Item item = LastOwnedBy(sender);
        if (item == null) return;

        ulong next = NextClientAfter(sender);
        if (next != sender) item.NetworkObject.ChangeOwnership(next);
    }

    // ---------- 서버 안에서 쓰는 도우미 ----------

    // 플레이어 캡슐이 서 있는 줄(z = 0)을 피해서 그 앞쪽 바닥에 놓는다.
    static Vector3 RandomPosition()
    {
        return new Vector3(Random.Range(-8f, 8f), 0.5f, Random.Range(2f, 6f));
    }

    // clientId가 소유한 아이템 중 가장 나중에 만들어진 것
    static Item LastOwnedBy(ulong clientId)
    {
        for (int i = Item.All.Count - 1; i >= 0; i--)
            if (Item.All[i].OwnerClientId == clientId) return Item.All[i];
        return null;
    }

    // 접속해 있는 사람들을 접속 순서대로 놓았을 때 clientId 다음 사람. 마지막 사람의 다음은 처음 사람이다.
    ulong NextClientAfter(ulong clientId)
    {
        var ids = NetworkManager.ConnectedClientsIds;
        for (int i = 0; i < ids.Count; i++)
            if (ids[i] == clientId) return ids[(i + 1) % ids.Count];
        return clientId;
    }
}
