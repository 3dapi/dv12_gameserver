-- 출처: ch05_game_database.md 5.15.3절 (원고 652행)
DELIMITER //
CREATE TRIGGER trg_wallet_audit AFTER UPDATE ON wallet
FOR EACH ROW
BEGIN
    INSERT INTO wallet_audit(account_id, old_gold, new_gold) VALUES (OLD.account_id, OLD.gold, NEW.gold);
END //
DELIMITER ;
