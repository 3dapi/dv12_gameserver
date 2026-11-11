-- 출처: ch05_game_database.md 5.9.1절 (원고 398행)
START TRANSACTION;
UPDATE wallet SET gold = gold - 500 WHERE account_id = 1 AND gold >= 500;
-- 영향받은 행이 0이면 잔액 부족: ROLLBACK 후 실패 응답
UPDATE wallet SET gold = gold + 500 WHERE account_id = 2;
INSERT INTO trade_log(from_id, to_id, gold) VALUES (1, 2, 500);
COMMIT;
