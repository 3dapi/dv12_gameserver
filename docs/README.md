# 게임서버프로그래밍 교재 원고

작성자: 오희성 (Heesung Oh)

이 폴더의 Markdown이 작업 원본이다. Google Docs 문서("[강의] 게임서버프로그래밍")는 저자가 반영을 요청할 때만 갱신하므로 이 폴더보다 오래된 내용일 수 있다. 두 쪽이 마지막으로 같았던 시점은 2026-11-04(1–6장)이다.

## 구성

| 장 | 파일 | 내용 |
|---|---|---|
| 1. 게임 서버 프로그래밍 | [ch01_intro.md](ch01_intro.md) | 교재 범위, 서버 분류와 구성(1.1–1.3) + 서버 아키텍처, 네트워크 특성과 TCP/UDP, 패킷 설계와 직렬화, 실행 모델, 보안, 운영(1.4–1.10) |
| 2. Unity Game Server | [ch02_unity.md](ch02_unity.md) | NGO 단계별 실습과 채팅 프로젝트(2.1–2.5) + NGO 내부 구조, 지연 보정 기법, 관심 영역(2.6–2.9) |
| 3. Unreal Game Server | [ch03_unreal.md](ch03_unreal.md) | Dedicated Server 단계별 실습(3.1–3.4) + Unreal 네트워킹 내부 구조, NGO와의 비교(3.5–3.7) |
| 4. Steam Listen Server | [ch04_steam_listen.md](ch04_steam_listen.md) | C++·Unity·Unreal 연동 실습(4.1–4.10) + Steamworks 구성, Networking Sockets, Listen Server 설계 특성(4.11–4.14) |
| 5. Game Database | [ch05_game_database.md](ch05_game_database.md) | MySQL·Redis 단계별 실습(5.1–5.7) + 모델링, 트랜잭션, 캐시, NoSQL, 확장성, 백업·보안, 서버 연동 설계(5.8–5.15) |
| 6. Distributed MO Game Server | [ch06_distributed_server.md](ch06_distributed_server.md) | Asio 기반 분산 서버 단계별 실습(6.1–6.9) + I/O 모델 심화, 분산 설계 원리, 성능 측정, 보안·운영 점검(6.10–6.14) |

각 장 끝의 "연습 문제" 절(1.10, 2.9, 3.7, 4.14, 5.15, 6.14)은 설명·계산·설계형 문제로 이루어져 있고, 6장에는 최종 점검 목록이 함께 있다.

## 검증 현황

- 6장 실습 1의 `Protocol.h`, `ChatServer.cpp`, `Dummy.cpp`는 Linux에서 `g++ -std=c++20`과 Asio 1.30.2(standalone)로 경고 없이 빌드하고 실행해 확인했다(더미 클라이언트 50개, 실패 0). Windows(IOCP) 빌드는 확인하지 않았다.
- 그 밖의 코드 예제는 아직 컴파일·실행 검증 전이다.
- 엔진·SDK의 버전에 따라 이름이 달라지는 항목(NGO의 RPC 속성, Tick Rate 기본값, Steam Networking Sockets 호출 이름 등)은 본문에서 사용하는 버전의 문서로 확인하도록 적어 두었다. 버전 표기 등 `[ ]` 칸은 저자가 채운다.
