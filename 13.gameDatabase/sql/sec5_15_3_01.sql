-- 출처: ch05_game_database.md 5.15.3절 (원고 600행)
DELIMITER //
CREATE PROCEDURE sp_transfer_gold(IN p_from BIGINT, IN p_to BIGINT, IN p_gold BIGINT, OUT p_result INT)
BEGIN
    DECLARE v_gold BIGINT;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        SET p_result = -1;                              -- 오류 (교착 상태로 취소된 경우도 여기로 온다)
    END;
    IF p_gold <= 0 OR p_from = p_to THEN
        SET p_result = 2;                               -- 잘못된 요청
    ELSE
        START TRANSACTION;
        -- 두 지갑을 계정 번호가 작은 쪽부터 잠근다. 값은 쓰지 않고 잠그기 위해 읽는다.
        SELECT gold INTO v_gold FROM wallet WHERE account_id = LEAST(p_from, p_to) FOR UPDATE;
        SELECT gold INTO v_gold FROM wallet WHERE account_id = GREATEST(p_from, p_to) FOR UPDATE;
        UPDATE wallet SET gold = gold - p_gold WHERE account_id = p_from AND gold >= p_gold;
        IF ROW_COUNT() = 0 THEN
            ROLLBACK;
            SET p_result = 1;                           -- 잔액 부족이거나 보내는 계정이 없다
        ELSE
            UPDATE wallet SET gold = gold + p_gold WHERE account_id = p_to;
            IF ROW_COUNT() = 0 THEN
                ROLLBACK;
                SET p_result = 3;                       -- 받는 계정이 없다
            ELSE
                INSERT INTO trade_log(from_id, to_id, gold) VALUES (p_from, p_to, p_gold);
                COMMIT;
                SET p_result = 0;                       -- 성공
            END IF;
        END IF;
    END IF;
END //
DELIMITER ;

CALL sp_transfer_gold(1, 2, 500, @r);
SELECT @r;
