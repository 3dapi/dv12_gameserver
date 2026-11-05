# 5. Game Database

## 5.1 게임 서버와 데이터 저장소

게임 상태의 실시간 처리는 게임 서버가 담당하고, 영속 저장·캐싱·조회는 데이터베이스 및 관련 서비스가 담당한다. 데이터별로 일관성, 접근 패턴, 갱신 빈도와 장애 시 복구 요구사항을 구분해 저장소를 선택한다.

## 5.2 RDBMS

MySQL 또는 MariaDB를 중심으로 계정, 캐릭터, 아이템, 게임 결과 저장을 복습한다. 테이블·키·인덱스·트랜잭션의 적용과 Connection Pool, DB Worker, 비동기 저장 처리의 역할을 살펴본다.

## 5.3 NoSQL

Redis: 캐시, 로그인 세션의 보조 정보, 매치메이킹 보조 상태, 랭킹 등 저지연 접근에 적합한 활용 사례를 검토한다. 저장 방식과 영속성·장애 시 유실 가능성을 함께 확인한다.

MongoDB: 유연한 스키마의 문서 데이터, 게임 기록 및 로그 데이터 등의 활용 사례를 살펴본다. 문서 모델, 인덱스, 조회 형태와 일관성 요구사항을 함께 검토한다.

## 5.4 NewSQL / Distributed SQL

TiDB, CockroachDB, YugabyteDB 등 분산 SQL 계열 시스템을 소개하고, 전통적인 RDBMS와 비교한다. 수평 확장, 분산 트랜잭션, 일관성, 지연 및 운영 복잡성을 중심으로 학습하며 교재의 최종 프로젝트에서 반드시 채택해야 하는 기술로 규정하지 않는다.

## 5.5 게임 서버 적용

로그인 및 계정 정보는 영속 DB에 저장하고, Room의 실시간 게임 상태는 Game Server 메모리에서 권한 있게 처리한다. 게임 결과를 저장할 때 데이터베이스 트랜잭션과 중복 처리 방지를 고려한다. Redis는 필요한 공유·캐시 용도로 제한하여 원본 데이터와 역할을 구분한다.

## 5.6 분산 환경의 데이터 일관성

서버가 여러 프로세스로 분리되면 같은 데이터를 서로 다른 시점에 읽거나 갱신할 수 있다. 영속 원본 데이터의 위치, 캐시 갱신과 무효화, 트랜잭션, 중복 요청, 멱등성, 읽기·쓰기 지연과 최종 일관성의 의미를 게임 결과 저장 사례를 통해 비교한다.

실습에서는 RDBMS를 계정과 결과 데이터의 원본 저장소로 사용하고, Redis는 로비·Room 목록 등의 공유 정보 또는 캐시에 선택적으로 적용한다. NewSQL은 기존 방식과 비교하는 소개 범위로 유지한다.

## 5.7 단계별 실습

### 5.7.1 DB 단계별 실습

**[검증 방식]** 5장도 같은 방식으로 검증한다. DB 기능을 호출하는 명령(/register, /login, /bonus, /score, /rank)을 가진 작은 콘솔 프로그램(DbShell)으로 먼저 시험하고, 검증된 DB 계층을 6장의 서버에 연결한다. 6장에서는 Unity 채팅 GUI의 버튼(로그인, 보너스 받기)이 같은 기능을 호출한다. 서버 로직이 바뀌면 SQL을 직접 실행하기 전에 이 명령과 버튼으로 먼저 확인한다. DB는 MySQL(또는 MariaDB), 캐시는 Redis이며 C++에서는 MySQL C API(libmysqlclient), OpenSSL, hiredis를 사용한다. 비밀번호는 평문으로 저장하지 않는다.

실습 1 — 스키마와 SQL 검증: 계정, 보너스 기록, 게임 결과 테이블을 만들고 SQL로 먼저 트랜잭션과 중복 방지를 확인한다. 기본키와 UNIQUE 제약은 중복 요청을 서버 코드가 아니라 DB가 막아 주는 장치이다.

```sql
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
```

**[확인할 것]** 계정 한 줄을 넣고 같은 INSERT를 두 번 실행하여 두 번째가 오류 1062(중복 키)로 거부되는지 확인한다. UPDATE 전에 ROLLBACK을 실행하면 bonus_log와 gold가 모두 원래대로인지 확인한다. 두 개의 mysql 창에서 같은 트랜잭션을 동시에 실행하여 한쪽만 성공하는지 확인한다.

**[흔한 오류]** 트랜잭션을 지원하지 않는 저장 엔진을 쓰는 경우, utf8(3바이트)로 만들어 이모지 이름이 깨지는 경우, 중복 방지를 서버 코드의 조회 후 삽입으로만 처리하여 동시 요청에서 뚫리는 경우.

실습 2 — C++ DB 계층과 DbShell: 가입, 로그인, 보너스 지급을 함수로 만들고 콘솔 명령으로 시험한다. 비밀번호는 계정별 salt와 PBKDF2로 해시하여 저장한다. 사용자 입력이 들어가는 문자열은 mysql_real_escape_string으로 처리한다(실서비스에서는 준비된 구문(prepared statement)을 사용한다). 보너스 지급은 실습 1의 트랜잭션을 그대로 따르며, 중복 키 오류(1062)를 이미 받은 보너스로 구분한다.

```cpp
// Db.h — MySQL C API. 모든 호출은 블로킹이며, 한 객체는 한 스레드에서만 쓴다.
#pragma once
#include <mysql.h>
#include <openssl/evp.h>
#include <openssl/rand.h>
#include <string>
#include <vector>

class Db
{
public:
    bool Open(const char* host, const char* user, const char* pw, const char* db)
    {
        m_ = mysql_init(nullptr);
        if (!mysql_real_connect(m_, host, user, pw, db, 3306, nullptr, 0)) return false;
        mysql_set_character_set(m_, "utf8mb4");
        return true;                                            // 연결이 끊기면 다시 Open한다
    }
    bool Register(const std::string& login, const std::string& pw)
    {
        unsigned char salt[16], hash[32];
        RAND_bytes(salt, sizeof salt);
        Hash(pw, salt, hash);
        std::string q = "INSERT INTO account (login, pw_hash, salt) VALUES ('" + Esc(login) + "','" + Hex(hash, 32) + "','" + Hex(salt, 16) + "')";
        return mysql_query(m_, q.c_str()) == 0;                 // 이미 있는 login이면 실패한다(UNIQUE)
    }
    bool Login(const std::string& login, const std::string& pw)
    {
        auto row = One("SELECT pw_hash, salt FROM account WHERE login='" + Esc(login) + "'", 2);
        if (row.empty()) return false;
        unsigned char salt[16], hash[32];
        FromHex(row[1], salt, 16);                              // 저장된 salt로 다시 계산해 비교한다
        Hash(pw, salt, hash);
        return row[0] == Hex(hash, 32);
    }
    std::string ClaimBonus(const std::string& login, const std::string& key)
    {
        const int amount = 100;
        auto row = One("SELECT id FROM account WHERE login='" + Esc(login) + "'", 1);
        if (row.empty()) return "error";
        const std::string id = row[0];
        mysql_query(m_, "START TRANSACTION");
        std::string q1 = "INSERT INTO bonus_log (account_id, bonus_key, amount) VALUES (" + id + ",'" + Esc(key) + "'," + std::to_string(amount) + ")";
        if (mysql_query(m_, q1.c_str()) != 0)
        {
            unsigned err = mysql_errno(m_);
            mysql_query(m_, "ROLLBACK");
            return err == 1062 ? "already" : "error";            // 1062: 중복 키 = 이미 받은 보너스
        }
        std::string q2 = "UPDATE account SET gold = gold + " + std::to_string(amount) + " WHERE id=" + id;
        if (mysql_query(m_, q2.c_str()) != 0) { mysql_query(m_, "ROLLBACK"); return "error"; }
        mysql_query(m_, "COMMIT");
        return "ok:" + std::to_string(amount);
    }
    bool SaveResult(const std::string& resultId, const std::string& room, const std::string& login, int score)
    {
        std::string q = "INSERT IGNORE INTO game_result (result_id, room, account_id, score) SELECT '" + Esc(resultId) + "','" + Esc(room)
                      + "', id, " + std::to_string(score) + " FROM account WHERE login='" + Esc(login) + "'";
        return mysql_query(m_, q.c_str()) == 0 && mysql_affected_rows(m_) == 1;   // 이미 저장된 결과면 false
    }
    void Slow() { mysql_query(m_, "SELECT SLEEP(3)"); }          // 실습 5의 시험용

private:
    MYSQL* m_ = nullptr;
    std::string Esc(const std::string& s)
    {
        std::string o(s.size() * 2 + 1, '\0');
        unsigned long n = mysql_real_escape_string(m_, &o[0], s.c_str(), (unsigned long)s.size());
        o.resize(n);
        return o;
    }
    std::vector<std::string> One(const std::string& sql, int cols)    // 한 행만 읽는다. 없으면 빈 벡터
    {
        std::vector<std::string> r;
        if (mysql_query(m_, sql.c_str()) != 0) return r;
        MYSQL_RES* res = mysql_store_result(m_);
        if (!res) return r;
        if (MYSQL_ROW row = mysql_fetch_row(res))
            for (int i = 0; i < cols; ++i) r.push_back(row[i] ? row[i] : "");
        mysql_free_result(res);
        return r;
    }
    static void Hash(const std::string& pw, const unsigned char* salt, unsigned char* out)
    {
        PKCS5_PBKDF2_HMAC(pw.c_str(), (int)pw.size(), salt, 16, 100000, EVP_sha256(), 32, out);
    }
    static std::string Hex(const unsigned char* d, int n)
    {
        static const char* t = "0123456789abcdef";
        std::string s;
        for (int i = 0; i < n; ++i) { s += t[d[i] >> 4]; s += t[d[i] & 15]; }
        return s;
    }
    static void FromHex(const std::string& s, unsigned char* out, int n)
    {
        for (int i = 0; i < n; ++i) out[i] = (unsigned char)std::stoi(s.substr(i * 2, 2), nullptr, 16);
    }
};

// DbShell.cpp — 콘솔 명령이 GUI의 버튼 역할을 한다
#include <iostream>
#include "Db.h"
int main()
{
    Db db;
    if (!db.Open("127.0.0.1", "game", "gamepw", "gamedb")) { std::cout << "DB 연결 실패\n"; return 1; }
    std::string cmd, a, b, c;
    int n;
    while (std::cin >> cmd)
    {
        if      (cmd == "/register") { std::cin >> a >> b; std::cout << (db.Register(a, b) ? "ok" : "fail") << "\n"; }
        else if (cmd == "/login")    { std::cin >> a >> b; std::cout << (db.Login(a, b) ? "ok" : "fail") << "\n"; }
        else if (cmd == "/bonus")    { std::cin >> a;      std::cout << db.ClaimBonus(a, "welcome") << "\n"; }
        else if (cmd == "/result")   { std::cin >> a >> b >> c >> n; std::cout << (db.SaveResult(a, b, c, n) ? "saved" : "duplicate") << "\n"; }   // 결과ID 방 계정 점수
        else if (cmd == "/quit") break;
    }
}
```

**[확인할 것]** /register 철수 pw1 → ok, 같은 이름으로 다시 → fail. /login 철수 pw1 → ok, 틀린 비밀번호 → fail. mysql에서 account를 조회하여 pw_hash에 평문이 없는지 확인한다. /bonus 철수를 두 번 실행하여 ok:100, already 순서로 나오고 gold가 한 번만 늘었는지 확인한다. 따옴표가 섞인 이름으로 로그인을 시도해도(예: x'or'1'='1) 로그인이 성공하지 않는지 확인한다.

**[흔한 오류]** 비밀번호를 평문 또는 salt 없는 해시로 저장하는 경우, 입력 문자열을 escape하지 않고 SQL에 이어 붙이는 경우(SQL 주입), 오류 번호를 확인하지 않고 모든 실패를 중복으로 처리하는 경우, 트랜잭션 없이 INSERT와 UPDATE를 따로 실행하는 경우.

실습 3 — 결과 저장의 중복 방지: 게임 결과를 저장하는 요청은 네트워크 재전송이나 서버 재시도로 두 번 도착할 수 있다. 서버가 결과마다 식별자(UUID)를 만들어 함께 저장하고, 같은 식별자는 한 번만 저장되게 한다. 이 요청 식별자 방식이 멱등성(같은 요청을 여러 번 처리해도 결과가 같음)을 확보하는 기본 방법이다.

**[확인할 것]** /result 8f3a-0001 room1 철수 50 → saved, 같은 명령을 다시 → duplicate. 다른 결과ID로 다시 저장하면 saved가 되며, game_result에 한 번만 기록되었는지 SELECT로 확인한다. 두 개의 DbShell에서 같은 명령을 동시에 실행해도 한쪽만 saved인지 확인한다.

**[흔한 오류]** 결과ID를 클라이언트가 임의로 정하게 하는 경우(서버가 생성하여 사용), 점수 누적(랭킹 반영) 같은 후속 작업을 저장 성공 여부와 관계없이 실행하는 경우(saved일 때만 실행한다).

실습 4 — Redis 캐시와 랭킹: 랭킹과 로그인 세션 같은 빠른 조회 데이터를 Redis에 둔다. 랭킹은 Sorted Set(ZINCRBY, ZREVRANGE), 세션은 만료(EX)를 가진 키로 저장한다. Redis는 캐시이고 원본은 RDBMS이므로, Redis의 데이터가 사라져도 game_result에서 다시 계산할 수 있어야 한다.

```cpp
// Cache.h — hiredis
#pragma once
#include <hiredis/hiredis.h>
#include <cstdarg>
#include <string>
#include <vector>

class Cache
{
public:
    bool Open(const char* host, int port) { c_ = redisConnect(host, port); return c_ && !c_->err; }
    void AddScore(const std::string& login, int score) { Run("ZINCRBY rank %d %s", score, login.c_str()); }
    std::vector<std::string> Top(int n)
    {
        std::vector<std::string> r;
        redisReply* rep = (redisReply*)redisCommand(c_, "ZREVRANGE rank 0 %d WITHSCORES", n - 1);
        if (rep && rep->type == REDIS_REPLY_ARRAY)
            for (size_t i = 0; i + 1 < rep->elements; i += 2)
                r.push_back(std::string(rep->element[i]->str) + " " + rep->element[i + 1]->str);
        if (rep) freeReplyObject(rep);
        return r;
    }
    void SetSession(const std::string& login, const std::string& token) { Run("SET session:%s %s EX 3600", login.c_str(), token.c_str()); }
private:
    redisContext* c_ = nullptr;
    void Run(const char* fmt, ...)
    {
        va_list ap; va_start(ap, fmt);
        redisReply* r = (redisReply*)redisvCommand(c_, fmt, ap);
        va_end(ap);
        if (r) freeReplyObject(r);
    }
};

// DbShell에 명령을 추가한다
//   /score 철수 50  →  if (db.SaveResult(newUuid, "room1", "철수", 50)) cache.AddScore("철수", 50);
//   /rank           →  for (auto& line : cache.Top(10)) std::cout << line << "\n";
// Redis가 비었을 때 원본에서 다시 만든다
//   SELECT a.login, SUM(r.score) FROM game_result r JOIN account a ON a.id = r.account_id GROUP BY a.login ORDER BY 2 DESC LIMIT 10;
```

**[확인할 것]** 서로 다른 계정에 점수를 넣고 /rank의 순서가 점수순인지, 같은 결과ID를 다시 저장하려 할 때 랭킹 점수가 늘지 않는지 확인한다. redis-cli에서 TTL session:철수로 만료가 설정되었는지 확인하고, FLUSHALL로 Redis를 비운 뒤 위 SELECT 결과로 랭킹을 복구해 본다.

**[흔한 오류]** 랭킹 갱신을 저장 성공 여부와 무관하게 실행하여 중복 점수가 쌓이는 경우, Redis 데이터를 유일한 원본으로 두는 경우, 만료 없이 세션 키가 계속 쌓이는 경우, 명령 실행 후 redisReply를 해제하지 않는 경우.

실습 5 — DB Worker 스레드: DB 호출은 블로킹이므로 네트워크 처리 스레드에서 직접 호출하면 DB가 느릴 때 모든 접속자의 채팅이 멈춘다. DB 호출 전용 스레드를 두고 요청을 큐로 전달하며, DB 연결은 이 스레드만 사용한다(한 MYSQL 연결을 여러 스레드가 동시에 쓰지 않는다). 6장 실습 5에서는 결과를 논리 스레드로 되돌리는 방식으로 이 클래스를 사용한다.

```cpp
// DbWorker.h
#pragma once
#include <condition_variable>
#include <deque>
#include <functional>
#include <mutex>
#include <thread>

class DbWorker
{
public:
    DbWorker() : th_([this] { Loop(); }) {}
    ~DbWorker()
    {
        { std::lock_guard<std::mutex> g(m_); stop_ = true; }
        cv_.notify_one();
        th_.join();
    }
    void Post(std::function<void()> job)
    {
        { std::lock_guard<std::mutex> g(m_); q_.push_back(std::move(job)); }
        cv_.notify_one();
    }
private:
    void Loop()
    {
        for (;;)
        {
            std::function<void()> job;
            {
                std::unique_lock<std::mutex> lk(m_);
                cv_.wait(lk, [this] { return stop_ || !q_.empty(); });
                if (q_.empty()) return;                 // 종료 요청이고 남은 작업이 없다
                job = std::move(q_.front());
                q_.pop_front();
            }
            job();                                      // 잠금 밖에서 실행한다
        }
    }
    std::mutex m_;
    std::condition_variable cv_;
    std::deque<std::function<void()>> q_;
    bool stop_ = false;
    std::thread th_;                                    // 마지막에 선언하여 위의 멤버가 먼저 초기화되게 한다
};

// DbShell에 추가: /slow 는 DB 스레드에서 3초 걸리고, /ping 은 즉시 응답해야 한다
DbWorker worker;
//   if (cmd == "/slow") worker.Post([&] { db.Slow(); std::cout << "slow done\n"; });
//   if (cmd == "/ping") std::cout << "pong\n";
```

**[확인할 것]** /slow 직후 /ping을 입력했을 때 3초를 기다리지 않고 pong이 바로 나오는지 확인한다(DB가 막혀도 입력을 받는 스레드는 막히지 않는다). /slow를 여러 번 입력하면 작업이 큐에 쌓여 순서대로 처리되는 것을 확인하고, 큐 길이에 상한을 두지 않았을 때 어떤 문제가 생기는지 설명한다.

**[흔한 오류]** 여러 스레드가 같은 MYSQL 연결을 쓰는 경우, 작업 람다가 지역 변수를 참조로 캡처한 채 지역 변수가 먼저 사라지는 경우, DB 결과를 DB 스레드에서 바로 게임 자료구조에 적용하는 경우(결과는 논리 스레드로 되돌려서 적용한다).

## 5.8 게임 데이터의 모델링

데이터베이스 설계는 "무엇을 저장하는가"보다 "어떻게 읽고 쓰는가"에서 출발한다. 게임 데이터는 성격이 서로 달라서 한 가지 저장소로 모두 처리하기 어렵다.

### 5.8.1 게임 데이터의 분류

- 계정과 인증: 아이디, 비밀번호 해시, 가입일. 정확성이 최우선이고 쓰기는 드물다. RDBMS가 적합하다.
- 캐릭터와 인벤토리: 레벨, 재화, 아이템. 로그인 시 읽고 플레이 중 가끔 쓴다. 재화와 아이템은 정확성이 중요하다. RDBMS 또는 문서형 DB.
- 실시간 상태: 접속 세션, 방 목록, 현재 위치. 자주 바뀌고 잃어도 복구가 가능하다. 메모리(Redis 등)에 둔다.
- 랭킹: 점수 갱신이 잦고 상위 N명 조회가 많다. Redis의 정렬 집합이 적합하다.
- 로그와 통계: 쓰기가 매우 많고 읽기는 분석할 때뿐이다. 추가 전용 저장소나 로그 시스템에 쌓는다.

### 5.8.2 테이블 설계

관계형 설계의 기본은 한 사실을 한 곳에 두는 정규화이다. 정규화하면 갱신 이상이 줄어들지만 조회 시 JOIN이 늘어난다. 읽기 성능이 중요한 곳은 일부러 비정규화하되, 어느 쪽이 원본인지 명확히 한다.

```sql
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
```

- 기본 키는 짧고 변하지 않는 값으로 정한다. 복합 기본 키(account_id, item_id)는 "한 계정의 아이템 전체"를 읽는 조회에 유리하다.
- 이름처럼 바뀔 수 있는 값을 기본 키로 쓰지 않는다.
- 문자열 길이, 정수 크기, NOT NULL 같은 제약을 스키마에 둔다. 잘못된 데이터가 들어오지 못하게 하는 마지막 방어선이다.

### 5.8.3 인덱스

인덱스는 책의 색인과 같다. 인덱스가 없으면 조건에 맞는 행을 찾으려고 테이블 전체를 훑는다(풀 스캔). 대부분의 RDBMS는 B-Tree 인덱스를 쓰며 값을 정렬된 상태로 유지해 조회 시간이 데이터 양의 로그에 비례한다.

- 조회 조건과 정렬에 쓰이는 열에 만든다. 대신 쓰기 때마다 인덱스도 갱신되므로 불필요한 인덱스는 쓰기를 느리게 하고 저장 공간을 쓴다.
- 복합 인덱스는 열 순서가 중요하다. (a, b) 인덱스는 a로 찾는 조회에는 쓰이지만 b만으로 찾는 조회에는 쓰이지 못한다.
- 조건 열에 함수를 씌우면(예: DATE(created_at)=...) 인덱스를 못 쓰는 경우가 많다.
- 실행 계획(EXPLAIN)으로 인덱스를 쓰는지 확인한다. 느린 쿼리는 추측하지 않고 실행 계획과 측정으로 원인을 찾는다.

자주 하는 실수는 SELECT *로 필요 없는 열까지 읽는 것, 반복문 안에서 쿼리를 한 건씩 실행하는 것(N+1 문제), 인덱스 없는 열로 조회하는 것, 트랜잭션을 길게 열어 두는 것이다.

### 5.8.4 ERD, 정규화, 반정규화

테이블을 하나씩 만들기 전에 데이터 사이의 관계를 그림으로 먼저 그린다. 이 그림이 ERD(Entity-Relationship Diagram, 개체-관계 다이어그램)이다. 개체는 테이블, 속성은 열, 관계는 개체 사이의 연결이다. 관계는 한쪽의 한 행이 다른 쪽의 몇 행과 연결되는지로 나눈다. 1:1, 1:N(한 계정에 캐릭터 여러 개), N:M(학생과 과목처럼 양쪽이 모두 여러 개)이 있고, N:M 관계는 가운데에 연결 테이블을 두어 1:N 두 개로 푼다.

```text
account (1) ----- (N) player (N) ----- (1) guild
   |
   +----- (N) inventory (N) ----- (1) item_template
   |
   +----- (1) wallet
```

위 그림은 게임 데이터의 한 부분을 단순화한 예이다. 계정 하나에 캐릭터(player)가 여러 개 있을 수 있고, 캐릭터는 길드 하나에 속한다. 인벤토리는 계정과 아이템 템플릿(아이템의 종류와 능력치를 정의한 테이블) 사이의 N:M 관계를 푼 연결 테이블이고, 수량(amount)처럼 관계 자체에 붙는 속성을 가진다.

정규화는 한 사실을 한 곳에만 두도록 테이블을 나누는 과정이다. 같은 사실이 여러 곳에 있으면 한 곳만 고쳤을 때 값이 어긋나는 갱신 이상이 생긴다. 게임 데이터로 세 단계를 본다.

- 제1정규형: 한 열에는 값 하나만 둔다. player 테이블의 items 열에 "1001,1002,1003"처럼 여러 아이템을 쉼표로 넣으면 특정 아이템을 가진 사람을 찾을 수 없고 수량도 표현할 수 없다. 별도의 inventory 테이블로 나눈다.
- 제2정규형: 복합 기본 키의 일부에만 종속된 열을 분리한다. inventory(account_id, item_id, item_name, amount)에서 item_name은 item_id에만 종속되므로 item_template으로 옮긴다. 그렇지 않으면 아이템 이름을 바꿀 때 모든 계정의 행을 고쳐야 한다.
- 제3정규형: 기본 키가 아닌 열에 종속된 열을 분리한다. player(player_id, name, job_id, job_name)에서 job_name은 job_id로 정해지므로 job 테이블로 옮긴다.

반정규화는 성능을 위해 일부러 중복을 허용하는 것이다. 길드의 인원수를 매번 player 테이블에서 세지 않고 guild 테이블의 member_count 열에 저장해 두는 것이 예이다. 읽기는 빨라지지만 일관성을 지킬 책임이 코드로 넘어온다. 가입과 탈퇴 때 한 트랜잭션 안에서 member_count를 함께 고치고, 어긋날 수 있으니 주기적으로 다시 계산해 바로잡는다(5.16절의 배치 작업). 순서는 항상 정규화로 설계하고, 측정으로 병목을 확인한 뒤, 필요한 곳만 반정규화하는 것이다. 어느 쪽이 원본인지 명확히 해 두어야 어긋났을 때 무엇을 믿고 고칠지 알 수 있다.

## 5.9 트랜잭션과 동시성 제어

### 5.9.1 ACID

트랜잭션은 여러 작업을 하나로 묶는 단위이다. 원자성(Atomicity)은 전부 성공하거나 전부 취소되는 것, 일관성(Consistency)은 제약을 항상 만족하는 것, 격리성(Isolation)은 동시에 실행되는 트랜잭션이 서로 간섭하지 않는 것, 지속성(Durability)은 커밋된 결과가 장애 후에도 남는 것이다.

게임에서 아이템 거래는 두 계정의 재화 이동과 아이템 이동이 한꺼번에 일어나야 하므로 대표적인 트랜잭션 대상이다. 서버가 중간에 죽어도 돈만 빠지고 아이템이 안 들어오는 일이 없어야 한다.

```sql
START TRANSACTION;
UPDATE wallet SET gold = gold - 500 WHERE account_id = 1 AND gold >= 500;
-- 영향받은 행이 0이면 잔액 부족: ROLLBACK 후 실패 응답
UPDATE wallet SET gold = gold + 500 WHERE account_id = 2;
INSERT INTO trade_log(from_id, to_id, gold) VALUES (1, 2, 500);
COMMIT;
```

조건(gold >= 500)을 UPDATE 문 안에 넣으면 조회와 갱신 사이에 다른 요청이 끼어드는 문제를 없애 준다. 먼저 SELECT로 잔액을 읽고 서버 코드에서 비교한 뒤 UPDATE하는 방식은 두 요청이 동시에 오면 잔액이 음수가 될 수 있다.

### 5.9.2 격리 수준과 이상 현상

격리 수준이 낮을수록 빠르지만 아래의 이상 현상이 생길 수 있다.

- Dirty Read: 커밋되지 않은 다른 트랜잭션의 값을 읽는다.
- Non-repeatable Read: 같은 행을 두 번 읽었는데 그 사이 다른 트랜잭션이 커밋해 값이 다르다.
- Phantom Read: 같은 조건으로 두 번 조회했는데 행의 개수가 다르다.

표준 격리 수준은 READ UNCOMMITTED, READ COMMITTED, REPEATABLE READ, SERIALIZABLE이며 아래로 갈수록 강하다. MySQL InnoDB의 기본은 REPEATABLE READ이다. 격리 수준의 세부 동작은 제품마다 다르므로 사용하는 DB의 문서를 확인한다.

### 5.9.3 락, 데드락, 낙관적 동시성

- 행 잠금: SELECT ... FOR UPDATE는 읽은 행을 커밋할 때까지 다른 트랜잭션이 바꾸지 못하게 잠근다. 잠금 범위와 시간은 짧을수록 좋다.
- 데드락: 두 트랜잭션이 서로 상대가 가진 잠금을 기다리는 상태이다. DB가 한쪽을 강제로 취소하므로 코드는 해당 오류(MySQL 오류 1213)를 받으면 트랜잭션을 처음부터 다시 시도한다. 여러 행을 갱신할 때 항상 같은 순서로(예: 계정 번호 오름차순) 잠그면 데드락이 줄어든다.
- 낙관적 동시성: 행에 version 열을 두고 UPDATE ... WHERE id=? AND version=?로 갱신한다. 영향받은 행이 0이면 다른 요청이 먼저 바꾼 것이므로 다시 읽어서 재시도한다. 충돌이 드문 데이터에 알맞다.

### 5.9.4 멱등성과 중복 처리

네트워크에서는 같은 요청이 두 번 도착할 수 있다(재시도, 재전송). 아이템 지급이 두 번 처리되면 안 되므로 요청마다 고유한 ID(요청 ID, 영수증 번호)를 만들고, 그 ID를 기본 키나 유일 키로 가진 테이블에 먼저 기록한다. 이미 있으면 중복 키 오류(MySQL 1062)가 나므로 지급하지 않고 이전 결과를 돌려준다. 5.7절 실습의 구조이다.

## 5.10 캐시와 Redis

### 5.10.1 왜 캐시가 필요한가

디스크 기반 DB는 한 번의 질의에 수 ms가 걸리지만 메모리 저장소는 수십~수백 µs 수준이다. 접속자가 많아지면 같은 데이터를 반복해서 읽는 요청이 DB를 압도한다. 자주 읽고 덜 바뀌는 데이터를 메모리에 두면 DB 부하가 크게 줄어든다.

### 5.10.2 캐시 패턴

- Cache-Aside: 읽을 때 캐시를 먼저 보고, 없으면 DB에서 읽어 캐시에 채운다. 쓸 때는 DB를 갱신하고 캐시 항목을 지운다. 가장 흔하고 이해하기 쉽다.
- Write-Through: 쓸 때 캐시와 DB를 함께 갱신한다. 읽기는 항상 최신이지만 쓰기가 느리다.
- Write-Behind: 캐시에만 먼저 쓰고 DB에는 나중에 모아서 쓴다. 쓰기가 빠르지만 캐시가 죽으면 최근 변경이 사라질 수 있다.

캐시의 가장 큰 어려움은 일관성이다. DB는 바뀌었는데 캐시가 낡은 값을 가지고 있을 수 있다. TTL(만료 시간)을 두어 오래된 값이 자연스럽게 사라지게 하고, 갱신할 때 캐시를 지우는 규칙을 일관되게 적용한다. 인기 항목이 한꺼번에 만료되어 DB에 요청이 몰리는 현상(캐시 스탬피드)은 만료 시각에 무작위 편차를 주어 완화한다.

### 5.10.3 Redis의 자료구조와 용도

- String: 세션 토큰, 카운터. SET key value EX 초 로 만료 시간을 함께 지정한다.
- Hash: 객체의 필드 모음. 플레이어 한 명의 임시 상태.
- Sorted Set: 점수로 정렬되는 집합. ZINCRBY로 점수를 올리고 ZREVRANGE로 상위 N명을 읽는다. 랭킹의 표준 구현이다.
- List/Stream: 큐. 작업 대기열이나 이벤트 기록.
- Pub/Sub: 가벼운 알림 전달. 구독자가 없으면 메시지가 사라지므로 중요한 이벤트에는 쓰지 않는다.

Redis는 기본적으로 단일 스레드로 명령을 하나씩 처리하므로 각 명령은 원자적이다. 여러 명령을 묶어 원자적으로 실행하려면 트랜잭션(MULTI/EXEC)이나 스크립트를 쓴다. 영속성은 스냅샷(RDB)과 쓰기 기록(AOF)으로 선택하며, 캐시로만 쓰면 잃어도 되지만 유일한 원본으로 쓰면 안 된다.

Redis로 분산 잠금을 만들 때는 SET key token NX PX 시간으로 잠금을 얻고, 해제 시 자신의 token인지 확인한다. 잠금 시간이 지나 다른 프로세스가 같은 잠금을 얻을 수 있으므로 중요한 정합성은 잠금에 의존하지 말고 DB의 제약과 트랜잭션으로 보장한다.

## 5.11 NoSQL과 선택 기준

NoSQL은 "관계형이 아닌" 저장소의 통칭이며 종류가 다양하다.

- 키-값(Redis, DynamoDB): 키로 값을 읽고 쓴다. 가장 빠르고 단순하며 복잡한 조건 검색이 어렵다.
- 문서형(MongoDB): JSON과 비슷한 문서를 저장한다. 스키마가 유연해서 캐릭터처럼 필드가 자주 바뀌는 데이터에 편리하다. 하나의 문서 안에서는 원자적이지만 여러 문서에 걸친 트랜잭션은 제한적이거나 비용이 크다.
- 와이드 컬럼(Cassandra): 쓰기가 많고 수평 확장이 필요한 데이터에 맞다. 질의 방식을 미리 정해 테이블을 설계한다.
- 그래프: 친구 관계처럼 관계 탐색이 중심인 데이터.

선택 질문: (1) 정확성이 얼마나 중요한가? 돈과 아이템은 트랜잭션이 필요하다. (2) 읽기와 쓰기 비율은? (3) 조회 방식이 정해져 있는가, 임의 조건 검색이 필요한가? (4) 데이터 크기와 증가 속도는? (5) 팀이 운영할 수 있는 기술인가? 새로운 기술이 좋아 보여도 팀이 운영할 수 없으면 장애 때 복구하지 못한다. 처음에는 RDBMS 하나와 Redis로 시작하고 필요가 확인된 곳만 다른 저장소를 추가하는 것이 안전하다.

## 5.12 확장성과 분산 환경의 일관성

### 5.12.1 복제와 읽기 분리

복제(replication)는 기본 DB(primary)의 변경을 복사본(replica)에 전달해 같은 데이터를 여러 곳에 두는 방식이다. 읽기를 복사본으로 분산하고, 기본 DB가 죽으면 복사본이 이어받아 가용성을 높인다. 복제에는 지연이 있어서 방금 쓴 값이 복사본에서는 아직 안 보일 수 있다. 쓴 직후에 읽어야 하는 경우(결제 직후 잔액 조회 등)는 기본 DB에서 읽는다.

### 5.12.2 샤딩

데이터가 한 대의 DB에 들어가지 않거나 쓰기가 한 대의 한계를 넘으면 데이터를 여러 DB에 나누어 저장한다(샤딩).

- 범위 기반: 계정 번호 1~100만은 샤드 1, 그 다음은 샤드 2. 구현이 쉽지만 최근 계정이 한 샤드에 몰릴 수 있다.
- 해시 기반: 계정 번호의 해시를 샤드 수로 나눈 나머지로 정한다. 고르게 분산되지만 샤드를 늘리면 대부분의 데이터가 이동한다. 일관 해싱이나 가상 샤드(작은 조각을 많이 만들어 샤드에 배정)로 이동량을 줄인다.
- 디렉터리 기반: 어느 계정이 어느 샤드에 있는지 별도 표에 기록한다. 유연하지만 그 표가 병목이자 단일 장애 지점이 된다.

샤드 키는 대부분의 요청이 한 샤드 안에서 끝나도록 정한다. 게임에서는 계정 번호가 흔하다. 두 계정에 걸친 거래는 서로 다른 샤드를 건드리게 되어 단일 트랜잭션으로 처리할 수 없으므로 다음의 분산 일관성 문제가 생긴다.

### 5.12.3 CAP과 최종 일관성

CAP 정리는 네트워크가 나뉘는 상황(Partition)에서 일관성(Consistency)과 가용성(Availability)을 둘 다 완전히 지킬 수 없다는 이론이다. 분산 시스템은 장애 때 "일관성을 지키려고 요청을 거부"하거나 "가용성을 지키려고 낡은 값을 허용"하는 쪽을 선택한다. 최종 일관성(eventual consistency)은 갱신이 멈추면 결국 모든 복사본이 같은 값이 된다는 약한 보장이다. 랭킹이나 친구의 접속 상태에는 충분하지만, 재화와 아이템에는 부족하다.

### 5.12.4 분산 트랜잭션: 2PC와 Saga

- 2단계 커밋(2PC): 모든 참여자에게 준비 가능 여부를 묻고(1단계), 모두 가능하면 커밋을 지시한다(2단계). 일관성은 강하지만 조정자가 죽으면 참여자가 잠금을 쥔 채 기다리고, 느리다.
- Saga: 긴 작업을 여러 로컬 트랜잭션으로 쪼개고, 실패하면 앞 단계를 되돌리는 보상 작업을 실행한다. 예를 들어 A의 재화 차감, B의 아이템 지급 순서로 진행하다 두 번째가 실패하면 A의 재화를 되돌려 준다. 중간 상태가 외부에 보일 수 있으므로 "처리 중" 상태를 기록하고 각 단계를 멱등하게 만든다.

이 교재는 Saga의 아이디어(상태 기록, 멱등한 단계, 보상)를 설명 수준으로 다루고, 실습은 단일 DB의 트랜잭션과 멱등 키까지만 구현한다.

## 5.13 백업, 마이그레이션, 보안

- 백업: 정기 전체 백업과 변경분(증분) 백업, 그리고 시점 복구(Point-in-Time Recovery)를 위한 바이너리 로그 보관. 백업은 복구해 보아야만 백업이다. 복구 시험을 정기적으로 한다.
- 스키마 변경: 운영 중인 큰 테이블에 열을 추가하면 오래 잠길 수 있다. 서비스를 멈추지 않는 방법(새 열 추가 후 점진 채움, 온라인 DDL 도구)을 미리 검토하고, 코드는 이전/새 스키마를 모두 견디도록 먼저 배포한다.
- SQL 인젝션 방지: 사용자 입력을 문자열로 이어 붙여 SQL을 만들지 않고 준비된 문장(prepared statement)에 매개변수로 전달한다. 5.7절의 이스케이프 방법은 학습용 최소 방어이며 실서비스는 준비된 문장을 쓴다.
- 최소 권한: 게임 서버용 DB 계정에는 필요한 테이블에 필요한 권한만 준다. 계정 정보는 소스 코드가 아니라 설정 파일이나 환경 변수에 두고 저장소에 올리지 않는다.
- 개인정보: 수집 항목을 최소화하고, 비밀번호는 솔트와 해시로 저장한다. 관련 법규는 서비스 지역에 따라 다르므로 서비스 전에 확인한다.

## 5.14 서버와 DB 연동 설계

### 5.14.1 언제 읽고 언제 저장하는가

- 로그인 시 읽기: DB에서 캐릭터를 읽어 서버 메모리에 올린다. 이후 플레이 중에는 메모리의 값을 쓴다.
- 저장 시점: 중요 이벤트(결제, 거래, 레벨 업)는 즉시, 일반 상태는 주기적(예: 수 분마다)으로, 로그아웃과 서버 종료 때 한 번 더 저장한다. 저장 주기가 길수록 DB 부하는 작지만 서버가 갑자기 죽을 때 잃는 양이 커진다. 잃어도 되는 범위를 먼저 정하고 주기를 정한다.
- 한 사람의 데이터를 두 서버가 동시에 들고 있지 않게 한다. 로그인 시 이전 접속을 끊고(중복 로그인 처리) 세션 소유 서버를 기록한다.

### 5.14.2 DB 작업 스레드와 연결 풀

1장에서 본 것처럼 DB 호출은 게임 루프에서 직접 하지 않는다. DB Worker 스레드(들)가 작업 큐에서 요청을 꺼내 실행하고 결과를 로직 스레드로 돌려보낸다. DB 연결은 만드는 비용이 크므로 연결을 미리 몇 개 만들어 두고 재사용하는 연결 풀을 쓰며, 연결 하나는 한 번에 한 스레드만 쓴다. 풀 크기는 DB가 감당할 동시 질의 수에 맞추고, 요청이 풀보다 많이 쌓이면 대기열 길이를 지표로 기록해 경보를 건다.

```text
[Logic thread] -- 요청(계정, 질의) --> [Job Queue] --> [DB Worker 1..N] --> MySQL
[Logic thread] <-- 결과 큐 -------------------------------+
```

오류 처리는 일시 오류(연결 끊김, 데드락)와 영구 오류(제약 위반)를 구분한다. 일시 오류는 제한된 횟수로 재시도하고, 영구 오류는 사용자에게 알리고 기록한다. 재시도 가능한 요청은 5.9.4절의 멱등 키로 중복을 막아야 한다.

## 5.15 SQL 응용과 데이터베이스 내부 로직

5.7절의 실습은 한 테이블에 넣고 읽는 SQL 위주였다. 서버가 실제로 쓰는 질의는 집계, 서브쿼리, 여러 테이블의 결합이 섞인다. 이 절의 질의는 5.8절의 account, inventory와 5.9절의 wallet(account_id, gold), trade_log(from_id, to_id, gold), 그리고 아래의 guild, player 테이블을 기준으로 한다.

```sql
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
```

**[검증 환경]** 이 절을 쓰는 환경에는 MySQL이 없었다. 표준 SQL 부분(집계, 서브쿼리, 집합 연산, 트리거 본문의 동작, 프로시저 안의 UPDATE 문장과 분기)은 SQLite에서 실행해 결과를 확인했다. MySQL에만 있는 문법(프로시저 선언과 `DELIMITER`, `DELETE ... ORDER BY ... LIMIT`, 슬로우 쿼리 설정)은 MySQL에서 실행하지 못했으니 사용하는 버전에서 확인한다.

### 5.15.1 집계와 그룹

COUNT, SUM, AVG, MAX, MIN은 여러 행을 하나의 값으로 묶는다. GROUP BY는 묶는 기준을, HAVING은 묶은 결과에 거는 조건을 지정한다. WHERE는 묶기 전의 행을 거르고 HAVING은 묶은 뒤의 결과를 거른다는 차이를 구분한다.

```sql
-- 길드별 인원과 평균 레벨 (5명 이상인 길드만)
SELECT guild_id, COUNT(*) AS members, AVG(level) AS avg_level
FROM player
WHERE guild_id IS NOT NULL
GROUP BY guild_id
HAVING COUNT(*) >= 5
ORDER BY avg_level DESC;
```

집계는 데이터가 많을수록 오래 걸린다. 접속자 수 통계나 랭킹처럼 자주 필요한 집계를 실시간 요청마다 계산하지 않고, 주기적으로 계산해 저장해 두거나(5.16절) Redis에 갱신하며 유지한다(5.10절).

### 5.15.2 서브쿼리와 집합 연산

서브쿼리는 질의 안에 들어 있는 질의이다. 하나의 값을 돌려주는 서브쿼리는 비교에, 여러 값을 돌려주는 서브쿼리는 IN과 EXISTS에 쓴다.

```sql
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
```

집합 연산은 두 질의의 결과를 합치거나 비교한다. UNION은 중복을 제거하고 합치며, UNION ALL은 중복을 그대로 둔다. 중복이 없다고 알고 있거나 중복이 있어도 되면 UNION ALL이 빠르다. 중복 제거를 위해 정렬이나 해시 작업이 필요하기 때문이다.

```sql
-- 거래에 한 번이라도 참여한 계정 (중복 제거)
SELECT from_id AS account_id FROM trade_log
UNION
SELECT to_id FROM trade_log;
```

### 5.15.3 저장 프로시저, 함수, 트리거

DBMS 안에 로직을 저장해 두는 방법이 있다.

- 저장 프로시저: 이름을 붙여 DB에 저장한 일련의 SQL이다. 서버는 `CALL`로 호출한다.
- 저장 함수: 값을 돌려주며 질의 안에서 쓸 수 있다.
- 트리거: 테이블의 INSERT, UPDATE, DELETE가 일어나면 자동으로 실행된다.

5.9절의 재화 이동을 프로시저로 만들면 다음과 같다(MySQL 문법). 5.9절의 문장에 세 가지를 더했다. 금액이 0 이하이거나 보내는 계정과 받는 계정이 같은 요청을 거절하고(음수 금액을 허용하면 받는 쪽의 재화를 빼앗는 요청이 된다), 받는 계정이 없으면 취소하고(없는데 빼기만 하고 끝나면 재화가 사라진다), 두 지갑을 계정 번호가 작은 쪽부터 잠근다(5.9.3절).

```sql
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
```

`DELIMITER //`는 mysql 클라이언트의 명령으로, 프로시저 본문 안의 세미콜론이 문장의 끝으로 해석되지 않도록 구분자를 바꾼다. 결과는 `p_result`로 돌려준다.

- 0: 성공.
- 1: 잔액이 부족하거나 보내는 계정이 없다.
- 2: 잘못된 요청(금액이 0 이하이거나 같은 계정끼리의 이동).
- 3: 받는 계정이 없다.
- -1: 그 밖의 오류. 교착 상태로 취소된 경우도 여기에 온다.

서버는 -1을 받으면 짧게 기다렸다가 제한된 횟수만 다시 시도할 수 있다(5.9.3절). 다만 이 프로시저는 같은 요청이 두 번 도착하면 두 번 처리한다. 네트워크 재전송으로 생기는 중복은 5.9.4절의 요청 ID로 따로 막아야 한다.

트리거로 재화 변경 이력을 자동으로 남기는 예는 다음과 같다(MySQL 문법). 변경 이력을 담는 wallet_audit(account_id, old_gold, new_gold, changed_at) 테이블이 있다고 가정하고, changed_at의 기본값은 현재 시각이다.

```sql
DELIMITER //
CREATE TRIGGER trg_wallet_audit AFTER UPDATE ON wallet
FOR EACH ROW
BEGIN
    INSERT INTO wallet_audit(account_id, old_gold, new_gold) VALUES (OLD.account_id, OLD.gold, NEW.gold);
END //
DELIMITER ;
```

`OLD`는 바뀌기 전의 행이고 `NEW`는 바뀐 뒤의 행이다. 이 본문은 SQLite에서 `DELIMITER` 없이 `END;`로 닫아 실행했고, wallet을 고칠 때마다 wallet_audit에 한 줄이 쌓이는 것을 확인했다.

DB 안에 로직을 두는 선택에는 장단점이 있다.

- 장점: 여러 문장을 한 번의 호출로 실행해 네트워크 왕복이 줄어든다. 트랜잭션 경계가 DB 안에 있어 서버가 중간에 죽어도 일관성이 지켜진다. 애플리케이션 계정에 테이블 권한을 주지 않고 프로시저 실행 권한만 줄 수 있다.
- 단점: 로직이 서버 코드와 DB 두 곳으로 갈라져 버전 관리, 배포, 디버깅, 시험이 어려워진다. 서버는 늘릴 수 있지만 DB는 늘리기 어려운데, 로직의 CPU 부하가 DB에 몰린다. DBMS마다 문법이 달라 옮기기 어렵다. 트리거는 호출하는 코드에 보이지 않는 숨은 동작이어서 원인을 추적하기 어렵고 대량 갱신을 느리게 한다.

게임 업계에서도 팀마다 선택이 갈린다. 이 교재는 로직을 서버 코드에 두는 것을 기본으로 하고, 트랜잭션 경계가 중요한 소수의 처리와 감사 기록에만 프로시저나 트리거를 검토한다.

### 5.15.4 느린 질의를 찾아 고치는 절차

성능 문제는 추측하지 않고 측정으로 접근한다.

1. 분석: MySQL의 슬로우 쿼리 로그를 켜서 기준 시간(long_query_time)보다 오래 걸린 질의를 모은다. `SET GLOBAL slow_query_log = 'ON'; SET GLOBAL long_query_time = 0.5;`처럼 설정한다. 모은 질의 중 가장 시간을 많이 쓰는 것부터 본다.
2. 원인 확인: EXPLAIN(5.8.3절)으로 풀 스캔인지, 인덱스를 쓰는지, 읽는 행 수가 얼마인지 본다. 흔한 원인은 인덱스가 없거나 쓰이지 못하는 조건, 필요 이상으로 많은 행과 열을 읽는 것, 반복문 안의 질의(N+1 문제)이다.
3. 개선: 인덱스를 추가하거나 조건을 고치고, 반복 질의를 JOIN이나 IN으로 한 번에 처리한다.
4. 정량 평가: 개선 전후의 실행 시간과 읽은 행 수를 같은 데이터로 비교해서 기록한다. 개선했다는 느낌이 아니라 숫자로 확인한다. 인덱스를 추가하면 쓰기가 느려지므로 쓰기 성능도 함께 확인한다.

## 5.16 배치 작업과 정기 처리

사용자의 요청과 상관없이 정해진 시각이나 주기에 실행되는 작업을 배치 작업이라고 한다. 게임 서버에는 이런 작업이 많다.

- 일일 초기화: 출석, 일일 퀘스트, 무료 횟수의 재설정.
- 집계: 랭킹, 통계, 길드 인원수 같은 반정규화한 값의 재계산(5.8.4절).
- 정리: 만료된 우편, 오래된 세션, 보관 기간이 지난 로그와 개인정보의 삭제(1.11절).
- 보고: 일별 접속자와 매출 집계.

### 5.16.1 어디에서 실행하는가

- 서버 프로세스 안의 타이머: 구현이 단순하고 서버의 자료구조에 바로 접근한다. 서버가 여러 대이면 같은 작업이 중복 실행되고, 서버가 꺼져 있던 시간의 작업은 누락된다.
- 별도의 배치 프로그램을 OS 스케줄러로 실행(Windows 작업 스케줄러, Linux cron): 서버와 분리되어 서버 장애의 영향이 적고 실행 기록이 남는다. 서버 메모리의 상태를 볼 수 없어 DB를 거쳐야 하고, 실행 환경을 따로 관리해야 한다.
- DB의 이벤트 스케줄러: DB 안의 작업은 한 곳에서 관리된다. 로직이 DB 안으로 들어가는 5.15.3절의 단점이 그대로 있다.

작은 규모에서는 서버 안의 타이머로 시작하고, 서버가 여러 대로 늘거나 작업이 무거워지면 별도 배치 프로그램으로 옮기는 것이 일반적이다.

### 5.16.2 배치 작업의 설계 원칙

- 멱등성: 두 번 실행해도 결과가 같아야 한다. "오늘 날짜의 초기화를 이미 했는가"를 DB에 기록하고, 이미 했으면 건너뛴다. 서버가 작업 도중 죽어서 다시 시작해도 안전하다.
- 중복 실행 방지: 같은 작업이 동시에 두 번 돌면 안 된다. MySQL의 이름 있는 잠금(GET_LOCK)이나 Redis의 `SET 키 값 NX PX 시간`(5.10절)으로 한 곳만 실행하게 한다. GET_LOCK은 연결 단위의 잠금이므로 연결 풀을 쓸 때는 잠그고 푸는 동안 같은 연결을 써야 한다.
- 나누어 처리: 100만 행을 한 문장으로 갱신하거나 삭제하면 오래 잠기고 복제가 밀린다. 1,000건씩 나누어 처리하고 그 사이에 짧게 쉬어서 서비스 질의를 방해하지 않게 한다.
- 실패와 기록: 시작과 종료 시각, 처리 건수, 실패 원인을 로그로 남긴다. 실패하면 경보를 보내고 재시도 규칙을 정한다.
- 시간 기준: 일일 초기화 기준 시각(예: 한국 시간 오전 5시)과 시간대를 정하고 코드와 DB에서 일관되게 쓴다. 서비스 시간이 몰리는 때를 피한다.

우편 테이블 mail(mail_id, account_id, …, expire_at)이 있다고 가정한다. expire_at에 인덱스를 두고 EXPLAIN(5.8.3절)으로 쓰이는지 확인한다. 인덱스가 없으면 반복할 때마다 테이블 전체를 훑는다.

```sql
-- 만료된 우편을 1,000건씩 지운다 (MySQL). 영향받은 행이 1,000건 미만이 될 때까지 반복한다.
DELETE FROM mail WHERE expire_at < NOW() ORDER BY mail_id LIMIT 1000;
```

### 5.16.3 서버 안의 주기 작업 예

서비스 날짜가 바뀌었는지 확인하는 코드는 두 부분으로 나눈다. 날짜 계산은 서버의 시스템 시간대에 의존하지 않도록 UTC에 9시간(한국)을 더하고 5시간(초기화 기준)을 빼서 구하는 순수 함수이며, 별도 헤더에 둔다.

```cpp
// GameTime.h — 서비스 날짜 계산 (네트워크와 무관한 순수 함수)
#pragma once
#include <chrono>

inline long long GameDay(std::chrono::system_clock::time_point tp)     // 한국 시간 오전 5시를 하루의 시작으로 본다
{
    using namespace std::chrono;
    return floor<days>(tp + hours(9) - hours(5)).time_since_epoch().count();
}
```

Asio 타이머로 1분마다 날짜를 확인하는 코드는 다음과 같다.

```cpp
#include <asio.hpp>
#include <chrono>
#include "GameTime.h"
using namespace std::chrono;

void WatchDayChange(asio::steady_timer& timer, long long& lastDay)
{
    timer.expires_after(seconds(60));                   // 1분마다 확인한다
    timer.async_wait([&timer, &lastDay](std::error_code ec) {
        if (ec) return;                                 // 취소된 경우
        long long today = GameDay(system_clock::now());
        if (today != lastDay)
        {
            lastDay = today;                            // DB에도 기록한다
            RunDailyReset();                            // 한 번만 실행되어야 한다
        }
        WatchDayChange(timer, lastDay);
    });
}
```

이 코드를 시험할 때는 주기를 1초로 줄이고, 시작 2초 뒤부터 `GameDay`에 넘기는 시각에 24시간을 더해 하루가 지난 것처럼 만들었다. `RunDailyReset`은 "daily reset"을 출력하게 했고, 5초 동안 한 번만 출력되었다.

lastDay의 처음 값은 서버를 시작할 때의 날짜가 아니라 DB에 저장해 둔 "마지막으로 처리한 날짜"를 읽어서 정한다. 서버가 날짜가 바뀌는 시각에 꺼져 있었더라도 다시 시작했을 때 초기화가 한 번 실행된다. 서버가 여러 대이면 5.16.2절의 중복 실행 방지를 함께 적용한다. GameDay 함수는 네트워크와 무관한 순수 함수이므로 단위 시험으로 경계(오전 4시 59분과 5시 정각)를 확인할 수 있다(6.13.2절).

## 5.17 5장 연습 문제

1. 계정, 인벤토리, 랭킹, 접속 세션, 전투 로그 각각에 알맞은 저장소를 고르고 이유를 한 줄로 써라.
2. 두 요청이 동시에 같은 계정의 재화 500을 차감하려 한다(잔액 600). SELECT 후 서버에서 비교하는 방식과 UPDATE ... WHERE gold >= 500 방식의 결과를 비교하라.
3. (account_id, item_id) 복합 인덱스로 item_id만 조건으로 한 조회가 느린 이유를 설명하고 해결책을 제시하라.
4. 아이템 지급 요청이 네트워크 재전송으로 두 번 도착했다. 중복 지급을 막는 방법을 테이블 구조와 함께 설명하라.
5. Cache-Aside 방식에서 캐시와 DB가 어긋나는 상황 한 가지를 들고 완화 방법을 쓰라.
6. 계정 번호 해시로 샤딩한 환경에서 두 계정 사이의 거래를 안전하게 처리하는 Saga 절차를 단계별로 쓰라.
7. 서버가 갑자기 종료될 때 최근 5분 동안의 플레이 기록이 사라지는 것을 허용할 수 있는지 논하고, 허용할 수 없는 항목은 어떻게 저장할지 제안하라.
8. 5.8.4절의 ERD에 "길드 창고"(길드원이 함께 쓰는 아이템 보관함)를 추가하라. 필요한 테이블과 관계(1:N, N:M)를 ERD로 그리고, 각 테이블이 제3정규형을 만족하는지 확인하라.
9. player 테이블에 길드 이름(guild_name) 열을 더해 이름을 중복 저장했다고 하자. 길드 이름이 바뀔 때 생기는 갱신 이상을 설명하고, 이 구조와 guild 테이블의 member_count 열(반정규화) 중 어느 쪽이 더 위험한지 비교하라.
10. 5.15절의 guild와 player 테이블에 데이터를 넣고 길드별 인원과 평균 레벨을 구하라. 레벨 10 이상인 캐릭터만 세는 조건과 인원이 5명 이상인 길드만 남기는 조건을 각각 WHERE와 HAVING 중 어디에 써야 하는지 정하고 실행해서 확인하라.
11. "아이템 1001을 가진 적이 없는 캐릭터"를 NOT EXISTS와 LEFT JOIN으로 각각 쓰고 결과가 같은지 확인하라.
12. 5.15.3절의 sp_transfer_gold에서 두 지갑을 계정 번호 순서로 잠그는 두 줄을 지우면, A에서 B로 보내는 요청과 B에서 A로 보내는 요청이 동시에 실행될 때 어떤 일이 생길 수 있는지 설명하라. 결과 코드 1, 2, 3, -1을 받은 서버가 각각 사용자에게 무엇을 응답하고 다시 시도할지 정하라.
13. 규칙 판정을 서버 코드에 두는 방식과 저장 프로시저에 두는 방식을 시험, 배포, 확장, 권한 관리의 네 가지 면에서 비교하라. 자신의 서버에서 프로시저로 옮길 처리가 있는지, 없다면 이유가 무엇인지 쓰라.
14. 5.7절의 game_result 테이블에 10만 행을 넣고 느린 질의 하나를 골라 5.15.4절의 네 단계로 개선하라. 개선 전후의 실행 시간과 읽은 행 수를 같은 데이터로 기록한다.
15. "매일 오전 5시에 모든 계정의 출석 횟수를 0으로 만든다"는 작업을 5.16.2절의 원칙(멱등성, 중복 실행 방지, 나누어 처리)에 따라 설계하라. 서버 두 대가 같은 시각에 이 작업을 시작할 때와, 작업 도중 서버가 죽었다가 다시 시작할 때 각각 어떻게 동작하는지 순서대로 써라.
