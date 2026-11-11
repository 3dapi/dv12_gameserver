// HostNet.h — Steam Networking Sockets 호스트
class HostNet
{
public:
    void Start()
    {
        SteamNetworkingUtils()->InitRelayNetworkAccess();           // SDR(릴레이) 사용 준비
        auto* s = SteamNetworkingSockets();
        m_Listen = s->CreateListenSocketP2P(0, 0, nullptr);
        m_Poll   = s->CreatePollGroup();
    }
    void Update()                                                    // 루프마다 호출한다
    {
        SteamAPI_RunCallbacks();
        auto* s = SteamNetworkingSockets();
        SteamNetworkingMessage_t* msgs[16];
        int n = s->ReceiveMessagesOnPollGroup(m_Poll, msgs, 16);
        for (int i = 0; i < n; ++i)
        {
            std::string line((const char*)msgs[i]->m_pData, msgs[i]->m_cbSize);
            OnLine(msgs[i]->m_conn, line);                           // 실습 1의 Handle과 같은 처리
            msgs[i]->Release();                                      // 반드시 해제한다
        }
    }
    void Send(HSteamNetConnection c, const std::string& line)
    {
        SteamNetworkingSockets()->SendMessageToConnection(c, line.c_str(), (uint32)line.size(),
                                                          k_nSteamNetworkingSend_Reliable, nullptr);
    }
private:
    HSteamListenSocket m_Listen = k_HSteamListenSocket_Invalid;
    HSteamNetPollGroup m_Poll   = k_HSteamNetPollGroup_Invalid;
    std::map<HSteamNetConnection, std::string> m_Names;              // 연결 → 이름
    void OnLine(HSteamNetConnection c, const std::string& line);     // HELLO/SAY 처리, 실습 1과 동일

    STEAM_CALLBACK(HostNet, OnStatus, SteamNetConnectionStatusChangedCallback_t);
};

void HostNet::OnStatus(SteamNetConnectionStatusChangedCallback_t* p)
{
    auto* s = SteamNetworkingSockets();
    switch (p->m_info.m_eState)
    {
    case k_ESteamNetworkingConnectionState_Connecting:               // 새 연결 요청
        if (p->m_eOldState != k_ESteamNetworkingConnectionState_None) break;
        if (m_Names.size() >= 4) { s->CloseConnection(p->m_hConn, 1001, "full", false); break; }
        if (s->AcceptConnection(p->m_hConn) != k_EResultOK) { s->CloseConnection(p->m_hConn, 0, nullptr, false); break; }
        s->SetConnectionPollGroup(p->m_hConn, m_Poll);
        m_Names[p->m_hConn] = "";
        break;
    case k_ESteamNetworkingConnectionState_ClosedByPeer:             // 상대가 닫음
    case k_ESteamNetworkingConnectionState_ProblemDetectedLocally:   // 연결 문제(타임아웃 등)
        s->CloseConnection(p->m_hConn, 0, nullptr, false);
        m_Names.erase(p->m_hConn);                                   // NOTICE left, USERS 갱신
        break;
    default: break;
    }
}

// ClientNet — 로비 데이터에서 읽은 호스트 SteamID로 접속한다
HSteamNetConnection ConnectToHost(uint64 hostSteamId)
{
    SteamNetworkingIdentity id;
    id.SetSteamID64(hostSteamId);
    return SteamNetworkingSockets()->ConnectP2P(id, 0, 0, nullptr);
}
