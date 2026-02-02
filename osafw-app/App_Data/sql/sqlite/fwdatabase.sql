-- core framework tables only, create app-specific tables in database.sql
PRAGMA foreign_keys = ON;

/* net core sessions */
DROP TABLE IF EXISTS fwsessions;
CREATE TABLE fwsessions (
  Id                    TEXT NOT NULL PRIMARY KEY,
  Value                 BLOB NOT NULL,
  ExpiresAtTime         DATETIME NOT NULL,
  SlidingExpirationInSeconds INTEGER NULL,
  AbsoluteExpiration    DATETIME NULL
);
CREATE INDEX IX_ExpiresAtTime ON fwsessions (ExpiresAtTime);

/* keys storage */
DROP TABLE IF EXISTS fwkeys;
CREATE TABLE fwkeys (
  iname                 TEXT NOT NULL PRIMARY KEY,
  itype                 INTEGER NOT NULL DEFAULT 0,

  XmlValue              TEXT NOT NULL,

  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  upd_time              DATETIME NULL
);
CREATE INDEX IX_fwkeys_itype ON fwkeys (itype);

/*application entities lookup - autofilled on demand*/
DROP TABLE IF EXISTS fwentities;
CREATE TABLE fwentities (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,

  icode                 TEXT NOT NULL DEFAULT '',
  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_fwentities_icode ON fwentities (icode);

-- for scheduled tasks
DROP TABLE IF EXISTS fwcron;
CREATE TABLE fwcron
(
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,

  icode                 TEXT NOT NULL DEFAULT '',
  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,

  cron                  TEXT NOT NULL,
  next_run              DATETIME NULL,

  start_date            DATETIME NULL,
  end_date              DATETIME NULL,

  status                INTEGER NOT NULL DEFAULT 0,

  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,

  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_fwcron_icode ON fwcron (icode);
CREATE INDEX IX_fwcron_next_run ON fwcron (next_run);

-- virtual controllers
DROP TABLE IF EXISTS fwcontrollers;
CREATE TABLE fwcontrollers
(
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    
    icode             TEXT NOT NULL DEFAULT '',
    url               TEXT NOT NULL DEFAULT '',
    iname             TEXT NOT NULL DEFAULT '',
    idesc             TEXT NULL,
    
    model             TEXT NOT NULL DEFAULT '',
    is_lookup         INTEGER NOT NULL DEFAULT 0,
    igroup            TEXT NOT NULL DEFAULT '',
    access_level      INTEGER NOT NULL DEFAULT 0,
    access_level_edit INTEGER NOT NULL DEFAULT 0,
    
    config            TEXT NULL,
    
    status            INTEGER NOT NULL DEFAULT 0,
    add_time          DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    add_users_id      INTEGER DEFAULT 0,
    upd_time          DATETIME NULL,
    upd_users_id      INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_fwcontrollers_icode ON fwcontrollers (icode);
CREATE UNIQUE INDEX UX_fwcontrollers_url ON fwcontrollers (url);

-- track framework database updates
DROP TABLE IF EXISTS fwupdates;
CREATE TABLE fwupdates
(
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    
    iname        TEXT NOT NULL DEFAULT '',
    idesc        TEXT NULL,
    
    applied_time DATETIME NULL,
    last_error   TEXT NULL,
    
    status       INTEGER NOT NULL DEFAULT 0,
    add_time     DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    add_users_id INTEGER DEFAULT 0,
    upd_time     DATETIME NULL,
    upd_users_id INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_fwupdates_iname ON fwupdates (iname);

/* upload categories */
DROP TABLE IF EXISTS att_categories;
CREATE TABLE att_categories (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,

  icode                 TEXT NOT NULL DEFAULT '',
  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,
  prio                  INTEGER NOT NULL DEFAULT 0,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);

/* attachments - file uploads */
DROP TABLE IF EXISTS att;
CREATE TABLE att (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  icode                 TEXT NOT NULL DEFAULT (lower(hex(randomblob(16)))),

  att_categories_id     INTEGER NULL,

  fwentities_id         INTEGER NULL,
  item_id               INTEGER NULL,

  is_s3                 INTEGER DEFAULT 0,
  is_inline             INTEGER DEFAULT 0,
  is_image              INTEGER DEFAULT 0,

  fname                 TEXT NOT NULL DEFAULT '',
  fsize                 INTEGER DEFAULT 0,
  ext                   TEXT NOT NULL DEFAULT '',
  iname                 TEXT NOT NULL DEFAULT '',

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0,

  FOREIGN KEY (att_categories_id) REFERENCES att_categories(id),
  FOREIGN KEY (fwentities_id) REFERENCES fwentities(id)
);
CREATE UNIQUE INDEX UX_att_icode ON att (icode);
CREATE INDEX IX_att_categories ON att (att_categories_id);
CREATE INDEX IX_att_fwentities ON att (fwentities_id, item_id);

/*junction to link multiple att files to multiple entity items*/
DROP TABLE IF EXISTS att_links;
CREATE TABLE att_links (
  att_id                INTEGER NOT NULL,
  fwentities_id         INTEGER NOT NULL,
  item_id               INTEGER NOT NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,

  FOREIGN KEY (att_id) REFERENCES att(id),
  FOREIGN KEY (fwentities_id) REFERENCES fwentities(id)
);
CREATE UNIQUE INDEX UX_att_links ON att_links (fwentities_id, item_id, att_id);
CREATE INDEX IX_att_links_att ON att_links (att_id, fwentities_id, item_id);

DROP TABLE IF EXISTS users;
CREATE TABLE users (
  id int PRIMARY KEY AUTOINCREMENT,

  email                 TEXT NOT NULL DEFAULT '',
  pwd                   TEXT NOT NULL DEFAULT '',
  access_level          INTEGER NOT NULL,
  is_readonly           INTEGER NOT NULL DEFAULT 0,

  iname                 TEXT GENERATED ALWAYS AS (fname || ' ' || lname) STORED,
  fname                 TEXT NOT NULL DEFAULT '',
  lname                 TEXT NOT NULL DEFAULT '',
  title                 TEXT NOT NULL DEFAULT '',

  address1              TEXT NOT NULL DEFAULT '',
  address2              TEXT NOT NULL DEFAULT '',
  city                  TEXT NOT NULL DEFAULT '',
  state                 TEXT NOT NULL DEFAULT '',
  zip                   TEXT NOT NULL DEFAULT '',
  phone                 TEXT NOT NULL DEFAULT '',

  lang                  TEXT NOT NULL DEFAULT 'en',
  ui_theme              INTEGER NOT NULL DEFAULT 0,
  ui_mode               INTEGER NOT NULL DEFAULT 0,

  date_format           INTEGER NOT NULL DEFAULT 0,
  time_format           INTEGER NOT NULL DEFAULT 0,
  timezone              TEXT NOT NULL DEFAULT 'UTC',

  idesc                 TEXT,
  att_id                INTEGER NULL,

  login_time            DATETIME,
  pwd_reset             TEXT NULL,
  pwd_reset_time        DATETIME NULL,
  mfa_secret            TEXT,
  mfa_recovery          TEXT,
  mfa_added             DATETIME,
  login                 TEXT NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0,

  FOREIGN KEY (att_id) REFERENCES att(id)
);
CREATE UNIQUE INDEX UX_users_email ON users (email);
CREATE UNIQUE INDEX UX_users_login ON users (login) WHERE login IS NOT NULL;

INSERT INTO users (fname, lname, email, pwd, access_level)
VALUES ('Website','Admin','admin@admin.com','CHANGE_ME',100);

/*user cookies (for permanent sessions)*/
DROP TABLE IF EXISTS users_cookies;
CREATE TABLE users_cookies (
    cookie_id           TEXT PRIMARY KEY NOT NULL,
    users_id            INTEGER NOT NULL,

    add_time            DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),

    FOREIGN KEY (users_id) REFERENCES users(id)
);

/*Site Settings - special table for misc site settings*/
DROP TABLE IF EXISTS settings;
CREATE TABLE settings (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  icat                  TEXT NOT NULL DEFAULT '',
  icode                 TEXT NOT NULL DEFAULT '',
  ivalue                TEXT NOT NULL DEFAULT '',

  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,
  input                 INTEGER NOT NULL DEFAULT 0,
  allowed_values        TEXT,

  is_user_edit          INTEGER DEFAULT 0,

  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_settings_icode ON settings (icode);
CREATE INDEX IX_settings_icat ON settings (icat);
INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc) VALUES
(1, 10, '', 'test', 'novalue', 'test settings', 'description');

/*Static pages*/
DROP TABLE IF EXISTS spages;
CREATE TABLE spages (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  parent_id             INTEGER NOT NULL DEFAULT 0,

  url                   TEXT NOT NULL DEFAULT '',
  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,
  head_att_id           INTEGER NULL,

  idesc_left            TEXT,
  idesc_right           TEXT,
  meta_keywords         TEXT NOT NULL DEFAULT '',
  meta_description      TEXT NOT NULL DEFAULT '',

  pub_time              DATETIME,
  template              TEXT,
  prio                  INTEGER NOT NULL DEFAULT 0,
  is_home               INTEGER DEFAULT 0,
  redirect_url          TEXT NOT NULL DEFAULT '',

  custom_head           TEXT,
  custom_css            TEXT,
  custom_js             TEXT,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0,

  FOREIGN KEY (head_att_id) REFERENCES att(id)
);
CREATE INDEX IX_spages_parent_id ON spages (parent_id, prio);
CREATE INDEX IX_spages_url ON spages (url);
INSERT INTO spages (parent_id, url, iname) VALUES
(0,'','Home'),
(0,'test-page','Test  page');
UPDATE spages SET is_home=1 WHERE id=1;

/*Logs types*/
DROP TABLE IF EXISTS log_types;
CREATE TABLE log_types (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  itype                 INTEGER NOT NULL DEFAULT 0,
  icode                 TEXT NOT NULL DEFAULT '',

  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,
  prio                  INTEGER NOT NULL DEFAULT 0,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE INDEX IX_log_types_icode ON log_types (icode);

/*Activity Logs*/
DROP TABLE IF EXISTS activity_logs;
CREATE TABLE activity_logs (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  reply_id              INTEGER NULL,
  log_types_id          INTEGER NOT NULL,
  fwentities_id         INTEGER NOT NULL,
  item_id               INTEGER NULL,

  idate                 DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  users_id              INTEGER NULL,
  idesc                 TEXT,
  payload               TEXT,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0,

  FOREIGN KEY (log_types_id) REFERENCES log_types(id),
  FOREIGN KEY (fwentities_id) REFERENCES fwentities(id),
  FOREIGN KEY (users_id) REFERENCES users(id)
);
CREATE INDEX IX_activity_logs_reply_id ON activity_logs (reply_id);
CREATE INDEX IX_activity_logs_log_types_id ON activity_logs (log_types_id);
CREATE INDEX IX_activity_logs_fwentities_id ON activity_logs (fwentities_id);
CREATE INDEX IX_activity_logs_item_id ON activity_logs (item_id);
CREATE INDEX IX_activity_logs_idate ON activity_logs (idate);
CREATE INDEX IX_activity_logs_users_id ON activity_logs (users_id);

/*user custom views*/
DROP TABLE IF EXISTS user_views;
CREATE TABLE user_views (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  icode                 TEXT NOT NULL,
  fields                TEXT,

  iname                 TEXT NOT NULL DEFAULT '',
  is_system             INTEGER NOT NULL DEFAULT 0,
  is_shared             INTEGER NOT NULL DEFAULT 0,
  density               TEXT NOT NULL DEFAULT '',

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_user_views ON user_views (add_users_id, icode, iname);

/*user lists*/
DROP TABLE IF EXISTS user_lists;
CREATE TABLE user_lists (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  entity                TEXT NOT NULL,

  iname                 TEXT NOT NULL,
  idesc                 TEXT,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE INDEX IX_user_lists ON user_lists (add_users_id, entity);

/*items linked to user lists */
DROP TABLE IF EXISTS user_lists_items;
CREATE TABLE user_lists_items (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  user_lists_id         INTEGER NOT NULL,
  item_id               INTEGER NOT NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0,

  FOREIGN KEY (user_lists_id) REFERENCES user_lists(id)
);
CREATE UNIQUE INDEX UX_user_lists_items ON user_lists_items (user_lists_id, item_id);

/*Custom menu items for sidebar*/
DROP TABLE IF EXISTS menu_items;
CREATE TABLE menu_items (
  id INTEGER PRIMARY KEY AUTOINCREMENT,

  iname                 TEXT NOT NULL default '',
  url                   TEXT NOT NULL default '',
  icon                  TEXT NOT NULL default '',
  controller            TEXT NOT NULL default '',
  access_level          INTEGER NOT NULL DEFAULT 0,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);

DROP TABLE IF EXISTS user_filters;
CREATE TABLE user_filters (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  icode                 TEXT NOT NULL,

  iname                 TEXT NOT NULL,
  idesc                 TEXT,
  is_system             INTEGER NOT NULL DEFAULT 0,
  is_shared             INTEGER NOT NULL DEFAULT 0,

  status                INTEGER DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);

-- run roles.sql if roles support required and also uncomment #define isRoles in Users model

-- after this file - run lookups.sql
