// ChatServer.cpp — 실습 2 + 6.14.3절(설정, 버전) + 6.15절(보안 보강)
#include <asio.hpp>
#include <chrono>
#include <deque>
#include <iostream>
#include <map>
#include <memory>
#include <set>
#include "Config.h"
#include "Log.h"
#include "Protocol.h"
#include "TokenBucket.h"
#ifndef SERVER_VERSION
#define SERVER_VERSION "dev"                      // CMake가 실제 값을 넣는다
#endif
using asio::ip::tcp;
class Server;
using SessionPtr = std::shared_ptr<class Session>;

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
    int drops_ = 0;
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
    asio::steady_timer retry_, report_;
    Limits lim_;
    Counters counters_;
    bool acceptFailing_ = false;
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
void Server::OnPacket(const SessionPtr& s, uint16_t type, const std::string& b)
{
    if (s->Name().empty() && type != HELLO)             // 로그인 전에는 HELLO만 허용한다 (다른 패킷으로 시간 제한을 늘릴 수 없다)
        return s->Close();
    if (type == HELLO && s->Name().empty())
    {
        auto p = b.find('|');
        std::string name = b.substr(0, p);
        std::string code = p == std::string::npos ? "" : b.substr(p + 1);
        if (code != "1234") return s->SendAndClose(Packet::Make(DENY, "code"));
        if (!IsValidName(name) || users_.count(name)) return s->SendAndClose(Packet::Make(DENY, "name"));
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
    if (all_.erase(s))                                  // 정원에 들어 있던 연결만 센다
    {
        auto it = perIp_.find(s->Addr());
        (void)it;
    }
    if (s->Name().empty()) return;
    users_.erase(s->Name());
    Broadcast(Packet::Make(NOTICE, s->Name() + " left"));
    Broadcast(Packet::Make(USERS, UserList()));
}

void Session::Start() { Arm(); ReadHeader(); }
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
void Session::ReadHeader()
{
    auto self = shared_from_this();                     // 완료 처리가 끝날 때까지 세션을 살려 둔다
    asio::async_read(sock_, asio::buffer(hdr_, 4), [this, self](std::error_code ec, size_t) {
        if (ec) return Close();
        uint16_t size, type;
        std::memcpy(&size, hdr_, 2);
        std::memcpy(&type, hdr_ + 2, 2);
        if (!IsValidHeader(size, type)) return Close();
        ReadBody(type, size);
    });
}
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
    timer_.cancel();
    std::error_code ec;
    sock_.close(ec);
    server_.Leave(shared_from_this());
}

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
