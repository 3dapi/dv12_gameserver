-- 출처: ch05_game_database.md 5.15.2절 (원고 583행)
-- 거래에 한 번이라도 참여한 계정 (중복 제거)
SELECT from_id AS account_id FROM trade_log
UNION
SELECT to_id FROM trade_log;
