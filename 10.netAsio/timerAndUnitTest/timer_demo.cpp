// 서버 안의 주기 작업: 서비스 날짜가 바뀌었는지 확인한다 (데모를 위해 주기를 1초로 줄였다)
#include <asio.hpp>
#include <chrono>
#include <iostream>
using namespace std::chrono;

long long GameDay(system_clock::time_point tp)
{
    return floor<days>(tp + hours(9) - hours(5)).time_since_epoch().count();
}
void RunDailyReset() { std::cout << "daily reset\n"; }

long long g_fakeShift = 0;                                  // 데모용: 시간을 앞당겨 날짜 변경을 흉내 낸다

void WatchDayChange(asio::steady_timer& timer, long long& lastDay)
{
    timer.expires_after(seconds(1));                        // 운영에서는 1분 정도
    timer.async_wait([&timer, &lastDay](std::error_code ec) {
        if (ec) return;                                     // 취소된 경우
        long long today = GameDay(system_clock::now() + hours(g_fakeShift));
        if (today != lastDay)
        {
            lastDay = today;
            RunDailyReset();                                // 한 번만 실행되어야 한다
        }
        WatchDayChange(timer, lastDay);
    });
}
int main()
{
    asio::io_context io;
    asio::steady_timer timer(io);
    long long lastDay = GameDay(system_clock::now());       // 운영에서는 DB에 저장한 마지막 처리 날짜를 읽는다
    WatchDayChange(timer, lastDay);
    asio::steady_timer shifter(io, seconds(2));
    shifter.async_wait([](std::error_code) { g_fakeShift = 24; });     // 2초 뒤 하루가 지난 것처럼 만든다
    io.run_for(seconds(5));
}
