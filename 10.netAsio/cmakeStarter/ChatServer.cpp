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
