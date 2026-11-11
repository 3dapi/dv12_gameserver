// test_protocol.cpp — 소켓 없이 시험할 수 있는 순수 함수만 모았다
#include <chrono>
#include <cstdio>
#include <cstring>
#include "Protocol.h"
using namespace std::chrono;

struct Header { uint16_t size; uint16_t type; };        // 전송 순서와 같은 순서로 선언한다
static_assert(sizeof(Header) == 4);

bool IsValidHeader(const Header& h)                     // 네트워크 코드에서 분리한 검증 함수
{
    return h.size <= kMaxBody && h.type >= HELLO && h.type <= PONG;
}

long long GameDay(system_clock::time_point tp)          // 한국 시간 오전 5시를 하루의 시작으로 본다
{
    return floor<days>(tp + hours(9) - hours(5)).time_since_epoch().count();
}

static int g_fail = 0;
#define CHECK(cond) do { if (!(cond)) { std::printf("FAIL %s:%d  %s\n", __FILE__, __LINE__, #cond); ++g_fail; } } while (0)

system_clock::time_point Kst(int y, unsigned m, unsigned d, int h, int mi)     // 한국 시각 -> time_point
{
    return sys_days{year{y} / month{m} / day{d}} + hours(h) + minutes(mi) - hours(9);
}

int main()
{
    // Packet::Make: 헤더 4바이트(길이, 종류, 리틀 엔디언) + 본문
    std::string p = Packet::Make(HELLO, "kim|1234");
    CHECK(p.size() == 4 + 8);
    Header h{};
    std::memcpy(&h, p.data(), 4);
    CHECK(h.size == 8 && h.type == HELLO);
    CHECK((unsigned char)p[0] == 8 && (unsigned char)p[1] == 0 && (unsigned char)p[2] == 1);

    // 경계값: kMaxBody(1024)
    CHECK(IsValidHeader({1023, HELLO}));
    CHECK(IsValidHeader({1024, HELLO}));
    CHECK(!IsValidHeader({1025, HELLO}));
    CHECK(!IsValidHeader({60000, SAY}));                // 거짓 길이
    CHECK(!IsValidHeader({0, 0}));                      // 알 수 없는 종류
    CHECK(!IsValidHeader({0, 9999}));
    CHECK(IsValidHeader({0, PING}));                    // 빈 본문은 허용

    // 서비스 날짜: 05:00 전후
    CHECK(GameDay(Kst(2026, 11, 4, 4, 59)) == GameDay(Kst(2026, 11, 3, 12, 0)));
    CHECK(GameDay(Kst(2026, 11, 4, 5, 0))  != GameDay(Kst(2026, 11, 4, 4, 59)));
    CHECK(GameDay(Kst(2026, 11, 4, 5, 0))  == GameDay(Kst(2026, 11, 4, 23, 59)));
    CHECK(GameDay(Kst(2026, 12, 31, 23, 0)) + 1 == GameDay(Kst(2027, 1, 1, 5, 0)));   // 해가 바뀌어도 하루씩 증가

    if (g_fail == 0) std::puts("ALL PASSED");
    return g_fail ? 1 : 0;
}
