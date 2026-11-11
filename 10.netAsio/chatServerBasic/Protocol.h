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
