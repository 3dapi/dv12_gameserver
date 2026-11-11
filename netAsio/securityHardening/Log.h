// Log.h — 시각(UTC)을 붙여 한 줄씩 남긴다
#pragma once
#include <chrono>
#include <format>
#include <iostream>
#include <string>

inline void Log(const std::string& msg)
{
    auto now = std::chrono::floor<std::chrono::seconds>(std::chrono::system_clock::now());
    std::cout << std::format("{:%F %T} ", now) << msg << std::endl;
}
