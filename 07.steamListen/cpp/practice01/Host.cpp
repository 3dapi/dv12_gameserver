// Host.cpp — Windows, Winsock2
#include <winsock2.h>
#include <ws2tcpip.h>
#include <iostream>
#include <string>
#include <vector>
#include <map>
#include <queue>
#include <mutex>
#include <thread>
#pragma comment(lib, "ws2_32.lib")

struct Peer { std::string name; std::string buf; };      // 이름이 비어 있으면 아직 HELLO 전
std::map<SOCKET, Peer> g_peers;
std::queue<std::string> g_input;                          // 키보드 입력 스레드가 채운다
std::mutex g_mtx;
std::string g_host = "Host";
const size_t kMax = 4;                                    // 정원

void SendLine(SOCKET s, const std::string& line)
{
    std::string m = line + "\n";
    send(s, m.c_str(), (int)m.size(), 0);
}
void Broadcast(const std::string& line)                   // 호스트 화면에도 표시한다
{
    std::cout << line << "\n";
    for (auto& kv : g_peers) if (!kv.second.name.empty()) SendLine(kv.first, line);
}
std::string UserList()
{
    std::string r = "USERS " + g_host;
    for (auto& kv : g_peers) if (!kv.second.name.empty()) r += "," + kv.second.name;
    return r;
}
bool Handle(SOCKET s, const std::string& line)           // false이면 연결을 닫는다
{
    Peer& p = g_peers[s];
    if (line.rfind("HELLO ", 0) == 0 && p.name.empty())
    {
        std::string name = line.substr(6);
        bool dup = (name == g_host);
        for (auto& kv : g_peers) if (kv.first != s && kv.second.name == name) dup = true;
        if (dup) { SendLine(s, "DENY name"); return false; }
        p.name = name;
        SendLine(s, "WELCOME");
        Broadcast("NOTICE " + name + " joined");
        Broadcast(UserList());
    }
    else if (line.rfind("SAY ", 0) == 0 && !p.name.empty())
        Broadcast("CHAT " + p.name + " " + line.substr(4));
    return true;
}
void Remove(SOCKET s)
{
    std::string name = g_peers[s].name;
    closesocket(s);
    g_peers.erase(s);
    if (!name.empty()) { Broadcast("NOTICE " + name + " left"); Broadcast(UserList()); }
}

int main(int argc, char** argv)
{
    WSADATA w; WSAStartup(MAKEWORD(2, 2), &w);
    if (argc > 1) g_host = argv[1];
    SOCKET ls = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    sockaddr_in a{}; a.sin_family = AF_INET; a.sin_addr.s_addr = INADDR_ANY; a.sin_port = htons(9000);
    bind(ls, (sockaddr*)&a, sizeof a);
    listen(ls, SOMAXCONN);
    std::thread([] {
        std::string l;
        while (std::getline(std::cin, l)) { std::lock_guard<std::mutex> g(g_mtx); g_input.push(l); }
    }).detach();

    for (;;)
    {
        fd_set rd; FD_ZERO(&rd); FD_SET(ls, &rd);
        for (auto& kv : g_peers) FD_SET(kv.first, &rd);   // 한 번에 64개(FD_SETSIZE)까지
        timeval tv{ 0, 50000 };
        select(0, &rd, nullptr, nullptr, &tv);

        if (FD_ISSET(ls, &rd))
        {
            SOCKET c = accept(ls, nullptr, nullptr);
            if (g_peers.size() >= kMax) { SendLine(c, "DENY full"); closesocket(c); }
            else g_peers[c] = Peer{};
        }
        std::vector<SOCKET> socks;
        for (auto& kv : g_peers) socks.push_back(kv.first);
        for (SOCKET s : socks)
        {
            if (!FD_ISSET(s, &rd)) continue;
            char tmp[512];
            int n = recv(s, tmp, sizeof tmp, 0);
            if (n <= 0) { Remove(s); continue; }
            Peer& p = g_peers[s];
            p.buf.append(tmp, n);
            bool keep = true; size_t pos;
            while (keep && (pos = p.buf.find('\n')) != std::string::npos)
            {
                std::string line = p.buf.substr(0, pos);
                p.buf.erase(0, pos + 1);
                keep = Handle(s, line);
            }
            if (!keep) Remove(s);
        }
        std::lock_guard<std::mutex> g(g_mtx);
        while (!g_input.empty())
        {
            std::string l = g_input.front(); g_input.pop();
            if (l == "/quit") return 0;
            if (l == "/users") std::cout << UserList() << "\n";
            else Broadcast("CHAT " + g_host + " " + l);
        }
    }
}
