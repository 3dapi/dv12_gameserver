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
