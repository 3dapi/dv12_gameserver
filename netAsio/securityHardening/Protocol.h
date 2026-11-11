// Protocol.h — 서버와 Dummy Client가 공유한다
#pragma once
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <string>

enum Type : uint16_t
{
    HELLO = 1, WELCOME, DENY, SAY, CHAT, NOTICE, USERS, PING, PONG,
    ROOM_CREATE = 10, ROOM_JOIN, ROOM_LEAVE, ROOM_LIST, ROOM_LIST_RES, ROOM_ADDR,
    BONUS = 20, BONUS_RES,
    SRV_REGISTER = 100, SRV_REPORT, SRV_CREATE_ROOM, SRV_ROOM_READY
};
const size_t kMaxBody = 1024;                           // 본문 최대 길이
const size_t kMaxConn = 3;                              // 시험을 위해 작게 둔다

struct Packet
{
    static std::string Make(uint16_t type, const std::string& body)
    {
        uint16_t size = (uint16_t)body.size();
        std::string s(4, '\0');
        std::memcpy(&s[0], &size, 2);                   // Windows(x86/x64)는 리틀 엔디언이다
        std::memcpy(&s[2], &type, 2);
        return s + body;
    }
};

// ---- 6.13절에서 추가: 네트워크 코드에서 분리한 순수 함수 ----
inline bool IsKnownType(uint16_t t)
{
    return (t >= HELLO && t <= PONG) || (t >= ROOM_CREATE && t <= ROOM_ADDR) ||
           (t >= BONUS && t <= BONUS_RES) || (t >= SRV_REGISTER && t <= SRV_ROOM_READY);
}
inline bool IsValidHeader(uint16_t size, uint16_t type)
{
    return size <= kMaxBody && IsKnownType(type);
}

// ---- 6.15절에서 추가: 본문(이름) 검증 ----
const size_t kMaxName = 32;                             // 이름의 최대 바이트 수 (UTF-8 한글은 한 글자가 3바이트)
inline bool IsValidName(const std::string& n)
{
    if (n.empty() || n.size() > kMaxName) return false;
    for (unsigned char c : n)
        if (c < 0x20 || c == 0x7F || c == '|' || c == ',') return false;       // 제어 문자와 프로토콜 구분자
    return true;
}
