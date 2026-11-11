# 13.gameDatabase

5장(게임 데이터베이스) 원고의 SQL과 확인용 스크립트.

| 파일 | 내용 |
|---|---|
| sql/secX_Y_NN.sql | 원고의 SQL 코드 블록. 파일 첫 줄에 원고 절과 행 번호 |
| check_sql_sqlite.py | 표준 SQL과 프로시저 논리를 SQLite로 확인한 스크립트 |

| 항목 | 내용 |
|---|---|
| 대상 | MySQL 8 |
| 검증 상태 | 표준 SQL만 SQLite로 확인. MySQL 전용 구문(DELIMITER 프로시저, FOR UPDATE, DELETE … LIMIT, 느린 질의 설정, GET_LOCK)은 MySQL에서 실행하지 않음 |
