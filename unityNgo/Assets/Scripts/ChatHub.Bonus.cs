using System;
using Unity.Netcode;

// ChatHub의 일부: 보너스 요청과 응답
public partial class ChatHub
{
    const int BonusAmount = 100;

    // 화면이 구독하는 이벤트. 서버의 응답이 도착하면 알려 준다. (지급 여부, 받은 양)
    public static event Action<bool, int> OnBonusResult;

    // 화면이 부르는 함수
    public void RequestBonus() { if (IsSpawned) ClaimBonusRpc(); }

    // 요청: 클라이언트 → 서버. 담긴 값이 없다. 누가 보냈는지는 서버가 알고, 얼마를 줄지는 서버가 정한다.
    [Rpc(SendTo.Server)]
    void ClaimBonusRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        int i = IndexOf(sender);
        if (i < 0) return;

        UserState u = users[i];
        bool ok = u.Gold == 0;                  // 골드가 0이면 아직 받지 않은 것이다
        if (ok)
        {
            u.Gold = BonusAmount;
            users[i] = u;                       // 골드는 모두에게 보이는 상태이다
        }

        // 응답: 서버 → 요청한 사람에게만
        BonusResultRpc(ok, ok ? BonusAmount : 0, RpcTarget.Single(sender, RpcTargetUse.Temp));
    }

    // 응답: 서버만 부를 수 있고, 호출할 때 정한 한 사람에게만 전달된다.
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void BonusResultRpc(bool ok, int amount, RpcParams rpcParams = default)
    {
        OnBonusResult?.Invoke(ok, amount);
    }
}
