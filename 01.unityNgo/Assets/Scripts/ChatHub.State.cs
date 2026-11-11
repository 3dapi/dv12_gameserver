using Unity.Netcode;
using UnityEngine;

// ChatHub의 일부: 접속자의 체력과 준비 상태
public partial class ChatHub
{
    const int MaxHp = 100;

    // ---------- 화면이 부르는 함수 ----------

    public void ChangeHp(int delta)  { if (IsSpawned) ChangeHpRpc(delta); }
    public void SetReady(bool ready) { if (IsSpawned) SetReadyRpc(ready); }

    // 시험용: 이 컴퓨터에서 접속자 목록에 직접 쓰면 어떻게 되는지 본다.
    public void TryWriteDirectly()
    {
        int i = IndexOf(NetworkManager.LocalClientId);
        if (i < 0) return;

        UserState u = users[i];
        u.Hp = 1;
        users[i] = u;           // 서버(호스트)에서는 바뀌고, 클라이언트에서는 거부된다
    }

    // ---------- 서버에서 실행되는 RPC 본문 ----------

    [Rpc(SendTo.Server)]
    void ChangeHpRpc(int delta, RpcParams rpcParams = default)
    {
        int i = IndexOf(rpcParams.Receive.SenderClientId);
        if (i < 0) return;

        delta = Mathf.Clamp(delta, -MaxHp, MaxHp);          // 요청값의 범위를 먼저 제한한다
        UserState u = users[i];
        u.Hp = Mathf.Clamp(u.Hp + delta, 0, MaxHp);         // 더한 결과도 0~MaxHp 안에 둔다
        users[i] = u;

        Debug.Log($"[서버] {u.Name} 체력 {u.Hp}");
    }

    [Rpc(SendTo.Server)]
    void SetReadyRpc(bool ready, RpcParams rpcParams = default)
    {
        int i = IndexOf(rpcParams.Receive.SenderClientId);
        if (i < 0) return;

        UserState u = users[i];
        u.Ready = ready;
        users[i] = u;

        Debug.Log($"[서버] {u.Name} {(ready ? "준비" : "대기")}");
    }
}
