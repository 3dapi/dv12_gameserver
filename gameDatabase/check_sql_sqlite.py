import re, sqlite3, random
SP = '/tmp/claude-0/-home-claude/435858f6-2050-54ae-a174-0700475d2646/scratchpad'
txt = open(SP + '/n5b_final.md', encoding='utf-8').read()
blocks = re.findall(r'```sql\n(.*?)```', txt, re.S)
print(len(blocks), 'sql blocks in n5b_final.md')
def find(prefix):
    r = [b for b in blocks if b.lstrip().startswith(prefix)]
    assert len(r) == 1, (prefix, len(r)); return r[0]

db = sqlite3.connect(':memory:', isolation_level=None)   # autocommit: BEGIN/COMMIT are issued explicitly
db.execute('PRAGMA foreign_keys=ON')
# 5.8.2 / 5.9 tables the section relies on (simplified types for SQLite)
db.executescript('''
CREATE TABLE account (account_id INTEGER PRIMARY KEY, login_name TEXT NOT NULL UNIQUE);
CREATE TABLE inventory (account_id INTEGER NOT NULL, item_id INTEGER NOT NULL, amount INTEGER NOT NULL,
  PRIMARY KEY (account_id, item_id), FOREIGN KEY (account_id) REFERENCES account(account_id));
CREATE TABLE wallet (account_id INTEGER PRIMARY KEY, gold INTEGER NOT NULL CHECK (gold >= 0));
CREATE TABLE trade_log (log_id INTEGER PRIMARY KEY AUTOINCREMENT, from_id INTEGER, to_id INTEGER, gold INTEGER);
CREATE TABLE wallet_audit (account_id INTEGER, old_gold INTEGER, new_gold INTEGER, changed_at TEXT DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE mail (mail_id INTEGER PRIMARY KEY AUTOINCREMENT, account_id INTEGER, expire_at INTEGER);
''')
# the book's own guild/player DDL, with MySQL-only words removed
ddl = find('CREATE TABLE guild')
ddl = re.sub(r'\)\s*ENGINE=InnoDB;', ');', ddl).replace('AUTO_INCREMENT', '')
db.executescript(ddl)

random.seed(7)
for a in range(1, 31): db.execute('INSERT INTO account VALUES (?,?)', (a, f'u{a}'))
for g, n in [(1, 'alpha'), (2, 'beta'), (3, 'gamma')]: db.execute('INSERT INTO guild VALUES (?,?)', (g, n))
players = []
pid = 1
for a in range(1, 31):
    guild = [1]*8 + [2]*3 + [3]*6 + [None]*13
    g = guild[a-1]
    lv = random.randint(1, 60)
    players.append((pid, a, f'p{a}', lv, g)); db.execute('INSERT INTO player VALUES (?,?,?,?,?)', players[-1]); pid += 1
for a in (1, 2, 3, 9, 12, 20): db.execute('INSERT INTO inventory VALUES (?,?,?)', (a, 1001, 1))
for a in (1, 4): db.execute('INSERT INTO inventory VALUES (?,?,?)', (a, 2002, 3))
tl = [(1, 2, 10), (2, 3, 5), (1, 3, 7), (5, 1, 1), (5, 2, 4)]
for f, t_, g in tl: db.execute('INSERT INTO trade_log(from_id,to_id,gold) VALUES (?,?,?)', (f, t_, g))

ok = True
def check(name, cond):
    global ok
    print(('PASS ' if cond else 'FAIL ') + name)
    ok = ok and cond

# 5.15.1 aggregate
q = find('-- 길드별 인원과 평균 레벨')
rows = db.execute(q).fetchall()
exp = {}
for _, a, n, lv, g in players:
    if g is not None: exp.setdefault(g, []).append(lv)
exp = {g: (len(v), sum(v)/len(v)) for g, v in exp.items() if len(v) >= 5}
exp_rows = sorted(((g, c, avg) for g, (c, avg) in exp.items()), key=lambda r: -r[2])
check('5.15.1 group/having/order', [(r[0], r[1], round(r[2], 6)) for r in rows] == [(g, c, round(avg, 6)) for g, c, avg in exp_rows])
check('5.15.1 HAVING removed guild with <5 members (guild 2 absent)', 2 not in [r[0] for r in rows])

# 5.15.2 subqueries
qs = [s.strip() for s in re.split(r'\n\n', find('-- 평균보다 레벨이 높은 캐릭터').strip()) if s.strip()]
avg = sum(p[3] for p in players)/len(players)
r1 = sorted(db.execute(qs[0]).fetchall())
check('5.15.2 above-average', r1 == sorted((p[2], p[3]) for p in players if p[3] > avg))
r_in = sorted(db.execute(qs[1].split('\n', 1)[1] if qs[1].startswith('--') else qs[1]).fetchall())
r_ex = sorted(db.execute(qs[2]).fetchall())
want = sorted((p[2],) for p in players if p[1] in (1, 2, 3, 9, 12, 20))
check('5.15.2 IN result', r_in == want)
check('5.15.2 EXISTS == IN', r_ex == r_in)
r_lj = sorted(db.execute(qs[3].split('\n', 1)[1] if qs[3].startswith('--') else qs[3]).fetchall())
check('5.15.2 LEFT JOIN anti-join (no guild)', r_lj == sorted((p[2],) for p in players if p[4] is None))
uni = find('-- 거래에 한 번이라도 참여한 계정')
r_u = sorted(x[0] for x in db.execute(uni).fetchall())
check('5.15.2 UNION dedups', r_u == sorted({x for f, t_, g in tl for x in (f, t_)}))
r_ua = db.execute(uni.replace('UNION', 'UNION ALL')).fetchall()
check('5.15.2 UNION ALL keeps duplicates', len(r_ua) == 2*len(tl))

# 5.15.3 trigger body (MySQL DELIMITER wrappers replaced by SQLite form)
trg = find('DELIMITER //\nCREATE TRIGGER')
trg_sqlite = trg.replace('DELIMITER //\n', '').replace('END //', 'END;').replace('DELIMITER ;', '').strip()
db.executescript(trg_sqlite)
db.execute('INSERT INTO wallet VALUES (1, 1000)'); db.execute('INSERT INTO wallet VALUES (2, 50)')
db.execute('UPDATE wallet SET gold = gold - 100 WHERE account_id = 1')
aud = db.execute('SELECT account_id, old_gold, new_gold, changed_at IS NOT NULL FROM wallet_audit').fetchall()
check('5.15.3 trigger writes one audit row with old/new', aud == [(1, 1000, 900, 1)])

# 5.15.3 procedure logic: same statements and branches, driven from Python (no MySQL here)
proc = find('DELIMITER //\nCREATE PROCEDURE')
for must in ('p_gold <= 0 OR p_from = p_to', 'gold >= p_gold', 'LEAST(p_from, p_to)', 'GREATEST(p_from, p_to)', 'SET p_result = 3'):
    assert must in proc, must
def transfer(frm, to, gold):
    if gold <= 0 or frm == to: return 2
    try:
        db.execute('BEGIN')
        c = db.execute('UPDATE wallet SET gold = gold - ? WHERE account_id = ? AND gold >= ?', (gold, frm, gold))
        if c.rowcount == 0: db.execute('ROLLBACK'); return 1
        c = db.execute('UPDATE wallet SET gold = gold + ? WHERE account_id = ?', (gold, to))
        if c.rowcount == 0: db.execute('ROLLBACK'); return 3
        db.execute('INSERT INTO trade_log(from_id,to_id,gold) VALUES (?,?,?)', (frm, to, gold))
        db.execute('COMMIT'); return 0
    except Exception:
        db.execute('ROLLBACK'); return -1
db.execute('DELETE FROM wallet_audit'); db.execute('DELETE FROM trade_log')
db.execute('UPDATE wallet SET gold = 1000 WHERE account_id = 1'); db.execute('UPDATE wallet SET gold = 50 WHERE account_id = 2')
def total(): return db.execute('SELECT SUM(gold) FROM wallet').fetchone()[0]
def bal(a): return db.execute('SELECT gold FROM wallet WHERE account_id=?', (a,)).fetchone()[0]
t0 = total()
check('proc: success -> 0', transfer(1, 2, 500) == 0 and bal(1) == 500 and bal(2) == 550)
check('proc: insufficient -> 1, unchanged', transfer(1, 2, 501) == 1 and bal(1) == 500 and bal(2) == 550)
check('proc: negative amount -> 2 (would have stolen without the guard)', transfer(1, 2, -100) == 2 and bal(1) == 500 and bal(2) == 550)
check('proc: zero amount -> 2', transfer(1, 2, 0) == 2)
check('proc: same account -> 2', transfer(1, 1, 10) == 2)
check('proc: missing recipient -> 3, sender restored by rollback', transfer(1, 99, 100) == 3 and bal(1) == 500)
check('proc: missing sender -> 1', transfer(98, 2, 10) == 1)
check('proc: total gold conserved', total() == t0)
check('proc: one trade_log row only for the success', db.execute('SELECT COUNT(*) FROM trade_log').fetchone()[0] == 1)
# what the 5.9.1-style sequence would do without the extra guards (shows why they were added)
db.execute('BEGIN')
db.execute('UPDATE wallet SET gold = gold - ? WHERE account_id = ? AND gold >= ?', (-100, 1, -100))   # negative: condition passes
db.execute('UPDATE wallet SET gold = gold + ? WHERE account_id = ?', (-100, 2))
inc_sender = bal(1); db.execute('ROLLBACK')
check('without the guard a negative amount would have INCREASED the sender (600)', inc_sender == 600)

# 5.16.2 batch delete loop logic (SQLite form of the MySQL DELETE ... ORDER BY ... LIMIT)
for i in range(2500): db.execute('INSERT INTO mail(account_id, expire_at) VALUES (1, ?)', (100 if i < 2300 else 900,))
batches = []
while True:
    n = db.execute('DELETE FROM mail WHERE mail_id IN (SELECT mail_id FROM mail WHERE expire_at < 500 ORDER BY mail_id LIMIT 1000)').rowcount
    batches.append(n)
    if n < 1000: break
check('5.16.2 batches 1000,1000,300 then stop; unexpired mail kept', batches == [1000, 1000, 300] and db.execute('SELECT COUNT(*) FROM mail').fetchone()[0] == 200)

print('ALL OK' if ok else 'SOME FAILED')
