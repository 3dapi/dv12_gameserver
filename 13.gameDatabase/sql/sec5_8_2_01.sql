-- 출처: ch05_game_database.md 5.8.2절 (원고 335행)
CREATE TABLE account (
  account_id   BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  login_name   VARCHAR(32)  NOT NULL UNIQUE,
  pw_salt      VARBINARY(16) NOT NULL,
  pw_hash      VARBINARY(32) NOT NULL,
  created_at   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE inventory (
  account_id   BIGINT UNSIGNED NOT NULL,
  item_id      INT UNSIGNED    NOT NULL,
  amount       INT UNSIGNED    NOT NULL,
  PRIMARY KEY (account_id, item_id),
  FOREIGN KEY (account_id) REFERENCES account(account_id)
) ENGINE=InnoDB;
