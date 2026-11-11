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
