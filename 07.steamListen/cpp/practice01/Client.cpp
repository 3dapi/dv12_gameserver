// Client.cpp — 사용법: Client.exe 127.0.0.1 철수
#include <winsock2.h>
#include <ws2tcpip.h>
#include <iostream>
#include <string>
#include <thread>
#pragma comment(lib, "ws2_32.lib")

int main(int argc, char** argv)
{
    if (argc < 3) return 1;
    WSADATA w; WSAStartup(MAKEWORD(2, 2), &w);
    SOCKET s = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    sockaddr_in a{}; a.sin_family = AF_INET; a.sin_port = htons(9000);
    inet_pton(AF_INET, argv[1], &a.sin_addr);
    if (connect(s, (sockaddr*)&a, sizeof a) != 0) { std::cout << "connect failed\n"; return 1; }
    std::string hello = std::string("HELLO ") + argv[2] + "\n";
    send(s, hello.c_str(), (int)hello.size(), 0);
    std::thread([s] {
        char b[512]; int n;
        while ((n = recv(s, b, sizeof b, 0)) > 0) std::cout.write(b, n);
        std::cout << "disconnected\n";
    }).detach();
    std::string l;
    while (std::getline(std::cin, l))
    {
        if (l == "/quit") break;
        l = "SAY " + l + "\n";
        send(s, l.c_str(), (int)l.size(), 0);
    }
    closesocket(s);
    WSACleanup();
}
