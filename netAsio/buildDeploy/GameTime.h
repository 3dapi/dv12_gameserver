// GameTime.h — 서비스 날짜 계산 (네트워크와 무관한 순수 함수)
#pragma once
#include <chrono>

inline long long GameDay(std::chrono::system_clock::time_point tp)     // 한국 시간 오전 5시를 하루의 시작으로 본다
{
    using namespace std::chrono;
    return floor<days>(tp + hours(9) - hours(5)).time_since_epoch().count();
}
