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

**[검증 방식]** 2~4장과 같다. C++ 서버도 눈으로 확인할 수 있는 채팅 프로그램으로 먼저 만들고, 2장의 Unity 채팅 GUI 씬을 클라이언트로 사용한다. 2장의 연결 패널과 채팅 패널은 그대로 쓰고, NetworkManager를 호출하던 부분만 TcpChatService 호출로 바꾼다. 서버 로직을 바꿀 때마다 GUI의 버튼으로 같은 시험을 반복한다. 이 절은 독립 실행형 Asio(헤더 온리, C++20)를 사용하고 Windows에서 빌드한다. 5장의 DB는 실습 5에서 연결한다.

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

Unity 쪽은 TcpChatService가 서버와 같은 패킷 형식으로 송수신한다. 수신은 별도 스레드에서 정확히 필요한 바이트를 읽고, 결과는 큐에 넣어 메인 스레드의 Update에서 이벤트로 알린다(Unity API는 메인 스레드에서만 호출한다). 2장의 ChatService와 같은 이벤트 방식이므로 GUI 스크립트는 이벤트를 구독하는 부분만 같다.

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

// 2장 채팅 패널의 버튼 처리만 바꾼다 (UI 스크립트는 이벤트를 구독한다)
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

## 6.13 서버 보안과 운영 점검

- 연결 수 제한: IP별, 전체 동시 연결 수를 제한한다. 인증 전 연결은 짧은 시간 안에 HELLO를 보내지 않으면 끊는다.
- 속도 제한: 세션마다 단위 시간당 허용 패킷 수를 정한다. 토큰 버킷 방식은 일정 속도로 토큰을 채우고 패킷마다 토큰을 소모하게 해, 순간적인 몰림은 허용하고 지속적인 과다 전송은 막는다.
- 입력 검증: 길이 제한(kMaxBody), 알 수 없는 종류, 인증 전에 허용되지 않는 패킷 종류를 모두 검사하고 위반하면 연결을 끊는다. 예외로 서버가 죽지 않게 모든 핸들러에서 입력 오류를 처리한다.
- 서버 간 신뢰: 내부 서버 사이의 통신도 인증한다. 내부망이 안전하다는 가정으로 인증을 생략하면 한 서버가 뚫렸을 때 전체가 뚫린다.
- 대규모 서비스 거부 공격(DDoS)은 서버 코드만으로 막을 수 없고 네트워크 단계의 방어(상용 DDoS 방어, 클라우드 방화벽)가 필요하다는 점을 알아 둔다.
- 로그와 관측: 중요한 사건(접속, 방 생성·종료, 오류, 느린 틱)을 로그로 남기고, 지표를 수집해 대시보드와 경보로 연결한다.
- 설정 분리: 포트, DB 주소, 정원 같은 값은 코드가 아니라 설정 파일로 두고, 비밀 정보는 저장소에 올리지 않는다.
- 서비스화: Windows는 Windows 서비스, Linux는 systemd 같은 도구로 서버를 서비스로 등록해 자동 재시작과 로그 수집을 구성한다. 컨테이너 배포와 CI/CD는 이 교재의 범위 밖이다.

## 6.14 6장 연습 문제와 최종 점검

1. epoll과 IOCP의 차이를 "읽기 요청을 누가 호출하는가"를 중심으로 설명하라.
2. Asio의 async_read 핸들러에서 shared_from_this를 붙잡지 않으면 어떤 문제가 생기는지 설명하라.
3. 송신 큐가 없는 서버가 한 세션에 async_write를 동시에 두 번 호출하면 어떤 문제가 생기는지 쓰라.
4. Game Server의 heartbeat가 끊겼을 때 Lobby가 해야 할 일을 순서대로 쓰라.
5. 일회용 티켓에 포함해야 할 값 네 가지와 각각의 목적을 쓰라.
6. 틱 30Hz, 방 100개를 한 프로세스에서 처리할 때 한 틱의 예산을 어떻게 나눌지 제안하라.
7. 송신 큐 길이 제한을 두는 이유와, 넘었을 때 취할 수 있는 조치 두 가지를 쓰라.

**[최종 점검]** 아래 항목을 모두 시험으로 확인한 뒤 제출한다. (1) 헤더가 쪼개져 도착해도 파싱된다. (2) 틀린 인증 코드와 중복 이름이 거부된다. (3) 정원 초과가 거부된다. (4) 클라이언트가 강제 종료되어도 접속자 목록이 갱신된다. (5) Game Server를 강제 종료하면 Lobby가 감지하고 방 배정에서 제외한다. (6) Dummy 200개 접속에서 지표를 기록한다. (7) DB 저장이 게임 루프를 막지 않는다. (8) README에 서버 구성도, 프로토콜 표, 실행 방법, 측정 결과가 있다.
