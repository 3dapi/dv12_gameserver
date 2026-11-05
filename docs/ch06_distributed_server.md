# 6. Distributed MO Game Server

## 6.1 최종 프로젝트의 목표와 범위

C++ 기반 네트워크 모듈을 이용하여 Gateway, Lobby/Room Manager, 복수의 Game Server 프로세스를 실행한다. 각 Game Server는 여러 Room을 관리할 수 있으며, Room을 서버에 배치하고 클라이언트를 해당 Room에 연결 또는 라우팅한다. 방별 게임 규칙은 서버가 권한을 가진다.

최종 목표에는 Room 생성·입장·퇴장, 게임 진행, 서버 간 관리 메시지, 데이터 저장, 기본적인 부하 테스트를 포함한다. 심리스 월드, Zone 경계의 객체 이전, 대규모 AOI 동기화와 고급 장애 복구는 제외한다.

## 6.2 Asio / IOCP / epoll 핵심 리뷰

IOCP는 Windows의 완료 기반 비동기 I/O 처리 메커니즘이고, epoll은 Linux의 준비 상태 통지 메커니즘이다. Asio는 이를 포함한 운영체제별 I/O 기능을 추상화하여 C++ 프로그램에서 비동기 통신을 구성하도록 돕는 라이브러리이다.

선수과목에서 다룬 TCP/UDP, Blocking/Non-blocking, Overlapped I/O, IOCP와 Session·Packet 처리 원리를 간략히 복습한다. IOCP 서버와 epoll 서버를 각각 재구현하지 않고 Asio를 이용한 통신 모듈을 제작한다.

## 6.3 C++ 공통 네트워크 모듈

io_context와 비동기 수신·송신, Session 수명과 연결 종료, Packet Header/Body 및 프레이밍, 부분 수신·송신과 패킷 분할·병합, Send Queue, Timeout/Heartbeat, 스레드 및 Job Queue를 구현·검증한다.

네트워크 I/O 처리와 게임 로직을 분리한다. 수신 Packet은 검증 후 Handler 및 Job Queue를 거쳐 Game Logic으로 전달하고, 게임 결과는 세션별 송신 큐를 통해 전송한다.

### 6.3.1 게임 서버의 스레드 실행 모델

Network I/O 작업과 Room 게임 로직 작업의 실행 문맥을 구분한다. I/O 스레드에서 받은 Packet을 Job Queue로 전달하고, 같은 Room에 대한 상태 변경은 한 실행 문맥에서 순서대로 처리하도록 설계한다. 공유 데이터의 소유권, Mutex와 Atomic 적용 범위, 잠금 경합과 교착 상태의 위험을 설명한다.

Asio의 io_context와 strand가 제공하는 실행 제어를 검토하되, strand가 모든 게임 상태의 동기화나 스레드 안전성을 자동 보장하는 것으로 설명하지 않는다. 선수과목의 Thread/Mutex 문법 설명 대신 실제 게임 서버 설계에 필요한 실행 모델을 다룬다.

## 6.4 단일 Room Game Server

다중 플레이어가 참여하는 Room을 구현한다. Player ID, Room ID, 입·퇴장, 준비 상태, 게임 시작·진행·종료, 서버 권한 판정 및 게임 결과를 관리한다. 먼저 단일 서버 프로세스에서 여러 Room을 안정적으로 처리하도록 완성한다.

## 6.5 분산 서버 프로세스 구성

Gateway Server: 클라이언트 연결, 인증된 세션과 게임 요청의 전달 또는 접속 경로 안내

Lobby / Room Manager: 방 목록, 생성·검색·입장 요청, Room 배치 및 Game Server 위치 관리

Game Server: 할당된 Room의 게임 루프, 플레이어 상태, 게임 규칙 및 결과 계산

Server-to-Server: 서버 등록·상태 보고·방 생성/삭제 요청·결과 통보 등 프로세스 사이의 관리 메시지

클라이언트가 모든 게임 패킷을 Gateway로 보내는 방식과 Game Server에 별도 연결하는 방식은 구현 선택지로 비교한다. 교재의 실제 코드에서는 한 가지 방식을 명확히 선택하여 일관되게 사용한다.

## 6.6 Room 분산과 서버 확장

복수의 Game Server 프로세스에 새 Room을 분산 배치한다. 서버별 Room 수와 동시 플레이어 수를 기준으로 단순한 배치 정책을 구현하고, 접속·퇴장·Room 종료에 따라 상태를 갱신한다.

서버 증설 시 새로운 Room을 추가 서버에 배치하는 수준을 필수 구현으로 삼는다. 실행 중인 Room을 다른 서버로 실시간 이전하는 기능은 필수 범위에서 제외한다.

### 6.6.1 기능 분산·데이터 분산·부하 분산

기능 분산은 Gateway, Lobby/Room Manager, Game Server 등 역할을 분리하는 것이다. 데이터 분산은 Room의 실시간 상태를 해당 Game Server가 소유하도록 배치하는 것이고, 부하 분산은 신규 Room을 서버별 부하에 따라 배치하는 것이다. 세 방법은 서로 다른 기준이며 동시에 적용할 수 있다.

Room마다 데이터 응집도가 높고 다른 Room과 공유해야 하는 실시간 정보가 적다는 특성을 이용한다. 게임 상태에 대한 단일 소유자를 두어 서버 간 동기화 비용을 줄인다.

## 6.7 데이터베이스 연동 및 검증

RDBMS에 계정과 게임 결과를 저장하며, 필요에 따라 Redis를 로비·서버의 공유 상태나 캐시에 활용한다. 클라이언트 재접속·중복 요청·서버 연결 종료 등 기본 예외 상황을 확인한다.

Dummy Client로 동시 접속, Room 생성/종료, Packet 처리량과 응답 지연을 측정한다. CPU, 메모리, 네트워크 및 DB 호출을 관찰하고 병목을 분석한다.

### 6.7.1 데이터 일관성·보안·장애 처리

Room의 실시간 게임 상태와 영속 데이터의 책임을 구분한다. 게임 종료 결과는 중복 저장되지 않도록 요청 식별자와 멱등성(Idempotency)을 고려하고, RDBMS의 트랜잭션과 Redis 캐시의 갱신·만료·정합성 문제를 점검한다.

클라이언트 요청은 신뢰하지 않는다는 원칙을 적용한다. 인증된 세션의 요청만 수락하고, 패킷 길이·형식·게임 상태·이동 속도·행동 가능 여부 등을 서버에서 검증한다. 잘못된 패킷, 중복 요청, 비정상 종료, 타임아웃, 재접속 등 기본 장애 상황을 재현한다.

## 6.8 단계별 구현 순서

1단계: Asio 기반 단일 서버, Session 및 Packet 송수신

2단계: 단일 Game Server의 다중 Room과 게임 로직

3단계: Lobby/Room Manager와 방 생성·검색·입장

4단계: Gateway 및 복수의 Game Server 구성, Server-to-Server 통신

5단계: Room 분산 배치, DB 저장, Dummy Client 및 부하 테스트

### 6.8.1 C++ 서버 단계별 실습

**[검증 방식]** 2~4장과 같다. C++ 서버도 눈으로 확인할 수 있는 채팅 프로그램으로 먼저 만들고, 2장(2.11절)의 Unity 채팅 GUI 씬을 클라이언트로 사용한다. 2장의 연결 패널과 채팅 패널은 그대로 쓰고, NetworkManager를 호출하던 부분만 TcpChatService 호출로 바꾼다(2.11.8절). 서버 로직을 바꿀 때마다 GUI의 버튼으로 같은 시험을 반복한다. 이 절은 독립 실행형 Asio(헤더 온리, C++20)를 사용하고 Windows에서 빌드한다. 5장의 DB는 실습 5에서 연결한다.

**[패킷 형식]** TCP는 메시지 경계가 없으므로 모든 패킷은 4바이트 헤더(본문 길이 uint16, 종류 uint16, 리틀 엔디언)와 본문으로 구성한다. 본문은 UTF-8 문자열이다. HELLO=1(이름|인증 코드), WELCOME=2, DENY=3(사유), SAY=4(대화), CHAT=5(이름|대화), NOTICE=6, USERS=7(이름 목록, 쉼표 구분), PING=8, PONG=9(받은 본문을 그대로 되돌린다)를 먼저 사용하고, 방·서버 간 패킷은 뒤의 실습에서 추가한다.

```cpp
// Protocol.h — 서버와 Dummy Client가 공유한다
#pragma once
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <string>

enum Type : uint16_t
{
    HELLO = 1, WELCOME, DENY, SAY, CHAT, NOTICE, USERS, PING, PONG,
    ROOM_CREATE = 10, ROOM_JOIN, ROOM_LEAVE, ROOM_LIST, ROOM_LIST_RES, ROOM_ADDR,
    BONUS = 20, BONUS_RES,
    SRV_REGISTER = 100, SRV_REPORT, SRV_CREATE_ROOM, SRV_ROOM_READY
};
const size_t kMaxBody = 1024;                           // 본문 최대 길이

struct Packet
{
    static std::string Make(uint16_t type, const std::string& body)
    {
        uint16_t size = (uint16_t)body.size();
        std::string s(4, '\0');
        std::memcpy(&s[0], &size, 2);                   // Windows(x86/x64)는 리틀 엔디언이다
        std::memcpy(&s[2], &type, 2);
        return s + body;
    }
};
```

실습 1 — Asio 서버: 프레이밍, 로그인, 채팅 중계: 스레드 하나로 접속 수락, 패킷 읽기, 로그인(인증 코드 1234, 이름 중복 거부), 대화 중계, 접속자 목록, 퇴장 처리를 구현한다. 헤더를 먼저 정확히 4바이트 읽고, 그 길이만큼 본문을 읽는다(async_read는 요청한 바이트를 모두 읽을 때까지 완료되지 않는다). 송신은 큐에 쌓고 한 번에 하나의 async_write만 진행한다.

```cpp
// ChatServer.cpp — 실습 1
#include <asio.hpp>
#include <deque>
#include <iostream>
#include <map>
#include <memory>
#include <set>
#include "Protocol.h"
using asio::ip::tcp;
class Server;
using SessionPtr = std::shared_ptr<class Session>;

class Session : public std::enable_shared_from_this<Session>
{
public:
    Session(tcp::socket s, Server& sv) : sock_(std::move(s)), server_(sv) {}
    void Start() { ReadHeader(); }
    void Send(std::string pkt)
    {
        bool idle = out_.empty();
        out_.push_back(std::move(pkt));
        if (idle) Write();                              // 쓰는 중이 아닐 때만 시작한다
    }
    void SendAndClose(std::string pkt) { closeAfter_ = true; Send(std::move(pkt)); }
    void SetName(const std::string& n) { name_ = n; }
    const std::string& Name() const { return name_; }
    void Close();
private:
    void ReadHeader();
    void ReadBody(uint16_t type, uint16_t size);
    void Write();
    tcp::socket sock_;
    Server& server_;
    char hdr_[4];
    std::string body_;
    std::deque<std::string> out_;                       // 보낼 패킷 큐
    std::string name_;
    bool closeAfter_ = false, closed_ = false;
};

class Server
{
public:
    Server(asio::io_context& io, uint16_t port) : acc_(io, tcp::endpoint(tcp::v4(), port)) { Accept(); }
    void OnPacket(const SessionPtr& s, uint16_t type, const std::string& b);
    void Leave(const SessionPtr& s);
private:
    void Accept();
    void Broadcast(const std::string& pkt) { for (auto& kv : users_) kv.second->Send(pkt); }
    std::string UserList()
    {
        std::string r;
        for (auto& kv : users_) r += (r.empty() ? "" : ",") + kv.first;
        return r;
    }
    tcp::acceptor acc_;
    std::set<SessionPtr> all_;                          // 접속한 모든 연결
    std::map<std::string, SessionPtr> users_;           // 로그인한 사용자
};

void Server::Accept()
{
    acc_.async_accept([this](std::error_code ec, tcp::socket sock) {
        if (!ec)
        {
            auto s = std::make_shared<Session>(std::move(sock), *this);
            all_.insert(s);
            s->Start();
        }
        Accept();
    });
}
void Server::OnPacket(const SessionPtr& s, uint16_t type, const std::string& b)
{
    if (type == HELLO && s->Name().empty())
    {
        auto p = b.find('|');
        std::string name = b.substr(0, p);
        std::string code = p == std::string::npos ? "" : b.substr(p + 1);
        if (code != "1234") return s->SendAndClose(Packet::Make(DENY, "code"));
        if (name.empty() || users_.count(name)) return s->SendAndClose(Packet::Make(DENY, "name"));
        s->SetName(name);
        users_[name] = s;
        s->Send(Packet::Make(WELCOME, ""));
        Broadcast(Packet::Make(NOTICE, name + " joined"));
        Broadcast(Packet::Make(USERS, UserList()));
    }
    else if (type == SAY && !s->Name().empty())
        Broadcast(Packet::Make(CHAT, s->Name() + "|" + b));
    else if (type == PING)
        s->Send(Packet::Make(PONG, b));
}
void Server::Leave(const SessionPtr& s)
{
    all_.erase(s);
    if (s->Name().empty()) return;
    users_.erase(s->Name());
    Broadcast(Packet::Make(NOTICE, s->Name() + " left"));
    Broadcast(Packet::Make(USERS, UserList()));
}

void Session::ReadHeader()
{
    auto self = shared_from_this();                     // 완료 처리가 끝날 때까지 세션을 살려 둔다
    asio::async_read(sock_, asio::buffer(hdr_, 4), [this, self](std::error_code ec, size_t) {
        if (ec) return Close();
        uint16_t size, type;
        std::memcpy(&size, hdr_, 2);
        std::memcpy(&type, hdr_ + 2, 2);
        ReadBody(type, size);
    });
}
void Session::ReadBody(uint16_t type, uint16_t size)
{
    auto self = shared_from_this();
    body_.resize(size);
    asio::async_read(sock_, asio::buffer(body_), [this, self, type](std::error_code ec, size_t) {
        if (ec) return Close();
        server_.OnPacket(self, type, body_);
        ReadHeader();
    });
}
void Session::Write()
{
    auto self = shared_from_this();
    asio::async_write(sock_, asio::buffer(out_.front()), [this, self](std::error_code ec, size_t) {
        if (ec) return Close();
        out_.pop_front();
        if (!out_.empty()) Write();
        else if (closeAfter_) Close();                  // 거부 패킷을 보낸 뒤에 닫는다
    });
}
void Session::Close()
{
    if (closed_) return;
    closed_ = true;
    std::error_code ec;
    sock_.close(ec);
    server_.Leave(shared_from_this());
}

int main()
{
    asio::io_context io;
    Server sv(io, 9000);
    io.run();
}
```

Unity 쪽은 TcpChatService가 서버와 같은 패킷 형식으로 송수신한다. 수신은 별도 스레드에서 정확히 필요한 바이트를 읽고, 결과는 큐에 넣어 메인 스레드의 Update에서 이벤트로 알린다(Unity API는 메인 스레드에서만 호출한다). 2장의 `ChatHub`(2.11.2절)와 같은 이벤트 방식이므로 GUI 스크립트는 이벤트를 구독하는 부분만 같다.

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class TcpChatService : MonoBehaviour                // 네트워크 코드: UI를 모른다
{
    public event Action<string> OnLog, OnStatus, OnRooms;
    public event Action<string[]> OnUsers;
    public event Action<string, string, int, string> OnRoomAddr;      // 방, host, port, ticket

    TcpClient _tcp;
    NetworkStream _ns;
    readonly object _wlock = new object();
    readonly ConcurrentQueue<KeyValuePair<ushort, string>> _q = new ConcurrentQueue<KeyValuePair<ushort, string>>();
    public bool Connected => _tcp != null && _tcp.Connected;

    public void Connect(string host, int port, string name, string code)
    {
        Disconnect();
        try { _tcp = new TcpClient(); _tcp.Connect(host, port); }                // 연습용: 잠깐 화면이 멈출 수 있다
        catch (Exception e) { OnStatus?.Invoke("접속 실패: " + e.Message); return; }
        _ns = _tcp.GetStream();
        new Thread(RxLoop) { IsBackground = true }.Start();
        Send(1, name + "|" + code);                                              // HELLO
    }
    public void Disconnect() { try { _tcp?.Close(); } catch (Exception) { } _tcp = null; }

    public void Say(string text) => Send(4, text);
    public void Ping() => Send(8, Time.realtimeSinceStartup.ToString("F3"));
    public void CreateRoom(string n) => Send(10, n);
    public void JoinRoom(string n) => Send(11, n);
    public void LeaveRoom() => Send(12, "");
    public void ListRooms() => Send(13, "");

    public void Send(ushort type, string body) => SendRaw(type, Encoding.UTF8.GetBytes(body));
    public void SendRaw(ushort type, byte[] b)
    {
        if (!Connected) return;
        var pkt = new byte[4 + b.Length];
        pkt[0] = (byte)b.Length; pkt[1] = (byte)(b.Length >> 8);                 // 리틀 엔디언 길이
        pkt[2] = (byte)type;     pkt[3] = (byte)(type >> 8);                     // 리틀 엔디언 종류
        Buffer.BlockCopy(b, 0, pkt, 4, b.Length);
        try { lock (_wlock) _ns.Write(pkt, 0, pkt.Length); } catch (Exception) { }
    }

    void RxLoop()                                                                // 수신 스레드
    {
        var ns = _ns;
        try
        {
            var h = new byte[4];
            while (true)
            {
                ReadExact(ns, h, 4);
                int size = h[0] | (h[1] << 8);
                ushort type = (ushort)(h[2] | (h[3] << 8));
                var b = new byte[size];
                ReadExact(ns, b, size);
                _q.Enqueue(new KeyValuePair<ushort, string>(type, Encoding.UTF8.GetString(b)));
            }
        }
        catch (Exception) { _q.Enqueue(new KeyValuePair<ushort, string>(0, "")); }   // 0: 연결 종료
    }
    static void ReadExact(NetworkStream ns, byte[] buf, int n)                   // n바이트를 모두 읽을 때까지 반복한다
    {
        int o = 0;
        while (o < n) { int r = ns.Read(buf, o, n - o); if (r <= 0) throw new IOException(); o += r; }
    }

    void Update()                                                                // 메인 스레드
    {
        while (_q.TryDequeue(out var kv))
        {
            string b = kv.Value;
            switch (kv.Key)
            {
                case 0:  OnStatus?.Invoke("연결이 끊어졌습니다"); break;
                case 2:  OnStatus?.Invoke("접속되었습니다"); break;
                case 3:  OnStatus?.Invoke("거부됨: " + b); break;
                case 5:  { int p = b.IndexOf('|'); OnLog?.Invoke("[" + b.Substring(0, p) + "] " + b.Substring(p + 1)); break; }
                case 6:  OnLog?.Invoke("* " + b); break;
                case 7:  OnUsers?.Invoke(b.Length == 0 ? new string[0] : b.Split(',')); break;
                case 9:  OnStatus?.Invoke("RTT " + ((Time.realtimeSinceStartup - float.Parse(b)) * 1000f).ToString("F0") + " ms"); break;
                case 14: OnRooms?.Invoke(b); break;
                case 15: { var f = b.Split('|'); OnRoomAddr?.Invoke(f[0], f[1], int.Parse(f[2]), f[3]); break; }
            }
        }
    }
    void OnDestroy() => Disconnect();
}

// 2장 채팅 화면(2.11.4절, uGUI 판은 2.11.9절)의 버튼 처리만 바꾼다 (UI 스크립트는 이벤트를 구독한다)
connectBtn.onClick.AddListener(() => svc.Connect(addrInput.text, int.Parse(portInput.text), nameInput.text, codeInput.text));
sendBtn.onClick.AddListener(() => { svc.Say(chatInput.text); chatInput.text = ""; });
burstBtn.onClick.AddListener(() => { for (int i = 0; i < 10; i++) svc.Say("burst " + i); });   // TCP 스트림에서 패킷이 붙어 오는 상황 시험
svc.OnLog += line => logText.text += line + "\n";
svc.OnStatus += s => statusText.text = s;
svc.OnUsers += names => userText.text = string.Join("\n", names);
```

**[확인할 것]** 서버를 실행하고 Unity 클라이언트(에디터와 빌드본)를 세 개 접속시켜 메시지가 모두에게 한 번씩 표시되고 입장·퇴장 때 접속자 목록이 갱신되는지 확인한다. 틀린 인증 코드와 중복된 이름은 거부 사유가 상태 Text에 표시되어야 한다. 연속 전송 버튼으로 10개를 한꺼번에 보내도 10개가 모두 순서대로 표시되는지 확인한다(TCP는 여러 패킷을 한 번에 붙여 보낼 수 있다). 에디터에서 Play를 멈추면 다른 클라이언트에 left 공지가 오는지 확인한다.

**[흔한 오류]** 헤더를 읽는 중 일부만 도착한 경우를 처리하지 않는 경우(async_read_some이나 Read 한 번에 전부 온다고 가정), 쓰는 중에 async_write를 다시 호출하는 경우(송신 큐를 쓴다), 람다에서 shared_ptr을 캡처하지 않아 완료 처리 전에 세션이 해제되는 경우, 거부 패킷을 보내자마자 닫아 클라이언트가 사유를 받지 못하는 경우.

실습 2 — 안정성: 정원, 타임아웃, 비정상 패킷: 클라이언트는 정상만 보내지 않는다. 서버는 연결 수를 제한하고, 일정 시간 아무것도 보내지 않는 연결을 끊고, 헤더가 거짓인 연결을 끊어야 한다. 실습 1의 코드에 다음을 추가한다.

```cpp
// Protocol.h에 추가
const size_t kMaxConn = 3;                              // 시험을 위해 작게 둔다

// Session — 멤버: asio::steady_timer timer_;  (sock_ 다음에 선언하고 생성자에서 timer_(sock_.get_executor())로 초기화)
void Session::Start() { Arm(); ReadHeader(); }
void Session::Arm()                                     // 마지막 수신 후 30초가 지나면 끊는다
{
    auto self = shared_from_this();
    timer_.expires_after(std::chrono::seconds(30));     // 대기 중이면 취소되고 다시 설정된다
    timer_.async_wait([this, self](std::error_code ec) { if (!ec) Close(); });
}
// ReadHeader의 완료 처리: 비정상 길이 방어
//     if (size > kMaxBody) return Close();
// ReadBody의 완료 처리: OnPacket을 호출하기 전에 Arm()을 호출한다
// Close(): timer_.cancel();

// Server::Accept — 정원
if (all_.size() >= kMaxConn)
{
    auto s = std::make_shared<Session>(std::move(sock), *this);
    s->SendAndClose(Packet::Make(DENY, "full"));
}
else { auto s = std::make_shared<Session>(std::move(sock), *this); all_.insert(s); s->Start(); }

// Unity — 하트비트(10초마다 PING)와 시험용 버튼
bool beat = true;
void Start() { InvokeRepeating(nameof(Beat), 10f, 10f); }
void Beat() { if (beat) svc.Ping(); }
beatToggle.onValueChanged.AddListener(v => beat = v);                       // 끄면 30초 뒤 서버가 끊어야 한다
badBtn.onClick.AddListener(() => svc.SendRaw(4, new byte[60000]));           // 길이 60000: kMaxBody를 넘는 거짓 헤더
```

**[확인할 것]** 클라이언트 네 개를 접속시켜 네 번째가 DENY full을 받는지, 하트비트를 끄고 30초가 지나면 서버가 연결을 끊고 다른 클라이언트에 left 공지가 가는지, 비정상 패킷 버튼을 누르면 서버가 그 연결만 끊고 나머지는 정상 동작하는지, 하트비트를 켜 두면 아무 대화가 없어도 연결이 유지되고 상태 Text에 RTT가 표시되는지 확인한다.

**[흔한 오류]** 타이머를 매 수신마다 새로 만들어 쌓는 경우(expires_after로 다시 설정한다), 정원 초과 연결을 all_에 넣어 정원 계산이 틀어지는 경우, 길이를 검사하기 전에 body_.resize(size)를 호출하는 경우, 잘못된 연결 하나의 예외로 서버 전체가 종료되는 경우.

실습 3 — 작업 분리(Job Queue)와 방(Room) 관리: 실습 1~2의 서버는 I/O 스레드 하나가 모든 일을 한다. 이를 I/O 스레드와 논리 스레드로 나눈다. I/O 스레드는 패킷을 읽어 논리 스레드의 작업 큐에 넣고, 논리 스레드가 사용자와 방 자료구조를 단독으로 소유한다. 자료구조에 접근하는 스레드가 하나이므로 잠금이 필요 없다. 방 만들기, 입장, 나가기, 목록 버튼을 Unity 채팅 씬에 추가하여 검증한다. 패킷 종류는 ROOM_CREATE=10(방 이름), ROOM_JOIN=11(방 이름), ROOM_LEAVE=12, ROOM_LIST=13, ROOM_LIST_RES=14(방이름:인원, 쉼표 구분)를 사용한다. 방 안에서 보낸 대화는 같은 방에만 전달하고, 방에 들어가지 않은 접속자(로비)의 대화는 로비 접속자에게만 전달한다.

```cpp
// main — I/O와 논리를 분리한다
asio::io_context io;                               // 네트워크 I/O (스레드 1개로 시작한다)
asio::io_context logic;                            // 게임 논리 (스레드 1개): 사용자와 방의 유일한 소유자
auto guard = asio::make_work_guard(logic);         // 작업이 없어도 run()이 끝나지 않게 한다
Server sv(io, logic, 9000);
std::thread t([&] { logic.run(); });
io.run();

// Session::ReadBody의 완료 처리 — I/O 스레드에서 논리 스레드로 넘긴다
asio::post(server_.Logic(), [this, self, type, b = body_] { server_.OnPacket(self, type, b); });

// Session::Send — 논리 스레드에서 호출되므로 소켓을 쓰는 I/O 스레드로 넘긴다
void Session::Send(std::string pkt)
{
    auto self = shared_from_this();
    asio::post(sock_.get_executor(), [this, self, p = std::move(pkt)]() mutable {
        bool idle = out_.empty();
        out_.push_back(std::move(p));
        if (idle && !closed_) Write();
    });
}

// Session::Close — 연결 정리도 논리 스레드에서 한다
asio::post(server_.Logic(), [this, self] { server_.Leave(self); });

// Server — 아래 함수는 모두 논리 스레드에서만 실행된다
struct Room { std::set<std::string> members; };
std::map<std::string, Room> rooms_;                // 방 이름 → 방
std::map<std::string, std::string> roomOf_;        // 사용자 이름 → 방 이름
const size_t kRoomMax = 4;

void Server::OnPacket(const SessionPtr& s, uint16_t type, const std::string& b)
{
    switch (type)
    {
    case HELLO:       Login(s, b); break;         // 실습 1과 같다
    case SAY:         Say(s, b); break;
    case ROOM_CREATE: CreateRoom(s, b); break;
    case ROOM_JOIN:   JoinRoom(s, b); break;
    case ROOM_LEAVE:  LeaveRoom(s); break;
    case ROOM_LIST:   SendRoomList(s); break;
    case PING:        s->Send(Packet::Make(PONG, b)); break;
    }
}
void Server::CreateRoom(const SessionPtr& s, const std::string& room)
{
    if (s->Name().empty()) return;                 // 로그인 전
    if (room.empty() || rooms_.count(room)) return s->Send(Packet::Make(NOTICE, "방 이름 오류"));
    rooms_[room] = Room{};
    JoinRoom(s, room);
}
void Server::JoinRoom(const SessionPtr& s, const std::string& room)
{
    if (s->Name().empty()) return;
    auto cur = roomOf_.find(s->Name());
    if (cur != roomOf_.end() && cur->second == room) return;       // 이미 그 방에 있다
    auto it = rooms_.find(room);
    if (it == rooms_.end()) return s->Send(Packet::Make(NOTICE, "방이 없습니다"));
    if (it->second.members.size() >= kRoomMax) return s->Send(Packet::Make(NOTICE, "방이 가득 찼습니다"));
    LeaveRoom(s);                                  // 다른 방에 있었다면 먼저 나간다 (it는 다른 방 삭제에 영향받지 않는다)
    it->second.members.insert(s->Name());
    roomOf_[s->Name()] = room;
    SendToRoom(room, Packet::Make(NOTICE, s->Name() + " joined"));
    SendToRoom(room, Packet::Make(USERS, MembersOf(room)));
    SendLobbyUsers();                              // 로비 목록에서 빠진다
}
void Server::LeaveRoom(const SessionPtr& s)
{
    auto r = roomOf_.find(s->Name());
    if (r == roomOf_.end()) return;
    std::string room = r->second;
    roomOf_.erase(r);
    auto& m = rooms_[room].members;
    m.erase(s->Name());
    if (m.empty()) rooms_.erase(room);             // 빈 방은 삭제한다
    else { SendToRoom(room, Packet::Make(NOTICE, s->Name() + " left")); SendToRoom(room, Packet::Make(USERS, MembersOf(room))); }
    SendLobbyUsers();
}
void Server::Say(const SessionPtr& s, const std::string& text)
{
    if (s->Name().empty()) return;
    std::string pkt = Packet::Make(CHAT, s->Name() + "|" + text);
    auto r = roomOf_.find(s->Name());
    if (r == roomOf_.end()) SendToLobby(pkt); else SendToRoom(r->second, pkt);
}
void Server::SendLobbyUsers()
{
    std::string list; std::vector<SessionPtr> to;
    for (auto& kv : users_) if (!roomOf_.count(kv.first)) { list += (list.empty() ? "" : ",") + kv.first; to.push_back(kv.second); }
    for (auto& s : to) s->Send(Packet::Make(USERS, list));
}
// Leave(연결 종료)는 LeaveRoom(s)을 먼저 호출한 뒤 users_에서 지운다.
```

**[확인할 것]** 방 두 개를 만들어 대화가 섞이지 않는지, 방 정원(4명)을 넘는 입장이 거부되는지, 마지막 사람이 나가면 방이 목록에서 사라지는지, 방 안에서 연결을 끊으면 방과 목록이 정리되는지, 같은 방에 두 번 입장해도 문제가 없는지 확인한다. 수신 스레드와 논리 스레드가 다른데도 자료구조에 잠금이 없는 이유(자료구조에는 논리 스레드만 접근한다)를 코드에서 찾아 설명한다.

**[흔한 오류]** 논리 스레드가 아닌 곳에서 users_나 rooms_를 읽는 경우, 람다가 this만 캡처하고 self를 캡처하지 않아 세션이 먼저 해제되는 경우, I/O 스레드를 여러 개로 늘리면서 세션별 strand를 쓰지 않는 경우(소켓과 송신 큐는 한 번에 한 스레드만 만져야 한다), 방을 비우다가 반복 중인 컨테이너를 지우는 경우.

실습 4 — Lobby와 Game Server 분리, 서버 간 통신: Lobby 서버(9000)는 로그인과 방 목록을 맡고, Game Server(9100, 9101 등)는 방 안의 대화 중계를 맡는다. Game Server는 시작할 때 Lobby에 등록하고 주기적으로 상태(방 수, 접속자 수)를 보고한다. 클라이언트가 방을 만들면 Lobby가 방 수가 가장 적은 Game Server를 골라 방 생성을 요청하고, 준비되면 클라이언트에게 접속 주소와 입장권(ticket)을 알려 준다. 클라이언트는 그 주소로 Game Server에 직접 접속하여 입장권으로 인증한다(6.5에서 비교한 방식 중 클라이언트가 Game Server에 직접 연결하는 방식을 선택한다). 서버 간 패킷은 SRV_REGISTER=100(id|host|port), SRV_REPORT=101(id|방 수|접속자 수), SRV_CREATE_ROOM=102(방|이름|입장권), SRV_ROOM_READY=103(방|id)이고, 클라이언트용으로 ROOM_ADDR=15(방|host|port|입장권)를 사용한다.

**[구현 순서]** 1. Session이 Server 클래스에 의존하지 않도록 IHandler 인터페이스(OnPacket, OnClose, Logic)를 받게 한다. 2. Lobby와 Game Server를 각각 IHandler로 구현한다. 3. Game Server가 Lobby에 connect하여 같은 Session 클래스로 서버 간 연결을 만든다. 4. Unity 채팅 씬에 Lobby용 TcpChatService와 Game Server용 TcpChatService를 하나씩 두고, ROOM_ADDR을 받으면 두 번째 서비스로 접속한다.

```cpp
struct IHandler
{
    virtual void OnPacket(const SessionPtr&, uint16_t type, const std::string& body) = 0;
    virtual void OnClose(const SessionPtr&) = 0;
    virtual asio::io_context& Logic() = 0;
};

// GameServer — Lobby에 접속하는 쪽 (서버 간 연결도 같은 Session 클래스를 쓴다)
void GameServer::ConnectLobby()
{
    auto sock = std::make_shared<tcp::socket>(io_);
    auto eps = tcp::resolver(io_).resolve(lobbyHost_, lobbyPort_);
    asio::async_connect(*sock, eps, [this, sock](std::error_code ec, const tcp::endpoint&) {
        if (ec) { Retry(); return; }                    // 몇 초 뒤 다시 시도한다
        link_ = std::make_shared<Session>(std::move(*sock), *this);
        link_->Start();
        link_->Send(Packet::Make(SRV_REGISTER, id_ + "|" + host_ + "|" + std::to_string(port_)));
    });
}

// Lobby (논리 스레드)
void Lobby::OnPacket(const SessionPtr& s, uint16_t type, const std::string& b)
{
    switch (type)
    {
    case HELLO:          Login(s, b); break;            // 실습 1과 같다
    case ROOM_CREATE:    RequestRoom(s, b); break;
    case ROOM_LIST:      SendRoomList(s); break;
    case SRV_REGISTER:   RegisterGame(s, b); break;     // id|host|port
    case SRV_REPORT:     UpdateLoad(s, b); break;       // id|방 수|접속자 수
    case SRV_ROOM_READY: RoomReady(b); break;           // 방|id
    }
}
void Lobby::RequestRoom(const SessionPtr& s, const std::string& room)
{
    GameInfo* g = PickLeastLoaded();                    // 방 수가 가장 적은 Game Server
    if (!g) return s->Send(Packet::Make(NOTICE, "사용 가능한 Game Server가 없습니다"));
    std::string ticket = NewTicket();                   // 일회용 난수
    pending_[room] = Pending{ s, ticket };
    g->link->Send(Packet::Make(SRV_CREATE_ROOM, room + "|" + s->Name() + "|" + ticket));
}
void Lobby::RoomReady(const std::string& b)             // Game Server가 방을 만들었다
{
    auto f = Split(b, '|');                             // 방, Game Server id
    auto p = pending_.find(f[0]);
    if (p == pending_.end() || !games_.count(f[1])) return;
    GameInfo& g = games_[f[1]];
    roomHost_[f[0]] = f[1];
    p->second.client->Send(Packet::Make(ROOM_ADDR, f[0] + "|" + g.host + "|" + std::to_string(g.port) + "|" + p->second.ticket));
    pending_.erase(p);
}

// GameServer (논리 스레드)
void GameServer::OnPacket(const SessionPtr& s, uint16_t type, const std::string& b)
{
    if (s == link_)                                     // Lobby가 보낸 서버 간 패킷
    {
        if (type == SRV_CREATE_ROOM)
        {
            auto f = Split(b, '|');                     // 방, 이름, 입장권
            rooms_[f[0]];                               // 빈 방을 만든다
            tickets_[f[2]] = Ticket{ f[1], f[0] };
            link_->Send(Packet::Make(SRV_ROOM_READY, f[0] + "|" + id_));
        }
        return;
    }
    if (type == HELLO && s->Name().empty())             // 클라이언트: 이름|입장권
    {
        auto f = Split(b, '|');
        auto t = f.size() == 2 ? tickets_.find(f[1]) : tickets_.end();
        if (t == tickets_.end() || t->second.name != f[0]) return s->SendAndClose(Packet::Make(DENY, "ticket"));
        s->SetName(f[0]);
        users_[f[0]] = s;
        std::string room = t->second.room;
        tickets_.erase(t);                              // 입장권은 한 번만 쓸 수 있다
        s->Send(Packet::Make(WELCOME, ""));
        JoinRoom(s, room);                              // 실습 3의 방 처리를 그대로 사용한다
    }
    else Dispatch(s, type, b);                          // SAY, ROOM_LEAVE, PING 등은 실습 3과 같다
}

// Unity — 두 번째 서비스로 Game Server에 접속한다
lobby.OnRoomAddr += (room, host, port, ticket) => game.Connect(host, port, myName, ticket);
```

**[확인할 것]** Lobby 1개와 Game Server 2개를 실행하고 방을 4개 만들어 두 서버에 2개씩 나뉘는지 확인한다. 한 방의 대화가 다른 방이나 다른 서버의 방에 보이지 않는지, 입장권 없이 또는 한 번 쓴 입장권으로 Game Server에 직접 접속하면 DENY ticket이 오는지 확인한다. Game Server 하나를 강제로 종료하면 그 서버의 접속자만 연결이 끊기고, Lobby는 연결 종료(OnClose)를 감지해 그 서버를 목록에서 빼며, 다시 실행하면 SRV_REGISTER로 돌아오는지 확인한다.

**[흔한 오류]** Game Server가 Lobby에 연결하기 전에 클라이언트의 방 생성을 받는 경우, 방 생성 요청 후 ROOM_READY가 오지 않아 pending_이 남는 경우(시간 제한을 두고 지운다), 입장권을 재사용할 수 있게 두는 경우, Game Server 종료 후에도 Lobby의 방 목록에 그 서버의 방이 남는 경우.

실습 5 — DB 연동과 Dummy Client 부하 시험: Lobby의 로그인을 5장의 DB 계층으로 연결하고, 많은 접속을 흉내 내는 Dummy Client로 서버의 한계를 측정한다. DB 호출은 블로킹이므로 논리 스레드나 I/O 스레드에서 직접 호출하지 않고 5장의 DB Worker 스레드에서 실행한 뒤, 결과를 논리 스레드로 되돌린다. 이제 HELLO의 본문은 아이디|비밀번호이다(실습용 서버에서만 평문 전송을 허용하고, 실제 서비스에서는 TLS 등 보호가 필요하다). BONUS=20 패킷은 서버의 DB 호출이 중복 지급을 막는지 보여 주는 시험 버튼용이다.

```cpp
// Lobby::Login — 논리 스레드
void Lobby::Login(const SessionPtr& s, const std::string& body)
{
    auto f = Split(body, '|');
    if (f.size() != 2) return s->SendAndClose(Packet::Make(DENY, "format"));
    dbWorker_.Post([this, s, f] {                       // DB 스레드: 블로킹 호출은 여기서만 한다
        bool ok = db_.Login(f[0], f[1]);
        asio::post(logic_, [this, s, f, ok] {           // 결과는 논리 스레드로 되돌린다
            if (!ok) return s->SendAndClose(Packet::Make(DENY, "login"));
            FinishLogin(s, f[0]);                       // 중복 접속 확인 후 WELCOME
        });
    });
}

// Lobby::OnPacket에 추가 — 보너스 요청
case BONUS:
    dbWorker_.Post([this, s] {
        std::string r = db_.ClaimBonus(s->Name(), "welcome");   // 5장의 ClaimBonus: "ok:100" 또는 "already"
        asio::post(logic_, [s, r] { s->Send(Packet::Make(BONUS_RES, r)); });
    });
    break;
```

**[확인할 것]** 올바른 계정으로 로그인되고 틀린 비밀번호는 DENY login이 오는지, 보너스 버튼을 두 번 눌렀을 때 두 번째에 already가 오는지 확인한다. DB를 잠시 멈추거나 느리게 만들어도 이미 접속한 사용자의 채팅이 끊기지 않는지 확인한다(DB 호출이 논리 스레드를 막지 않는다는 증거이다).

```cpp
// Dummy.cpp — 사용법: Dummy.exe 127.0.0.1 9000 200 100  (접속 수, 메시지 간격 ms)
#include <asio.hpp>
#include <atomic>
#include <chrono>
#include <functional>
#include <vector>
#include <iostream>
#include <memory>
#include "Protocol.h"
using asio::ip::tcp;
std::atomic<long> g_conn{0}, g_fail{0}, g_sent{0}, g_recv{0}, g_rttSum{0}, g_rttCnt{0};

struct Dummy : std::enable_shared_from_this<Dummy>
{
    tcp::socket sock; asio::steady_timer timer; int id; char hdr[4]; std::string body;
    Dummy(asio::io_context& io, int i) : sock(io), timer(io), id(i) {}

    void Start(const tcp::resolver::results_type& eps, int ms)
    {
        auto self = shared_from_this();
        asio::async_connect(sock, eps, [this, self, ms](std::error_code ec, const tcp::endpoint&) {
            if (ec) { ++g_fail; return; }
            ++g_conn;
            Send(HELLO, "dummy" + std::to_string(id) + "|1234");
            Read();
            Tick(ms);
        });
    }
    void Send(uint16_t type, const std::string& b)       // 간격이 쓰기 시간보다 충분히 길 때만 안전하다(정식 코드는 송신 큐를 쓴다)
    {
        auto pkt = std::make_shared<std::string>(Packet::Make(type, b));
        asio::async_write(sock, asio::buffer(*pkt), [self = shared_from_this(), pkt](std::error_code ec, size_t) { if (!ec) ++g_sent; });
    }
    void Tick(int ms)
    {
        auto self = shared_from_this();
        timer.expires_after(std::chrono::milliseconds(ms));
        timer.async_wait([this, self, ms](std::error_code ec) {
            if (ec) return;
            Send(SAY, "hello");
            if (id == 0) Send(PING, std::to_string(std::chrono::steady_clock::now().time_since_epoch().count()));
            Tick(ms);
        });
    }
    void Read()
    {
        auto self = shared_from_this();
        asio::async_read(sock, asio::buffer(hdr, 4), [this, self](std::error_code ec, size_t) {
            if (ec) return;
            uint16_t size, type; std::memcpy(&size, hdr, 2); std::memcpy(&type, hdr + 2, 2);
            body.resize(size);
            asio::async_read(sock, asio::buffer(body), [this, self, type](std::error_code ec, size_t) {
                if (ec) return;
                ++g_recv;
                if (type == PONG)
                {
                    long long t0 = std::stoll(body), t1 = std::chrono::steady_clock::now().time_since_epoch().count();
                    g_rttSum += (long)((t1 - t0) / 1000000); ++g_rttCnt;     // ms (steady_clock 단위가 ns라고 가정)
                }
                Read();
            });
        });
    }
};

int main(int argc, char** argv)
{
    if (argc < 5) return 1;
    asio::io_context io;
    auto eps = tcp::resolver(io).resolve(argv[1], argv[2]);
    int n = std::stoi(argv[3]), ms = std::stoi(argv[4]);
    std::vector<std::shared_ptr<Dummy>> v;
    for (int i = 0; i < n; ++i) { v.push_back(std::make_shared<Dummy>(io, i)); v.back()->Start(eps, ms); }
    asio::steady_timer stat(io);
    std::function<void()> report = [&] {
        stat.expires_after(std::chrono::seconds(1));
        stat.async_wait([&](std::error_code) {
            long c = g_rttCnt.exchange(0), s = g_rttSum.exchange(0);
            std::cout << "conn " << g_conn << " fail " << g_fail << " sent/s " << g_sent.exchange(0) << " recv/s " << g_recv.exchange(0)
                      << " rtt " << (c ? s / c : -1) << "ms" << std::endl;
            report();
        });
    };
    report();
    io.run();
}
```

**[확인할 것]** 접속 수를 50, 200, 1000으로 늘리며 초당 송수신량과 RTT를 기록한다. 방이 없는 로비 대화는 접속 수의 제곱에 비례하여 전달량이 늘어나므로 수신량이 어떻게 변하는지 확인하고, 같은 부하를 방 단위(실습 3)와 Game Server 2개(실습 4)로 나눴을 때 달라지는 점을 비교한다. 서버의 CPU 사용률(논리 스레드가 100%에 도달하는 시점), 메모리, 연결 실패 수를 함께 기록한다. 같은 PC에서 Dummy와 서버를 함께 실행하면 서로 CPU를 나누어 쓰므로 결과를 일반화하지 않는다.

**[흔한 오류]** Dummy가 한 소켓에 async_write를 겹쳐 호출하는 경우, 방이 없는 대화를 모든 접속자에게 보내 부하를 과대평가하는 경우, 측정 중 로그를 과하게 출력하여 서버가 느려지는 경우, 접속 수가 많은 상태에서 Windows의 임시 포트와 파일 핸들 한도를 확인하지 않는 경우.

**[결과물]** 실행 파일(Lobby, GameServer, Dummy), 패킷 정의(Protocol.h), 서버 구성도, 시험 결과표(실습별 확인 항목과 부하 측정값)를 README와 함께 제출한다.

## 6.9 최종 산출물과 제외 범위

산출물: 실행 가능한 C++ 서버 프로그램들, 테스트용 클라이언트, 프로토콜 정의, 서버 구성도, 데이터베이스 스키마, 실행 방법 및 검증 결과를 포함한 README.

제외: Seamless World, 다수 Zone 간 실시간 상태 이전, 분산 AOI, 고급 자동 확장·Failover, 완전한 MMORPG 서비스. 해당 기술은 후속 고급 교재 또는 별도 프로젝트에서 다룬다.

## 6.10 I/O 모델 심화: select, epoll, IOCP, Asio

6.2절이 요약한 내용을 구현 관점에서 풀어 쓴다. 서버가 Windows에서 돌면 IOCP를, Linux에서 돌면 epoll을 쓰게 되므로 둘 다 이해해야 하고, Asio는 이 둘을 같은 코드로 감싸 주는 도구이다.

### 6.10.1 블로킹, 논블로킹, 다중화

- 블로킹 I/O + 연결당 스레드: 가장 단순하다. 연결 수만큼 스레드가 필요해 수천 연결부터 메모리와 문맥 전환 비용이 커진다.
- 논블로킹 + 다중화(select, poll): 한 스레드가 여러 소켓의 상태를 한꺼번에 감시한다. select는 감시할 소켓 수에 제한(Windows 기본 64개)이 있고, 호출할 때마다 전체 목록을 검사하므로 연결이 많으면 느리다.
- epoll(Linux): 감시 목록을 커널이 유지하고 준비된 소켓만 돌려준다. 연결 수가 많아도 준비된 수에 비례해 처리한다.
- IOCP(Windows): I/O를 운영체제에 맡기고 완료되면 알림을 받는다. 스레드 풀이 완료 큐에서 결과를 꺼내 처리한다.

### 6.10.2 준비 통지(epoll)와 완료 통지(IOCP)

두 방식의 근본 차이는 "언제 알려 주는가"이다.

```text
epoll (Reactor):  "읽을 수 있다" 통지 -> 내가 recv() 호출 -> 데이터 처리
IOCP  (Proactor): "읽어 두었다" 통지(완료) -> 이미 채워진 버퍼를 처리
```

- epoll은 소켓이 읽기/쓰기 가능해지면 알려 준다. 실제 recv/send는 내가 호출하고, 논블로킹 소켓이므로 한 번에 일부만 처리될 수 있다. 버퍼를 다 비울 때까지 반복하거나(에지 트리거), 한 번만 읽고 다음 통지를 기다린다(레벨 트리거). 더 읽을 것이 없으면 EAGAIN/EWOULDBLOCK이 반환된다.
- IOCP는 WSARecv/WSASend로 요청을 미리 걸어 둔다(Overlapped I/O). 요청이 끝나면 GetQueuedCompletionStatus로 완료를 받는다. 요청한 동안 버퍼가 유효해야 하며, 소켓을 닫을 때 걸려 있는 요청이 취소되며 완료가 오는 점을 고려해 객체 수명을 관리해야 한다.

어느 쪽이든 공통으로 지켜야 할 것: 부분 수신과 부분 송신(6.3절의 프레이밍), 송신 큐(한 번에 하나의 쓰기만 진행), 객체 수명 관리(요청이 걸려 있는 동안 세션이 살아 있어야 함), 연결 종료의 모든 경로 처리이다.

### 6.10.3 Asio의 위치

Asio는 Proactor 모델의 인터페이스를 제공한다. async_read, async_write를 호출하면 완료 시 핸들러가 호출된다. Windows에서는 IOCP를 그대로 쓰고, Linux에서는 epoll(Reactor)로 준비를 감지한 뒤 라이브러리가 내부에서 recv/send를 수행해 Proactor처럼 보이게 한다. 그래서 6.8.1절의 서버 코드는 두 플랫폼에서 같은 구조로 동작한다.

- io_context: 핸들러 실행의 중심. io.run()을 호출한 스레드가 핸들러를 실행한다. run을 호출하는 스레드가 하나이면 핸들러는 순차 실행된다.
- shared_from_this 패턴: 비동기 요청이 걸려 있는 동안 세션 객체가 살아 있도록 핸들러가 shared_ptr을 붙잡는다. 6.8.1절 코드의 self가 그것이다.
- strand: io_context를 여러 스레드가 실행할 때 같은 객체의 핸들러가 동시에 실행되지 않게 직렬화한다. 이 교재의 구조는 I/O 스레드 하나와 논리 스레드 하나로 데이터를 분리하므로 strand 없이 안전하지만, I/O 스레드를 늘리면 strand가 필요하다.

Asio를 쓰지 않고 IOCP나 epoll을 직접 구현하면 플랫폼마다 코드가 달라진다. 반대로 Asio를 쓰더라도 아래에서 무슨 일이 일어나는지 알아야 문제를 해결할 수 있다. 연결이 끊긴 줄 모르고 방치되는 현상이나 CPU 점유율이 높은 현상이 그런 예이다.

## 6.11 분산 서버의 설계 원리

### 6.11.1 왜 나누는가

서버 한 대(한 프로세스)의 한계는 CPU 코어, 메모리, 네트워크 대역폭, 장애에서 온다. 접속자가 늘어나면 한 대로는 부족하고, 서버가 죽으면 모든 방이 사라진다. 그래서 역할과 데이터를 여러 프로세스로 나눈다.

- 수직 확장(scale up): 더 좋은 장비로 바꾼다. 단순하지만 한계가 있고 비싸다.
- 수평 확장(scale out): 같은 일을 하는 서버를 늘린다. 한계가 없지만 서버 간 조정이 필요하다.

### 6.11.2 역할의 분리

```text
Client --> [Gateway] --> [Lobby] --> 방 배치 결정
                            |  (Room 생성 요청)
                            v
                     [Game Server 1..N]  <-- 방이 실제로 실행되는 곳
Client ------------ (티켓으로 직접 접속) ------> Game Server
```

- Gateway: 외부 접속을 받고 인증하고 속도 제한을 건다. 내부 서버를 외부에 직접 노출하지 않는다.
- Lobby/Room Manager: 방 목록, 매칭, 방 배치를 맡는다. 어느 Game Server가 여유 있는지 알고 있다.
- Game Server: 방을 실제로 실행한다(6.4절의 단일 Room 서버). 부하가 큰 일은 여기서 한다.
- DB/캐시: 영속 데이터. 서버들이 공유한다.

나누면 각 역할을 독립적으로 확장하고 장애를 격리할 수 있다. 대신 서버 간 통신, 상태 일치, 배포 순서 같은 복잡도가 생긴다. 서버를 나누는 것은 필요가 확인되었을 때 해야 하며, 처음부터 나누지 말고 한 프로세스로 시작해 경계가 분명한 부분부터 떼어 낸다.

### 6.11.3 서버 등록, 상태 보고, 장애 감지

Game Server는 시작하면 Lobby에 자신을 등록하고(SRV_REGISTER), 주기적으로 현재 방 수와 접속자 수를 보고한다(SRV_REPORT). 이 보고가 겸해서 생존 신호(heartbeat)이다. Lobby가 일정 시간 보고를 받지 못하면 그 서버를 장애로 보고 방 배치 대상에서 제외한다.

- 방 배치 전략: 가장 여유 있는 서버(least loaded), 순서대로(round robin), 지역이 가까운 서버 중 선택. 단순한 전략부터 시작하고 지표를 보고 조정한다.
- 장애 시 영향: Game Server가 죽으면 그 서버의 방은 사라진다. 방 상태를 외부에 저장하지 않았다면 복구할 수 없으므로, 플레이어에게 알리고 로비로 돌려보낸다. 이 교재는 고급 장애 복구를 제외 범위로 두었지만(6.9절) 위 동작은 구현한다.
- 우아한 종료(graceful shutdown): 서버를 내릴 때는 새 방 배정을 멈추고(drain) 진행 중인 방이 끝나기를 기다리거나 플레이어에게 공지한 뒤 종료한다.

### 6.11.4 티켓을 이용한 접속

플레이어가 Lobby에서 방 배정을 받으면 Game Server의 주소와 함께 일회용 티켓(방 ID, 사용자, 만료 시각을 포함한 값)을 받는다. 플레이어는 이 티켓으로 Game Server에 접속하고, Game Server는 티켓이 유효한지 확인한다. 티켓은 서버만 만들 수 있게 서명하거나 Lobby와 Game Server 사이에서 확인하며, 만료와 일회성으로 재사용을 막는다. 이렇게 하면 임의 사용자가 임의 방에 들어오는 것을 막고, Game Server가 인증 로직을 중복하지 않아도 된다.

### 6.11.5 상태를 어디에 두는가

서버가 상태를 자기 메모리에 들고 있으면(stateful) 빠르지만 그 서버가 죽으면 상태를 잃고 같은 사용자의 요청이 항상 같은 서버로 가야 한다. 상태를 외부 저장소(DB, Redis)에 두면(stateless) 어떤 서버가 받아도 처리할 수 있고 서버를 쉽게 늘릴 수 있지만 요청마다 외부 접근 비용이 든다. 게임의 실시간 방은 stateful이 자연스럽고, Gateway나 Lobby는 가능한 한 stateless에 가깝게 만드는 것이 일반적이다.

### 6.11.6 서버 간 메시지의 신뢰성

TCP는 한 연결 안에서 바이트의 순서와 도착을 보장하지만, 서버 사이의 "요청 하나"가 처리되었다는 것까지 보장하지는 않는다. Lobby가 SRV_CREATE_ROOM을 보낸 직후 Game Server와의 연결이 끊겼다고 하자. Lobby는 방이 만들어졌는지 알 수 없다. 요청이 도착하기 전에 끊겼을 수도 있고, 방을 만든 뒤 응답(SRV_ROOM_READY)을 보내기 전에 끊겼을 수도 있기 때문이다. 서버 간 통신은 이런 불확실함을 전제로 설계한다.

- 응답을 기다리고 시간 제한을 둔다: SRV_CREATE_ROOM을 보낸 뒤 SRV_ROOM_READY가 정해진 시간(예: 5초) 안에 오지 않으면 실패로 처리한다. 6.8.1절 실습 4에서 `pending_`이 남는 오류가 바로 이것을 빠뜨린 경우이다. 실패하면 요청한 클라이언트에게 사유를 알린다. 조용히 무시하면 사용자는 응답을 계속 기다린다.
- 재시도는 횟수와 간격을 제한한다: 응답이 없다고 즉시 계속 다시 보내면 이미 힘든 서버에 부하를 더한다. 간격을 점점 늘리고(지수 백오프, 예: 1초, 2초, 4초) 횟수의 상한(예: 3회)을 둔다. 상한을 넘으면 실패로 처리한다.
- 재시도해도 결과가 같게 만든다(멱등성): 재시도하면 같은 요청이 두 번 도착할 수 있다. 실습 4의 Game Server는 같은 방 이름과 입장권으로 SRV_CREATE_ROOM이 다시 와도 `rooms_[방]`과 `tickets_[입장권]`을 같은 값으로 다시 쓸 뿐이어서 결과가 달라지지 않는다. 다만 입장권을 이미 한 번 쓴 뒤에 같은 요청이 늦게 도착하면 입장권이 다시 살아나므로, 사용한 입장권은 일정 시간 기억해 두었다가 거절해야 한다. 반면 "골드를 100 더하라"처럼 요청마다 효과가 쌓이는 메시지는 요청 식별자를 붙이고, 받은 쪽이 처리한 식별자를 기억해 두었다가 중복이면 이전 결과만 돌려주어야 한다(6.7.1절, 5장의 중복 지급 방지).
- 순서에 기대지 않는다: 서로 다른 연결로 온 메시지는 순서가 바뀔 수 있다. 상태를 갱신하는 메시지에는 순번이나 시각을 붙여 오래된 것을 버린다. SRV_REPORT처럼 "가장 최근 값"만 의미 있는 메시지는 마지막 값만 쓰면 된다.
- 연결이 끊긴 것을 알아차린다: 상대가 비정상으로 사라지면 TCP 연결이 끊기지 않은 채 남기도 한다(전원 차단, 네트워크 단절). 연결 종료 통지만 기다리지 말고 SRV_REPORT의 주기를 하트비트로 쓴다(6.11.3절).

### 6.11.7 장애 시나리오와 기대 동작

장애는 일어나는 것으로 가정하고, 어떤 장애에서 무엇을 보고(감지), 서버가 무엇을 해야 하는지(기대 동작)를 미리 정해 두어야 시험할 수 있다. 이 교재의 구현 범위를 함께 적는다.

- Game Server 하나가 종료되거나 비정상 종료한다
  - 감지: Lobby가 서버 간 연결의 종료(OnClose)를 감지하거나 SRV_REPORT가 끊긴 것을 감지한다.
  - 기대 동작: Lobby는 그 서버를 방 배치 대상에서 빼고 그 서버에 있던 방을 목록에서 지운다. 그 서버에 접속해 있던 플레이어는 연결이 끊긴 것을 알고 로비로 돌아온다.
  - 교재 범위: 실습 4의 [확인할 것]에서 확인한다. 방의 상태를 다른 서버에서 복구하는 것은 제외한다(6.9절).
- Lobby가 종료된다
  - 감지: Game Server의 Lobby 연결(link_)이 끊긴다.
  - 기대 동작: 이미 진행 중인 방은 클라이언트가 Game Server에 직접 연결되어 있으므로 계속 동작한다. 새 방은 만들 수 없고, Game Server는 몇 초 간격으로 Lobby에 다시 연결해 SRV_REGISTER를 보낸다.
  - 교재 범위: 실습 4의 `Retry`는 처음 연결에 실패했을 때만 쓰인다. 중간에 연결이 끊긴 뒤에도 다시 연결하게 하려면 서버 간 연결의 OnClose에서 ConnectLobby를 다시 호출해야 한다(연습 문제 14). 또 Lobby는 방 목록을 메모리에만 두므로 다시 시작하면 진행 중인 방을 모른다. 이를 막으려면 Game Server가 등록할 때 자신의 방 목록을 함께 보내거나 방 목록을 Redis 같은 외부 저장소에 두어야 한다. 이 교재는 구현하지 않고 한계로 기록한다.
- DB가 느려지거나 응답하지 않는다
  - 감지: DB Worker의 대기열 길이가 늘어나고 질의가 시간 제한을 넘긴다.
  - 기대 동작: 게임 루프는 멈추지 않는다(최종 점검 (7)). 로그인과 저장 요청은 시간 제한 뒤에 실패로 응답하고, 대기열이 한도를 넘으면 새 로그인을 거절한다. 결과 저장은 요청 식별자를 붙여 DB가 돌아온 뒤 다시 시도해도 한 번만 반영되게 한다(5장).
  - 교재 범위: 5장의 DB Worker 분리를 사용한다. 재시도 대기열의 영속화는 제외한다.
- 서버 간 네트워크가 끊겼지만 연결은 닫히지 않는다
  - 감지: SRV_REPORT가 시간 안에 오지 않는다.
  - 기대 동작: 시간 제한 뒤 그 연결을 직접 닫고 첫 번째 시나리오와 같이 처리한다.
  - 교재 범위: 6.11.3절의 장애 감지로 처리한다.
- 접속이 한꺼번에 몰린다
  - 감지: 동시 연결 수, 로그인 시도 속도, 틱 처리 시간이 한계에 다가간다.
  - 기대 동작: 정원을 넘는 연결은 `DENY full`로 거절하고(실습 2), 세션별 속도 제한으로 한 연결이 서버를 독점하지 못하게 한다(6.15.2절). 서버가 느려졌다고 해서 이미 접속한 사용자의 연결을 끊지 않는다.
  - 교재 범위: 실습 2와 6.12.2절의 역압, Dummy로 재현한다.

이 목록은 시험 계획의 출발점이다. 각 시나리오를 실제로 재현하고 기대 동작과 실제 동작이 같은지 기록한다(6.13.5절, 최종 점검 (11)).

## 6.12 성능 설계와 측정

### 6.12.1 틱 예산과 병목

서버 성능은 한 틱 안에 일을 끝내는 능력이다. 30Hz에서는 33ms 안에 입력 처리, 로직, 전송 준비를 마쳐야 한다. 병목은 대개 아래 네 가지 중 하나이다.

- CPU: 로직 계산이나 직렬화가 느림. 프로파일러로 상위 함수를 찾는다.
- 메모리: 잦은 할당과 해제, 복사. 객체 풀과 버퍼 재사용으로 줄인다.
- 네트워크: 대역폭 포화, 작은 패킷의 과다. 묶어 보내고(batching) 압축·양자화·관심 영역으로 전송량을 줄인다.
- 잠금 경쟁: 여러 스레드가 같은 잠금을 기다림. 데이터 소유를 분리해 잠금을 없앤다.

### 6.12.2 역압(Backpressure)과 느린 클라이언트

서버가 보내는 속도가 클라이언트가 받는 속도보다 빠르면 송신 큐가 무한히 커져 서버 메모리가 고갈된다. 세션별 송신 큐의 최대 길이를 정해 두고, 넘으면 오래된 상태 갱신을 버리거나 그 클라이언트를 끊는다. 느린 클라이언트 하나가 서버 전체를 위협하지 못하게 하는 장치이다.

### 6.12.3 측정 방법

1. 목표를 정한다: 예) 방 하나에 8명, 서버 한 대에 방 100개, 틱 30Hz 유지, 틱 처리 시간 최대 20ms 이하.
2. 지표를 만든다: 틱 처리 시간(평균, 최대), 접속자 수, 초당 수신·송신 패킷과 바이트, 송신 큐 길이, 메모리.
3. Dummy 클라이언트로 접속자를 단계적으로 늘리며 지표가 목표를 넘는 지점을 찾는다.
4. 병목을 프로파일러(Visual Studio 프로파일러, Linux의 perf 등)로 확인하고 가장 큰 것부터 고친다.
5. 고친 뒤 같은 시험을 다시 해 개선을 숫자로 확인한다.

측정 없이 고치는 최적화는 대부분 틀린 곳을 고친다. 시험 환경(같은 장비에서 서버와 Dummy를 함께 돌리는 경우)이 결과에 영향을 주므로, 결과를 보고할 때는 장비 사양과 시험 조건을 함께 기록한다.

## 6.13 테스트

6.8.1절의 [확인할 것]은 Unity 채팅 GUI에서 사람이 눈으로 확인하는 방식이다. 처음 만들 때는 이 방식이 가장 확실하다. 그러나 서버 코드를 고칠 때마다 같은 확인을 손으로 반복하면 빠뜨리는 항목이 생기고, 빠뜨린 항목에서 오류가 나온다. 반복해야 하는 확인은 프로그램으로 만들어 두고 코드를 고칠 때마다 실행한다. 이 절은 시험의 종류를 구분하고, 단위 시험과 통합 시험을 만들고, 시험이 찾지 못하는 오류를 샌니타이저로 찾고, 부하·장애 시험과 결과 기록까지 다룬다.

**[검증 환경]** 이 절과 6.14절의 명령과 출력은 Ubuntu 24.04(g++ 13.3, clang++ 18.1, CMake 3.28, 독립 실행형 Asio 1.30.2)에서 직접 실행해 얻었다. Windows(Visual Studio)에서의 설정은 각 도구의 문서를 따라 적었고 같은 방식으로 실행해 보지 못했으므로 출력의 모양이 다를 수 있다.

### 6.13.1 시험의 종류

- 단위 시험(unit test): 함수나 클래스 하나를 소켓과 DB 없이 시험한다. 밀리초 단위로 끝나고, 실패하면 어느 함수가 틀렸는지 바로 알 수 있다. 예: Packet::Make, 헤더 검증, 날짜 계산, 속도 제한.
- 통합 시험(integration test): 실행 중인 서버에 실제로 접속해서 여러 부분이 함께 동작하는지 본다. 초 단위로 걸리고, 실패하면 원인을 따로 찾아야 한다. 예: 로그인, 대화 중계, 갑작스러운 연결 종료.
- 부하 시험: Dummy 클라이언트로 많은 접속을 만들어 서버의 한계를 잰다(6.12.3절).
- 장애 시험: 프로세스 종료, 연결 단절, 느린 클라이언트처럼 일부러 문제를 일으켜 6.11.7절에서 정한 기대 동작이 나오는지 확인한다.
- 회귀 시험(regression test): 한 번 고친 오류가 다시 나오지 않게 하는 시험이다. 오류를 고칠 때는 먼저 그 오류를 재현하는 시험을 쓰고, 시험이 실패하는 것을 확인한 뒤에 고치고, 시험이 통과하는 것을 확인한다. 이 시험은 지우지 않고 계속 실행한다.

시험은 아래쪽(단위 시험)이 많고 위쪽(통합, 부하 시험)이 적은 모양이 일반적이다. 통합 시험만 있으면 느리고 원인을 찾기 어렵고, 단위 시험만 있으면 부분이 맞아도 합쳐서 틀리는 경우를 놓친다. 둘 다 필요하다.

### 6.13.2 단위 시험

단위 시험이 가능하려면 시험할 코드가 소켓, 시계, DB 같은 바깥 세계를 직접 부르지 않아야 한다. 입력을 값으로 받아 결과를 값으로 돌려주는 함수는 시험하기 쉽다. 반대로 함수 안에서 `system_clock::now()`를 부르면 시험 결과가 실행하는 시각에 따라 달라진다. 현재 시각은 인자로 받게 하면 시험에서 원하는 시각을 넣을 수 있다.

6.8.1절의 서버는 `Session::ReadHeader`의 완료 처리 안에서 헤더를 해석하고(실습 2에서는 그 안에서 길이도 검사한다) 곧바로 다음 읽기로 넘어가므로, 검사 부분만 따로 시험할 수 없다. 검사 부분을 함수로 꺼내는 것이 첫 작업이다. Protocol.h의 끝에 다음을 추가한다.

```cpp
// Protocol.h에 추가 — 네트워크 코드에서 분리한 순수 함수
inline bool IsKnownType(uint16_t t)
{
    return (t >= HELLO && t <= PONG) || (t >= ROOM_CREATE && t <= ROOM_ADDR) ||
           (t >= BONUS && t <= BONUS_RES) || (t >= SRV_REGISTER && t <= SRV_ROOM_READY);
}
inline bool IsValidHeader(uint16_t size, uint16_t type)
{
    return size <= kMaxBody && IsKnownType(type);
}
```

실습 2에서 추가한 `if (size > kMaxBody) return Close();` 줄은 `if (!IsValidHeader(size, type)) return Close();`로 바꾼다. 이제 알 수 없는 종류의 패킷을 보낸 연결도 끊는다(6.15.3절). 새 패킷 종류를 enum에 추가하면 IsKnownType에도 추가해야 한다. 잊으면 새 패킷을 보낸 연결이 끊기는데, 아래 단위 시험에 새 종류를 한 줄 추가해 두면 잊지 않는다.

5.16.3절에서 쓴 서비스 날짜 계산도 시각을 인자로 받는 순수 함수이므로 별도 헤더로 두고 시험한다.

```cpp
// GameTime.h — 서비스 날짜 계산 (네트워크와 무관한 순수 함수)
#pragma once
#include <chrono>

inline long long GameDay(std::chrono::system_clock::time_point tp)     // 한국 시간 오전 5시를 하루의 시작으로 본다
{
    using namespace std::chrono;
    return floor<days>(tp + hours(9) - hours(5)).time_since_epoch().count();
}
```

시험 프로그램은 소켓 없이 위 함수들만 호출한다. 외부 시험 라이브러리(GoogleTest, Catch2 등)를 쓰면 편하지만 이 교재는 설치 없이 따라 할 수 있도록 매크로 하나로 만든다.

```cpp
// test_protocol.cpp — 소켓 없이 시험할 수 있는 순수 함수만 모았다
#include <chrono>
#include <cstdio>
#include "GameTime.h"
#include "Protocol.h"
using namespace std::chrono;

static int g_fail = 0;
#define CHECK(cond) do { if (!(cond)) { std::printf("FAIL %s:%d  %s\n", __FILE__, __LINE__, #cond); ++g_fail; } } while (0)

system_clock::time_point Kst(int y, unsigned m, unsigned d, int h, int mi)     // 한국 시각 -> time_point
{
    return sys_days{year{y} / month{m} / day{d}} + hours(h) + minutes(mi) - hours(9);
}

void TestPacket()
{
    std::string p = Packet::Make(HELLO, "kim|1234");     // 헤더 4바이트(길이, 종류, 리틀 엔디언) + 본문
    CHECK(p.size() == 4 + 8);
    CHECK((unsigned char)p[0] == 8 && (unsigned char)p[1] == 0);        // 길이 8
    CHECK((unsigned char)p[2] == 1 && (unsigned char)p[3] == 0);        // 종류 1
    CHECK(p.substr(4) == "kim|1234");
    CHECK(Packet::Make(PING, "").size() == 4);                          // 빈 본문
}

void TestHeader()
{
    CHECK(IsValidHeader(1023, HELLO));
    CHECK(IsValidHeader(1024, HELLO));                   // 경계: kMaxBody
    CHECK(!IsValidHeader(1025, HELLO));
    CHECK(!IsValidHeader(60000, SAY));                   // 거짓 길이
    CHECK(!IsValidHeader(0, 0));                         // 알 수 없는 종류
    CHECK(!IsValidHeader(0, 9999));
    CHECK(IsValidHeader(0, PING));                       // 빈 본문은 허용
    CHECK(IsValidHeader(5, SRV_ROOM_READY));             // 서버 간 패킷
    CHECK(!IsValidHeader(5, 16));                        // 정의되지 않은 번호(ROOM_ADDR=15 다음)
}

void TestGameDay()
{
    CHECK(GameDay(Kst(2026, 11, 4, 4, 59)) == GameDay(Kst(2026, 11, 3, 12, 0)));   // 오전 5시 전은 전날
    CHECK(GameDay(Kst(2026, 11, 4, 5, 0)) != GameDay(Kst(2026, 11, 4, 4, 59)));     // 5시 정각에 바뀐다
    CHECK(GameDay(Kst(2026, 11, 4, 5, 0)) == GameDay(Kst(2026, 11, 4, 23, 59)));
    CHECK(GameDay(Kst(2026, 12, 31, 23, 0)) + 1 == GameDay(Kst(2027, 1, 1, 5, 0)));  // 해가 바뀌어도 하루씩 증가
}

int main()
{
    TestPacket();
    TestHeader();
    TestGameDay();
    if (g_fail == 0) std::puts("ALL PASSED");
    return g_fail ? 1 : 0;                                // 0이 아니면 ctest가 실패로 본다
}
```

CMakeLists.txt에 시험 프로그램을 등록한다. `enable_testing`과 `add_test`를 쓰면 `ctest`가 등록된 시험을 모두 실행하고 하나라도 실패하면 실패로 보고한다.

```cmake
enable_testing()                                    # 단위 시험: ctest로 실행한다
add_executable(test_protocol test_protocol.cpp)
add_test(NAME protocol COMMAND test_protocol)
```

```text
cmake --build build
ctest --test-dir build --output-on-failure

    Start 1: protocol
1/1 Test #1: protocol .........................   Passed    0.00 sec

100% tests passed, 0 tests failed out of 1
```

Visual Studio 생성기처럼 구성(Debug, Release)이 여러 개인 경우에는 `ctest --test-dir build -C Release --output-on-failure`처럼 구성을 지정한다.

**시험이 제 역할을 하는지 확인한다.** 시험이 통과한다는 사실만으로는 시험이 쓸모 있다는 것을 알 수 없다. 코드를 일부러 틀리게 고쳐 시험이 실패하는지 확인한다. 아래는 실제로 고쳐서 얻은 결과이다.

- GameTime.h의 `hours(5)`를 `hours(4)`로 바꾼다(기준 시각을 한 시간 틀리게). `GameDay`의 경계 시험 두 줄이 실패했다.
  `FAIL test_protocol.cpp:41  GameDay(Kst(2026, 11, 4, 4, 59)) == GameDay(Kst(2026, 11, 3, 12, 0))`
  `FAIL test_protocol.cpp:42  GameDay(Kst(2026, 11, 4, 5, 0)) != GameDay(Kst(2026, 11, 4, 4, 59))`
- IsValidHeader의 `size <= kMaxBody`를 `size < kMaxBody`로 바꾼다(경계값을 하나 틀리게). 경계 시험이 실패했다.
  `FAIL test_protocol.cpp:29  IsValidHeader(1024, HELLO)`
- IsKnownType의 `t <= ROOM_ADDR`를 `t <= ROOM_ADDR + 1`로 바꾼다(정의되지 않은 번호를 통과시키게). 정의되지 않은 번호 시험이 실패했다.
  `FAIL test_protocol.cpp:36  !IsValidHeader(5, 16)`

첫째와 둘째는 경계의 양쪽(4시 59분과 5시 정각, 1024와 1025)을 함께 시험했기 때문에, 셋째는 정의되지 않은 번호를 직접 넣어 보았기 때문에 잡혔다. 경계의 양쪽을 시험하는 것이 단위 시험의 기본이다.

좋은 단위 시험의 기준은 다음과 같다.

- 경계값을 시험한다: 최대 길이의 직전, 정확히 최대, 직후. 하루가 바뀌는 시각의 직전과 정각. 연말과 연초.
- 한 시험은 한 가지 이유로만 실패한다: 이름만 보고 무엇이 틀렸는지 알 수 있어야 한다.
- 시험끼리 독립이다: 실행 순서나 앞 시험의 결과에 기대지 않는다.
- 결과가 매번 같다(결정적): 현재 시각, 난수, 스레드 타이밍에 기대지 않는다. 시각은 인자로 받고 난수는 씨앗을 정한다.
- 빠르다: 전체가 몇 초 안에 끝나야 코드를 고칠 때마다 실행한다.

### 6.13.3 통합 시험: [최종 점검]을 프로그램으로

6.16절의 [최종 점검] 항목 중 서버에 접속해서 확인하는 것은 프로그램으로 만들 수 있다. 아래의 IntegrationTest는 실행 중인 서버에 접속해서 헤더 쪼개기, 인증 거부, 대화 중계, 갑작스러운 종료, 거짓 길이, 정원 초과를 차례로 확인하고 결과를 출력한다. 종료 코드가 0이면 모두 통과이다.

```cpp
// IntegrationTest.cpp — 실행 중인 서버에 접속해 [최종 점검]의 일부를 자동으로 확인한다
// 사용: IntegrationTest [포트]   (서버를 먼저 실행해 둔다. 기본 포트 9000)
#include <asio.hpp>
#include <chrono>
#include <cstdio>
#include <functional>
#include <thread>
#include "Protocol.h"
using asio::ip::tcp;
using namespace std::chrono_literals;
using ms = std::chrono::milliseconds;

struct Reply
{
    bool ok = false;                // 패킷을 받았는가
    bool closed = false;            // 상대가 연결을 닫았는가 (ok=false이고 closed=false이면 시간 초과)
    uint16_t type = 0;
    std::string body;
};

class TestClient
{
public:
    explicit TestClient(uint16_t port) : sock_(io_)
    {
        sock_.connect(tcp::endpoint(asio::ip::make_address("127.0.0.1"), port));
    }
    void SendRaw(const std::string& bytes) { asio::write(sock_, asio::buffer(bytes)); }
    void Send(uint16_t type, const std::string& body) { SendRaw(Packet::Make(type, body)); }

    Reply Recv(ms timeout = 1000ms)                     // 패킷 하나를 받는다
    {
        Reply r;
        char h[4];
        if (auto ec = ReadExact(h, 4, timeout)) { r.closed = ec != asio::error::timed_out; return r; }
        uint16_t size, type;
        std::memcpy(&size, h, 2);
        std::memcpy(&type, h + 2, 2);
        r.body.resize(size);
        if (size > 0)
            if (auto ec = ReadExact(r.body.data(), size, timeout)) { r.closed = ec != asio::error::timed_out; return r; }
        r.type = type;
        r.ok = true;
        return r;
    }
    Reply WaitFor(const std::function<bool(const Reply&)>& pred, ms timeout = 1500ms)   // 조건에 맞는 패킷이 올 때까지 읽는다
    {
        auto end = std::chrono::steady_clock::now() + timeout;
        Reply r;
        while (std::chrono::steady_clock::now() < end)
        {
            r = Recv(100ms);
            if (r.ok && pred(r)) return r;
            if (r.closed) break;
        }
        return Reply{};
    }
    bool WaitClosed(ms timeout = 1500ms)                // 서버가 연결을 닫을 때까지 남은 패킷을 읽어 버린다
    {
        auto end = std::chrono::steady_clock::now() + timeout;
        while (std::chrono::steady_clock::now() < end)
            if (Recv(100ms).closed) return true;
        return false;
    }
    void Abort()                                        // RST로 끊는다 (프로세스가 갑자기 죽은 것과 비슷하다)
    {
        std::error_code ec;
        sock_.set_option(asio::socket_base::linger(true, 0), ec);
        sock_.close(ec);
    }
private:
    std::error_code ReadExact(char* p, size_t n, ms timeout)
    {
        std::error_code result = asio::error::would_block;
        asio::async_read(sock_, asio::buffer(p, n), [&](std::error_code ec, size_t) { result = ec; });
        io_.restart();
        io_.run_for(timeout);
        if (result == asio::error::would_block)         // 시간 안에 끝나지 않았다
        {
            sock_.cancel();
            io_.restart();
            io_.run();                                  // 취소된 완료 처리를 비운다
            return asio::error::timed_out;
        }
        return result;
    }
    asio::io_context io_;
    tcp::socket sock_;
};

static uint16_t g_port = 9000;
static int g_fail = 0;
#define CHECK(cond) do { if (!(cond)) { std::printf("    FAIL line %d: %s\n", __LINE__, #cond); ++g_fail; } } while (0)

bool Login(TestClient& c, const std::string& name)
{
    c.Send(HELLO, name + "|1234");
    return c.WaitFor([](const Reply& r) { return r.type == WELCOME; }).ok;
}
bool GotNotice(TestClient& c, const std::string& text)
{
    return c.WaitFor([&](const Reply& r) { return r.type == NOTICE && r.body == text; }).ok;
}

void TestSplitHeader()          // 최종 점검 (1): 헤더가 쪼개져 도착해도 파싱된다
{
    TestClient c(g_port);
    for (char ch : Packet::Make(HELLO, "split|1234"))
    {
        c.SendRaw(std::string(1, ch));                  // 한 바이트씩 보낸다
        std::this_thread::sleep_for(10ms);
    }
    CHECK(c.WaitFor([](const Reply& r) { return r.type == WELCOME; }).ok);
}
void TestDeny()                 // 최종 점검 (2): 틀린 인증 코드와 중복 이름이 거부된다
{
    TestClient bad(g_port);
    bad.Send(HELLO, "bad|0000");
    Reply r = bad.Recv();
    CHECK(r.ok && r.type == DENY && r.body == "code");
    CHECK(bad.WaitClosed());                            // 거부 패킷을 받은 뒤 연결이 닫힌다

    TestClient a(g_port), b(g_port);
    CHECK(Login(a, "dup"));
    b.Send(HELLO, "dup|1234");
    r = b.Recv();
    CHECK(r.ok && r.type == DENY && r.body == "name");
}
void TestChatAndPing()          // 대화 중계와 PING/PONG
{
    TestClient a(g_port), b(g_port);
    CHECK(Login(a, "alice") && Login(b, "bob"));
    a.Send(SAY, "hi");
    CHECK(b.WaitFor([](const Reply& r) { return r.type == CHAT && r.body == "alice|hi"; }).ok);
    b.Send(PING, "x");
    CHECK(b.WaitFor([](const Reply& r) { return r.type == PONG && r.body == "x"; }).ok);
}
void TestAbruptClose()          // 최종 점검 (4): 클라이언트가 갑자기 끊겨도 접속자 목록이 갱신된다
{
    TestClient a(g_port);
    CHECK(Login(a, "stay"));
    {
        TestClient b(g_port);
        CHECK(Login(b, "gone"));
        CHECK(GotNotice(a, "gone joined"));
        b.Abort();
    }
    CHECK(GotNotice(a, "gone left"));
    CHECK(a.WaitFor([](const Reply& r) { return r.type == USERS && r.body == "stay"; }).ok);
}
void TestFakeLength()           // 실습 2 이후: 거짓 길이 헤더를 보낸 연결만 끊긴다
{
    TestClient a(g_port), b(g_port);
    CHECK(Login(a, "honest") && Login(b, "liar"));
    b.SendRaw(std::string("\x60\xEA\x04\x00", 4));      // 길이 60000(0xEA60, 리틀 엔디언이므로 60 EA), 종류 SAY, 본문 없음
    CHECK(b.WaitClosed());
    a.Send(PING, "still");
    CHECK(a.WaitFor([](const Reply& r) { return r.type == PONG && r.body == "still"; }).ok);
}
void TestCapacity()             // 실습 2 이후, 최종 점검 (3): 정원 초과가 거부된다 (kMaxConn = 3)
{
    TestClient c1(g_port), c2(g_port), c3(g_port);
    CHECK(Login(c1, "u1") && Login(c2, "u2") && Login(c3, "u3"));
    TestClient c4(g_port);
    Reply r = c4.Recv();
    CHECK(r.ok && r.type == DENY && r.body == "full");
}

struct Case { const char* name; void (*fn)(); };

int main(int argc, char** argv)
{
    if (argc > 1) g_port = (uint16_t)std::stoi(argv[1]);
    const Case cases[] = {
        {"헤더 쪼개기", TestSplitHeader}, {"인증 거부", TestDeny}, {"대화와 PING", TestChatAndPing},
        {"갑작스러운 종료", TestAbruptClose}, {"거짓 길이", TestFakeLength}, {"정원", TestCapacity},
    };
    try
    {
        for (const Case& c : cases)
        {
            int before = g_fail;
            c.fn();
            std::printf("%s  %s\n", g_fail == before ? "PASS" : "FAIL", c.name);
            std::this_thread::sleep_for(300ms);         // 서버가 이전 시험의 연결 종료를 처리할 시간을 준다
        }
    }
    catch (const std::exception& e)
    {
        std::printf("시험을 진행하지 못했다: %s\n서버가 포트 %d에서 실행 중인지 확인하라.\n", e.what(), g_port);
        return 2;
    }
    std::puts(g_fail == 0 ? "ALL PASSED" : "SOME FAILED");
    return g_fail ? 1 : 0;
}
```

서버를 한 창에서 실행하고 다른 창에서 IntegrationTest를 실행한다. 실습 1까지만 만든 서버에서는 앞의 네 시험만 통과하고 실습 2에서 만드는 기능을 확인하는 마지막 두 시험은 실패한다. 실패하는 것이 정상이다. 실습 2를 마친 서버에서는 모두 통과해야 한다. 아래는 실제로 두 서버에 실행한 결과이다. 실습 1 서버에 실행한 결과는 다음과 같다.

```text
PASS  헤더 쪼개기
PASS  인증 거부
PASS  대화와 PING
PASS  갑작스러운 종료
    FAIL line 156: b.WaitClosed()
FAIL  거짓 길이
    FAIL line 166: r.ok && r.type == DENY && r.body == "full"
FAIL  정원
SOME FAILED
```

실습 2를 마친 서버에 실행한 결과는 다음과 같다.

```text
PASS  헤더 쪼개기
PASS  인증 거부
PASS  대화와 PING
PASS  갑작스러운 종료
PASS  거짓 길이
PASS  정원
ALL PASSED
```

서버를 실행하지 않고 IntegrationTest를 실행하면 `시험을 진행하지 못했다: connect: Connection refused`를 출력하고 종료 코드 2로 끝난다. 시험이 실패한 것(1)과 시험을 진행하지 못한 것(2)을 구분하기 위해서이다.

이 시험 프로그램에서 눈여겨볼 점은 다음과 같다.

- 시간 제한: 서버가 응답하지 않아도 시험이 멈추지 않도록 모든 수신에 시간 제한(`run_for`)을 둔다. 동기 방식으로 읽으면 서버의 오류 하나가 시험 전체를 멈추게 한다.
- 일부러 나쁘게 보내기: 헤더를 한 바이트씩 보내고, 길이를 속이고, 연결을 RST로 끊는다. 정상적인 클라이언트는 이런 상황을 만들지 않으므로 시험 프로그램이 대신 만들어야 한다.
- 시험끼리 독립: 시험마다 다른 이름으로 접속하고, 한 시험이 끝나면 연결을 닫고 서버가 정리할 시간을 준다. 앞 시험의 연결이 남아 있으면 정원 시험이 틀어진다.
- 하트비트 30초 시험은 넣지 않았다. 느린 시험은 자동 실행에서 빼고 실습 2의 GUI로 확인하거나, 시간 제한을 설정으로 빼서(6.14.3절) 시험할 때만 짧게 준다.

**[흔한 오류]** 서버를 먼저 실행하지 않는 경우, 이전 시험 때문에 서버에 같은 이름이 남아 `DENY name`이 오는 경우(서버를 다시 시작한다), 시험 사이의 대기가 짧아 정원이 모자란 경우, 시간 제한을 너무 짧게 잡아 느린 PC에서 간헐적으로 실패하는 경우. 간헐적으로 실패하는 시험은 믿을 수 없는 시험이 되어 결국 무시되므로 원인을 찾아 고친다.

### 6.13.4 샌니타이저와 정적 분석

모든 시험이 통과해도 정의되지 않은 동작(undefined behavior)은 남아 있을 수 있다. 해제된 메모리를 읽어도 운이 좋으면 이전 값이 그대로 남아 있어서 아무 일도 일어나지 않는다. 샌니타이저는 컴파일러가 실행 파일에 검사 코드를 넣어, 이런 오류가 일어나는 순간 보고하게 하는 도구이다.

- AddressSanitizer(ASan): 해제된 메모리 접근(use-after-free), 배열 범위 밖 접근(buffer overflow), 이중 해제 등을 찾는다.
- UndefinedBehaviorSanitizer(UBSan): 부호 있는 정수의 넘침, 잘못된 시프트, 널 포인터 접근 같은 정의되지 않은 동작을 찾는다.
- ThreadSanitizer(TSan): 잠금 없이 두 스레드가 같은 데이터에 접근하는 경쟁 상태(data race)를 찾는다. 6.3.1절처럼 I/O 스레드와 논리 스레드를 나눈 구조를 점검할 때 쓴다. ASan과는 같이 쓸 수 없어 따로 빌드한다.

g++와 clang++는 컴파일과 링크 옵션에 `-fsanitize=address,undefined -fno-omit-frame-pointer -g`를 주면 된다. TSan은 `-fsanitize=thread`이다. CMake에서는 아래 옵션을 CMakeLists.txt에 두고(6.14.4절의 전체 파일에 들어 있다) `-DENABLE_SANITIZERS=ON`으로 설정한다.

```cmake
option(ENABLE_SANITIZERS "ASan/UBSan을 켠다 (g++, clang)" OFF)
if(ENABLE_SANITIZERS AND NOT MSVC)
  add_compile_options(-fsanitize=address,undefined -fno-omit-frame-pointer)
  add_link_options(-fsanitize=address,undefined)
endif()
```

```text
cmake -S . -B build-san -DASIO_DIR=<Asio 폴더> -DENABLE_SANITIZERS=ON -DCMAKE_BUILD_TYPE=Debug
cmake --build build-san
```

Visual Studio(MSVC)에서는 위 옵션을 쓰지 않는다. 프로젝트 속성의 `C/C++ > General > Enable AddressSanitizer`(컴파일러 옵션 `/fsanitize=address`)를 켠다. Visual Studio 2019 16.9 이상에서 지원되고, 편집하며 계속하기(`/ZI`), `/RTC1`, 증분 링크(`/INCREMENTAL`)와는 함께 쓸 수 없으므로 끈다. 이 글을 쓸 때 확인한 Microsoft 문서는 AddressSanitizer만 지원 대상으로 설명한다. UBSan과 TSan이 필요하면 Linux(또는 WSL)에서 g++나 clang++로 빌드한다.

**실험: 시험이 놓치는 오류.** 실습 2까지 완성한 서버에서 `ReadHeader`, `ReadBody`, `Write`의 완료 처리가 붙잡고 있는 `self`를 일부러 지운다. 6.8.1절의 [흔한 오류] 중 "람다에서 shared_ptr을 캡처하지 않아 완료 처리 전에 세션이 해제되는 경우"를 만드는 것이다.

```cpp
// 일부러 틀리게 고친다 (ReadHeader, ReadBody, Write 모두 같은 방식)
asio::async_read(sock_, asio::buffer(hdr_, 4), [this](std::error_code ec, size_t) {       // [this, self] -> [this]
// ReadBody에서는 self를 쓰던 호출도 바꾼다
server_.OnPacket(shared_from_this(), type, body_);
```

이 서버를 일반 빌드(`-O2`)로 만들어 IntegrationTest를 세 번 반복 실행했을 때 세 번 모두 `ALL PASSED`였고 서버도 죽지 않았다. 시험은 오류를 보지 못한 것이다. 같은 서버를 ASan으로 빌드해 IntegrationTest를 실행하면 두 번째 시험 중에 서버가 다음 보고를 출력하고 멈춘다. 줄 번호는 각자의 파일에 따라 다르고, 긴 호출 스택은 줄였다.

```text
==2204==ERROR: AddressSanitizer: heap-use-after-free on address 0x5140000003d1 at pc ...
READ of size 1 at 0x5140000003d1 thread T0
    #0 in Session::Close() ChatServer.cpp:150
    #1 in operator() ChatServer.cpp:119
...
freed by thread T0 here:
    #12 in ~<lambda> ChatServer.cpp:113
...
previously allocated by thread T0 here:
    #9 in operator() ChatServer.cpp:74
...
SUMMARY: AddressSanitizer: heap-use-after-free ChatServer.cpp:150 in Session::Close()
```

보고서는 세 가지를 알려 준다. 잘못 접근한 곳(`Session::Close`의 `if (closed_)`, 150줄)과 그 호출자(`ReadHeader`의 완료 처리, 119줄), 이미 해제한 곳(`Arm`이 붙잡던 타이머 완료 처리가 끝나면서 마지막 shared_ptr이 사라진 곳, 113줄), 객체를 만든 곳(`Accept`의 `make_shared`, 74줄)이다. 이 세 곳을 따라가면 "읽기 완료 처리가 세션을 살려 두지 않아서, 다른 곳이 마지막 참조를 놓는 순간 세션이 사라진다"는 원인을 알 수 있다. 눈으로 코드를 읽어서는 찾기 어렵고, 시험도 통과했던 오류이다.

샌니타이저를 쓸 때 지킬 점은 다음과 같다.

- 대개 두 배 안팎으로 느려지고 메모리도 더 쓴다. 성능 측정(6.12.3절)에는 쓰지 않고, 개발 중 통합 시험을 돌릴 때와 배포 전 점검에 쓴다.
- 보고된 오류는 고치기 전에 재현 시험(회귀 시험)을 하나 추가한다.
- 샌니타이저가 오류를 찾지 못했다는 것이 오류가 없다는 뜻은 아니다. 시험이 실행한 경로에서만 검사한다.

정적 분석 도구는 코드를 실행하지 않고 읽어서 의심스러운 곳을 알려 준다. 컴파일러 경고(`-Wall -Wextra`, MSVC는 `/W4`)를 켜고 경고를 0으로 유지하는 것이 가장 쉬운 시작이다. clang-tidy의 `bugprone-*` 검사를 실습 1의 ChatServer.cpp에 실행한 결과 다음 세 가지를 보고했다.

```text
ChatServer.cpp:113:24: warning: 2 adjacent parameters of 'ReadBody' of similar type ('uint16_t') are easily swapped by mistake [bugprone-easily-swappable-parameters]
ChatServer.cpp:138:5: warning: the value returned by this function should not be disregarded; neglecting it may lead to errors [bugprone-unused-return-value]
ChatServer.cpp:142:5: warning: an exception may be thrown in function 'main' which should not throw exceptions [bugprone-exception-escape]
```

첫째는 `ReadBody(type, size)`의 두 인자가 같은 형이라 순서를 바꿔 호출해도 컴파일되는 문제이다. 둘째는 `sock_.close(ec)`의 반환값을 쓰지 않았다는 경고인데, 오류를 `ec`로 받는 형태라 이 코드에서는 문제가 아니다. 정적 분석의 보고는 이처럼 사람이 판단해야 한다. 셋째는 진짜 문제이다. `main`에서 예외가 밖으로 나가면 프로그램이 `terminate`로 비정상 종료한다. 같은 포트로 서버를 두 번 실행하면 이 일이 벌어진다(6.14.2절). `main` 전체를 `try`/`catch`로 감싸 원인을 출력하고 종료 코드 1로 끝나게 고친다(6.14.3절의 main).

### 6.13.5 부하 시험과 장애 시험

부하 시험은 6.12.3절의 절차로 Dummy 클라이언트를 단계적으로 늘리며 지표를 기록한다. 이 절에서는 정상적인 사용으로는 만들기 어려운 상황을 일부러 만드는 장애 시험을 다룬다. 6.11.7절의 시나리오 중 최소한 두 가지를 골라 재현한다.

- 서버 프로세스를 강제로 종료한다: Windows는 작업 관리자나 `taskkill /F /IM GameServer.exe`, Linux는 `kill -9 <PID>`이다. 정상 종료와 달리 서버가 종료 처리를 할 기회가 없다. Lobby가 이를 감지하는 시간과 감지한 뒤의 동작을 기록한다.
- 연결을 갑자기 끊는다: IntegrationTest의 `Abort`처럼 RST로 끊거나, 케이블을 뽑는 것처럼 패킷 없이 사라지는 경우를 구분한다. 후자는 하트비트 시간 제한(또는 TCP keepalive)으로 알 수 있다.
- 느린 클라이언트를 만든다: 접속해서 아무것도 읽지 않는 클라이언트를 만들고 다른 클라이언트가 계속 대화를 보낼 때 송신 큐 길이와 서버 메모리가 어떻게 변하는지 본다(6.12.2절).
- 네트워크 상태를 나쁘게 만든다: 같은 PC의 루프백 통신은 지연과 손실이 거의 없어서 실제 환경과 다르다. Windows에서는 Clumsy(WinDivert를 쓰는 네트워크 상태 흉내 도구, 저장소의 README는 Windows 7, 8, 10을 지원한다고 적고 있다), Linux에서는 `tc`의 netem을 쓴다. 예를 들어 `sudo tc qdisc add dev eth0 root netem delay 100ms loss 1%`는 지연 100ms와 손실 1%를 만들고, `sudo tc qdisc del dev eth0 root`로 되돌린다. 이 도구들은 이 교재를 쓰면서 실행해 보지 못했다. 사용하기 전에 해당 도구의 문서에서 지원하는 환경과 사용법을 확인한다.
- DB를 느리게 만든다: 시험용 DB에서 긴 질의를 일부러 실행하거나(MySQL의 `SELECT SLEEP(5)`) DB 서비스를 잠시 멈추고, 게임 루프의 틱 시간이 길어지지 않는지(최종 점검 (7)) 확인한다.

장애 시험은 시험용 환경에서만 한다. 운영 서버에서 장애를 일으키는 시험(카오스 엔지니어링)은 감지·복구 절차가 갖춰진 뒤에 계획을 세워 진행하는 별도의 주제이다.

### 6.13.6 시험 결과의 기록

시험은 실행하고 끝이 아니라 기록으로 남아야 한다. 같은 시험이 나중에 실패했을 때 무엇이 달라졌는지 비교할 기준이 되기 때문이다. 시험 결과는 README의 시험 절에 다음 항목으로 적는다.

- 대상: 시험한 서버의 버전 문자열(6.14.3절)과 빌드 종류.
- 환경: 운영체제, CPU, 메모리, 컴파일러와 버전, 서버와 시험 프로그램이 같은 PC인지 여부.
- 시험 이름과 절차, 기대 결과, 실제 결과, 판정(통과/실패).
- 실패한 시험: 재현 절차, 서버 로그, 호출 스택(6.14.2절), 처리 결과(고친 커밋 또는 알려진 한계로 기록).

단위 시험과 통합 시험은 매번 같은 결과가 나와야 하므로 결과표 대신 "모두 통과"와 실행한 버전만 적으면 된다. 부하 시험과 장애 시험은 숫자와 관찰이 결과이므로 항목별로 자세히 적는다.

## 6.14 빌드와 배포

서버를 만드는 일은 실행 파일이 만들어지는 것으로 끝나지 않는다. 같은 소스에서 언제나 같은 실행 파일을 만들 수 있어야 하고, 운영 중에 문제가 생기면 그 실행 파일이 어느 소스로 만들어졌는지 알 수 있어야 하고, 다른 PC로 옮겨 실행할 수 있어야 하고, 새 버전에 문제가 있으면 이전 버전으로 되돌릴 수 있어야 한다. 이 절은 빌드 종류, 심볼 파일과 크래시 분석, 버전 표시와 설정 파일, 배포 묶음, 서비스 실행, 배포 절차를 순서대로 다룬다. 검증 환경은 6.13절과 같다.

### 6.14.1 빌드 종류

- Debug: 최적화를 끄고 디버깅 정보를 넣는다. 중단점을 걸고 변수를 확인하기 쉽지만 느리다. 개발 중 오류를 추적할 때 쓴다.
- Release: 최적화를 켠다(g++와 clang은 `-O3`, MSVC는 `/O2`). `NDEBUG`를 정의해 `assert`를 없앤다. 가장 빠르지만 디버깅 정보가 없어 변수 값을 확인하기 어렵다.
- RelWithDebInfo: 최적화(`-O2`)를 켜고 디버깅 정보를 더한다. 실행 속도는 Release에 가깝고 크래시 분석이 가능하다. 성능 측정과 배포용 빌드의 기본으로 삼는다. 6.14.4절의 CMakeLists.txt는 종류를 지정하지 않으면 이것을 쓴다.

서버 성능 측정(6.12.3절)과 배포는 최적화된 빌드로 한다. Debug 빌드로 잰 숫자는 같은 코드가 최적화와 검사 코드 때문에 크게 느려진 값이어서 의미가 없다. 반대로 Debug에서는 정상인데 Release에서만 틀리게 동작한다면 초기화하지 않은 변수나 정의되지 않은 동작(6.13.4절)일 가능성이 크다.

CMake로 종류를 고르는 방법은 생성기에 따라 다르다. Makefile이나 Ninja처럼 구성이 하나인 생성기는 설정할 때 정한다. Visual Studio처럼 구성이 여러 개인 생성기는 빌드할 때 정한다. 위의 최적화 옵션은 g++에서 CMake가 실제로 쓴 값으로 확인했다(Debug는 `-g`, RelWithDebInfo는 `-O2 -g -DNDEBUG`, Release는 `-O3 -DNDEBUG`).

```text
cmake -S . -B build -DASIO_DIR=<Asio 폴더> -DCMAKE_BUILD_TYPE=Release       (Makefile, Ninja)
cmake --build build

cmake -S . -B build -DASIO_DIR=<Asio 폴더>                                  (Visual Studio)
cmake --build build --config Release                                         (실행 파일: build\Release\ChatServer.exe)
```

### 6.14.2 심볼 파일과 크래시 분석

서버가 비정상 종료하면 "어느 함수의 몇 번째 줄에서 죽었는가"를 알아야 고칠 수 있다. 이 정보는 디버깅 정보(심볼)에 들어 있다. 그런데 배포하는 실행 파일에 디버깅 정보를 그대로 두면 크기가 커지고(아래 예에서 약 16배), 소스 구조를 역분석하기 쉬워진다. 그래서 디버깅 정보를 실행 파일에서 분리해 **빌드마다 따로 보관**하고, 운영 서버에는 분리한 실행 파일만 둔다.

- Windows(MSVC)에서는 링커가 만드는 `.pdb` 파일이 심볼 파일이다. Release 계열 빌드에서도 만들 수 있고(CMake의 RelWithDebInfo, 또는 프로젝트 속성에서 디버그 정보 생성), 실행 파일과 PDB는 같은 빌드에서 나온 것끼리만 짝이 맞는다. 서버가 비정상 종료할 때 덤프 파일(.dmp)을 남기게 하고(Windows 오류 보고의 LocalDumps 설정, 또는 프로그램에서 `MiniDumpWriteDump` 함수 호출) Visual Studio에서 덤프 파일을 열어 같은 빌드의 PDB로 호출 스택을 본다. 이 부분은 이 교재를 쓰면서 Windows에서 실행해 보지 못했으므로 Microsoft 문서의 절차를 확인하며 진행한다.
- Linux(g++, clang)에서는 `objcopy`와 `strip`으로 디버깅 정보를 따로 떼어 낸다. 아래 명령은 실제로 실행해 확인한 것이다.

```text
cmake --install build --prefix dist --strip                                # 디버깅 정보를 뺀 실행 파일을 dist에 설치
mkdir symbols
objcopy --only-keep-debug build/ChatServer symbols/ChatServer.debug       # 원본에서 디버깅 정보만 따로 저장
objcopy --add-gnu-debuglink=symbols/ChatServer.debug dist/ChatServer       # 짝이 되는 심볼 파일의 이름을 실행 파일에 기록
```

RelWithDebInfo로 만든 `build/ChatServer`는 1,921,136바이트였고, 분리한 `dist/ChatServer`는 121,344바이트, `symbols/ChatServer.debug`는 1,803,296바이트였다. `strip`만 쓰고 심볼 파일을 따로 저장하지 않으면 그 빌드의 디버깅 정보는 영영 사라진다. 순서대로 실행한다.

실제 크래시로 분석을 연습해 본다. 실습 1~2의 서버는 `main`에서 예외를 잡지 않으므로, 서버를 두 번 실행하면 두 번째 서버가 이미 사용 중인 포트에 연결하려다 예외가 밖으로 나가 비정상 종료한다. core 파일이 만들어지도록 `ulimit -c unlimited`를 실행한 뒤 시험한다. 배포판에 따라 core 파일이 현재 폴더에 만들어지지 않고 `coredumpctl` 같은 도구로 관리되기도 한다.

```text
ulimit -c unlimited
cd dist
./ChatServer &                       (첫 번째 서버)
./ChatServer                         (두 번째 서버)
terminate called after throwing an instance of 'std::system_error'
  what():  bind: Address already in use
Aborted (core dumped)
```

`ChatServer.debug`가 없는 상태에서 `gdb ./ChatServer core`를 실행하고 `bt`(호출 스택 출력)를 입력하면 우리 코드의 프레임이 모두 `??`로 나온다. 어디서 죽었는지 알 수 없다.

```text
#9  0x000055c0a9688fc1 in ?? ()
#10 0x000055c0a9695f36 in ?? ()
#11 0x000055c0a9696c03 in ?? ()
#12 0x000055c0a9689034 in ?? ()
#13 0x00007efc9c22a1ca in __libc_start_call_main (...)
```

`ChatServer.debug`를 실행 파일과 같은 폴더에 두고 같은 명령을 실행하면 함수 이름과 파일, 줄 번호가 나온다. (출력의 파일 경로와 인자는 줄였다.)

```text
#9  ... in asio::detail::throw_exception<std::system_error> (e=...) at asio/detail/throw_exception.hpp:46
#10 ... in asio::detail::do_throw_error (err=..., location=... "bind") at asio/detail/impl/throw_error.ipp:41
#11 ... in asio::detail::throw_error (location=0x... "bind", err=...) at asio/detail/throw_error.hpp:50
#12 ... asio::basic_socket_acceptor<...>::basic_socket_acceptor<asio::io_context> (this=..., context=..., endpoint=..., ...)
#13 ... in Server::Server (port=9000, io=..., this=...) at ChatServer.cpp:44
#14 main () at ChatServer.cpp:145
```

호출 스택은 아래(#14)에서 위로 읽는다. `main`의 145줄에서 `Server`를 만들었고, 생성자(44줄)가 acceptor를 만들면서 `bind`가 실패해 예외가 던져졌다. 우리 코드와 라이브러리 코드가 섞여 있으면 `ChatServer.cpp`가 나오는 프레임부터 본다. 두 파일이 같은 빌드에서 나왔는지는 빌드 ID로 확인할 수 있다. `readelf -n ChatServer`와 `readelf -n ChatServer.debug`가 같은 `Build ID`를 출력해야 한다.

심볼 파일을 다룰 때의 규칙은 다음과 같다.

- 배포하는 빌드마다 실행 파일, 심볼 파일, 소스 커밋(6.14.3절의 버전)을 한 세트로 보관한다. 이름에 버전 문자열을 넣는다.
- 심볼 파일은 운영 서버에 두지 않는다. 그 버전이 운영에 남아 있는 동안은 반드시 보관한다.
- 최적화된 빌드에서는 함수가 합쳐지거나 변수가 사라져 `<optimized out>`이 나오기도 한다. 그래도 어느 함수의 몇 번째 줄 근처인지는 알 수 있다.

크래시를 분석하는 순서는 다음과 같다.

1. core나 덤프 파일과 서버 로그를 모은다.
2. 로그의 시작 줄(6.14.3절의 버전 출력)로 어느 빌드인지 확인한다.
3. 그 빌드의 심볼 파일을 가져와 호출 스택을 본다.
4. 해당 줄의 소스를 읽고 원인을 가설로 세운다.
5. 재현하는 시험(6.13절)을 먼저 추가하고 실패하는 것을 확인한 뒤에 고친다.

### 6.14.3 버전 표시와 설정 파일

**버전.** 운영 중인 서버가 어느 소스로 만든 것인지 알 수 있어야 한다. 버전 번호(주.부.수정, 예: 0.1.0)에 짧은 git 커밋 해시를 붙여 `0.1.0-be8ac97`처럼 쓰고, 서버가 시작할 때 로그에 한 줄로 남긴다. CMake가 이 값을 만들어 `SERVER_VERSION`이라는 정의로 컴파일에 넣는다(6.14.4절의 CMakeLists.txt). 커밋 해시는 CMake를 설정하는 시점의 값이므로, 커밋이 바뀐 뒤 다시 설정하지 않으면 갱신되지 않는다. 배포용 빌드는 새 폴더에서 설정부터 다시 한다. 접속 응답(WELCOME)이나 별도 패킷에 버전을 담는 것은 1.6.3절의 프로토콜 버전과 함께 연습 문제로 남긴다(연습 문제 20).

**설정 파일.** 포트, 정원, DB 주소처럼 환경마다 다른 값은 코드에 쓰지 않고 설정 파일에서 읽는다. 그래야 개발 PC, 시험 서버, 운영 서버에서 같은 실행 파일을 쓸 수 있다. `키 = 값` 형식의 설정 파일을 읽는 작은 클래스는 다음과 같다. 숫자여야 하는 값이 숫자가 아니면 기본값으로 조용히 넘어가지 않고 오류를 던져 서버가 시작할 때 알리게 한다. 설정이 잘못된 채로 서버가 도는 것보다 시작하지 않는 편이 낫다.

```cpp
// Config.h — "키 = 값" 형식의 설정 파일을 읽는다. '#' 뒤는 주석이다.
#pragma once
#include <charconv>
#include <fstream>
#include <map>
#include <sstream>
#include <stdexcept>
#include <string>

class Config
{
public:
    bool Load(const std::string& path)                  // 파일이 없으면 false
    {
        std::ifstream f(path);
        if (!f) return false;
        Parse(f);
        return true;
    }
    void Parse(std::istream& in)
    {
        std::string line;
        while (std::getline(in, line))
        {
            if (auto hash = line.find('#'); hash != std::string::npos) line.erase(hash);
            auto eq = line.find('=');
            if (eq == std::string::npos) continue;
            std::string k = Trim(line.substr(0, eq)), v = Trim(line.substr(eq + 1));
            if (!k.empty()) kv_[k] = v;
        }
    }
    std::string GetString(const std::string& key, const std::string& def) const
    {
        auto it = kv_.find(key);
        return it == kv_.end() ? def : it->second;
    }
    int GetInt(const std::string& key, int def) const   // 값이 있는데 숫자가 아니면 기본값으로 넘기지 않고 오류로 알린다
    {
        auto it = kv_.find(key);
        if (it == kv_.end()) return def;
        const std::string& s = it->second;
        int v = 0;
        auto r = std::from_chars(s.data(), s.data() + s.size(), v);
        if (r.ec != std::errc() || r.ptr != s.data() + s.size())
            throw std::runtime_error("config: '" + key + "' must be an integer, got '" + s + "'");
        return v;
    }
private:
    static std::string Trim(const std::string& s)
    {
        auto b = s.find_first_not_of(" \t\r"), e = s.find_last_not_of(" \t\r");
        return b == std::string::npos ? "" : s.substr(b, e - b + 1);
    }
    std::map<std::string, std::string> kv_;
};
```

`server.example.ini`는 저장소에 올리는 견본이다. 서버 PC에서 `server.ini`로 복사해 고쳐 쓰고, 실제 값이 든 `server.ini`는 저장소에 올리지 않는다(.gitignore에 적는다).

```text
# server.example.ini — 이 파일을 server.ini로 복사해서 고쳐 쓴다 (server.ini는 저장소에 올리지 않는다)
port = 9000
```

ChatServer.cpp는 맨 위에 다음을 추가하고, `main`을 아래로 바꾼다. 이 `main`은 두 가지를 더 한다. 하나는 시작할 때 버전을 출력하는 것이고, 하나는 `try`/`catch`로 모든 예외를 잡아 원인을 출력하고 종료 코드 1로 끝나는 것이다. 6.13.4절의 clang-tidy가 지적한 문제(예외가 `main` 밖으로 나가 비정상 종료)를 고치는 것이다.

```cpp
// ChatServer.cpp — include 목록에 추가
#include "Config.h"
// using asio::ip::tcp; 위에 추가
#ifndef SERVER_VERSION
#define SERVER_VERSION "dev"                      // CMake가 실제 값을 넣는다
#endif

// main을 바꾼다
int main(int argc, char** argv)
{
    try
    {
        Config cfg;
        std::string path = argc > 1 ? argv[1] : "server.ini";
        if (!cfg.Load(path)) std::cerr << "설정 파일이 없어 기본값을 쓴다: " << path << "\n";
        int port = cfg.GetInt("port", 9000);
        asio::io_context io;
        Server sv(io, (uint16_t)port);
        std::cout << "ChatServer " << SERVER_VERSION << " port " << port << std::endl;
        io.run();
    }
    catch (const std::exception& e)
    {
        std::cerr << "fatal: " << e.what() << "\n";      // 예: bind: Address already in use
        return 1;
    }
}
```

실행 결과는 다음과 같다. 서버를 두 번 실행해도 이제 core 파일을 남기며 죽지 않고 원인을 출력하고 끝난다.

```text
./ChatServer
설정 파일이 없어 기본값을 쓴다: server.ini
ChatServer 0.1.0-be8ac97 port 9000

./ChatServer                      (이미 실행 중일 때)
설정 파일이 없어 기본값을 쓴다: server.ini
fatal: bind: Address already in use          (종료 코드 1)

port = 90oo 로 적은 설정 파일로 실행
fatal: config: 'port' must be an integer, got '90oo'          (종료 코드 1)
```

Config도 단위 시험 대상이다. test_protocol.cpp에 다음을 추가한다. 숫자가 아닌 값(`12abc`)이 오류로 처리되는지, 없는 키가 기본값을 주는지, 주석과 공백이 무시되는지 확인한다.

```cpp
// test_protocol.cpp — include에 추가
#include <sstream>
#include "Config.h"

// 시험 함수 추가
void TestConfig()
{
    Config c;
    std::istringstream in("# 서버 설정\nport = 9100  # 포트\nname=lobby\n\nbad line\nlimit = 12abc\n");
    c.Parse(in);
    CHECK(c.GetInt("port", 9000) == 9100);
    CHECK(c.GetString("name", "x") == "lobby");
    CHECK(c.GetInt("missing", 7) == 7);                  // 없는 키는 기본값
    bool thrown = false;
    try { c.GetInt("limit", 1); } catch (const std::runtime_error&) { thrown = true; }
    CHECK(thrown);                                       // 숫자가 아닌 값은 오류
}

// main에서 TestGameDay() 다음에 호출
    TestConfig();
```

### 6.14.4 배포 묶음

6.14.1~6.14.3절의 내용을 합친 CMakeLists.txt 전체는 다음과 같다. 1.10.1절의 파일에 빌드 종류, 샌니타이저 옵션, 버전, 시험, 설치를 더한 것이다. 위 파일들(Protocol.h, Config.h, GameTime.h, ChatServer.cpp, IntegrationTest.cpp, test_protocol.cpp, server.example.ini)이 같은 폴더에 있어야 한다. Windows용 설정(`ws2_32`, `mswsock`, `_WIN32_WINNT`)은 Asio 문서의 안내를 따른 것이며 이 파일을 Windows에서 빌드해 보지는 못했다.

```cmake
cmake_minimum_required(VERSION 3.20)
project(ChatServer VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# 빌드 종류를 지정하지 않으면 최적화 + 심볼(RelWithDebInfo)로 만든다.
# Visual Studio처럼 구성이 여러 개인 생성기는 --config Release 등으로 고른다.
if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
  set(CMAKE_BUILD_TYPE RelWithDebInfo)
endif()

option(ENABLE_SANITIZERS "ASan/UBSan을 켠다 (g++, clang)" OFF)
if(ENABLE_SANITIZERS AND NOT MSVC)
  add_compile_options(-fsanitize=address,undefined -fno-omit-frame-pointer)
  add_link_options(-fsanitize=address,undefined)
endif()

# 버전: 어느 소스로 만든 서버인지 알 수 있도록 git 커밋을 붙인다 (CMake를 설정하는 시점의 값이다).
execute_process(COMMAND git rev-parse --short HEAD
                WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
                OUTPUT_VARIABLE GIT_SHA OUTPUT_STRIP_TRAILING_WHITESPACE ERROR_QUIET)
if(NOT GIT_SHA)
  set(GIT_SHA "nogit")
endif()
set(SERVER_VERSION "${PROJECT_VERSION}-${GIT_SHA}")
message(STATUS "ChatServer version ${SERVER_VERSION}")

add_compile_definitions(ASIO_STANDALONE)
include_directories(${ASIO_DIR}/include)            # cmake -DASIO_DIR=<Asio 폴더>
find_package(Threads REQUIRED)
if(WIN32)
  add_compile_definitions(_WIN32_WINNT=0x0A00)      # Windows 10 이상
endif()

function(add_asio_program name source)              # 서버와 시험 프로그램이 같은 설정을 쓴다
  add_executable(${name} ${source})
  target_link_libraries(${name} Threads::Threads)
  if(WIN32)
    target_link_libraries(${name} ws2_32 mswsock)
  endif()
endfunction()

add_asio_program(ChatServer ChatServer.cpp)
target_compile_definitions(ChatServer PRIVATE SERVER_VERSION="${SERVER_VERSION}")
add_asio_program(IntegrationTest IntegrationTest.cpp)   # 서버를 먼저 실행한 뒤 직접 실행한다

enable_testing()                                    # 단위 시험: ctest로 실행한다
add_executable(test_protocol test_protocol.cpp)
add_test(NAME protocol COMMAND test_protocol)

# 배포 묶음에 들어갈 것: 실행 파일과 설정 견본 (심볼 파일은 따로 보관한다)
install(TARGETS ChatServer RUNTIME DESTINATION .)
install(FILES server.example.ini DESTINATION .)
```

빌드부터 배포 묶음과 심볼 파일을 만드는 전 과정은 다음 명령이다. 이 순서를 스크립트 파일 하나로 만들어 두면 사람이 실수할 여지가 줄어든다.

```text
cmake -S . -B build -DASIO_DIR=<Asio 폴더>
-- ChatServer version 0.1.0-be8ac97
cmake --build build
ctest --test-dir build --output-on-failure
100% tests passed, 0 tests failed out of 1

mkdir symbols
cmake --install build --prefix dist --strip
objcopy --only-keep-debug build/ChatServer symbols/ChatServer.debug
objcopy --add-gnu-debuglink=symbols/ChatServer.debug dist/ChatServer
cd dist
cmake -E tar cf ../ChatServer-0.1.0-be8ac97.zip --format=zip ChatServer server.example.ini
```

만들어진 `ChatServer-0.1.0-be8ac97.zip`에는 `ChatServer`(137,736바이트)와 `server.example.ini` 두 파일이 들어 있고, `symbols/ChatServer.debug`는 운영 서버로 보내지 않고 내부 저장소에 같은 버전 이름으로 보관한다. `cmake -E tar`는 CMake가 제공하는 압축 명령이어서 운영체제와 상관없이 같은 명령으로 쓸 수 있다.

Windows(Visual Studio)에서는 `cmake --build build --config Release`, `cmake --install build --config Release --prefix dist`로 만들고, `objcopy` 대신 빌드 폴더의 `ChatServer.pdb`를 `symbols` 폴더로 복사해 보관한다. 이 명령들은 CMake 문서를 따라 적었고 Windows에서 실행해 보지 못했다.

### 6.14.5 서비스로 실행하기

운영 서버는 사람이 로그인해서 직접 실행하지 않는다. 서버 PC가 재부팅되면 자동으로 시작되고, 비정상 종료하면 자동으로 다시 시작되며, 출력이 로그로 수집되어야 한다. Linux에서는 systemd가 이 일을 한다. 서비스 설정 파일(`/etc/systemd/system/chatserver.service`)은 다음과 같다.

```ini
[Unit]
Description=Chat Game Server
After=network.target
StartLimitIntervalSec=60
StartLimitBurst=5

[Service]
User=gameserver
WorkingDirectory=/opt/chatserver
ExecStart=/opt/chatserver/ChatServer /opt/chatserver/server.ini
Restart=on-failure
RestartSec=3
LimitNOFILE=65536
LimitCORE=infinity

[Install]
WantedBy=multi-user.target
```

- `User=gameserver`: 서버를 관리자 계정이 아닌 권한이 적은 전용 계정으로 실행한다. 서버가 뚫려도 피해가 줄어든다(6.15절).
- `Restart=on-failure`와 `RestartSec=3`: 비정상 종료하면 3초 뒤 다시 시작한다. `StartLimitBurst=5`와 `StartLimitIntervalSec=60`은 60초 안에 5번 넘게 다시 시작하면 멈추게 한다. 시작하자마자 죽는 서버를 무한히 다시 시작하며 로그와 CPU를 낭비하는 것을 막는다. 자동 재시작은 장애를 숨기므로 재시작이 일어나면 경보가 가도록 한다(6.15.5절).
- `LimitNOFILE=65536`: 한 프로세스가 열 수 있는 파일(소켓 포함)의 수이다. 기본값이 작으면 접속이 많을 때 `Too many open files`로 연결을 받지 못한다. 실습 5의 [흔한 오류]가 Windows에서 확인하라고 한 파일 핸들 한도와 같은 종류의 문제이고, 이 오류가 서버를 어떻게 망가뜨리는지는 6.15.1절에서 실험한다.
- `LimitCORE=infinity`: 비정상 종료할 때 core 파일을 남길 수 있게 한다(6.14.2절).

```text
sudo systemctl daemon-reload
sudo systemctl enable --now chatserver          (부팅 때 자동 시작하고 지금 시작)
sudo systemctl status chatserver
journalctl -u chatserver -f                     (서버 출력을 로그로 따라 본다)
```

이 설정 파일은 `systemd-analyze verify`로 문법을 검사했다. 실행 파일이 없다는 지적 외에는 오류가 없었고, 일부러 `Restart`의 철자를 틀리게 고치자 `Unknown key name 'Restrat' in section 'Service', ignoring.`을 출력했다. 이 컨테이너에서는 systemd로 서비스를 실제로 시작해 보지는 못했으므로, 위 명령은 서버 PC에서 처음 실행할 때 결과를 확인한다.

Windows에서는 콘솔 프로그램을 그대로 서비스로 등록할 수 없다. 서비스는 서비스 제어 관리자와 통신해야 하기 때문이다. 방법은 세 가지이다. 프로그램이 서비스 제어 인터페이스를 직접 구현하거나, NSSM 같은 서비스 래퍼 도구로 감싸거나, 작업 스케줄러에 "시스템 시작 때 실행"으로 등록한다. 이 교재의 실습은 콘솔 창에서 직접 실행하며, Windows 서비스화는 다루지 않는다.

### 6.14.6 배포 절차와 롤백

배포는 서버 파일을 교체하는 일이지만, 교체하는 동안 접속해 있는 사용자의 경험을 함께 관리해야 한다. 아래는 Lobby 하나와 Game Server 여러 대로 구성한 6장의 서버를 기준으로 한 절차이다.

1. 배포할 빌드를 확정한다: 버전 문자열을 정하고 이전 버전과 달라진 점(커밋 목록)을 적는다.
2. 자동으로 확인한다: 단위 시험(ctest)이 통과하고, 통합 시험이 통과하고, 샌니타이저 빌드로 통합 시험을 한 번 더 실행해 오류가 없는지 본다(6.13절).
3. 시험 서버에 먼저 배포한다: 운영과 같은 구성의 시험 서버에서 [최종 점검]을 다시 하고 Dummy로 부하를 건다.
4. 운영 반영 전에 준비한다: 점검 공지가 필요한지 판단하고, 이전 버전의 배포 묶음과 심볼 파일이 있는지, DB 백업이 있는지(5장) 확인한다.
5. 서버를 하나씩 교체한다(롤링 교체). Game Server 한 대를 교체하는 순서는 다음과 같다.
   - Lobby가 그 서버에 새 방을 배정하지 않게 한다(drain, 6.11.3절). 6장 구현에서는 해당 서버의 등록을 Lobby에서 빼는 방식으로 할 수 있다.
   - 진행 중인 방이 끝나기를 기다리거나 방 안의 사용자에게 공지한 뒤 서버를 종료한다.
   - 실행 파일을 새 버전으로 바꾸고 시작한다. 시작 로그의 버전 문자열이 맞는지 확인한다.
   - SRV_REGISTER로 Lobby에 등록되고 새 방이 배정되는지 확인한 뒤 다음 서버로 넘어간다.
   - Lobby는 교체 중에 로그인이 끊기므로 사용자가 적은 시간에 마지막으로 교체한다.
6. 확인한다: 시작 로그의 버전, 통합 시험(시험 계정으로), 지표(틱 처리 시간, 접속자, 오류 수)가 평소와 같은지 일정 시간 지켜본다.
7. 문제가 있으면 되돌린다(롤백): 이전 버전의 배포 묶음으로 같은 순서로 교체한다. 롤백은 평소에 연습해 두어야 필요할 때 쓸 수 있다.

서버가 여러 버전이 섞여 있는 시간이 생기므로 다음을 지킨다.

- 패킷 형식을 바꾸면 받는 쪽을 먼저 새 버전으로 바꾸고 보내는 쪽을 나중에 바꾼다. 이 교재의 서버는 알 수 없는 종류의 패킷을 보낸 연결을 끊으므로(6.13.2절), 새 패킷을 보내는 서버를 먼저 바꾸면 아직 옛 버전인 상대가 연결을 끊는다.
- DB 구조를 바꿀 때는 이전 버전도 새 구조에서 동작하도록 확장부터 한다. 열을 추가할 때는 기본값을 주고, 서버를 모두 새 버전으로 바꾼 다음 배포에서 이전 열을 지운다. 열을 먼저 지우면 롤백이 불가능하다.
- 설정 파일의 형식을 바꾸면 새 버전이 옛 설정 파일도 읽을 수 있거나, 배포 절차에 설정 변경이 들어 있어야 한다.

### 6.14.7 지속적 통합(CI)

1.10.2절은 main 브랜치를 "항상 빌드되고 시험을 통과하는 상태"로 유지하라고 했다. 사람의 약속만으로는 지켜지지 않으므로, 소스가 저장소에 올라올 때마다 깨끗한 환경에서 자동으로 빌드하고 시험을 실행하게 한다. 이것이 지속적 통합(CI, Continuous Integration)이다. 서버에서는 보통 다음을 실행한다.

- 깨끗한 환경에서 `cmake -S . -B build`와 `cmake --build build`: 내 PC에만 있는 파일이나 설정에 기대지 않는지 확인한다.
- `ctest --test-dir build --output-on-failure`: 단위 시험.
- 샌니타이저 빌드(`-DENABLE_SANITIZERS=ON`)로 단위 시험과 통합 시험.
- 배포 묶음과 심볼 파일을 만들어 버전 이름으로 보관.

GitHub Actions, Jenkins, GitLab CI 같은 도구가 이 일을 해 주며, 설정 파일의 문법은 도구마다 다르므로 각 도구의 문서를 따른다. 이 교재는 위 명령을 한 번에 실행하는 스크립트를 만드는 것까지를 범위로 하고, CI 서비스의 설정과 컨테이너 배포는 범위 밖이다. 배포 자동화(CD)는 CI가 안정된 뒤에 시험 서버로의 자동 배포부터 시작하는 것이 일반적이다.

## 6.15 서버 보안과 운영 점검

- 연결 수 제한: IP별, 전체 동시 연결 수를 제한한다. 인증 전 연결은 짧은 시간 안에 HELLO를 보내지 않으면 끊는다.
- 속도 제한: 세션마다 단위 시간당 허용 패킷 수를 정한다. 토큰 버킷 방식은 일정 속도로 토큰을 채우고 패킷마다 토큰을 소모하게 해, 순간적인 몰림은 허용하고 지속적인 과다 전송은 막는다.
- 입력 검증: 길이 제한(kMaxBody), 알 수 없는 종류, 인증 전에 허용되지 않는 패킷 종류를 모두 검사하고 위반하면 연결을 끊는다. 예외로 서버가 죽지 않게 모든 핸들러에서 입력 오류를 처리한다.
- 서버 간 신뢰: 내부 서버 사이의 통신도 인증한다. 내부망이 안전하다는 가정으로 인증을 생략하면 한 서버가 뚫렸을 때 전체가 뚫린다.
- 대규모 서비스 거부 공격(DDoS)은 서버 코드만으로 막을 수 없고 네트워크 단계의 방어(상용 DDoS 방어, 클라우드 방화벽)가 필요하다는 점을 알아 둔다.
- 로그와 관측: 중요한 사건(접속, 방 생성·종료, 오류, 느린 틱)을 로그로 남기고, 지표를 수집해 대시보드와 경보로 연결한다.
- 설정 분리: 포트, DB 주소, 정원 같은 값은 코드가 아니라 설정 파일로 두고, 비밀 정보는 저장소에 올리지 않는다.
- 서비스화: Windows는 Windows 서비스, Linux는 systemd 같은 도구로 서버를 서비스로 등록해 자동 재시작과 로그 수집을 구성한다. 컨테이너 배포와 CI/CD는 이 교재의 범위 밖이다.

위 여덟 항목을 하나씩 구현하거나 점검한다. 구현은 스레드 하나로 동작하는 실습 1~2의 채팅 서버에 6.14.3절의 설정 파일을 적용한 코드를 바탕으로 한다. 이 절에서 새로 만들거나 고치는 파일은 Log.h(새 파일), TokenBucket.h(새 파일), Protocol.h, ChatServer.cpp, test_protocol.cpp, IntegrationTest.cpp이다.

**[검증 환경]** 6.13절과 같은 환경(Ubuntu 24.04, g++ 13.3, clang++ 18.1, Asio 1.30.2)에서 `-Wall -Wextra -pedantic` 경고 없이 컴파일했다. 6.13절의 통합 시험 6개와 이 절에서 추가한 5개가 통과했고, 샌니타이저를 켠 빌드(6.13.4절)에서도 보고 없이 통과했다. Windows에서는 실행해 보지 못했다. 실습 3 이후의 구조(I/O 스레드와 논리 스레드의 분리, Session이 Server 대신 IHandler를 받는 구조)에는 적용해 보지 않았다. 그 구조에서는 지표와 주소별 연결 수를 어느 스레드가 만지는지부터 정해야 한다(연습 문제 15).

한 연결이 서버 안에서 거치는 검사는 다음 순서이고, 아래 소절이 이 순서를 따른다.

1. 접속을 받을 때: 전체 정원과 같은 주소의 연결 수를 확인한다(6.15.1절).
2. 로그인하기 전: 정해진 시간 안에 HELLO를 보내지 않으면 끊는다(6.15.1절).
3. 헤더를 읽을 때: 길이와 종류가 올바른지 확인한다(6.13.2절).
4. 본문을 읽은 뒤: 세션의 속도 제한을 넘으면 처리하지 않고 버린다(6.15.2절).
5. 처리하기 전: 로그인하기 전에 HELLO가 아닌 패킷이면 끊고, 본문의 내용을 검증한다(6.15.3절).
6. 처리하는 중: 예외가 나면 그 연결만 끊는다(6.15.3절).

### 6.15.1 연결 수 제한과 로그인 전 연결

연결 하나에는 파일 핸들 한 개, 세션 객체와 버퍼, 타이머 한 개가 든다. 공격이 아니어도 버그가 있는 클라이언트가 연결을 계속 열면 같은 일이 생긴다. 실습 2는 전체 연결 수(`kMaxConn`)와 마지막 수신 후 30초의 무응답 제한을 넣었다. 이것만으로는 다음이 남는다.

- 한 주소가 정원을 독점한다: PC 한 대가 연결을 `kMaxConn`개 열어 두면 다른 사용자는 `DENY full`을 받는다.
- 로그인하지 않은 연결이 30초 동안 자리를 차지한다: 연결만 열고 아무것도 보내지 않는 연결을 많이 만들면 정원이 찬다. 로그인한 사용자가 한동안 말이 없는 것과 로그인하지 않은 연결이 말이 없는 것은 다르게 다뤄야 한다.
- 정원보다 먼저 파일 한도에 닿을 수 있다: 이때 `accept`가 실패하는데, 실습 1의 `Accept()`는 오류가 나도 곧바로 다시 `accept`를 부른다(아래 실험).

먼저 설정에서 읽을 제한값과 지표를 담는 구조체를 만든다. 값은 6.14.3절의 설정 파일에서 읽고(6.15.6절), 지표는 6.15.5절에서 다룬다.

```cpp
struct Limits                                           // 설정 파일에서 읽는다 (6.14.3절의 Config)
{
    size_t maxPerIp = 20;                               // 같은 주소에서 동시에 연결할 수 있는 수
    int authSec = 5;                                    // 로그인 전: 이 시간 안에 패킷을 보내야 한다
    int idleSec = 30;                                   // 로그인 후: 이 시간 동안 아무것도 보내지 않으면 끊는다
    int rate = 20, burst = 40;                          // 세션마다 초당 처리할 패킷 수와 한꺼번에 허용할 수
    int maxDrops = 50;                                  // 속도 제한으로 버린 패킷이 이만큼 쌓이면 연결을 끊는다
    int statsSec = 10;                                  // 지표를 로그로 남기는 간격 (0이면 남기지 않는다)
};

struct Counters                                         // 지표: 서버가 시작한 뒤의 누적 값
{
    size_t accepted = 0, denied = 0, packets = 0, dropped = 0, timeouts = 0;
};
```

Session과 Server에 멤버를 더한다. 새로 생긴 멤버에는 주석을 달았다. Session의 생성자는 제한값(`Server::Lim()`)이 필요한데 Server가 Session 뒤에 정의되므로, 클래스 안에서는 선언만 두고 Server 정의 뒤에서 구현한다. include 목록에는 `Log.h`와 `TokenBucket.h`를 더한다.

```cpp
class Session : public std::enable_shared_from_this<Session>
{
public:
    Session(tcp::socket s, Server& sv);
    void Start();
    void Send(std::string pkt)
    {
        bool idle = out_.empty();
        out_.push_back(std::move(pkt));
        if (idle) Write();                              // 쓰는 중이 아닐 때만 시작한다
    }
    void SendAndClose(std::string pkt) { closeAfter_ = true; Send(std::move(pkt)); }
    void SetName(const std::string& n) { name_ = n; }
    const std::string& Name() const { return name_; }
    const asio::ip::address& Addr() const { return addr_; }
    void Close();
private:
    void ReadHeader();
    void ReadBody(uint16_t type, uint16_t size);
    void Write();
    void Arm();
    tcp::socket sock_;
    asio::steady_timer timer_;
    Server& server_;
    TokenBucket bucket_;                                // 속도 제한
    asio::ip::address addr_;                            // 상대 주소 (같은 주소의 연결 수를 센다)
    int drops_ = 0;                                     // 속도 제한으로 버린 패킷 수
    char hdr_[4];
    std::string body_;
    std::deque<std::string> out_;                       // 보낼 패킷 큐
    std::string name_;
    bool closeAfter_ = false, closed_ = false;
};

class Server
{
public:
    Server(asio::io_context& io, uint16_t port, const Limits& lim)
        : acc_(io, tcp::endpoint(tcp::v4(), port)), retry_(io), report_(io), lim_(lim)
    {
        Accept();
        if (lim_.statsSec > 0) Report();
    }
    const Limits& Lim() const { return lim_; }
    Counters& Count() { return counters_; }
    void OnPacket(const SessionPtr& s, uint16_t type, const std::string& b);
    void Leave(const SessionPtr& s);
private:
    void Accept();
    void Report();
    void Broadcast(const std::string& pkt) { for (auto& kv : users_) kv.second->Send(pkt); }
    std::string UserList()
    {
        std::string r;
        for (auto& kv : users_) r += (r.empty() ? "" : ",") + kv.first;
        return r;
    }
    tcp::acceptor acc_;
    asio::steady_timer retry_, report_;                 // accept 재시도용, 지표 보고용
    Limits lim_;                                        // 설정에서 읽은 제한값
    Counters counters_;                                 // 지표
    bool acceptFailing_ = false;                        // 직전 accept가 실패했는가 (같은 오류를 한 번만 기록하려고)
    std::set<SessionPtr> all_;                          // 접속한 모든 연결
    std::map<asio::ip::address, size_t> perIp_;         // 주소별 연결 수
    std::map<std::string, SessionPtr> users_;           // 로그인한 사용자
};

Session::Session(tcp::socket s, Server& sv)
    : sock_(std::move(s)), timer_(sock_.get_executor()), server_(sv),
      bucket_(sv.Lim().rate, sv.Lim().burst, TokenBucket::Clock::now())
{
    std::error_code ec;
    auto ep = sock_.remote_endpoint(ec);
    if (!ec) addr_ = ep.address();
}
```

`Server::Accept`는 세 가지를 한다. 정원과 주소별 제한을 확인하고, 거절하는 연결에는 사유(`full` 또는 `ip`)를 보낸 뒤 닫고, `accept` 자체가 실패하면 100ms 뒤에 다시 시도한다. `Leave`는 정원 안에 있던 연결이 끊길 때만 주소별 연결 수를 줄인다. 정원 초과로 거절된 연결도 `Close()`에서 `Leave`를 부르므로, `all_.erase(s)`가 실제로 지웠는지로 구분해야 수가 틀어지지 않는다.

```cpp
void Server::Accept()
{
    acc_.async_accept([this](std::error_code ec, tcp::socket sock) {
        if (ec == asio::error::operation_aborted) return;
        if (ec)                                         // 파일 한도(EMFILE) 같은 오류: 바로 다시 호출하면 CPU를 계속 쓴다
        {
            if (!acceptFailing_) Log("accept failed: " + ec.message());     // 같은 오류가 계속되어도 한 번만 남긴다
            acceptFailing_ = true;
            retry_.expires_after(std::chrono::milliseconds(100));
            retry_.async_wait([this](std::error_code) { Accept(); });
            return;
        }
        acceptFailing_ = false;
        auto s = std::make_shared<Session>(std::move(sock), *this);
        auto it = perIp_.find(s->Addr());
        if (all_.size() >= kMaxConn)
        {
            ++counters_.denied;
            s->SendAndClose(Packet::Make(DENY, "full"));
        }
        else if (it != perIp_.end() && it->second >= lim_.maxPerIp)
        {
            ++counters_.denied;
            Log("deny ip " + s->Addr().to_string());
            s->SendAndClose(Packet::Make(DENY, "ip"));
        }
        else
        {
            ++perIp_[s->Addr()];
            all_.insert(s);
            ++counters_.accepted;
            s->Start();
        }
        Accept();
    });
}

void Server::Leave(const SessionPtr& s)
{
    if (all_.erase(s))                                  // 정원에 들어 있던 연결만 센다
    {
        auto it = perIp_.find(s->Addr());
        if (it != perIp_.end() && --it->second == 0) perIp_.erase(it);
    }
    if (s->Name().empty()) return;
    users_.erase(s->Name());
    Broadcast(Packet::Make(NOTICE, s->Name() + " left"));
    Broadcast(Packet::Make(USERS, UserList()));
}
```

타이머는 로그인 여부에 따라 기다리는 시간을 달리한다. `Arm()`은 패킷을 처리한 뒤에 부른다. 실습 2처럼 `OnPacket` 앞에서 부르면 HELLO를 처리하는 시점에는 아직 로그인 전이어서, 로그인한 뒤에도 짧은 시간(`auth_timeout`)이 그대로 남아 방금 로그인한 연결이 끊긴다. 처리가 끝난 뒤에 부르면 로그인 결과가 반영된다. 이미 닫힌 세션에는 타이머를 걸지 않는다. `Arm()`을 부르는 위치는 6.15.3절의 `ReadBody` 완성본에서 본다.

```cpp
void Session::Arm()                                     // 마지막으로 패킷을 처리한 뒤 정해진 시간이 지나면 끊는다
{
    if (closed_) return;
    auto self = shared_from_this();
    int sec = name_.empty() ? server_.Lim().authSec : server_.Lim().idleSec;   // 로그인 전에는 더 짧게 기다린다
    timer_.expires_after(std::chrono::seconds(sec));    // 대기 중이면 취소되고 다시 설정된다
    timer_.async_wait([this, self](std::error_code ec) {
        if (ec) return;                                 // 다시 설정되었거나 취소되었다
        ++server_.Count().timeouts;
        Log("timeout " + addr_.to_string() + (name_.empty() ? " (before login)" : " " + name_));
        Close();
    });
}
```

**실험 — 파일 한도.** 정원이 파일 한도보다 크면 정원에 닿기 전에 파일 한도에 닿는다. Protocol.h의 `kMaxConn`을 1000으로 바꿔 빌드하고, `ulimit -n 20`으로 파일 한도를 20으로 낮춘 셸에서 서버를 실행한 뒤 접속 30개를 열었다(접속을 여는 데에는 짧은 스크립트를 썼다. 실습 5의 Dummy로도 같은 상황을 만들 수 있다). 접속을 연 뒤 2초 동안 서버의 CPU 사용 시간을 `/proc/<PID>/stat`의 utime과 stime으로 읽었다.

- 서버는 접속 13개만 받았다. 서버 자신이 파일 핸들 7개(표준 입출력 3개, 리스닝 소켓 1개, Asio가 내부에서 쓰는 epoll·eventfd·timerfd 3개)를 쓰고 있었기 때문이다(`ls /proc/<PID>/fd`로 확인했다).
- 오류가 나도 곧바로 `Accept()`를 다시 부르던 버전은 2초 중 1.98초 동안 CPU를 썼다. 기다리는 연결이 그대로 남아 있어서 `accept`가 즉시 같은 오류로 돌아오고, 이것이 쉬지 않는 반복이 된다.
- 위 `Accept()`처럼 100ms 뒤에 다시 시도하게 고친 버전은 같은 상황에서 0.00초를 썼다. 로그에는 `accept failed: Too many open files`가 한 번 남았다.
- 두 버전 모두 연결 5개를 끊자 기다리던 연결 5개가 이어서 처리되었다.

서비스에서는 `kMaxConn`을 설정 파일 값으로 옮기고 파일 한도(6.14.5절의 `LimitNOFILE`)보다 충분히 작게 둔다. 서버 자신이 쓰는 핸들(위 실험에서는 7개), 로그 파일, DB 연결, 서버 간 연결도 핸들을 쓰기 때문이다. 이 실험은 Linux에서 했고, Windows에서는 핸들 한도에 닿는 방식이 달라 같은 결과를 확인하지 못했다.

**시험.** 이 절의 시험은 제한값을 작게 줄인 설정 파일로 서버를 따로 실행하고, IntegrationTest에 `security`를 인자로 주어 실행한다. 정원(`kMaxConn = 3`)은 그대로이고 같은 주소 제한은 2, 로그인 전 시간 제한은 1초이다. 서비스에서는 이렇게 작은 값을 쓰지 않는다.

```text
# 6.15절 시험용 설정 (서비스에서는 이렇게 작은 값을 쓰지 않는다)
port = 9100
max_per_ip = 2
auth_timeout = 1
idle_timeout = 30
rate_per_sec = 5
rate_burst = 10
max_drops = 20
stats_sec = 1
```

IntegrationTest.cpp의 `main`을 고쳐 시험 묶음을 고르게 한다. 시험 함수는 `main` 앞에 둔다. include 목록에 `<iterator>`를 더한다.

```cpp
    bool security = argc > 2 && std::string(argv[2]) == "security";     // 6.15절의 시험 묶음
    const Case cases[] = {
        {"헤더 쪼개기", TestSplitHeader}, {"인증 거부", TestDeny}, {"대화와 PING", TestChatAndPing},
        {"갑작스러운 종료", TestAbruptClose}, {"거짓 길이", TestFakeLength}, {"정원", TestCapacity},
    };
    const Case securityCases[] = {
        {"같은 주소 제한", TestPerIp}, {"로그인 전 시간 제한", TestAuthTimeout},
        {"로그인 전 패킷", TestPreLogin}, {"이름 검증", TestBadName}, {"속도 제한", TestRateLimit},
    };
    const Case* list = security ? securityCases : cases;
    size_t count = security ? std::size(securityCases) : std::size(cases);
    try
    {
        for (size_t i = 0; i < count; ++i)
        {
            const Case& c = list[i];
```

같은 주소 제한 시험은 연결 둘이 로그인하고 셋째가 `DENY ip`를 받는지, 둘이 닫히면 자리가 돌아와 다시 로그인할 수 있는지 확인한다. 로그인 전 시간 제한 시험은 아무것도 보내지 않은 연결이 1초 뒤 끊기고, 같은 시간 동안 로그인해 있던 연결은 끊기지 않는지 확인한다.

```cpp
void TestPerIp()                // 같은 주소에서는 max_per_ip(2)개까지만 연결되고, 닫히면 자리가 돌아온다
{
    TestClient c1(g_port);
    CHECK(Login(c1, "ip1"));
    {
        TestClient c2(g_port);
        CHECK(Login(c2, "ip2"));
        TestClient c3(g_port);
        Reply r = c3.Recv();
        CHECK(r.ok && r.type == DENY && r.body == "ip");
    }
    std::this_thread::sleep_for(300ms);                 // 서버가 닫힌 연결을 처리할 시간을 준다
    TestClient c4(g_port);
    CHECK(Login(c4, "ip4"));                            // 자리가 비면 다시 접속할 수 있다
}

void TestAuthTimeout()          // 로그인하지 않은 연결은 auth_timeout(1초) 뒤에 끊기고, 로그인한 연결은 끊기지 않는다
{
    TestClient silent(g_port), user(g_port);
    CHECK(Login(user, "timer"));
    CHECK(silent.WaitClosed(3000ms));
    std::this_thread::sleep_for(1500ms);                // 로그인한 지 auth_timeout을 넘겼다
    user.Send(PING, "alive");
    CHECK(user.WaitFor([](const Reply& r) { return r.type == PONG && r.body == "alive"; }).ok);
}
```

주소를 기준으로 하는 제한에는 한계가 있다. 한 건물이나 이동통신망의 많은 사용자가 같은 공인 주소를 함께 쓰는 경우(NAT)가 흔해서, 값을 너무 작게 두면 정상 사용자를 막는다. 서버 앞에 프록시나 로드 밸런서가 있으면 모든 연결이 그 장비의 주소로 보인다. 그래서 `max_per_ip`의 기본값은 넉넉하게(20) 두고, 지표(6.15.5절의 `denied`)를 보며 조정한다.

### 6.15.2 속도 제한

패킷 하나를 처리하는 비용이 작아도, 도착하는 속도가 높으면 합계가 커진다. 특히 이 채팅 서버는 받은 SAY 하나를 접속자 모두에게 다시 보내므로(`Broadcast`), 한 연결이 보내는 패킷 하나가 접속자 수만큼의 송신으로 늘어난다. 한 연결이 서버의 처리 시간을 독점하면 다른 모든 사용자의 처리가 늦어진다(6.12.1절). 그래서 연결마다 패킷을 받는 속도에 한도를 둔다.

토큰 버킷(token bucket)은 이를 단순하게 구현한다. 통에는 토큰이 최대 `burst`개까지 들어가고, 시간이 지나면 초당 `rate`개씩 채워진다. 패킷 하나를 처리할 때마다 토큰 하나를 꺼내고, 토큰이 없으면 그 패킷은 처리하지 않는다. 오래 쉬었다 보내면 `burst`개까지는 한꺼번에 처리되고(순간적인 몰림 허용), 계속 보내면 평균 `rate`개로 제한된다(지속적인 과다 전송 차단).

```cpp
// TokenBucket.h — 세션마다 하나씩 둔다. 현재 시각을 인자로 받으므로 시험에서 시각을 직접 정할 수 있다.
#pragma once
#include <algorithm>
#include <chrono>

class TokenBucket
{
public:
    using Clock = std::chrono::steady_clock;
    TokenBucket(double perSecond, double burst, Clock::time_point now)
        : rate_(perSecond), cap_(burst), tokens_(burst), last_(now) {}

    bool Allow(Clock::time_point now)                   // 패킷 하나를 처리해도 되면 true
    {
        double dt = std::max(0.0, std::chrono::duration<double>(now - last_).count());
        last_ = now;
        tokens_ = std::min(cap_, tokens_ + dt * rate_);  // 지난 시간만큼 토큰을 채우되 통의 크기를 넘지 않는다
        if (tokens_ < 1.0) return false;
        tokens_ -= 1.0;
        return true;
    }
private:
    double rate_, cap_, tokens_;
    Clock::time_point last_;
};
```

현재 시각을 인자로 받는 것은 시험을 위해서이다. 안에서 시계를 읽으면 시험이 실제 시간이 흐르기를 기다려야 하고 결과가 흔들린다(6.13.2절). `std::max(0.0, …)`는 시각이 거꾸로 전달되어도 토큰이 줄지 않게 한다. 단위 시험은 test_protocol.cpp에 추가한다. include 목록에 `"TokenBucket.h"`를 더하고 `main`에서 `TestTokenBucket()`을 부른다.

```cpp
void TestTokenBucket()
{
    auto t0 = TokenBucket::Clock::now();
    TokenBucket b(10.0, 5.0, t0);                        // 초당 10개 채움, 통 크기 5
    for (int i = 0; i < 5; ++i) CHECK(b.Allow(t0));      // 처음에는 통이 가득 차 있다
    CHECK(!b.Allow(t0));                                 // 6번째는 거절
    CHECK(b.Allow(t0 + milliseconds(100)));              // 100ms 뒤 토큰 1개가 채워진다
    CHECK(!b.Allow(t0 + milliseconds(100)));
    auto later = t0 + seconds(10);
    for (int i = 0; i < 5; ++i) CHECK(b.Allow(later));   // 오래 쉬어도 5개까지만 쌓인다
    CHECK(!b.Allow(later));
}
```

세션마다 버킷을 하나 두고(`bucket_`), 본문을 읽은 뒤 `OnPacket`을 부르기 전에 확인한다. 넘으면 그 패킷을 처리하지 않고 버리며, 버린 횟수가 `max_drops`에 이르면 연결을 끊는다.

```cpp
        if (!bucket_.Allow(TokenBucket::Clock::now()))  // 속도 제한: 이 패킷은 처리하지 않고 버린다
        {
            ++server_.Count().dropped;
            if (++drops_ >= server_.Lim().maxDrops)
            {
                Log("rate limit: close " + addr_.to_string() + (name_.empty() ? "" : " " + name_));
                return Close();
            }
            return ReadHeader();
        }
```

정책을 정할 때 고려한 점은 다음과 같다.

- 넘친 패킷은 조용히 버린다. 거절 응답을 보내면 그 응답이 또 송신 부하가 된다.
- 한 번 넘쳤다고 끊지 않는다. 정상 클라이언트도 잠깐 몰아 보낼 수 있다. 계속 넘칠 때(버린 패킷이 `max_drops`에 이를 때)만 끊는다.
- 한도는 정상 클라이언트가 보내는 최대 속도를 재어 그보다 넉넉하게 잡는다. 하트비트는 10초에 한 번, 채팅은 사람이 치는 속도, 이동 입력은 틱 속도에 비례하는 식이다. 기본값은 초당 20개와 순간 40개이다. 본문은 최대 `kMaxBody`(1,024바이트)이므로 이 설정에서 한 연결이 보낼 수 있는 입력은 초당 20KB 안팎이다.
- 개수만 제한하므로, 큰 패킷을 반복해서 보내는 것은 바이트 단위로 따로 제한해야 한다.
- 로그인 시도(HELLO)는 다른 패킷보다 비싸다(5장에서 DB 조회와 비밀번호 해시 계산이 든다). 실패한 로그인이 짧은 시간에 반복되는 것은 따로 제한한다(연습 문제 17).

통합 시험은 `rate_per_sec = 5`, `rate_burst = 10`, `max_drops = 20`인 서버에서 한 클라이언트가 SAY 60개를 한꺼번에 보내게 한다. 폭주한 연결은 끊겨야 하고, 같은 방에서 지켜보던 연결은 순간 허용량만큼만 받으면서 영향 없이 동작해야 한다. 지켜보던 연결이 받은 대화는 시험이 8~12개를 허용하며, 따로 센 값은 60개 중 10개였다. 서버 로그에는 `rate limit: close 127.0.0.1 flood`가 남는다.

```cpp
int CountChat(TestClient& c, const std::string& body)   // 대화 중 body와 같은 것이 몇 개 오는지 센다
{
    int n = 0;
    for (;;)
    {
        Reply r = c.Recv(300ms);
        if (!r.ok) break;
        if (r.type == CHAT && r.body == body) ++n;
    }
    return n;
}

void TestRateLimit()            // 폭주하는 연결은 끊기고, 다른 연결은 영향을 받지 않는다
{
    TestClient flood(g_port), watcher(g_port);
    CHECK(Login(flood, "flood") && Login(watcher, "watch"));
    for (int i = 0; i < 60; ++i) flood.Send(SAY, "x");  // rate_burst(10)를 넘는 패킷을 한꺼번에 보낸다
    CHECK(flood.WaitClosed());                          // 버린 패킷이 max_drops(20)에 이르면 끊는다
    int got = CountChat(watcher, "flood|x");
    CHECK(got >= 8 && got <= 12);                       // 순간 허용량만큼만 전달된다
    watcher.Send(PING, "ok");
    CHECK(watcher.WaitFor([](const Reply& r) { return r.type == PONG && r.body == "ok"; }).ok);
}
```

### 6.15.3 입력 검증

6.7.1절의 원칙대로 클라이언트가 보내는 모든 것을 신뢰하지 않는다. 검증은 값싼 것부터 크기, 종류, 상태, 내용의 순서로 한다.

- 크기와 종류: 헤더의 길이와 종류는 6.13.2절의 `IsValidHeader`가 본문을 읽기 전에 검사한다. 길이를 검사하기 전에 `body_.resize(size)`를 부르면 거짓 길이 하나로 큰 메모리를 할당하게 된다(실습 2의 [흔한 오류]).
- 상태: 로그인하기 전에는 HELLO만 받는다. 다른 패킷은 로그인 전의 연결이 보낼 수 없는 것이므로 바로 끊는다. PING도 허용하면 로그인하지 않은 연결이 PING을 계속 보내 시간 제한을 영원히 피할 수 있다(처리한 패킷마다 타이머를 다시 설정하기 때문이다). PING을 허용한 서버에서 `auth_timeout`을 1초로 두고 로그인하지 않은 연결이 PING을 0.5초마다 보내게 했더니 3.5초 뒤에도 연결이 살아 있었다. HELLO만 허용하는 서버는 바로 끊었다.
- 내용: 모양이 올바른 패킷이어도 본문의 값이 올바른 것은 아니다.

```cpp
    if (s->Name().empty() && type != HELLO)             // 로그인 전에는 HELLO만 허용한다 (다른 패킷으로 시간 제한을 늘릴 수 없다)
        return s->Close();
```

HELLO의 본문은 `이름|인증 코드`이다. 이름이 비어 있는지만 보던 실습 1~2의 코드는 이름에 쉼표가 들어와도 받아 준다. 클라이언트가 접속자 목록(USERS, 쉼표로 구분)을 쉼표로 나누어 읽으면 이름 하나가 둘로 보이게 된다. 줄바꿈 같은 제어 문자가 든 이름은 로그에 가짜 줄을 만들 수 있고, 긴 이름은 모든 USERS 패킷의 크기를 키운다. 이름에 허용하는 것을 정해 순수 함수로 만들고 단위 시험을 한다. 이 함수는 Protocol.h의 끝에 더하고, 단위 시험은 test_protocol.cpp에 더해 `main`에서 부른다.

```cpp
// ---- 6.15절에서 추가: 본문(이름) 검증 ----
const size_t kMaxName = 32;                             // 이름의 최대 바이트 수 (UTF-8 한글은 한 글자가 3바이트)
inline bool IsValidName(const std::string& n)
{
    if (n.empty() || n.size() > kMaxName) return false;
    for (unsigned char c : n)
        if (c < 0x20 || c == 0x7F || c == '|' || c == ',') return false;       // 제어 문자와 프로토콜 구분자
    return true;
}
```

```cpp
void TestName()
{
    CHECK(IsValidName("alice"));
    CHECK(IsValidName("홍길동"));                          // UTF-8 한글
    CHECK(!IsValidName(""));
    CHECK(!IsValidName("a,b"));                          // USERS 목록의 구분자
    CHECK(!IsValidName("a|b"));                          // 패킷 본문의 구분자
    CHECK(!IsValidName("line\nbreak"));                  // 제어 문자 (로그를 위조할 수 있다)
    CHECK(IsValidName(std::string(kMaxName, 'x')));      // 경계: 최대 길이
    CHECK(!IsValidName(std::string(kMaxName + 1, 'x')));
}
```

서버에서는 이름이 비어 있는지만 보던 조건을 이 함수로 바꾼다.

```cpp
        if (!IsValidName(name) || users_.count(name)) return s->SendAndClose(Packet::Make(DENY, "name"));
```

이 검사는 바이트 단위이다. UTF-8로 올바른 문자열인지, 보기에 같은 이름(예: 전각 문자)이 있는지는 검사하지 않는다. 이름을 DB로 보낼 때는 이 검사와 별개로 항상 매개변수로 전달한다(5.13절의 준비된 문장).

예외도 입력 오류의 한 종류이다. 입력을 처리하다 예외가 나면(파싱 중 `std::stoi`가 던지는 예외, 메모리 부족 등) 아무도 잡지 않을 때 `io.run()`까지 전파되어 서버 전체가 종료된다. `ReadBody`에서 `OnPacket` 호출을 감싸 그 연결만 끊는다. 속도 제한과 `Arm()` 호출 위치까지 반영한 `ReadBody`의 완성본은 다음과 같다.

```cpp
void Session::ReadBody(uint16_t type, uint16_t size)
{
    auto self = shared_from_this();
    body_.resize(size);
    asio::async_read(sock_, asio::buffer(body_), [this, self, type](std::error_code ec, size_t) {
        if (ec) return Close();
        if (!bucket_.Allow(TokenBucket::Clock::now()))  // 속도 제한: 이 패킷은 처리하지 않고 버린다
        {
            ++server_.Count().dropped;
            if (++drops_ >= server_.Lim().maxDrops)
            {
                Log("rate limit: close " + addr_.to_string() + (name_.empty() ? "" : " " + name_));
                return Close();
            }
            return ReadHeader();
        }
        ++server_.Count().packets;
        try { server_.OnPacket(self, type, body_); }
        catch (const std::exception& e)                 // 한 연결의 입력 오류로 서버 전체가 종료되지 않게 한다
        {
            Log(std::string("packet error: ") + e.what());
            return Close();
        }
        if (closed_) return;
        Arm();                                          // 로그인 여부가 바뀔 수 있으므로 처리한 뒤에 설정한다
        ReadHeader();
    });
}
```

이 처리는 일부러 예외를 던지게 고친 서버(SAY의 본문이 `boom`이면 `std::runtime_error`를 던진다)로 확인했다. 위 코드는 `packet error: boom`을 로그에 남기고 그 연결만 끊었으며 다른 사용자는 PING에 계속 응답했다. try/catch를 뺀 서버는 `fatal: boom`을 출력하고 종료 코드 1로 끝나, 접속해 있던 모든 사용자의 연결이 끊겼다. 예외를 잡아 연결을 끊는 것은 서버가 죽지 않게 하는 방어이지 버그를 고치는 것이 아니다. 예외가 난 시점에 자료구조가 일부만 갱신된 상태일 수 있으므로(예외 안전성) 원인을 로그로 찾아 고쳐야 하고, 이 로그는 경보 대상으로 삼는다(6.15.5절).

나머지 두 시험은 다음과 같다. 로그인 전 패킷 시험이 끊기는 시간을 500ms로 제한하는 데는 이유가 있다. 로그인 전 검사를 지워도 연결은 `auth_timeout`(1초) 뒤에 시간 제한으로 끊기므로, 끊기기를 1.5초 기다리는 시험은 검사가 사라져도 통과한다. 처음 쓴 시험이 그랬고, 이 검사를 지운 서버를 통과시켰다. 시험은 시간 제한보다 먼저 끊기는지를 확인해야 한다.

```cpp
void TestPreLogin()             // 로그인 전에 HELLO가 아닌 패킷을 보내면 바로 끊긴다 (auth_timeout 1초를 기다리지 않는다)
{
    TestClient a(g_port);
    a.Send(SAY, "hi");
    CHECK(a.WaitClosed(500ms));
    TestClient b(g_port);
    b.Send(PING, "x");                                  // PING으로 로그인 전 시간 제한을 늘릴 수 없다
    CHECK(b.WaitClosed(500ms));
}

void TestBadName()              // 목록의 구분자(,)가 들어간 이름은 거부된다
{
    TestClient c(g_port);
    c.Send(HELLO, "a,b|1234");
    Reply r = c.Recv();
    CHECK(r.ok && r.type == DENY && r.body == "name");
}
```

서버를 `./ChatServer security.ini`로 실행하고 다른 터미널에서 `./IntegrationTest 9100 security`를 실행한 결과는 다음과 같다.

```text
PASS  같은 주소 제한
PASS  로그인 전 시간 제한
PASS  로그인 전 패킷
PASS  이름 검증
PASS  속도 제한
ALL PASSED
```

시험이 오류를 실제로 잡는지 확인하려고, 서버를 일부러 틀리게 고쳐서 같은 시험을 돌렸다(6.13.2절의 방법).

- 연결이 끊길 때 주소별 연결 수를 줄이지 않는 서버: "같은 주소 제한"이 실패한다.
- `Arm()`을 `OnPacket` 앞에서 부르는 서버: "로그인 전 시간 제한"이 실패한다(로그인한 연결이 1초 뒤 끊겨 PONG이 오지 않는다).
- 로그인 전 검사를 지운 서버, 로그인 전에 PING을 허용하는 서버: 둘 다 "로그인 전 패킷"이 실패한다.
- 속도 제한을 끈 서버: "속도 제한"이 실패한다.

**알 수 없는 종류의 패킷.** 1.6.3절은 버전이 다른 프로그램이 섞여도 동작하도록 알 수 없는 종류의 패킷을 연결을 끊지 않고 무시하라고 했는데, 이 서버(6.13.2절의 `IsValidHeader`)는 끊는다. 두 선택은 상대가 누구인지에 따라 갈린다. 접속할 때 프로토콜 버전을 확인해 버전이 맞는 클라이언트만 받는 서버라면, 그 클라이언트가 보낼 수 있는 종류는 정해져 있으므로 그 밖의 종류는 버그이거나 공격이다. 이때는 끊는 편이 안전하다. 반대로 새 버전과 옛 버전이 오래 섞여 있어야 하는 관계에서는 모르는 종류를 무시해야 어느 쪽을 먼저 배포해도 연결이 끊기지 않는다. 이 교재의 서버는 접속할 때 버전을 확인하지 않고 클라이언트와 서버를 함께 만들어 배포한다고 가정하므로 끊는 쪽을 택했다. 접속 때 버전을 확인하게 고치는 것은 연습 문제 20이다. 어느 쪽을 택하든 정책을 정해 문서에 적어 둔다. 배포 순서가 이 정책에 따라 달라지기 때문이다(6.14.6절).

### 6.15.4 서버 간 신뢰와 서비스 거부 공격

**서버 간 신뢰.** 실습 4의 Lobby는 클라이언트와 Game Server를 같은 포트로 받고, 패킷 종류만으로 누구의 패킷인지 구분한다.

```cpp
void Lobby::OnPacket(const SessionPtr& s, uint16_t type, const std::string& b)
{
    switch (type)
    {
    case HELLO:          Login(s, b); break;            // 실습 1과 같다
    case ROOM_CREATE:    RequestRoom(s, b); break;
    case ROOM_LIST:      SendRoomList(s); break;
    case SRV_REGISTER:   RegisterGame(s, b); break;     // id|host|port
    case SRV_REPORT:     UpdateLoad(s, b); break;       // id|방 수|접속자 수
    case SRV_ROOM_READY: RoomReady(b); break;           // 방|id
    }
}
```

이 코드에서는 어느 클라이언트든 연결해서 `SRV_REGISTER`를 보내면 Game Server로 등록될 수 있다. 그러면 Lobby는 방 생성 요청을 그 가짜 서버로 보내고, 사용자에게 그 서버의 주소와 입장권을 알려 준다. `SRV_REPORT`로 부하 값을 거짓으로 보내 방 배정을 한쪽으로 몰 수도 있다. 반대쪽인 `GameServer::OnPacket`은 자신이 Lobby로 연결해서 만든 연결(`s == link_`)의 패킷만 서버 간 패킷으로 다루므로 클라이언트가 Lobby인 척할 수 없다. 연결을 누가 시작했는지가 인증의 일부가 된 것이다. 이 교재의 Lobby 코드는 이 구멍을 막지 않았고, 시험으로 재현하지도 않았다. 설계로 막는 방법은 다음과 같다(구현은 연습 문제 16).

- 서버 간 통신 전용 포트를 따로 열고, 방화벽이나 보안 그룹으로 Game Server 주소에서만 접근하게 한다. `SRV_*` 패킷은 그 포트로 들어온 연결에서만 처리하고, 클라이언트용 포트로 온 것은 연결을 끊는다.
- 등록할 때 공유 비밀 키를 함께 보내고(`SRV_REGISTER`의 본문에 더한다) Lobby가 설정 파일의 값과 비교한다. 맞지 않거나, 등록하지 않은 연결이 `SRV_REPORT` 등을 보내면 끊는다. 키는 설정 파일에 두고 저장소에 올리지 않는다(6.15.6절).
- 위 방법은 키를 평문으로 보내므로 같은 망에서 패킷을 엿보는 상대에게는 약하다. 서버 간 연결을 TLS로 암호화하고 서로의 인증서를 확인하는 방법이 일반적이다. Asio는 OpenSSL을 쓰는 `asio::ssl`을 제공한다. 이 교재의 범위 밖이다.
- 입장권은 6.11.4절의 원칙대로 서명하고, 만료와 일회성을 둔다.
- 최소 권한: Game Server가 DB에 직접 접근할 필요가 없으면 DB 계정을 주지 않는다. 한 서버가 뚫렸을 때 피해가 그 서버가 가진 권한으로 제한된다.

**서비스 거부 공격(DoS, DDoS).** 서비스 거부 공격은 서버가 정상 사용자를 처리하지 못하게 만드는 공격이다. 크게 세 가지로 나눈다.

- 대역폭을 채우는 공격: 서버에 닿기 전에 회선이 가득 찬다. 서버 코드로는 막을 수 없다.
- 연결 자원을 채우는 공격: 연결 수, 연결 대기열, 파일 핸들을 소모시킨다. 운영체제 설정과 이 절의 6.15.1절의 제한이 도움이 된다.
- 서버 로직의 비싼 요청을 반복하는 공격: 로그인, DB 조회처럼 비싼 요청을 반복한다. 6.15.2절의 속도 제한과 요청별 제한이 도움이 된다.

대역폭을 채우는 공격은 네트워크 단계의 방어(상용 DDoS 방어 서비스, 클라우드 사업자의 방어 기능과 방화벽)가 필요하다. 구조로도 대비할 수 있다. 6.11.2절의 Gateway처럼 외부에 공개하는 서버를 제한하고 내부 서버의 주소를 알리지 않으면 공격 대상이 줄어든다. 실습 4처럼 클라이언트가 Game Server에 직접 접속하는 구조에서는 Game Server의 주소가 사용자에게 알려지므로, 이 점에서는 불리하다. 공격이 의심되면 지표(6.15.5절)로 확인하고, 제한을 강화하고, 네트워크 사업자나 방어 서비스에 연락한다. 이때 로그는 증거가 되므로 보관한다.

### 6.15.5 로그와 관측

**무엇을 남기는가.** 운영 중인 서버에서는 문제가 생긴 뒤에야 로그를 본다. 그래서 사건이 일어난 시각, 대상(주소, 이름), 종류를 한 줄에 하나씩 남긴다. 접속, 거부(사유), 시간 초과, 속도 제한으로 끊김, 오류, 시작과 종료가 대상이다. 남기지 않는 것도 정한다. 비밀번호와 입장권은 로그에 넣지 않고, 패킷 전체를 매번 남기지 않는다. 접속 주소는 공격에 대응하려면 필요하므로 남기지만, 다른 정보와 결합하면 개인을 식별할 수 있으므로 개인정보처럼 접근을 제한하고 보관 기간을 정한다(1.11.2절). 측정 중에 로그를 과하게 출력하면 서버가 느려진다(실습 5의 [흔한 오류]). 같은 오류가 반복될 때는 한 번만 남긴다(위 `acceptFailing_`).

시각이 있는 한 줄을 남기는 함수는 다음과 같다. 시각은 UTC로 남긴다. 서버가 여러 대이고 시간대가 다를 때 로그를 시간순으로 맞추기 쉽다. `std::format`과 `<chrono>`의 시각 출력은 C++20 표준이다.

```cpp
// Log.h — 시각(UTC)을 붙여 한 줄씩 남긴다
#pragma once
#include <chrono>
#include <format>
#include <iostream>
#include <string>

inline void Log(const std::string& msg)
{
    auto now = std::chrono::floor<std::chrono::seconds>(std::chrono::system_clock::now());
    std::cout << std::format("{:%F %T} ", now) << msg << std::endl;
}
```

**지표.** 로그는 사건을 남기고, 지표는 상태를 숫자로 남긴다. `Counters`의 값을 일정한 간격(`stats_sec`)으로 한 줄로 남기는 함수는 다음과 같다. 서버를 시작할 때 `stats_sec`가 0보다 크면 `Report()`를 한 번 불러 시작한다.

```cpp
void Server::Report()                                   // 지표를 한 줄로 남기고 다음 보고를 예약한다
{
    report_.expires_after(std::chrono::seconds(lim_.statsSec));
    report_.async_wait([this](std::error_code ec) {
        if (ec) return;
        Log(std::format("stats conn={} users={} accepted={} denied={} packets={} dropped={} timeouts={}",
                        all_.size(), users_.size(), counters_.accepted, counters_.denied,
                        counters_.packets, counters_.dropped, counters_.timeouts));
        Report();
    });
}
```

6.15.1~6.15.3절의 시험을 돌린 서버가 남긴 로그의 일부는 다음과 같다. 이 시험에서는 `stats_sec = 1`로 두었다.

```text
2026-11-05 11:28:56 deny ip 127.0.0.1
2026-11-05 11:28:57 stats conn=2 users=1 accepted=5 denied=1 packets=4 dropped=0 timeouts=0
2026-11-05 11:28:58 timeout 127.0.0.1 (before login)
2026-11-05 11:28:58 stats conn=1 users=1 accepted=5 denied=1 packets=4 dropped=0 timeouts=1
2026-11-05 11:28:59 stats conn=1 users=1 accepted=5 denied=1 packets=4 dropped=0 timeouts=1
2026-11-05 11:29:00 stats conn=0 users=0 accepted=8 denied=1 packets=8 dropped=0 timeouts=1
2026-11-05 11:29:01 rate limit: close 127.0.0.1 flood
2026-11-05 11:29:01 stats conn=0 users=0 accepted=10 denied=1 packets=20 dropped=20 timeouts=1
```

값은 다음처럼 읽는다. `conn`은 지금 연결 수, `users`는 그중 로그인한 수이다. 두 번째 줄의 `conn=2 users=1`은 연결 둘 중 하나가 로그인하지 않았다는 뜻이고, 곧 이어서 `timeout 127.0.0.1 (before login)`이 남았다. `accepted`, `denied`, `packets`, `dropped`, `timeouts`는 시작한 뒤의 누적 값이므로 이전 줄과의 차이로 읽는다. `denied`가 갑자기 늘면 정원이나 주소별 제한에 닿고 있다는 뜻이고, `dropped`가 늘면 속도 제한에 걸리는 연결이 있다는 뜻이며, `timeouts`가 늘면 로그인하지 않고 방치되는 연결이 많다는 뜻이다. 어느 쪽이든 원인이 공격인지 클라이언트의 버그인지는 주소와 이름을 보고 판단한다.

**경보.** 사람이 로그를 계속 볼 수 없으므로 기준을 정해 알린다. 이 서버에서 경보로 삼을 만한 것은 다음과 같다.

- 서버가 재시작되었다(6.14.5절의 자동 재시작은 장애를 숨긴다).
- 접속자 수가 짧은 시간에 크게 줄었다.
- 틱 처리 시간이 예산(6.12.1절)을 넘는 일이 계속된다.
- `packet error` 같은 오류 로그가 늘었다.
- 디스크 사용량이 한계에 가까워졌다(로그가 쌓인다).

지표를 모아 보여 주고 경보를 보내는 도구(Prometheus, Grafana 등)가 있다. 도구의 설정은 이 교재의 범위 밖이고, 여기까지는 `stats` 줄을 파일에서 읽는 정도로 충분하다. systemd 서비스는 표준 출력을 저널에 모은다(6.14.5절의 `journalctl`). 로그 파일은 크기나 기간으로 나누어 오래된 것을 지운다. 개인정보가 로그에 있다면 보관 기간도 정해야 한다(1.11절).

### 6.15.6 설정과 비밀 정보

6.14.3절의 설정 파일에 이 절의 제한값을 더한다. 키 이름과 기본값은 다음 견본과 같다. 서비스에 맞는 값은 측정한 뒤 정한다.

```text
# server.example.ini — 이 파일을 server.ini로 복사해서 고쳐 쓴다 (server.ini는 저장소에 올리지 않는다)
port = 9000

# 연결 제한 (6.15.1절)
max_per_ip = 20          # 같은 주소에서 동시에 연결할 수 있는 수
auth_timeout = 5         # 로그인 전: 이 시간(초) 안에 패킷을 보내야 한다
idle_timeout = 30        # 로그인 후: 이 시간(초) 동안 아무것도 보내지 않으면 끊는다

# 속도 제한 (6.15.2절)
rate_per_sec = 20        # 세션마다 초당 처리할 패킷 수
rate_burst = 40          # 한꺼번에 허용할 패킷 수
max_drops = 50           # 버린 패킷이 이만큼 쌓이면 연결을 끊는다

# 지표 (6.15.5절)
stats_sec = 10           # 지표를 로그로 남기는 간격(초). 0이면 남기지 않는다
```

`main`은 제한값을 읽고 검증한다. 숫자가 아니거나 0 이하인 값은 서버가 시작할 때 알려 준다. 시작 로그에는 읽은 값을 남긴다. 키 이름을 틀리게 쓰면 조용히 기본값이 쓰이는데, 이 로그를 보면 오타를 찾을 수 있다. 6.14.3절의 시작 메시지도 `Log`로 바꾼다.

```cpp
int main(int argc, char** argv)
{
    try
    {
        Config cfg;
        std::string path = argc > 1 ? argv[1] : "server.ini";
        if (!cfg.Load(path)) std::cerr << "설정 파일이 없어 기본값을 쓴다: " << path << "\n";
        int port = cfg.GetInt("port", 9000);
        Limits lim;
        lim.maxPerIp = (size_t)cfg.GetInt("max_per_ip", (int)lim.maxPerIp);
        lim.authSec = cfg.GetInt("auth_timeout", lim.authSec);
        lim.idleSec = cfg.GetInt("idle_timeout", lim.idleSec);
        lim.rate = cfg.GetInt("rate_per_sec", lim.rate);
        lim.burst = cfg.GetInt("rate_burst", lim.burst);
        lim.maxDrops = cfg.GetInt("max_drops", lim.maxDrops);
        lim.statsSec = cfg.GetInt("stats_sec", lim.statsSec);
        if (lim.maxPerIp < 1 || lim.authSec < 1 || lim.idleSec < 1 || lim.rate < 1 || lim.burst < 1 || lim.maxDrops < 1)
            throw std::runtime_error("config: limits must be positive");
        asio::io_context io;
        Server sv(io, (uint16_t)port, lim);
        Log(std::string("ChatServer ") + SERVER_VERSION + " port " + std::to_string(port));
        Log(std::format("limits max_per_ip={} auth_timeout={}s idle_timeout={}s rate={}/s burst={} max_drops={}",
                        lim.maxPerIp, lim.authSec, lim.idleSec, lim.rate, lim.burst, lim.maxDrops));   // 읽은 값을 남긴다 (키 이름의 오타를 찾을 수 있다)
        io.run();
    }
    catch (const std::exception& e)
    {
        std::cerr << "fatal: " << e.what() << "\n";      // 예: bind: Address already in use
        return 1;
    }
}
```

실행 결과는 다음과 같다. 위에서부터 견본 설정으로 시작한 경우, 제한값이 0인 경우, 숫자가 아닌 경우이다(종료 코드는 마지막 줄에 적었다).

```text
2026-11-05 11:23:52 ChatServer dev port 9000
2026-11-05 11:23:52 limits max_per_ip=20 auth_timeout=5s idle_timeout=30s rate=20/s burst=40 max_drops=50
```

```text
fatal: config: limits must be positive
exit=1
```

```text
fatal: config: 'rate_per_sec' must be an integer, got 'fast'
exit=1
```

비밀 정보는 설정 파일 가운데서도 다르게 다룬다. DB 비밀번호, 서버 간 공유 키, 외부 서비스의 토큰이 해당한다.

- 저장소에 올리지 않는다. 저장소에는 키 이름만 있는 견본(`server.example.ini`)을 올리고 실제 값이 든 `server.ini`는 `.gitignore`에 적는다.
- 서버 PC에서 파일의 접근 권한을 서비스 전용 계정만 읽을 수 있게 한다(Linux에서는 `chmod 600`).
- 실수로 저장소에 올렸다면 파일을 지우는 것으로 끝나지 않는다. 형상 관리는 이력을 남기므로(1.10.2절) 그 비밀은 이미 새어 나간 것으로 보고 폐기하고 새로 발급한다.
- 개발용 비밀과 운영용 비밀을 분리한다. 개발 PC에서 운영 DB에 접속할 수 있으면 개발 PC가 뚫릴 때 운영도 뚫린다.
- 코드에 적힌 비밀도 비밀 정보이다. 이 교재의 실습용 인증 코드 `1234`는 코드에 적혀 있고 모든 사용자가 같다. 서비스에서는 쓸 수 없고, 5장의 계정과 비밀번호 해시, 로그인 뒤의 세션 토큰으로 바꿔야 한다.

서버를 실행하는 계정과 DB 계정은 필요한 권한만 준다. 서버는 서비스 전용 계정으로 실행하고(6.14.5절의 `User=gameserver`), DB 계정에는 테이블을 지우거나 권한을 바꾸는 권한을 주지 않는다. 서버가 뚫려도 할 수 있는 일이 그 계정의 권한으로 제한된다.

### 6.15.7 운영 전 점검

서비스화는 6.14.5절에서 다뤘다. 컨테이너 배포와 CI/CD 도구의 설정은 이 절 첫머리의 "서비스화" 항목에 적었듯이 이 교재의 범위 밖이며, 6.14.7절에서는 CI가 실행할 명령까지만 다뤘다. 서비스를 열기 전에 다음을 점검하고 결과를 README에 적는다.

- 네트워크: 외부에 여는 포트는 클라이언트가 접속하는 포트뿐이다. 서버 간 포트와 DB, Redis의 포트는 외부에서 접근할 수 없다. 시험 서버와 운영 서버는 서로 다른 망이나 계정을 쓴다.
- 계정과 권한: 서비스 전용 계정으로 실행한다. 기본 계정과 기본 비밀번호는 없앴다. 관리 기능에 접근하는 경로를 확인했다.
- 자원 한도: 파일 한도(`LimitNOFILE`)가 `kMaxConn`과 다른 핸들 수보다 충분히 크다. 메모리와 디스크 용량, 로그 보관 정책을 정했다. 서버들의 시계가 동기화되어 있다(입장권의 만료 시각과 로그의 시각이 서버마다 어긋나면 문제가 된다).
- 장애 대비: 자동 재시작과 재시작 횟수 제한(6.14.5절), core 파일과 심볼 보관(6.14.2절), DB 백업과 복구 연습(5.13절), 롤백 절차(6.14.6절)가 준비되어 있다.
- 관측: 지표 로그를 모으고 있고, 경보 기준과 받을 사람이 정해져 있다(6.15.5절).
- 절차서: 시작, 정지, 배포, 롤백, 장애 대응 절차와 연락 체계가 문서로 있다.
- 법규: 개인정보 수집과 보관, 미성년자 보호, 확률형 아이템 정보 공개 같은 서비스 종류에 따른 의무를 확인했다(1.11절).

**[흔한 오류]** 속도 제한이나 주소별 제한을 너무 작게 잡아 정상 사용자를 끊는 경우(공용 주소 NAT 환경과 몰아 보내는 클라이언트를 고려하지 않음), 로그인 전과 후의 시간 제한을 구분하지 않는 경우, 예외를 잡고 아무것도 기록하지 않는 경우, 시험이 제한 시간보다 오래 기다려서 검사가 사라져도 통과하는 경우, 정원이 파일 한도보다 큰 경우, 오류 로그를 반복마다 남겨 로그가 폭주하는 경우, 비밀 정보를 저장소에 올리는 경우, 서버 간 패킷을 클라이언트가 보낼 수 없다고 가정하는 경우.

## 6.16 6장 연습 문제와 최종 점검

1. epoll과 IOCP의 차이를 "읽기 요청을 누가 호출하는가"를 중심으로 설명하라.
2. Asio의 async_read 핸들러에서 shared_from_this를 붙잡지 않으면 어떤 문제가 생기는지 설명하라.
3. 송신 큐가 없는 서버가 한 세션에 async_write를 동시에 두 번 호출하면 어떤 문제가 생기는지 쓰라.
4. Game Server의 heartbeat가 끊겼을 때 Lobby가 해야 할 일을 순서대로 쓰라.
5. 일회용 티켓에 포함해야 할 값 네 가지와 각각의 목적을 쓰라.
6. 틱 30Hz, 방 100개를 한 프로세스에서 처리할 때 한 틱의 예산을 어떻게 나눌지 제안하라.
7. 송신 큐 길이 제한을 두는 이유와, 넘었을 때 취할 수 있는 조치 두 가지를 쓰라.
8. 서버가 PING의 본문을 그대로 되돌려 주므로(PONG), 본문이 64바이트를 넘는 PING은 거부하는 규칙을 6.13.2절의 `IsValidHeader`에 더하려 한다. 코드를 고치기 전에 경계값(64와 65)을 확인하는 단위 시험을 먼저 쓰고, 시험이 실패하는 것을 확인한 뒤에 코드를 고쳐 통과시켜라. 기존 시험이 모두 통과하는지, Dummy와 Unity 클라이언트가 보내는 PING이 이 규칙에 걸리지 않는지도 확인한다.
9. 6.13.2절의 방법(코드를 일부러 틀리게 고쳐 시험이 실패하는지 본다)으로 6.15절의 단위 시험을 점검하라. `IsValidName`의 `n.size() > kMaxName`을 `>=`로, `TokenBucket::Allow`의 `tokens_ < 1.0`을 `<=`로 바꿔 보고, `std::min(cap_, …)`를 지워 보고, 각각 어느 CHECK가 실패하는지 적어라. `IsValidName`의 조건을 하나씩 지워 보면 `TestName`이 잡지 못하는 것이 하나 있다. 찾아서 그것을 잡는 CHECK를 더하라.
10. 실습 3의 방 기능을 6.13.3절의 통합 시험에 추가하라. 방 두 개에 클라이언트를 둘씩 입장시켜 한 방의 대화가 다른 방에 보이지 않는 것과, 마지막 사람이 나가면 방이 목록에서 사라지는 것을 자동으로 확인한다. 방 안에서 `Abort`로 연결을 갑자기 끊었을 때 방과 목록이 정리되는 것도 시험하라.
11. 6.13.4절의 실험을 직접 해 보라. 실습 2까지 완성한 서버에서 `ReadHeader`, `ReadBody`, `Write` 완료 처리의 `self` 캡처를 빼고, 일반 빌드와 ASan 빌드에서 각각 IntegrationTest를 세 번씩 실행해 결과를 비교한다. ASan 보고서에서 잘못 접근한 곳, 해제한 곳, 객체를 만든 곳이 각각 어느 함수인지 쓰고, `self`를 되돌린 뒤 보고가 사라지는지 확인한다. 일반 빌드에서는 같은 오류를 시험이 잡지 못한 이유도 설명하라.
12. `main`에서 예외를 잡지 않는 서버(6.14.3절로 고치기 전의 서버)를 같은 포트로 두 번 실행해 비정상 종료를 일으키고(6.14.2절), 심볼 파일이 없을 때와 있을 때의 호출 스택을 비교하라. 죽은 줄을 찾아 원인을 설명한다. Windows에서는 PDB와 덤프 파일로 같은 실습을 한다.
13. 6.14.4절의 배포 묶음을 만드는 명령을 스크립트 파일 하나로 만들고, 저장소를 새 폴더에 복제해 처음부터 실행하라. 묶음의 파일 목록, 서버를 시작했을 때 로그의 버전 문자열, 심볼 파일을 보관한 위치와 이름을 기록한다. 커밋을 하나 더 만든 뒤 CMake를 다시 설정하지 않고 빌드하면 버전 문자열이 어떻게 되는지 확인하고, 배포용 빌드를 새 폴더에서 설정부터 하는 이유를 설명하라.
14. 실습 4의 `Retry`는 Game Server가 Lobby에 처음 연결하지 못했을 때만 쓰인다. 연결된 뒤에 Lobby가 종료되었다가 다시 실행되는 경우에도 Game Server가 다시 연결해 `SRV_REGISTER`를 보내도록, 서버 간 연결(`link_`)의 `OnClose`에서 `ConnectLobby`를 다시 호출하게 고쳐라. 재시도 간격은 1초에서 시작해 실패할 때마다 두 배로 늘리되 30초를 넘기지 않게 한다(지수 백오프, 6.11.6절). Lobby를 다시 시작한 직후에는 Lobby의 방 목록이 비어 있는 한계가 남는지 확인하고, 이를 줄이는 방법을 제안하라(6.11.7절).
15. 6.15절의 보강을 실습 3의 구조(I/O 스레드와 논리 스레드)에 적용하라. 지표(`Counters`)와 주소별 연결 수(`perIp_`)를 어느 스레드가 읽고 쓰는지 적고, 두 스레드가 함께 만지는 값은 `std::atomic`으로 바꾸거나 한 스레드만 만지게 한다. ThreadSanitizer(6.13.4절)로 점검하고, 6.15절의 통합 시험이 그대로 통과하는지 확인하라.
16. 실습 4의 Lobby가 `SRV_REGISTER`의 본문에 설정 파일의 공유 키(`server_key`)가 들어 있을 때만 Game Server로 등록하게 고쳐라. 등록하지 않은 연결이 `SRV_REPORT`나 `SRV_ROOM_READY`를 보내면 그 연결을 끊는다. 클라이언트로 접속해 `SRV_REGISTER`를 보내는 통합 시험을 먼저 쓰고, 고치기 전에는 실패하고 고친 뒤에는 통과하는지 확인하라. 키가 저장소에 올라가지 않았는지도 확인한다(6.15.6절).
17. 같은 주소에서 틀린 인증 코드로 `HELLO`를 보내 `DENY code`를 받는 일이 60초 안에 5번을 넘으면, 그 주소의 새 연결을 5분 동안 `DENY blocked`로 거절하게 만들어라. 시각을 `TokenBucket`처럼 인자로 받게 해서 단위 시험을 먼저 쓴다. 정상 사용자가 막히지 않으려면 횟수와 시간을 어떻게 정해야 하는지, 여러 사용자가 같은 공인 주소를 쓰는 환경에서는 어떤 문제가 생기는지 설명하라.
18. Lobby 1개와 Game Server 2개를 실행하고 6.14.6절의 순서대로 Game Server 하나를 새 버전으로 교체하라. 새 버전은 시작 로그의 버전 문자열로 구분한다. 이어서 이전 버전으로 되돌려라(롤백). 각 단계에서 접속해 있던 사용자와 새로 만드는 방에 어떤 영향이 있었는지 기록한다.
19. 6.11.6절에서 설명한 입장권 재사용 문제를 재현하라. Game Server가 먼저 연결하므로, Lobby 대신 접속을 받는 시험용 프로그램이 같은 방 이름과 입장권으로 `SRV_CREATE_ROOM`을 두 번 보내고, 두 요청 사이에 클라이언트가 그 입장권으로 입장하게 한다. 입장권이 다시 살아나 두 번째 입장이 성공하는지 확인하고, 사용한 입장권을 일정 시간 기억해 두었다가 거절하도록 고쳐라. 기억해 두는 시간은 무엇을 기준으로 정해야 하는지 설명한다.
20. 1.6.3절의 프로토콜 버전을 구현하라. HELLO의 본문을 `이름|인증 코드|프로토콜 버전`으로 늘리고, 서버가 지원하지 않는 버전이면 `DENY version`을 보내고 닫는다. 서버는 WELCOME의 본문에 6.14.3절의 `SERVER_VERSION`을 담고, 클라이언트(통합 시험이나 Unity 채팅 GUI)는 이를 화면에 보여 준다. 버전 필드가 없는 옛 클라이언트를 받을지 거절할지 정하고, 이 정책이 6.15.3절의 "알 수 없는 종류의 패킷" 정책과 맞는지 점검해 시험으로 확인하라.

**[최종 점검]** 아래 항목을 모두 시험으로 확인한 뒤 제출한다. (1) 헤더가 쪼개져 도착해도 파싱된다. (2) 틀린 인증 코드와 중복 이름이 거부된다. (3) 정원 초과가 거부된다. (4) 클라이언트가 강제 종료되어도 접속자 목록이 갱신된다. (5) Game Server를 강제 종료하면 Lobby가 감지하고 방 배정에서 제외한다. (6) Dummy 200개 접속에서 지표를 기록한다. (7) DB 저장이 게임 루프를 막지 않는다. (8) README에 서버 구성도, 프로토콜 표, 실행 방법, 측정 결과가 있다.

**[최종 점검 — 시험·배포·운영]** 위 여덟 항목에 더해 다음을 확인한다. (9) 단위 시험이 ctest로 통과하고, 통합 시험이 통과하며, 샌니타이저를 켠 빌드에서도 보고가 없다. 결과를 6.13.6절의 형식으로 README에 적었다. (10) 배포 묶음이 새 폴더에서 처음부터 만들어지고, 서버를 시작하면 로그에 버전이 나오며, 그 버전 이름으로 심볼 파일이 보관되어 있다(6.14.4절). (11) 장애 시나리오(6.11.7절) 중 두 가지 이상을 재현하고 기대 동작과 실제 동작을 기록했다(6.13.5절). (12) 연결 수 제한, 로그인 전 시간 제한, 속도 제한, 입력 검증이 시험으로 확인되어 있고 비밀 정보가 저장소에 없다(6.15절). (13) 운영 전 점검 목록(6.15.7절)과 롤백 절차(6.14.6절)를 README에 적었다. 확인하지 못한 항목이 있으면 무엇을 왜 못 했는지도 README에 적는다.
