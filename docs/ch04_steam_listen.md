# 4. Steam Listen Server

Steamworks SDK의 로비, 친구 초대, 네트워킹 API를 활용하여 플레이어가 호스트 역할을 겸하는 Listen Server 방식의 온라인 게임을 직접 구현한다. Steam은 연결·매치메이킹·릴레이 기능을 제공하지만 게임 규칙, 서버 권한 및 게임 상태 동기화는 엔진이나 개발자가 구현해야 한다. 이 Part는 선택 과정이 아닌 필수 실습 과정이다.

## 4.1 학습 목표

별도의 상시 운영 Dedicated Server나 자체 분산 서버를 개발하지 않고도 Steam 플랫폼을 통해 친구와 접속하여 함께 플레이할 수 있는 소규모 멀티플레이 게임을 완성한다. Steam이 제공하는 네트워킹·로비 서비스와 게임 엔진/개발자가 구현할 게임 서버 로직을 구분한다.

## 4.2 Steam Listen Server의 구조

플레이어 한 명이 호스트가 되어 게임 클라이언트와 서버 역할을 동시에 수행한다. 다른 플레이어는 호스트가 처리하는 게임 상태와 규칙에 따라 게임에 참여한다. Listen Server는 호스트가 참여하는 Client/Server 구조이며, 반드시 완전한 대칭형 P2P 게임 로직을 뜻하지 않는다.

호스트의 종료나 연결 단절 시 세션이 중단될 수 있다. 호스트 이전(Host Migration)은 자동 제공을 전제로 하지 않으며 심화 주제로만 소개한다.

## 4.3 Steam Lobby 및 친구 초대

Steamworks SDK의 로비 생성·검색·참여, 친구 초대, 참여 수락, 방 메타데이터와 호스트 식별 정보를 학습한다. 로비를 생성하는 것만으로 게임 서버가 자동 생성되는 것은 아니며, 로비와 실제 게임 네트워크 연결 절차를 분리하여 설계한다.

## 4.4 Steam Networking

Steam Networking Sockets의 연결 생성·수락·송수신 개념, Steam Datagram Relay(SDR)를 통한 중계, NAT 환경에서의 연결을 학습한다. Steam이 제공하는 연결 인프라를 이용하더라도 게임 객체의 상태 복제와 치트 방지를 포함한 게임 로직은 별도로 구현해야 한다.

## 4.5 게임 상태 동기화와 서버 권한

호스트가 플레이어 입장·퇴장과 정원을 관리하고 메시지를 중계한다. 이동·상호작용 같은 게임 규칙은 2~3장과 같이 소유 클라이언트가 처리하고 호스트는 이를 전달한다. 동기화 범위와 전송 주기를 결정하고, RTT·호스트 이탈·중복 요청 상황을 테스트한다. 엔진의 복제 기능을 이용하는 경우 해당 엔진의 서버 권한 모델과 Steam의 연결 계층을 구분한다.

## 4.6 C++ 소켓 기반 Listen Server 구현

첫 실습에서는 C++과 일반 소켓(Winsock2 등)을 사용하여 호스트가 게임 클라이언트와 서버 역할을 동시에 수행하는 소규모 Listen Server를 작성한다. 접속·송수신·클라이언트 식별·간단한 상태 동기화를 구현하여 호스트형 서버의 동작 원리를 확인한다.

다음 실습에서는 일반 소켓 통신 코드를 Steam Networking Sockets 기반 통신으로 전환한다. CreateListenSocketP2P, ConnectP2P, 연결 상태 콜백, 메시지 송수신, SteamID 기반 접속과 Relay를 학습한다. 일반 TCP/UDP 소켓 API와 Steam Networking Sockets API는 동일하지 않으며, 소켓 코드를 그대로 사용한다고 Steam 네트워크를 통과하는 것은 아니다.

### 4.6.1 C++ 단계별 실습

**[검증 방식]** 2~3장과 같다. Steam 연결은 눈으로 확인하기 어려우므로 먼저 채팅 프로그램으로 만들어 검증한다. C++ 단계에서는 콘솔 명령(/say, /users, /quit)이 GUI의 버튼 역할을 한다. 세 실습은 통신 방식만 바꾸고 메시지 형식은 그대로 유지한다. 한 줄이 한 메시지이며 HELLO(접속), WELCOME(승인), DENY(거부), SAY(대화), CHAT(전달), NOTICE(공지), USERS(접속자 목록)를 사용한다.

실습 1 — Winsock 호스트(Listen Server) 채팅: 한 프로세스가 서버 소켓을 열고 자신도 플레이어로 참여한다. 접속한 클라이언트에게 승인 또는 거부(정원, 이름 중복)를 알리고, 대화를 모두에게 중계하며, 입장·퇴장 때마다 접속자 목록을 보낸다. 호스트의 키보드 입력은 별도 스레드가 읽는다.

```cpp
// Host.cpp — Windows, Winsock2
#include <winsock2.h>
#include <ws2tcpip.h>
#include <iostream>
#include <string>
#include <vector>
#include <map>
#include <queue>
#include <mutex>
#include <thread>
#pragma comment(lib, "ws2_32.lib")

struct Peer { std::string name; std::string buf; };      // 이름이 비어 있으면 아직 HELLO 전
std::map<SOCKET, Peer> g_peers;
std::queue<std::string> g_input;                          // 키보드 입력 스레드가 채운다
std::mutex g_mtx;
std::string g_host = "Host";
const size_t kMax = 4;                                    // 정원

void SendLine(SOCKET s, const std::string& line)
{
    std::string m = line + "\n";
    send(s, m.c_str(), (int)m.size(), 0);
}
void Broadcast(const std::string& line)                   // 호스트 화면에도 표시한다
{
    std::cout << line << "\n";
    for (auto& kv : g_peers) if (!kv.second.name.empty()) SendLine(kv.first, line);
}
std::string UserList()
{
    std::string r = "USERS " + g_host;
    for (auto& kv : g_peers) if (!kv.second.name.empty()) r += "," + kv.second.name;
    return r;
}
bool Handle(SOCKET s, const std::string& line)           // false이면 연결을 닫는다
{
    Peer& p = g_peers[s];
    if (line.rfind("HELLO ", 0) == 0 && p.name.empty())
    {
        std::string name = line.substr(6);
        bool dup = (name == g_host);
        for (auto& kv : g_peers) if (kv.first != s && kv.second.name == name) dup = true;
        if (dup) { SendLine(s, "DENY name"); return false; }
        p.name = name;
        SendLine(s, "WELCOME");
        Broadcast("NOTICE " + name + " joined");
        Broadcast(UserList());
    }
    else if (line.rfind("SAY ", 0) == 0 && !p.name.empty())
        Broadcast("CHAT " + p.name + " " + line.substr(4));
    return true;
}
void Remove(SOCKET s)
{
    std::string name = g_peers[s].name;
    closesocket(s);
    g_peers.erase(s);
    if (!name.empty()) { Broadcast("NOTICE " + name + " left"); Broadcast(UserList()); }
}

int main(int argc, char** argv)
{
    WSADATA w; WSAStartup(MAKEWORD(2, 2), &w);
    if (argc > 1) g_host = argv[1];
    SOCKET ls = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    sockaddr_in a{}; a.sin_family = AF_INET; a.sin_addr.s_addr = INADDR_ANY; a.sin_port = htons(9000);
    bind(ls, (sockaddr*)&a, sizeof a);
    listen(ls, SOMAXCONN);
    std::thread([] {
        std::string l;
        while (std::getline(std::cin, l)) { std::lock_guard<std::mutex> g(g_mtx); g_input.push(l); }
    }).detach();

    for (;;)
    {
        fd_set rd; FD_ZERO(&rd); FD_SET(ls, &rd);
        for (auto& kv : g_peers) FD_SET(kv.first, &rd);   // 한 번에 64개(FD_SETSIZE)까지
        timeval tv{ 0, 50000 };
        select(0, &rd, nullptr, nullptr, &tv);

        if (FD_ISSET(ls, &rd))
        {
            SOCKET c = accept(ls, nullptr, nullptr);
            if (g_peers.size() >= kMax) { SendLine(c, "DENY full"); closesocket(c); }
            else g_peers[c] = Peer{};
        }
        std::vector<SOCKET> socks;
        for (auto& kv : g_peers) socks.push_back(kv.first);
        for (SOCKET s : socks)
        {
            if (!FD_ISSET(s, &rd)) continue;
            char tmp[512];
            int n = recv(s, tmp, sizeof tmp, 0);
            if (n <= 0) { Remove(s); continue; }
            Peer& p = g_peers[s];
            p.buf.append(tmp, n);
            bool keep = true; size_t pos;
            while (keep && (pos = p.buf.find('\n')) != std::string::npos)
            {
                std::string line = p.buf.substr(0, pos);
                p.buf.erase(0, pos + 1);
                keep = Handle(s, line);
            }
            if (!keep) Remove(s);
        }
        std::lock_guard<std::mutex> g(g_mtx);
        while (!g_input.empty())
        {
            std::string l = g_input.front(); g_input.pop();
            if (l == "/quit") return 0;
            if (l == "/users") std::cout << UserList() << "\n";
            else Broadcast("CHAT " + g_host + " " + l);
        }
    }
}
// Client.cpp — 사용법: Client.exe 127.0.0.1 철수
#include <winsock2.h>
#include <ws2tcpip.h>
#include <iostream>
#include <string>
#include <thread>
#pragma comment(lib, "ws2_32.lib")

int main(int argc, char** argv)
{
    if (argc < 3) return 1;
    WSADATA w; WSAStartup(MAKEWORD(2, 2), &w);
    SOCKET s = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    sockaddr_in a{}; a.sin_family = AF_INET; a.sin_port = htons(9000);
    inet_pton(AF_INET, argv[1], &a.sin_addr);
    if (connect(s, (sockaddr*)&a, sizeof a) != 0) { std::cout << "connect failed\n"; return 1; }
    std::string hello = std::string("HELLO ") + argv[2] + "\n";
    send(s, hello.c_str(), (int)hello.size(), 0);
    std::thread([s] {
        char b[512]; int n;
        while ((n = recv(s, b, sizeof b, 0)) > 0) std::cout.write(b, n);
        std::cout << "disconnected\n";
    }).detach();
    std::string l;
    while (std::getline(std::cin, l))
    {
        if (l == "/quit") break;
        l = "SAY " + l + "\n";
        send(s, l.c_str(), (int)l.size(), 0);
    }
    closesocket(s);
    WSACleanup();
}
```

**[확인할 것]** 호스트 1개와 클라이언트 3개를 실행하여 메시지 한 줄이 모든 창에 한 번씩 표시되는지, 같은 이름으로 접속하면 DENY name이, 다섯 번째 접속에는 DENY full이 오는지, 클라이언트를 강제 종료하면 NOTICE와 USERS가 갱신되는지 확인한다. 호스트가 /quit으로 종료하면 클라이언트에 disconnected가 표시되고 세션이 끝나는 것도 확인한다(호스트 종료는 곧 세션 종료).

**[흔한 오류]** TCP는 메시지 경계가 없으므로 recv 한 번이 한 줄이라고 가정하는 경우(위 코드는 \n으로 자르고 처리한다), 방화벽이 9000번 포트를 막는 경우, 전송 중 열린 소켓을 순회하며 지우는 경우(Remove는 순회가 끝난 뒤에 호출한다).

실습 2 — Steam 초기화와 로비 채팅: 같은 채팅을 Steam 로비로 옮긴다. 로비는 방을 찾고 모이는 곳이며 게임 연결이 아니다. 로비 채팅(SendLobbyChatMsg)으로 접속자 목록과 대화를 확인하고, 로비 데이터에 호스트의 SteamID를 기록해 둔다(실습 3에서 접속에 사용한다).

**[구현 순서]** 1. Steamworks SDK를 받아 헤더와 steam_api64.lib를 프로젝트에 추가하고 실행 폴더에 steam_api64.dll과 steam_appid.txt(내용은 테스트용 480)를 둔다. 2. Steam 클라이언트를 켠 상태에서 SteamAPI_Init()이 true인지 확인한다. 3. 루프마다 SteamAPI_RunCallbacks()를 호출한다(콜백이 여기서 전달된다). 4. 로비 생성·참가·채팅을 구현한다. 5. 친구 초대로 들어오는 참가 요청(GameLobbyJoinRequested_t)을 처리한다.

```cpp
// LobbyChat.h — Steamworks SDK (steam_api.h). 클래스 이름과 시그니처는 사용하는 SDK 버전의 헤더로 확인한다.
#include "steam/steam_api.h"
#include <iostream>
#include <string>

class LobbyChat
{
public:
    bool Init() { return SteamAPI_Init(); }
    void Update() { SteamAPI_RunCallbacks(); }                       // 루프마다 호출한다

    void Create()                                                    // 호스트: 로비 생성
    {
        SteamAPICall_t h = SteamMatchmaking()->CreateLobby(k_ELobbyTypeFriendsOnly, 4);
        m_Created.Set(h, this, &LobbyChat::OnCreated);
    }
    void Join(CSteamID id)                                           // 클라이언트: 로비 참가
    {
        SteamAPICall_t h = SteamMatchmaking()->JoinLobby(id);
        m_Entered.Set(h, this, &LobbyChat::OnEntered);
    }
    void Say(const std::string& s)
    {
        SteamMatchmaking()->SendLobbyChatMsg(m_Lobby, s.c_str(), (int)s.size() + 1);
    }
    std::string HostId() const { return SteamMatchmaking()->GetLobbyData(m_Lobby, "host"); }

private:
    CSteamID m_Lobby;
    CCallResult<LobbyChat, LobbyCreated_t> m_Created;
    CCallResult<LobbyChat, LobbyEnter_t>   m_Entered;
    CCallback<LobbyChat, LobbyChatMsg_t>    m_ChatMsg{ this, &LobbyChat::OnChatMsg };
    CCallback<LobbyChat, LobbyChatUpdate_t> m_ChatUpd{ this, &LobbyChat::OnChatUpdate };
    CCallback<LobbyChat, GameLobbyJoinRequested_t> m_JoinReq{ this, &LobbyChat::OnJoinReq };

    void OnCreated(LobbyCreated_t* p, bool ioFail)
    {
        if (ioFail || p->m_eResult != k_EResultOK) { std::cout << "create failed\n"; return; }
        m_Lobby = CSteamID(p->m_ulSteamIDLobby);
        SteamMatchmaking()->SetLobbyData(m_Lobby, "game", "chat");
        SteamMatchmaking()->SetLobbyData(m_Lobby, "host", std::to_string(SteamUser()->GetSteamID().ConvertToUint64()).c_str());
        std::cout << "lobby " << m_Lobby.ConvertToUint64() << " created\n";
    }
    void OnEntered(LobbyEnter_t* p, bool ioFail)
    {
        if (ioFail || p->m_EChatRoomEnterResponse != k_EChatRoomEnterResponseSuccess) { std::cout << "join failed\n"; return; }
        m_Lobby = CSteamID(p->m_ulSteamIDLobby);
        std::cout << "joined, host=" << HostId() << "\n";
    }
    void OnChatMsg(LobbyChatMsg_t* p)                                // 보낸 사람 자신에게도 전달된다
    {
        char buf[1024]; CSteamID user; EChatEntryType type;
        int n = SteamMatchmaking()->GetLobbyChatEntry(CSteamID(p->m_ulSteamIDLobby), p->m_iChatID, &user, buf, sizeof buf, &type);
        if (n > 0) std::cout << SteamFriends()->GetFriendPersonaName(user) << ": " << buf << "\n";
    }
    void OnChatUpdate(LobbyChatUpdate_t*)                            // 입장·퇴장 때 접속자 목록을 다시 그린다
    {
        int n = SteamMatchmaking()->GetNumLobbyMembers(m_Lobby);
        std::cout << "USERS";
        for (int i = 0; i < n; ++i)
            std::cout << (i ? "," : " ") << SteamFriends()->GetFriendPersonaName(SteamMatchmaking()->GetLobbyMemberByIndex(m_Lobby, i));
        std::cout << "\n";
    }
    void OnJoinReq(GameLobbyJoinRequested_t* p) { Join(p->m_steamIDLobby); }   // 친구 초대 수락
};
```

**[확인할 것]** 서로 다른 Steam 계정으로 로그인한 PC 두 대에서 실행한다(한 PC에서 Steam 계정을 둘 동시에 쓸 수 없다). 한쪽이 로비를 만들고 친구 목록에서 초대하여 다른 쪽이 수락하면 두 화면에 같은 대화가 한 번씩 표시되고 입장·퇴장 때 USERS가 갱신되는지 확인한다. 테스트용 App ID 480은 모든 개발자가 공유하므로 로비 검색에 다른 사람의 로비가 섞일 수 있어, 로비 데이터 game=chat 같은 필터를 쓴다.

**[흔한 오류]** Steam 클라이언트를 켜지 않아 SteamAPI_Init이 실패하는 경우, steam_appid.txt가 실행 파일 폴더에 없는 경우, RunCallbacks를 호출하지 않아 콜백이 오지 않는 경우, 로비가 연결이라고 생각하여 로비 입장만으로 게임 데이터가 오갈 것으로 기대하는 경우.

실습 3 — Steam Networking Sockets로 전환: 실습 1의 Winsock 호출을 Steam Networking Sockets로 바꾼다. 클라이언트는 로비 데이터의 호스트 SteamID로 연결하고(ConnectP2P), 호스트는 연결 상태 콜백에서 정원을 확인하여 수락한다(AcceptConnection). 메시지 형식은 실습 1과 같고, Steam의 소켓은 메시지 단위로 전달되므로 \n으로 자르는 코드는 필요 없다.

```cpp
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
```

**[확인할 것]** 실습 1과 같은 시험(이름 중복, 정원 초과, 강제 종료, 호스트 종료)을 Steam 연결에서 다시 수행하여 결과가 같은지 비교한다. 연결 순서는 로비 입장 → 호스트 SteamID 읽기 → ConnectP2P → HELLO이며, 각 단계의 로그를 남겨 어느 단계에서 실패했는지 구분한다. 일반 소켓 코드는 Steam 네트워크를 통과하지 않으므로 Winsock 호출이 남아 있지 않은지 확인한다.

**[흔한 오류]** InitRelayNetworkAccess를 호출하지 않아 릴레이 연결이 늦어지는 경우, 수신한 메시지를 Release하지 않는 경우, 연결 상태 콜백에서 CloseConnection을 호출하지 않아 연결이 남는 경우, 호스트와 클라이언트가 같은 Steam 계정인 경우.

## 4.7 Unity와 Steam Listen Server 연동

Part 1에서 제작한 Unity Netcode 멀티플레이 예제에 Steam 로비 생성·검색, 친구 초대 및 참여 기능을 연결한다. Steamworks API의 C# 래퍼와 Unity Netcode에서 호환되는 Steam Transport 구현체 또는 직접 작성한 Transport 어댑터를 사용하여 Host/Client 통신 경로를 Steam 네트워킹으로 구성한다.

Unity Netcode for GameObjects의 기본 Transport인 Unity Transport가 Steam 네트워킹으로 자동 전환되는 것은 아니다. 교재 예제의 Unity/Netcode 버전과 Transport 호환성을 확인한 조합을 명시하고, 로비 참가와 실제 게임 세션 연결을 분리하여 검증한다. NetworkVariable과 RPC의 동작은 기존 Unity 게임 로직을 재사용한다.

### 4.7.1 Unity 단계별 실습

**[검증 방식]** 2장의 채팅 GUI 씬(ChatScene, 2.11절)을 그대로 사용한다. 씬에 Steam 패널(로비 만들기, 로비 찾기, 참가, 친구 초대 버튼과 로비 목록 Text)을 하나 추가하고, 접속 이후의 채팅·채널·보너스 시험은 2장 패널을 그대로 사용한다. 연결 경로만 Unity Transport에서 Steam으로 바뀌므로 2장의 시험 항목이 같은 결과로 나와야 한다. 이 절의 Steamworks.NET은 Steamworks SDK의 C# 래퍼이다.

실습 1 — Steamworks.NET 설치와 초기화: 패키지를 설치하고 Steam이 초기화되었는지 GUI의 상태 Text에 표시한다.

**[구현 순서]** 1. Package Manager의 Add package from git URL로 Steamworks.NET 저장소의 패키지를 추가한다[URL과 버전은 사용하는 버전으로 확인하여 기재]. 2. 프로젝트 루트에 steam_appid.txt(480)를 둔다. 3. 패키지가 제공하는 SteamManager를 씬에 하나만 두거나 아래처럼 직접 초기화한다. 4. Steam 클라이언트를 켠 상태에서 실행한다.

```csharp
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

public class SteamBoot : MonoBehaviour
{
    [SerializeField] Text statusText;

    void Awake()
    {
        bool ok = SteamAPI.Init();                                  // Steam 클라이언트가 켜져 있어야 한다
        statusText.text = ok ? "Steam: " + SteamFriends.GetPersonaName() : "Steam 초기화 실패";
    }
    void Update() { SteamAPI.RunCallbacks(); }                     // 콜백 전달, 매 프레임 호출
    void OnDestroy() { SteamAPI.Shutdown(); }
}
```

**[확인할 것]** 에디터에서 Play하면 상태 Text에 Steam 계정 이름이 표시되는지 확인한다. Steam을 끄고 실행하면 실패 메시지가 표시되어야 한다.

실습 2 — 로비 생성·검색·참가·초대: 버튼으로 로비를 만들고, 찾고, 들어가고, 친구를 초대한다. 로비 데이터에 게임 이름과 호스트 SteamID를 기록해 두어 검색과 접속에 사용한다.

```csharp
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

public class SteamLobbyPanel : MonoBehaviour
{
    [SerializeField] Button createBtn, findBtn, joinBtn, inviteBtn;
    [SerializeField] Text listText, statusText;

    CallResult<LobbyCreated_t> _created;
    CallResult<LobbyMatchList_t> _list;
    Callback<LobbyEnter_t> _entered;
    Callback<GameLobbyJoinRequested_t> _joinReq;
    CSteamID _lobby, _found;
    public ulong HostSteamId { get; private set; }

    void Start()
    {
        _created = CallResult<LobbyCreated_t>.Create(OnCreated);
        _list    = CallResult<LobbyMatchList_t>.Create(OnList);
        _entered = Callback<LobbyEnter_t>.Create(OnEntered);
        _joinReq = Callback<GameLobbyJoinRequested_t>.Create(r => SteamMatchmaking.JoinLobby(r.m_steamIDLobby));   // 친구 초대 수락

        createBtn.onClick.AddListener(() =>
            _created.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4)));
        findBtn.onClick.AddListener(() =>
        {
            SteamMatchmaking.AddRequestLobbyListStringFilter("game", "chat", ELobbyComparison.k_ELobbyComparisonEqual);
            _list.Set(SteamMatchmaking.RequestLobbyList());
        });
        joinBtn.onClick.AddListener(() => { if (_found.IsValid()) SteamMatchmaking.JoinLobby(_found); });
        inviteBtn.onClick.AddListener(() => SteamFriends.ActivateGameOverlayInviteDialog(_lobby));
    }

    void OnCreated(LobbyCreated_t r, bool ioFail)
    {
        if (ioFail || r.m_eResult != EResult.k_EResultOK) { statusText.text = "로비 생성 실패"; return; }
        _lobby = new CSteamID(r.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(_lobby, "game", "chat");
        SteamMatchmaking.SetLobbyData(_lobby, "host", SteamUser.GetSteamID().m_SteamID.ToString());
        HostSteamId = SteamUser.GetSteamID().m_SteamID;
        statusText.text = "로비 생성: " + _lobby;
    }
    void OnList(LobbyMatchList_t r, bool ioFail)
    {
        listText.text = "";
        for (int i = 0; i < r.m_nLobbiesMatching; i++)
        {
            _found = SteamMatchmaking.GetLobbyByIndex(i);          // 첫 번째 로비를 선택한다
            listText.text += _found + "  " + SteamMatchmaking.GetNumLobbyMembers(_found) + "명\n";
        }
    }
    void OnEntered(LobbyEnter_t e)
    {
        _lobby = new CSteamID(e.m_ulSteamIDLobby);
        ulong.TryParse(SteamMatchmaking.GetLobbyData(_lobby, "host"), out var host);
        HostSteamId = host;
        statusText.text = "로비 입장, host=" + host;
    }
}
```

**[확인할 것]** PC 두 대(서로 다른 Steam 계정)에서 한쪽이 로비를 만들고 다른 쪽이 찾기 또는 초대로 입장하는지, 입장한 쪽의 HostSteamId가 호스트의 SteamID와 같은지 확인한다. 이 단계에서는 NetworkManager를 시작하지 않는다. 로비 입장은 게임 연결이 아니다.

실습 3 — NGO 연결을 Steam으로: 로비를 만든 쪽이 StartHost, 입장한 쪽이 호스트 SteamID를 Transport에 지정하고 StartClient를 호출한다. 이후 2장의 연결 패널과 채팅 패널은 수정하지 않는다. [2장과의 대응: 연결 패널은 2.5.4절의 `NetStarter`와 2.10.4절의 `JoinPanel`, 채팅 패널은 2.11.4절의 `ChatLogPanel`과 `ChatInputPanel`이다. 접속 버튼이 주소·포트 대신 로비 정보를 쓰게 하는 부분만 아래 구현 순서 2번에서 바뀐다(2.19.3절).]

**[구현 순서]** 1. Steam 연결을 지원하는 NGO Transport 구현체를 프로젝트에 추가하고 NetworkManager의 Network Transport에 지정한다[사용 구현체와 호환 NGO 버전을 확인하여 기재]. 2. 접속 버튼의 동작을 Steam 모드에서는 로비 정보를 이용하도록 바꾼다. 3. 2장의 ConnectionApprovalCallback(이름|인증 코드, 2.10.4절의 `ApprovalGate`)을 그대로 사용한다.

```csharp
// 호스트: 로비 생성 직후
NetworkManager.Singleton.StartHost();

// 클라이언트: 로비 입장 직후 (Transport 클래스와 필드 이름은 사용하는 구현체의 문서를 따른다)
var transport = NetworkManager.Singleton.GetComponent<MySteamTransport>();
transport.TargetSteamId = steamLobbyPanel.HostSteamId;
NetworkManager.Singleton.StartClient();
```

**[확인할 것]** 2장 시험(2.10.4절, 2.11절)을 같은 순서로 반복한다. 올바른 인증 코드로 접속, 틀린 코드·중복 이름 거부, 정원 초과, 채팅 중계, 채널, 보너스 요청과 응답, 가상 이동이 Steam 연결에서도 같은 결과인지 비교한다.

실습 4 — 호스트 종료와 접속 실패: 호스트가 종료되면 세션이 끝난다는 Listen Server의 특성을 화면으로 확인한다. 클라이언트는 연결이 끊기면 사유를 상태 Text에 표시하고 로비를 떠난다.

```csharp
void Start()
{
    NetworkManager.Singleton.OnClientDisconnectCallback += id =>
    {
        if (id != NetworkManager.Singleton.LocalClientId) return;       // 내 연결이 끊어진 경우만
        string reason = NetworkManager.Singleton.DisconnectReason;      // 승인 거부 사유 또는 빈 문자열
        statusText.text = string.IsNullOrEmpty(reason) ? "호스트와 연결이 끊어졌습니다" : reason;
        if (_lobby.IsValid()) SteamMatchmaking.LeaveLobby(_lobby);
    };
}
```

**[확인할 것]** 호스트를 종료하면 모든 클라이언트의 상태 Text가 바뀌고 로비에서 나가는지, 로비 입장은 성공했지만 호스트가 이미 종료된 경우 StartClient가 실패하는 화면이 나오는지 확인한다. Host Migration은 자동 제공되지 않는다.

**[흔한 오류]** 로비 입장만으로 NGO 연결이 이루어진다고 생각하는 경우, Unity Transport가 그대로 지정된 경우, SteamAPI.RunCallbacks를 호출하지 않는 경우, 같은 PC에서 두 클라이언트를 같은 Steam 계정으로 실행하는 경우.

## 4.8 Unreal과 Steam Listen Server 연동

Part 2에서 작성한 Unreal 멀티플레이 예제를 Listen Server 방식으로 실행하고, Online Subsystem Steam을 통해 Steam 세션 생성·검색·참여 및 친구 초대 기능을 연동한다. 게임 세션의 호스트가 실제 플레이어라는 점을 Dedicated Server와 비교한다.

Steam Sockets/NetDriver 구성 등 Steam 기반 통신 경로를 확인하고, Unreal의 Replication과 RPC 게임 로직을 그대로 활용한다. Steam의 로비·세션 서비스와 Unreal의 게임 서버 권한·상태 복제는 서로 다른 계층임을 구분한다. 호스트 종료·참여 실패·네트워크 지연을 테스트한다.

### 4.8.1 Unreal 단계별 실습

**[검증 방식]** 3장의 채팅 맵과 UMG 위젯을 그대로 사용한다. 위젯에 세션 패널(세션 만들기, 찾기, 참가 버튼과 목록 TextBlock)을 추가하고, 접속 이후의 채팅·채널·보너스 시험은 3장 위젯을 그대로 사용한다. 3장은 Dedicated Server였으나 이 장은 방을 만든 플레이어가 서버를 겸하는 Listen Server이므로 호스트 플레이어의 PlayerController도 서버에 존재한다는 점을 확인한다.

실습 1 — Steam 온라인 서브시스템 설정: Online Subsystem Steam 플러그인을 켜고 설정 파일을 작성한다. Steam 세션은 에디터의 PIE에서는 동작하지 않으므로 Standalone Game 또는 패키징한 빌드로 시험한다. 친구 초대 오버레이는 패키징한 빌드에서 확인한다.

```ini
// MyGame.Build.cs
PublicDependencyModuleNames.AddRange(new string[] { "OnlineSubsystem", "OnlineSubsystemUtils" });
DynamicallyLoadedModuleNames.Add("OnlineSubsystemSteam");

; Config/DefaultEngine.ini  (항목 이름과 값은 사용하는 엔진 버전의 문서로 확인한다)
[/Script/Engine.GameEngine]
+NetDriverDefinitions=(DefName="GameNetDriver",DriverClassName="OnlineSubsystemSteam.SteamNetDriver",DriverClassNameFallback="OnlineSubsystemUtils.IpNetDriver")

[OnlineSubsystem]
DefaultPlatformService=Steam

[OnlineSubsystemSteam]
bEnabled=true
SteamDevAppId=480
bInitServerOnClient=true

[/Script/OnlineSubsystemSteam.SteamNetDriver]
NetConnectionClassName="OnlineSubsystemSteam.SteamNetConnection"
```

**[확인할 것]** Steam을 켜고 Standalone Game으로 실행하여 로그에 OnlineSubsystemSteam이 초기화되었다는 메시지가 나오는지 확인한다. 같은 설정에서 Steam 없이 실행하면 NULL 서브시스템으로 대체되어 LAN 시험이 가능하다.

실습 2 — 세션 생성·검색·참가: GameInstance 서브시스템에 세션 기능을 구현하고 위젯의 버튼에서 호출한다. 호스트는 세션을 만든 뒤 ?listen 옵션으로 맵을 열고, 클라이언트는 검색 결과로 참가한 뒤 해석된 접속 주소로 이동한다.

```cpp
// ChatSessionSubsystem.cpp
#include "OnlineSubsystem.h"
#include "OnlineSessionSettings.h"
#include "Interfaces/OnlineSessionInterface.h"

IOnlineSessionPtr UChatSessionSubsystem::Sess() const
{
    IOnlineSubsystem* Sub = IOnlineSubsystem::Get();
    return Sub ? Sub->GetSessionInterface() : nullptr;
}

void UChatSessionSubsystem::CreateRoom()
{
    IOnlineSessionPtr S = Sess(); if (!S.IsValid()) return;
    FOnlineSessionSettings Set;
    Set.NumPublicConnections = 4;                                     // 정원
    Set.bShouldAdvertise = true;
    Set.bUsesPresence = true;
    Set.bUseLobbiesIfAvailable = true;
    Set.bAllowJoinInProgress = true;
    Set.bAllowJoinViaPresence = true;
    Set.bIsLANMatch = IOnlineSubsystem::Get()->GetSubsystemName() == "NULL";
    Set.Set(FName("GAME"), FString("Chat"), EOnlineDataAdvertisementType::ViaOnlineService);
    S->OnCreateSessionCompleteDelegates.AddUObject(this, &UChatSessionSubsystem::OnCreated);
    S->CreateSession(0, NAME_GameSession, Set);
}
void UChatSessionSubsystem::OnCreated(FName, bool bOk)
{
    if (bOk) GetWorld()->ServerTravel(TEXT("/Game/Maps/ChatMap?listen"));   // 호스트가 서버가 된다
    else OnStatus.Broadcast(TEXT("세션 생성 실패"));
}

void UChatSessionSubsystem::FindRooms()
{
    IOnlineSessionPtr S = Sess(); if (!S.IsValid()) return;
    Search = MakeShared<FOnlineSessionSearch>();
    Search->MaxSearchResults = 20;
    Search->bIsLanQuery = IOnlineSubsystem::Get()->GetSubsystemName() == "NULL";
    Search->QuerySettings.Set(SEARCH_PRESENCE, true, EOnlineComparisonOp::Equals);
    S->OnFindSessionsCompleteDelegates.AddUObject(this, &UChatSessionSubsystem::OnFound);
    S->FindSessions(0, Search.ToSharedRef());
}
void UChatSessionSubsystem::OnFound(bool bOk)
{
    FString Text;
    if (bOk && Search.IsValid())
        for (int32 i = 0; i < Search->SearchResults.Num(); ++i)
            Text += FString::Printf(TEXT("[%d] %s (%d명 참가 가능)\n"), i,
                *Search->SearchResults[i].Session.OwningUserName,
                Search->SearchResults[i].Session.NumOpenPublicConnections);
    OnList.Broadcast(Text);                                           // 위젯의 목록 TextBlock에 표시
}

void UChatSessionSubsystem::JoinRoom(int32 Index)
{
    IOnlineSessionPtr S = Sess();
    if (!S.IsValid() || !Search.IsValid() || !Search->SearchResults.IsValidIndex(Index)) return;
    S->OnJoinSessionCompleteDelegates.AddUObject(this, &UChatSessionSubsystem::OnJoined);
    S->JoinSession(0, NAME_GameSession, Search->SearchResults[Index]);
}
void UChatSessionSubsystem::OnJoined(FName Name, EOnJoinSessionCompleteResult::Type R)
{
    FString Addr;
    if (R == EOnJoinSessionCompleteResult::Success && Sess()->GetResolvedConnectString(Name, Addr))
        GetWorld()->GetFirstPlayerController()->ClientTravel(Addr, TRAVEL_Absolute);   // 이후는 3장 접속 흐름
    else OnStatus.Broadcast(TEXT("참가 실패"));
}
```

**[확인할 것]** 서로 다른 Steam 계정의 PC 두 대에서 한쪽이 세션을 만들고 다른 쪽이 목록에서 찾아 참가하는지 확인한다. 세션 생성과 맵 이동, 세션 참가와 접속 주소 이동이 서로 다른 단계라는 점을 로그로 구분한다.

실습 3 — 3장 시험을 Listen Server에서 반복: 세션으로 연결된 뒤에는 3장의 PreLogin(인증·정원), 채팅 중계, 채널, 상태 복제, 보너스 응답이 그대로 동작해야 한다. 호스트 플레이어는 서버이면서 로컬 플레이어이므로 위젯 생성과 서버 전용 처리를 구분한다.

```cpp
// 공통 코드에서 구분한다
if (IsNetMode(NM_ListenServer) && IsLocalController())
{
    // 호스트 플레이어: 서버이면서 화면이 있다. 위젯을 만들고, 이 컨트롤러의 Server RPC는 네트워크를 거치지 않고 로컬에서 바로 실행된다.
}
else if (!HasAuthority() && IsLocalController())
{
    // 원격 클라이언트
}

// 연결 실패와 호스트 종료 처리 (GameInstance의 Init에서 등록)
GEngine->OnNetworkFailure().AddUObject(this, &UChatSessionSubsystem::OnNetFail);
void UChatSessionSubsystem::OnNetFail(UWorld*, UNetDriver*, ENetworkFailure::Type T, const FString& Msg)
{
    OnStatus.Broadcast(FString::Printf(TEXT("연결 끊김: %s"), *Msg));
    UGameplayStatics::OpenLevel(this, TEXT("MenuMap"));              // 메뉴로 복귀
}
```

**[확인할 것]** 3장의 시험표를 같은 순서로 다시 수행하여 결과를 비교한다. 호스트를 종료하면 클라이언트가 메뉴로 돌아가고 상태 TextBlock에 사유가 표시되는지, 호스트 자신의 메시지도 다른 클라이언트와 같은 경로로 표시되는지 확인한다.

**[흔한 오류]** PIE에서 Steam 세션을 시험하는 경우, 같은 Steam 계정으로 두 클라이언트를 실행하는 경우, ServerTravel에 ?listen 옵션을 빠뜨리는 경우, 호스트 플레이어를 원격 클라이언트로 가정하여 서버 전용 코드를 건너뛰는 경우.

## 4.9 Steam Listen Server 통합 실습

실습 1(C++): 일반 소켓 기반 2인 호스트 게임을 구현하고 Steam Networking Sockets 방식으로 확장하여 로비·접속·메시지 교환을 검증한다.

실습 2(Unity): Part 1의 Unity Netcode 예제에 Steam 로비·친구 초대·호스트 접속을 연동하고 2~4인 게임을 실행한다.

실습 3(Unreal): Part 2의 게임을 Unreal Listen Server로 실행하고 Steam 세션 생성·검색·참여와 친구 초대를 검증한다.

공통 검증: Steam 계정과 App ID/빌드 및 네트워크 환경의 제약을 확인하고, 호스트 종료·참가 실패·네트워크 지연 상황에서 게임 동작을 점검한다.

## 4.10 학습 성과와 후속 과정

이 Part까지 마친 학생은 Unity Netcode, Unreal Dedicated Server 및 Steam Listen Server라는 세 가지 실용적인 멀티플레이 구현 경로를 경험한다. Part 5의 데이터베이스 및 Part 6의 자체 분산 서버 구축은 이러한 게임 제작 역량을 확장하는 과정이며, 기본 멀티플레이 게임 완성을 위한 필수 선행 조건은 아니다.

## 4.11 Steamworks의 구성과 동작 원리

앞의 실습은 Steam 기능을 호출하는 방법이었다. 이 절은 각 기능이 무엇이고 어떻게 연결되는지 정리한다. Steam은 게임 서버가 아니라 게임이 사용하는 서비스의 모음이라는 점이 출발점이다.

### 4.11.1 Steam이 제공하는 것과 제공하지 않는 것

- 제공하는 것: 사용자 식별(SteamID), 친구 목록과 초대, 로비(방 목록과 매칭), 연결 서비스(Networking Sockets, NAT 통과, 중계), 업적·클라우드 저장 같은 부가 서비스.
- 제공하지 않는 것: 게임 규칙의 처리, 게임 상태의 동기화, 서버 프로세스의 실행. 이것은 호스트 플레이어의 게임이 맡는다.

그래서 Steam 연동 게임은 로비(만나는 곳)와 게임 연결(실제로 플레이하는 곳)이 분리된다. 로비에 모였다고 게임 연결이 만들어지는 것이 아니며, 호스트가 로비에 자신의 SteamID를 기록하고 참가자가 그것을 읽어 게임 연결을 맺는 단계가 따로 필요하다. 4.7절의 실습 2와 3이 이 두 단계를 분리한 이유이다.

### 4.11.2 App ID, SteamID, 초기화

- App ID: Steam이 게임을 구별하는 번호이다. 개발 중에는 공용 테스트 App ID 480(Spacewar)을 쓴다. 480은 전 세계 개발자가 함께 쓰므로 로비 검색에 다른 개발자의 로비가 섞여 나온다. 로비에 게임 고유의 키와 값을 넣고 검색 필터로 걸러야 하는 이유이다.
- SteamID: 계정마다 부여된 64비트 식별자이다. 접속자 ID로 쓸 수 있고, 연결 대상을 지정할 때도 쓴다. 이름은 바뀔 수 있으므로 식별자로는 SteamID를 쓴다.
- 초기화: Steam 클라이언트가 실행 중이어야 SteamAPI_Init이 성공한다. 이후 매 프레임 SteamAPI_RunCallbacks를 호출해야 Steam이 보낸 결과(로비 생성 결과, 초대 수락 등)가 콜백으로 전달된다. 이 호출을 빼면 요청은 갔는데 응답이 없는 것처럼 보인다.
- 비동기 호출: 로비 생성, 로비 검색처럼 서버(Steam)와 통신하는 호출은 결과를 즉시 돌려주지 않고 CallResult/Callback으로 알려 준다. 요청 코드와 결과 처리 코드가 분리되므로 상태 변수를 두고 어느 단계인지 추적한다.

### 4.11.3 로비의 구조

로비는 Steam이 관리하는 작은 방이다. 최대 인원, 공개 범위(친구 전용, 공개), 로비 데이터(키-값), 멤버별 데이터를 가진다. 로비 소유자가 나가면 Steam이 다른 멤버에게 소유권을 넘기지만, 게임 연결의 호스트까지 자동으로 바뀌는 것은 아니다.

```text
[Steam 서비스]  Lobby { max=4, data{game=chat, host=<SteamID>}, members[A,B,C] }
      ^ 생성/검색/참가/초대              ^ 로비 채팅 (Steam 경유, 게임 연결과 별개)
   A(호스트)        B           C
```

로비 채팅 기능(SendLobbyChatMsg)으로 채팅 자체를 구현할 수도 있다. 이 교재는 게임 연결 위에서 채팅을 구현해 서버가 하는 일(중계와 접속자 관리)을 직접 보이기 위해, 로비를 만남의 장소로만 쓴다.

### 4.11.4 친구 초대 흐름

1. 호스트가 오버레이의 초대 창(ActivateGameOverlayInviteDialog)에서 친구를 고른다.
2. 친구의 Steam 클라이언트에 초대가 표시되고, 수락하면 게임이 실행 중이면 GameLobbyJoinRequested 콜백이, 실행 중이 아니면 실행 인자로 로비 정보가 전달된다.
3. 게임이 JoinLobby를 호출하고 LobbyEnter 콜백에서 호스트 SteamID를 읽는다.
4. 호스트에 게임 연결을 맺는다.

오버레이는 게임이 DirectX/OpenGL/Vulkan으로 화면을 그리는 환경에서 Steam이 끼워 넣는 화면이다. 에디터나 일부 실행 환경에서는 표시되지 않을 수 있으므로 패키징한 빌드에서 확인한다.

## 4.12 Steam Networking Sockets

Steam Networking Sockets(ISteamNetworkingSockets)는 Steam이 제공하는 연결 계층이다. 게임이 UDP 기반 신뢰/비신뢰 메시지 전송을 직접 구현하지 않아도 되고, NAT 통과와 중계까지 처리해 준다.

### 4.12.1 연결 모델

TCP 소켓과 비슷하게 서버 쪽은 Listen Socket을 만들고(CreateListenSocketP2P), 클라이언트는 상대 SteamID로 연결한다(ConnectP2P). 서버는 연결 요청 콜백(연결 상태 변화)에서 AcceptConnection으로 수락한다. 접속자별로 Connection 핸들이 하나씩 생기며, 접속자 관리는 이 핸들의 목록을 관리하는 일이다. P2P에서는 IP 주소 대신 SteamID와 가상 포트(virtual port)로 상대를 지정한다.

- 메시지 전송: SendMessageToConnection에 플래그를 준다. Reliable은 순서와 도착을 보장하고, Unreliable은 보장하지 않는다. 메시지 경계가 보존되므로 TCP처럼 길이 헤더를 직접 붙여 자르는 일이 필요 없다.
- 수신: ReceiveMessagesOnConnection 또는 여러 연결을 묶은 Poll Group에서 한꺼번에 받는다. 매 프레임 호출한다.
- 연결 상태: Connecting, Connected, ClosedByPeer, ProblemDetectedLocally 등의 상태 변화가 콜백으로 온다. 접속과 종료를 감지하는 방법이 이 콜백이다. 정상 종료인지 네트워크 문제인지 구분해 사용자에게 알린다.

### 4.12.2 NAT 통과와 중계(SDR)

가정용 공유기 뒤의 PC는 바깥에서 먼저 연결을 걸 수 없는 경우가 많다(NAT). 이 문제를 푸는 방법은 다음과 같다.

- 직접 연결(NAT 통과): 양쪽이 중간 서버의 도움으로 서로의 외부 주소를 알아내고 동시에 패킷을 보내 통로를 연다. 지연이 가장 짧다. 공유기 종류에 따라 실패할 수 있다.
- 중계(Relay): 직접 연결이 안 되면 중계 서버를 거친다. Steam은 전 세계에 중계 네트워크(SDR, Steam Datagram Relay)를 운영한다. 지연이 조금 늘지만 연결이 거의 항상 성공하고, 상대 플레이어의 IP 주소가 노출되지 않는다.

Steam Networking Sockets는 이 둘을 자동으로 선택한다. 중계 네트워크 정보를 미리 받아 두려면 초기화 직후 중계 접근을 초기화하는 호출(InitRelayNetworkAccess)을 한다. 개발자가 NAT 종류를 직접 판별하거나 포트를 열도록 안내하지 않아도 된다는 점이 Steam 연동의 큰 장점이다.

### 4.12.3 일반 소켓과의 차이

- 주소: IP:포트 대신 SteamID와 가상 포트.
- 메시지 단위: 바이트 스트림이 아니라 메시지 단위이므로 프레이밍이 필요 없다.
- 신뢰성 선택: 한 연결에서 메시지마다 신뢰/비신뢰를 고른다.
- 암호화: 연결 데이터가 암호화되어 전송된다.
- 의존성: Steam 클라이언트와 로그인이 필요하다. Steam이 없는 환경(콘솔, 모바일, 다른 스토어)에서는 쓸 수 없으므로 연결 계층을 교체 가능하게 설계하는 것이 좋다. 4.6절에서 일반 소켓 버전을 먼저 만들고 Steam 버전으로 바꿔 보는 이유가 이것이다.

## 4.13 Listen Server의 설계 특성

### 4.13.1 호스트가 곧 서버

Listen Server에서는 한 플레이어의 PC가 서버 프로세스를 겸한다. 장점과 한계가 모두 여기서 나온다.

- 장점: 서버 운영 비용이 없다. 호스트 플레이어는 지연이 없다. 개발이 단순하다.
- 한계: 호스트의 회선과 PC 성능이 방 전체의 품질을 결정한다. 호스트가 종료하면 세션이 끝난다(Host Migration은 자동 제공되지 않는다). 호스트가 규칙을 바꾸는 부정 행위가 가능하다. 호스트의 IP가 노출되는 문제는 SDR 중계로 줄일 수 있다.

호스트 이점(host advantage)도 알아 둔다. 호스트는 입력이 즉시 반영되지만 다른 참가자는 왕복 지연만큼 늦다. 대전 게임에서 불공정하게 느껴질 수 있는 이유이며, 협동 게임이나 친구끼리 하는 게임에서는 크게 문제가 되지 않는다.

### 4.13.2 세션의 생명 주기

```text
로비 생성 -> 친구 초대/검색 -> 로비 입장 -> (호스트 SteamID 획득)
   -> 게임 연결(P2P) -> 접속 승인(이름, 인증 코드, 정원) -> 플레이
   -> 연결 끊김 감지 -> 퇴장 통지 -> (재접속 또는 로비 복귀)
호스트 종료 -> 모든 연결 종료 -> 클라이언트는 사유 표시 후 로비 복귀
```

이 흐름에서 각 전이는 실패할 수 있다. 로비 생성이 실패하거나, 입장한 직후 호스트가 사라지거나, 연결은 맺었는데 승인에서 거부될 수 있으므로 각 단계마다 사용자에게 보이는 메시지를 둔다. 4장 실습의 상태 Text가 이 용도이다.

### 4.13.3 Host Migration

호스트가 나가도 세션을 이어 가려면 남은 참가자 중 한 명이 새 호스트가 되어야 한다. 이를 위해서는 (1) 새 호스트를 정하는 규칙, (2) 게임 상태의 복구(마지막 상태를 모든 참가자가 가지고 있어야 한다), (3) 나머지 참가자의 재연결이 필요하다. 구현 비용이 크고 오류가 많아서 많은 게임이 세션 종료를 택한다. 이 교재도 구현하지 않는다. 안정적인 세션이 필요하면 Dedicated Server(3장, 6장)가 맞는 선택이다.

### 4.13.4 Dedicated 구조로의 전환 비용

이 장의 채팅 프로그램처럼 서버 로직을 UI와 분리해 두면, 호스트 안에 있던 서버 로직을 별도 프로세스로 옮겨 Dedicated Server로 전환할 수 있다. 6장의 C++ 서버가 이 분리를 직접 구현한다. 처음부터 서버 로직과 화면 코드를 섞어 놓으면 전환 비용이 매우 커진다.

## 4.14 4장 연습 문제

1. 로비 입장과 게임 연결을 분리해야 하는 이유를 설명하고, 호스트 SteamID가 어떻게 전달되는지 그려라.
2. 테스트 App ID 480으로 로비를 검색할 때 다른 개발자의 로비가 섞여 나오는 이유와 해결 방법을 설명하라.
3. SteamAPI_RunCallbacks를 호출하지 않으면 어떤 증상이 나타나는지 로비 생성을 예로 들어 설명하라.
4. 직접 연결과 SDR 중계의 장단점을 비교하라.
5. 호스트 이점이 대전 게임에서 문제가 되는 이유를 지연 시간으로 설명하라.
6. Host Migration에 필요한 세 가지 요소를 쓰고, 이 교재가 구현하지 않는 이유를 설명하라.
