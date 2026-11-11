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
