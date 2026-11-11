// test_protocol.cpp — 소켓 없이 시험할 수 있는 순수 함수만 모았다
#include <chrono>
#include <cstdio>
#include <sstream>
#include "Config.h"
#include "GameTime.h"
#include "Protocol.h"
#include "TokenBucket.h"
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

int main()
{
    TestPacket();
    TestHeader();
    TestGameDay();
    TestConfig();
    TestTokenBucket();
    TestName();
    if (g_fail == 0) std::puts("ALL PASSED");
    return g_fail ? 1 : 0;                                // 0이 아니면 ctest가 실패로 본다
}
