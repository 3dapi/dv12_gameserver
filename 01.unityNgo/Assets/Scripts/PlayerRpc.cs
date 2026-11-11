using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// RPC 연습: 핑(클라이언트 → 서버 → 그 클라이언트)과 외치기(클라이언트 → 서버 → 모두)
public class PlayerRpc : NetworkBehaviour
{
    public static PlayerRpc Local;                                       // 이 컴퓨터에서 내가 조종하는 플레이어
    public static readonly List<string> ShoutLog = new List<string>();   // 이 컴퓨터가 받은 외침 목록

    public float LastRttMs { get; private set; } = -1f;                  // 마지막으로 잰 왕복 시간(ms). 아직 없으면 -1

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        Local = this;
        ShoutLog.Clear();
    }

    public override void OnNetworkDespawn()
    {
        if (Local == this) Local = null;
    }

    // ---------- 1) 핑: 클라이언트 → 서버 → 그 클라이언트 ----------

    public void SendPing() => PingRpc(Time.realtimeSinceStartup);

    // 호출은 보내는 쪽에서 하고, 본문은 서버에서 실행된다. 이 오브젝트의 소유자만 호출할 수 있다.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void PingRpc(float sentAt, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;        // 보낸 클라이언트의 ClientId
        Debug.Log($"[서버] Ping 받음: 보낸 클라이언트 {sender}");
        PongRpc(sentAt, RpcTarget.Single(sender, RpcTargetUse.Temp));   // 보낸 클라이언트에게만 되돌린다
    }

    // 대상은 호출할 때 정한다(SpecifiedInParams). 본문은 그 대상의 컴퓨터에서 실행된다.
    [Rpc(SendTo.SpecifiedInParams)]
    void PongRpc(float sentAt, RpcParams rpcParams = default)
    {
        LastRttMs = (Time.realtimeSinceStartup - sentAt) * 1000f;
    }

    // ---------- 2) 외치기: 클라이언트 → 서버 → 모두 ----------

    public void Shout(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        ShoutToServerRpc(text);
    }

    // 클라이언트 → 서버. 이 오브젝트의 소유자만 호출할 수 있으므로, 이 오브젝트의 주인이 곧 보낸 사람이다.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void ShoutToServerRpc(string text)
    {
        if (text.Length > 60) text = text.Substring(0, 60);        // 서버가 한 번 더 검사한다
        ShoutToAllRpc(GetComponent<PlayerProfile>().PlayerName, text);
    }

    // 서버 → 모든 클라이언트(호스트 포함). 클라이언트가 직접 부르지 못하도록 서버 전용으로 막는다.
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    void ShoutToAllRpc(string who, string text)
    {
        ShoutLog.Add($"{who}: {text}");
        if (ShoutLog.Count > 8) ShoutLog.RemoveAt(0);
    }

    // 시험용: 클라이언트가 서버 전용 RPC를 직접 부르면 어떻게 되는지 본다.
    public void FakeShout()
    {
        ShoutToAllRpc("서버", "클라이언트가 서버인 척 보낸 메시지");
    }
}
