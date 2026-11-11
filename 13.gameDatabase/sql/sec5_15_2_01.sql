-- 출처: ch05_game_database.md 5.15.2절 (원고 563행)
-- 평균보다 레벨이 높은 캐릭터
SELECT name, level FROM player
WHERE level > (SELECT AVG(level) FROM player);

-- 아이템 1001을 가진 캐릭터 (IN과 EXISTS는 같은 결과를 낸다)
SELECT name FROM player
WHERE account_id IN (SELECT account_id FROM inventory WHERE item_id = 1001);

SELECT name FROM player p
WHERE EXISTS (SELECT 1 FROM inventory i WHERE i.account_id = p.account_id AND i.item_id = 1001);

-- 길드가 없는 캐릭터 (LEFT JOIN으로 짝이 없는 행 찾기)
SELECT p.name FROM player p
LEFT JOIN guild g ON g.guild_id = p.guild_id
WHERE g.guild_id IS NULL;
