-- demo tables, use for reference/development, remove when not required

/*Demo Dictionary table*/
DROP TABLE IF EXISTS demo_dicts;
CREATE TABLE demo_dicts (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,

  iname                 TEXT NOT NULL default '',
  idesc                 TEXT,
  prio                  INTEGER NOT NULL DEFAULT 0,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
INSERT INTO demo_dicts (iname, idesc, add_time) VALUES ('test1', 'test1 description', CURRENT_TIMESTAMP);
INSERT INTO demo_dicts (iname, idesc, add_time) VALUES ('test2', 'test2 description', CURRENT_TIMESTAMP);
INSERT INTO demo_dicts (iname, idesc, add_time) VALUES ('test3', 'test3 description', CURRENT_TIMESTAMP);

/*Demo table*/
DROP TABLE IF EXISTS demos;
CREATE TABLE demos (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  parent_id             INTEGER NOT NULL DEFAULT 0,
  demo_dicts_id         INTEGER NULL,

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
  ftime                 INTEGER NOT NULL DEFAULT 0,

  att_id                INTEGER NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0,

  FOREIGN KEY (demo_dicts_id) REFERENCES demo_dicts(id),
  FOREIGN KEY (att_id) REFERENCES att(id)
);
CREATE UNIQUE INDEX UX_demos_email ON demos (email);
CREATE INDEX IX_demos_demo_dicts_id ON demos (demo_dicts_id);
CREATE INDEX IX_demos_dict_link_auto_id ON demos (dict_link_auto_id);

/*junction table*/
DROP TABLE IF EXISTS demos_demo_dicts;
CREATE TABLE demos_demo_dicts (
  demos_id              INTEGER NULL,
  demo_dicts_id         INTEGER NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0,

  FOREIGN KEY (demos_id) REFERENCES demos(id),
  FOREIGN KEY (demo_dicts_id) REFERENCES demo_dicts(id)
);
CREATE INDEX IX_demos_demo_dicts_demos_id ON demos_demo_dicts (demos_id, demo_dicts_id);
CREATE INDEX IX_demos_demo_dicts_demo_dicts_id ON demos_demo_dicts (demo_dicts_id, demos_id);

/*subtable for demo items*/
DROP TABLE IF EXISTS demos_items;
CREATE TABLE demos_items (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  demos_id              INTEGER NOT NULL,

  demo_dicts_id         INTEGER NULL,
  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,
  is_checkbox           INTEGER NOT NULL DEFAULT 0,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0,

  FOREIGN KEY (demos_id) REFERENCES demos(id),
  FOREIGN KEY (demo_dicts_id) REFERENCES demo_dicts(id)
);
CREATE INDEX IX_demos_items_demos_id ON demos_items (demos_id);
CREATE INDEX IX_demos_items_demo_dicts_id ON demos_items (demo_dicts_id, demos_id);
