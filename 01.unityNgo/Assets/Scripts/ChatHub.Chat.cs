using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// ChatHub의 일부: 채팅 보내기(전체, 채널, 귓속말)와 공지
public partial class ChatHub
{
    const int MaxChatLength = 200;      // 한 줄의 최대 글자 수

    // ---------- 화면이 부르는 함수 ----------

    public void SendChat(string text)                       { if (IsSpawned) SendChatRpc(text); }
    public void SendChannelChat(string text)                { if (IsSpawned) SendChannelChatRpc(text); }
    public void SendWhisper(ulong targetId, string text)    { if (IsSpawned) SendWhisperRpc(targetId, text); }

    // 서버(호스트)가 모든 접속자에게 공지를 보낸다.
    public void Announce(string text)
    {
        if (!IsServer) return;
        text = text.Trim();
        if (text.Length == 0) return;
        ToAll("[공지] " + text);
    }

    // ---------- 서버에서 실행되는 RPC 본문 ----------

    // 전체 채팅
    [Rpc(SendTo.Server)]
    void SendChatRpc(string text, RpcParams rpcParams = default)
    {
        if (!CheckChat(rpcParams.Receive.SenderClientId, ref text, out UserState me)) return;
        ToAll($"{me.Name}: {text}");
    }

    // 채널 채팅: 보낸 사람이 들어가 있는 채널의 접속자에게만 전달한다
    [Rpc(SendTo.Server)]
    void SendChannelChatRpc(string text, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!CheckChat(sender, ref text, out UserState me)) return;
        if (me.Channel.Length == 0)
        {
            ToOne(sender, "채널에 들어가 있지 않다. 먼저 채널에 입장한다.");
            return;
        }

        ToGroup(MembersOf(me.Channel), $"[{me.Channel}] {me.Name}: {text}");
    }

    // 귓속말: 받는 사람과 보낸 사람에게만 전달한다
    [Rpc(SendTo.Server)]
    void SendWhisperRpc(ulong targetId, string text, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!CheckChat(sender, ref text, out UserState me)) return;
        if (targetId == sender || !TryGetUser(targetId, out UserState target))
        {
            ToOne(sender, "귓속말을 받을 사람이 올바르지 않다.");
            return;
        }

        var members = new List<ulong> { sender, targetId };
        ToGroup(members, $"[귓속말] {me.Name} → {target.Name}: {text}");
    }

    // 채팅 요청을 검사한다. 통과하면 true를 돌려주고, text는 다듬어진 문장이 된다.
    bool CheckChat(ulong sender, ref string text, out UserState me)
    {
        if (!TryGetUser(sender, out me)) return false;      // 목록에 없는 사람의 요청은 버린다

        text = (text ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim();   // 줄바꿈으로 가짜 줄을 만들지 못하게 한다
        if (text.Length == 0) return false;
        if (text.Length > MaxChatLength) text = text.Substring(0, MaxChatLength);
        return true;
    }
}
