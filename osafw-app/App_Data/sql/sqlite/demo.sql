-- SQLite demo tables, use for reference/development, remove when not required

PRAGMA foreign_keys = OFF;

DROP TABLE IF EXISTS demos_items;
DROP TABLE IF EXISTS demos_demo_dicts;
DROP TABLE IF EXISTS demos;
DROP TABLE IF EXISTS demo_dicts;

/* Demo Dictionary table */
CREATE TABLE demo_dicts (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,

  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,
  prio                  INTEGER NOT NULL DEFAULT 0,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);

INSERT INTO demo_dicts (iname, idesc, add_time) VALUES
('test1', 'test1 description', CURRENT_TIMESTAMP),
('test2', 'test2 description', CURRENT_TIMESTAMP),
('test3', 'test3 description', CURRENT_TIMESTAMP);

/* Demo table */
CREATE TABLE demos (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  parent_id             INTEGER NOT NULL DEFAULT 0,
  demo_dicts_id         INTEGER NULL REFERENCES demo_dicts(id),

  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,

  email                 TEXT NOT NULL DEFAULT '',

  fint                  INTEGER NOT NULL DEFAULT 0,
  ffloat                REAL NOT NULL DEFAULT 0,

  dict_link_auto_id     INTEGER NOT NULL DEFAULT 0,
  dict_link_multi       TEXT NOT NULL DEFAULT '',

  fcombo                INTEGER NOT NULL DEFAULT 0,
  fradio                INTEGER NOT NULL DEFAULT 0,
  fyesno                INTEGER NOT NULL DEFAULT 0,
  is_checkbox           INTEGER NOT NULL DEFAULT 0,

  fdate_combo           DATE,
  fdate_pop             DATE,
  fdatetime             DATETIME,
  fdatetime_utc         DATETIME,
  fdatetime_offset      DATETIMEOFFSET,
  fdatetime_local       DATETIME,
  ftime                 INTEGER NOT NULL DEFAULT 0,

  att_id                INTEGER NULL REFERENCES att(id),

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_demos_email ON demos (email);
CREATE INDEX IX_demos_demo_dicts_id ON demos (demo_dicts_id);
CREATE INDEX IX_demos_dict_link_auto_id ON demos (dict_link_auto_id);

/* junction table */
CREATE TABLE demos_demo_dicts (
  demos_id              INTEGER NULL REFERENCES demos(id),
  demo_dicts_id         INTEGER NULL REFERENCES demo_dicts(id),

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE INDEX IX_demos_demo_dicts_demos_id ON demos_demo_dicts (demos_id, demo_dicts_id);
CREATE INDEX IX_demos_demo_dicts_demo_dicts_id ON demos_demo_dicts (demo_dicts_id, demos_id);

/* subtable for demo items */
CREATE TABLE demos_items (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  demos_id              INTEGER NOT NULL REFERENCES demos(id),

  demo_dicts_id         INTEGER NULL REFERENCES demo_dicts(id),
  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,
  is_checkbox           INTEGER NOT NULL DEFAULT 0,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE INDEX IX_demos_items_demos_id ON demos_items (demos_id);
CREATE INDEX IX_demos_items_demo_dicts_id ON demos_items (demo_dicts_id, demos_id);

PRAGMA foreign_keys = ON;

/*
TEST DATA
INSERT statements for demos table
*/
WITH RECURSIVE nums(n) AS (
  SELECT 1
  UNION ALL
  SELECT n + 1 FROM nums WHERE n < 50
)
INSERT INTO demos (
  parent_id, demo_dicts_id, iname, idesc, email, fint, ffloat,
  dict_link_auto_id, dict_link_multi, fcombo, fradio, fyesno, is_checkbox,
  fdate_combo, fdate_pop, fdatetime, fdatetime_utc, fdatetime_offset,
  fdatetime_local, ftime, att_id, status, add_time, add_users_id
)
SELECT
  abs(random()) % 10,
  abs(random()) % 3 + 1,
  'Name' || n,
  'Description' || n,
  'email' || n,
  abs(random()) % 1000,
  (abs(random()) % 1000) + (abs(random()) % 1000) / 1000.0,
  abs(random()) % 3 + 1,
  'LinkMulti' || n,
  abs(random()) % 3 + 1,
  abs(random()) % 3 + 1,
  abs(random()) % 2,
  abs(random()) % 2,
  date('2023-01-01', '+' || (abs(random()) % 365) || ' days'),
  date('2023-01-01', '+' || (abs(random()) % 365) || ' days'),
  datetime('now', '+' || (abs(random()) % 1440) || ' minutes'),
  datetime('now', '+' || (abs(random()) % 1440) || ' minutes'),
  strftime('%Y-%m-%dT%H:%M:%SZ', 'now', '+' || (abs(random()) % 1440) || ' minutes'),
  datetime('now', '+' || (abs(random()) % 1440) || ' minutes'),
  abs(random()) % 86400,
  NULL,
  0,
  CURRENT_TIMESTAMP,
  1
FROM nums;
