# netAsio

Asio(standalone) 기반 C++20 채팅 서버 예제.

| 폴더 | 내용 | 원고 위치 |
|---|---|---|
| cmakeStarter | CMake 시작 예제 | 1.10.1절 |
| chatServerBasic | 채팅 서버·더미 클라이언트 | 6.8 실습 1 |
| timerAndUnitTest | 서비스 날짜 타이머, 프로토콜 단위 시험 | 5.16.3절, 6.13.2절 |
| buildDeploy | CMake 빌드·설치·버전 표시, systemd 유닛 | 6.14절 |
| securityHardening | 연결 제한, 속도 제한, 입력 검증, 로그, 통합 시험, 실험 코드 | 6.13~6.15절 |

| 항목 | 내용 |
|---|---|
| 필요 | C++20 컴파일러, standalone Asio 1.30 이상(`-DASIO_STANDALONE`, `-I<asio>/include`) |
| 검증 환경 | Ubuntu 24.04, g++ 13.3, clang++ 18.1, CMake 3.28, Asio 1.30.2 |
| 검증 상태 | Linux에서 빌드·실행 확인(원고 작성 시), 이 폴더로 옮긴 뒤 다시 컴파일 확인(2026-11-10). Windows 미확인 |
| systemd 유닛 | 문법만 확인, 실제 서비스 시작은 하지 않음 |
