using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// 채팅 서버 기능을 맡는 오브젝트. 씬에 하나만 두고 서버가 소유한다.
// 기능이 많아서 여러 파일로 나눈다(partial). 이 파일은 접속자 목록과 입장·퇴장을 맡는다.
public partial class ChatHub : NetworkBehaviour
{
    public static ChatHub Instance { get; private set; }

    // 화면이 구독하는 이벤트. 서버가 보낸 한 줄이 도착하면 알려 준다. ChatHub는 화면이 무엇인지 모른다.
    public static event Action<string> OnLog;

    // 접속자 목록. 서버만 고치고 모든 접속자가 읽는다.
    NetworkList<UserState> users;
    public NetworkList<UserState> Users => users;

    void Awake()
    {
        users = new NetworkList<UserState>();       // NetworkList는 Awake에서 만든다
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
        if (!IsServer) return;

        users.Clear();                              // 서버를 다시 시작했을 때 지난 접속자를 비운다
        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            AddUser(clientId, false);               // 호스트 자신처럼 이미 접속해 있는 사람

        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
        if (NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // ---------- 서버에서만 실행되는 부분: 입장과 퇴장 ----------

    void OnClientConnected(ulong clientId) => AddUser(clientId, true);

    void OnClientDisconnected(ulong clientId)
    {
        if (!IsSpawned || NetworkManager.ShutdownInProgress) return;    // 서버를 끄는 중이면 정리할 필요가 없다

        int i = IndexOf(clientId);
        if (i < 0) return;

        string leftName = users[i].Name.ToString();
        users.RemoveAt(i);
        ToAll($"* {leftName} 퇴장");
    }

    void AddUser(ulong clientId, bool announce)
    {
        if (IndexOf(clientId) >= 0) return;         // 이미 등록되어 있으면 건너뛴다

        ApprovalGate.Names.TryGetValue(clientId, out string userName);    // 접속 승인 때 확인한 이름
        if (string.IsNullOrEmpty(userName)) userName = $"Player {clientId}";

        users.Add(new UserState { Id = clientId, Name = ToFixed(userName), Hp = 100 });
        if (announce) ToAll($"* {userName} 입장");
    }

    // ---------- 목록을 찾는 도우미 ----------

    int IndexOf(ulong clientId)
    {
        for (int i = 0; i < users.Count; i++)
            if (users[i].Id == clientId) return i;
        return -1;
    }

    // 클라이언트도 부를 수 있다. 목록에서 ClientId로 접속자를 찾는다.
    public bool TryGetUser(ulong clientId, out UserState user)
    {
        int i = IndexOf(clientId);
        user = i >= 0 ? users[i] : default;
        return i >= 0;
    }

    // 문자열을 NetworkList에 담을 수 있는 고정 길이 문자열로 바꾼다. 16자까지만 쓴다.
    static FixedString64Bytes ToFixed(string text)
    {
        if (text == null) text = "";
        if (text.Length > 16) text = text.Substring(0, 16);
        return new FixedString64Bytes(text);
    }

    // 같은 채널에 들어가 있는 접속자들의 ClientId 목록 (채널은 실습 9, 10에서 쓴다)
    List<ulong> MembersOf(FixedString64Bytes channel)
    {
        var ids = new List<ulong>();
        for (int i = 0; i < users.Count; i++)
            if (users[i].Channel.Equals(channel)) ids.Add(users[i].Id);
        return ids;
    }

    // ---------- 서버가 한 줄을 내보내는 세 가지 방법 ----------

    void ToAll(string line)                             // 모든 접속자에게
    {
        ServerLog(line);
        LineToAllRpc(line);
    }

    void ToGroup(List<ulong> clientIds, string line)    // 지정한 접속자들에게만
    {
        ServerLog(line);
        LineToSomeRpc(line, RpcTarget.Group(clientIds, RpcTargetUse.Temp));
    }

    void ToOne(ulong clientId, string line)             // 한 사람에게만 (안내, 거절 사유)
    {
        LineToSomeRpc(line, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    void ServerLog(string line)
    {
        Debug.Log($"[서버] {line}");
        if (!IsClient) Emit(line);      // 화면만 있는 서버(Server 모드)는 클라이언트용 RPC를 받지 않으므로 직접 보여 준다
    }

    // 서버가 모든 클라이언트(호스트 포함)에게 한 줄을 보낸다. 서버만 부를 수 있다.
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    void LineToAllRpc(string line)
    {
        Emit(line);
    }

    // 서버가 호출할 때 정한 접속자에게만 한 줄을 보낸다. 서버만 부를 수 있다.
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void LineToSomeRpc(string line, RpcParams rpcParams = default)
    {
        Emit(line);
    }

    void Emit(string line)
    {
        OnLog?.Invoke(line);
    }
}
