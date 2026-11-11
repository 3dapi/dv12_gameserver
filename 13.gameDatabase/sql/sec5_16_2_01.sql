-- 출처: ch05_game_database.md 5.16.2절 (원고 707행)
-- 만료된 우편을 1,000건씩 지운다 (MySQL). 영향받은 행이 1,000건 미만이 될 때까지 반복한다.
DELETE FROM mail WHERE expire_at < NOW() ORDER BY mail_id LIMIT 1000;
