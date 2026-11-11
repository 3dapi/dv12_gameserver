-- 출처: ch05_game_database.md 5.15절 (원고 524행)
CREATE TABLE guild (
  guild_id     INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  name         VARCHAR(32) NOT NULL UNIQUE
) ENGINE=InnoDB;

CREATE TABLE player (
  player_id    BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  account_id   BIGINT UNSIGNED NOT NULL,
  name         VARCHAR(32) NOT NULL UNIQUE,
  level        INT UNSIGNED NOT NULL DEFAULT 1,
  guild_id     INT UNSIGNED NULL,
  FOREIGN KEY (account_id) REFERENCES account(account_id),
  FOREIGN KEY (guild_id)   REFERENCES guild(guild_id)
) ENGINE=InnoDB;
