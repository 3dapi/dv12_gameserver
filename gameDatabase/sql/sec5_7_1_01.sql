-- 출처: ch05_game_database.md 5.7.1절 (원고 39행)
CREATE DATABASE IF NOT EXISTS gamedb DEFAULT CHARACTER SET utf8mb4;
USE gamedb;

CREATE TABLE IF NOT EXISTS account (
    id       BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    login    VARCHAR(32) NOT NULL UNIQUE,         -- 중복 가입을 DB가 막는다
    pw_hash  CHAR(64) NOT NULL,                   -- PBKDF2 결과(16진수)
    salt     CHAR(32) NOT NULL,                   -- 계정마다 다른 난수(16진수)
    gold     INT NOT NULL DEFAULT 0,
    created  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS bonus_log (
    account_id BIGINT UNSIGNED NOT NULL,
    bonus_key  VARCHAR(32) NOT NULL,
    amount     INT NOT NULL,
    claimed    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (account_id, bonus_key),          -- 같은 보너스는 한 번만 기록된다(멱등성)
    FOREIGN KEY (account_id) REFERENCES account(id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS game_result (
    result_id  CHAR(36) PRIMARY KEY,              -- 요청 식별자(UUID). 같은 결과를 두 번 저장하지 않는다
    room       VARCHAR(32) NOT NULL,
    account_id BIGINT UNSIGNED NOT NULL,
    score      INT NOT NULL,
    saved      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

-- 보너스 지급: 기록과 지급이 함께 성공하거나 함께 취소되어야 한다
START TRANSACTION;
INSERT INTO bonus_log (account_id, bonus_key, amount) VALUES (1, 'welcome', 100);   -- 두 번째 실행은 오류 1062
UPDATE account SET gold = gold + 100 WHERE id = 1;
COMMIT;
