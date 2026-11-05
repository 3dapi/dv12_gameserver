# 게임서버프로그래밍 교재 원고

작성자: 오희성 (Heesung Oh)

이 폴더의 Markdown이 작업 원본이다. Google Docs 문서("[강의] 게임서버프로그래밍")는 저자가 반영을 요청할 때만 갱신하므로 이 폴더보다 오래된 내용일 수 있다. 두 쪽이 마지막으로 같았던 시점은 2026-11-04(1–6장)이다. 2장은 2026-11-05에 Unity 6.3 LTS 기준으로 새로 썼으므로 Google Docs의 2장과 크게 다르다. 1·5·6장도 같은 날 NCS 기준 검토를 반영해 보강했으므로(검토 내용은 [ncs_review.md](ncs_review.md)) Google Docs보다 길다.

## 구성

| 장 | 파일 | 내용 |
|---|---|---|
| 1. 게임 서버 프로그래밍 | [ch01_intro.md](ch01_intro.md) | 교재 범위, 서버 분류와 구성(1.1–1.3) + 서버 아키텍처, 네트워크 특성과 TCP/UDP, 패킷 설계와 직렬화, 실행 모델, 보안, 운영(1.4–1.9) + 개발 환경·형상 관리·진단 도구, 라이선스·개인정보·법규(1.10–1.11) |
| 2. Unity Game Server | [ch02_unity.md](ch02_unity.md) | Unity 6.3 LTS 설치와 C# 개발자용 Unity 기본기(2.2–2.3), NGO 개념(2.4), 설치·연결·스폰·이동·NetworkVariable·RPC·접속 승인 단계별 실습(2.5–2.10), 채팅 프로그램 완성(2.11), 오브젝트 생성·제거(2.12), 지연 시험과 다른 PC 연결(2.13–2.14) + NGO 내부 구조, 지연과 지연 보정, 관심 영역, 실습 프로젝트, 참고자료(2.15–2.20) |
| 3. Unreal Game Server | [ch03_unreal.md](ch03_unreal.md) | Dedicated Server 단계별 실습(3.1–3.4) + Unreal 네트워킹 내부 구조, NGO와의 비교(3.5–3.7) |
| 4. Steam Listen Server | [ch04_steam_listen.md](ch04_steam_listen.md) | C++·Unity·Unreal 연동 실습(4.1–4.10) + Steamworks 구성, Networking Sockets, Listen Server 설계 특성(4.11–4.14) |
| 5. Game Database | [ch05_game_database.md](ch05_game_database.md) | MySQL·Redis 단계별 실습(5.1–5.7) + 모델링(ERD·정규화 포함), 트랜잭션, 캐시, NoSQL, 확장성, 백업·보안, 서버 연동 설계(5.8–5.14) + SQL 응용과 DB 내부 로직, 배치 작업(5.15–5.16) |
| 6. Distributed MO Game Server | [ch06_distributed_server.md](ch06_distributed_server.md) | Asio 기반 분산 서버 단계별 실습(6.1–6.9) + I/O 모델 심화, 분산 설계 원리(서버 간 메시지의 신뢰성, 장애 시나리오 포함), 성능 측정(6.10–6.12) + 테스트, 빌드와 배포, 서버 보안과 운영 점검(6.13–6.15) |

각 장 끝의 "연습 문제" 절(1.12, 2.20, 3.7, 4.14, 5.17, 6.16)은 설명·계산·설계형 문제로 이루어져 있고, 6장에는 최종 점검 목록이 함께 있다.

## 검증 현황

- 6장 실습 1의 `Protocol.h`, `ChatServer.cpp`, `Dummy.cpp`는 Linux에서 `g++ -std=c++20`과 Asio 1.30.2(standalone)로 경고 없이 빌드하고 실행해 확인했다(더미 클라이언트 50개, 실패 0). Windows(IOCP) 빌드는 확인하지 않았다.
- 2장의 코드(C# 블록 52개)는 Unity에서 컴파일하거나 실행하지 못했다. 괄호·문자열·`using` 구조를 도구로 검사하고, 쓰인 NGO의 이름·호출 형태와 본문에 인용한 오류 문구를 NGO 2.13.3의 문서·소스와 대조했다. 2.19.2절의 `[실행 확인 후 기재]` 칸은 저자가 Unity 6.3 LTS에서 실습을 실행한 뒤 채운다.
- 2장의 그림 17장은 `images/`에 있다. PNG는 본문에 삽입된 것이고 같은 이름의 SVG는 고쳐 그릴 때 쓰는 원본이다. 에디터 화면 그림은 실제 화면이 아니라 개념도이다.
- 1장 1.10.1절의 CMake 예제와 6장 6.13–6.15절의 코드·명령은 Linux(Ubuntu 24.04, g++ 13.3, clang++ 18.1, CMake 3.28, Asio 1.30.2)에서 컴파일하고 실행해 결과를 확인했다. 단위 시험과 통합 시험, 샌니타이저 빌드, 설정 오류 메시지, 연결 수·속도 제한 등이 해당한다. systemd 유닛 파일은 `systemd-analyze verify`로 문법만 확인했고 서비스를 실제로 시작하지는 않았다.
- 5장 5.8.4–5.16절의 표준 SQL, 트리거 본문, 프로시저의 논리는 SQLite에서 실행해 확인했다. MySQL에서만 되는 구문(`DELIMITER`로 선언하는 프로시저, `FOR UPDATE`의 잠금 순서, `DELETE ... ORDER BY ... LIMIT`, 느린 질의 로그 설정, `GET_LOCK`)은 MySQL에서 실행하지 못했다. 5.8.4절의 ERD는 그림 파일이 아니라 글자로 그린 도식이다.
- Windows에서의 실행(IOCP, MSVC, Windows 서비스 등록, 덤프 분석, Visual Studio의 ASan)은 확인하지 않았다. 6장의 `[검증 환경]` 문장은 Linux에서 확인했다는 뜻이다.
- 그 밖의 코드 예제(3·4장의 실습, 5.7절의 실습, 6장 실습 2~5)는 아직 컴파일·실행 검증 전이다.
- 엔진·SDK의 버전에 따라 이름이 달라지는 항목(NGO의 RPC 속성, Tick Rate 기본값, Steam Networking Sockets 호출 이름 등)은 본문에서 사용하는 버전의 문서로 확인하도록 적어 두었다. 버전 표기 등 `[ ]` 칸은 저자가 채운다.
- NCS 검토(ncs_review.md)는 능력단위의 이름과 교재 주제를 대조한 것이다. NCS 공식 사이트에 접속하지 못해 수행준거 원문은 확인하지 못했다.
