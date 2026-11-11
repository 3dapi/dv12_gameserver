-- 출처: ch05_game_database.md 5.15.1절 (원고 547행)
-- 길드별 인원과 평균 레벨 (5명 이상인 길드만)
SELECT guild_id, COUNT(*) AS members, AVG(level) AS avg_level
FROM player
WHERE guild_id IS NOT NULL
GROUP BY guild_id
HAVING COUNT(*) >= 5
ORDER BY avg_level DESC;
