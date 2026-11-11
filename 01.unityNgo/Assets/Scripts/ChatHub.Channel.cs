using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

// ChatHub의 일부: 채널 만들기, 들어가기, 나가기
// 채널을 따로 저장하지 않는다. 접속자 목록(users)의 Channel 칸이 곧 채널이다.
public partial class ChatHub
{
    const int MaxChannelNameLength = 16;
    const string LobbyName = "로비";      // 화면에서 "어느 채널에도 없음"을 가리키는 이름. 채널 이름으로는 쓸 수 없다.

    // ---------- 화면이 부르는 함수 ----------

    public void CreateChannel(string channelName) { if (IsSpawned) CreateChannelRpc(channelName); }
    public void JoinChannel(string channelName)   { if (IsSpawned) JoinChannelRpc(channelName); }
    public void LeaveChannel()                    { if (IsSpawned) LeaveChannelRpc(); }

    // 만들어진 채널의 이름과 인원 수. 접속자 목록에서 계산하므로 클라이언트도 부를 수 있다.
    public Dictionary<string, int> GetChannels()
    {
        var result = new Dictionary<string, int>();
        for (int i = 0; i < users.Count; i++)
        {
            if (users[i].Channel.Length == 0) continue;         // 로비에 있는 사람은 채널이 아니다
            string channelName = users[i].Channel.ToString();
            result.TryGetValue(channelName, out int count);
            result[channelName] = count + 1;
        }
        return result;
    }

    // ---------- 서버에서 실행되는 RPC 본문 ----------

    [Rpc(SendTo.Server)]
    void CreateChannelRpc(string channelName, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!CheckChannelName(sender, ref channelName)) return;

        if (ChannelExists(ToFixed(channelName)))
        {
            ToOne(sender, $"이미 있는 채널이다: {channelName}");
            return;
        }
        MoveToChannel(sender, channelName);
    }

    [Rpc(SendTo.Server)]
    void JoinChannelRpc(string channelName, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!CheckChannelName(sender, ref channelName)) return;

        if (!ChannelExists(ToFixed(channelName)))
        {
            ToOne(sender, $"없는 채널이다: {channelName}");
            return;
        }
        MoveToChannel(sender, channelName);
    }

    [Rpc(SendTo.Server)]
    void LeaveChannelRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!TryGetUser(sender, out UserState me)) return;

        if (me.Channel.Length == 0)
        {
            ToOne(sender, "채널에 들어가 있지 않다.");
            return;
        }
        MoveToChannel(sender, "");                              // 빈 문자열은 로비
    }

    // ---------- 서버 안에서 쓰는 도우미 ----------

    // 채널 이름을 다듬고 검사한다. 통과하면 true를 돌려주고, channelName은 다듬어진 이름이 된다.
    bool CheckChannelName(ulong sender, ref string channelName)
    {
        if (!TryGetUser(sender, out _)) return false;           // 목록에 없는 사람의 요청은 버린다

        channelName = (channelName ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim();
        if (channelName.Length == 0 || channelName.Length > MaxChannelNameLength || channelName == LobbyName)
        {
            ToOne(sender, $"채널 이름은 1~{MaxChannelNameLength}자이고 '{LobbyName}'는 쓸 수 없다.");
            return false;
        }
        return true;
    }

    // 그 이름을 가진 접속자가 한 명이라도 있으면 채널이 있는 것이다.
    bool ChannelExists(FixedString64Bytes channel)
    {
        for (int i = 0; i < users.Count; i++)
            if (users[i].Channel.Equals(channel)) return true;
        return false;
    }

    // 접속자를 다른 채널로 옮기고 관련된 사람들에게 알린다. 로비로 보낼 때는 빈 문자열을 넘긴다.
    void MoveToChannel(ulong clientId, string newChannel)
    {
        int i = IndexOf(clientId);
        if (i < 0) return;

        UserState u = users[i];
        FixedString64Bytes before = u.Channel;
        FixedString64Bytes after = ToFixed(newChannel);

        if (before.Equals(after))
        {
            ToOne(clientId, "이미 그 채널에 있다.");
            return;
        }

        u.Channel = after;
        users[i] = u;                                           // 꺼내서 고친 뒤 다시 대입해야 변화가 전달된다

        if (before.Length > 0)                                  // 나온 채널에 남은 사람들에게
            ToGroup(MembersOf(before), $"* {u.Name} 님이 [{before}] 채널에서 나갔다");

        if (after.Length > 0)                                   // 들어간 채널의 사람들에게(들어온 본인 포함)
            ToGroup(MembersOf(after), $"* {u.Name} 님이 [{after}] 채널에 들어왔다");
        else
            ToOne(clientId, $"[{before}] 채널에서 나왔다. 지금은 로비이다.");
    }
}
