-- SQLite core framework tables only, create app-specific tables in database.sql

PRAGMA foreign_keys = OFF;

DROP TABLE IF EXISTS user_filters;
DROP TABLE IF EXISTS menu_items;
DROP TABLE IF EXISTS user_lists_items;
DROP TABLE IF EXISTS user_lists;
DROP TABLE IF EXISTS user_views;
DROP TABLE IF EXISTS activity_logs;
DROP TABLE IF EXISTS log_types;
DROP TABLE IF EXISTS spages;
DROP TABLE IF EXISTS settings;
DROP TABLE IF EXISTS users_cookies;
DROP TABLE IF EXISTS users;
DROP TABLE IF EXISTS att_links;
DROP TABLE IF EXISTS att;
DROP TABLE IF EXISTS att_categories;
DROP TABLE IF EXISTS fwupdates;
DROP TABLE IF EXISTS fwcontrollers;
DROP TABLE IF EXISTS fwcron;
DROP TABLE IF EXISTS fwentities;
DROP TABLE IF EXISTS fwkeys;
DROP TABLE IF EXISTS fwsessions;

/* net core sessions */
CREATE TABLE fwsessions (
  Id                    TEXT NOT NULL PRIMARY KEY,
  Value                 BLOB NOT NULL,
  ExpiresAtTime         TEXT NOT NULL,
  SlidingExpirationInSeconds INTEGER NULL,
  AbsoluteExpiration    TEXT NULL
);
CREATE INDEX IX_fwsessions_ExpiresAtTime ON fwsessions (ExpiresAtTime);

/* keys storage */
CREATE TABLE fwkeys (
  iname                 TEXT NOT NULL PRIMARY KEY,
  itype                 INTEGER NOT NULL DEFAULT 0, -- 0-generic key, 10-data protection key

  XmlValue              TEXT NOT NULL,

  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, -- to help cleanup older than 90 days keys
  upd_time              DATETIME
);
CREATE INDEX IX_fwkeys_itype ON fwkeys (itype);

/* application entities lookup - autofilled on demand */
CREATE TABLE fwentities (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,

  icode                 TEXT NOT NULL DEFAULT '', -- basically table name
  iname                 TEXT NOT NULL DEFAULT '', -- human readable name
  idesc                 TEXT,

  status                INTEGER NOT NULL DEFAULT 0, -- 0-ok, 1-under upload, 127-deleted
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_fwentities_icode ON fwentities (icode);

-- for scheduled tasks
CREATE TABLE fwcron (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,

  icode                 TEXT NOT NULL DEFAULT '', -- Internal job code
  iname                 TEXT NOT NULL DEFAULT '', -- Human-readable name/title
  idesc                 TEXT,

  cron                  TEXT NOT NULL,
  next_run              DATETIME NULL,

  start_date            DATETIME NULL,
  end_date              DATETIME NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_fwcron_icode ON fwcron (icode);
CREATE INDEX IX_fwcron_next_run ON fwcron (next_run);

-- virtual controllers
CREATE TABLE fwcontrollers (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,

  icode                 TEXT NOT NULL DEFAULT '',
  url                   TEXT NOT NULL DEFAULT '',
  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT NULL,

  model                 TEXT NOT NULL DEFAULT '',
  is_lookup             INTEGER NOT NULL DEFAULT 0,
  igroup                TEXT NOT NULL DEFAULT '',
  access_level          INTEGER NOT NULL DEFAULT 0,
  access_level_edit     INTEGER NOT NULL DEFAULT 0,

  config                TEXT NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME NULL,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_fwcontrollers_icode ON fwcontrollers (icode);
CREATE UNIQUE INDEX UX_fwcontrollers_url ON fwcontrollers (url);

-- track framework database updates
CREATE TABLE fwupdates (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,

  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT NULL,

  applied_time          DATETIME NULL,
  last_error            TEXT NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME NULL,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_fwupdates_iname ON fwupdates (iname);

/* upload categories */
CREATE TABLE att_categories (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,

  icode                 TEXT NOT NULL DEFAULT '',
  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,
  prio                  INTEGER NOT NULL DEFAULT 0,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);

/* attachments - file uploads */
CREATE TABLE att (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  icode                 TEXT NOT NULL DEFAULT '',

  att_categories_id     INTEGER NULL REFERENCES att_categories(id),
  fwentities_id         INTEGER NULL REFERENCES fwentities(id),
  item_id               INTEGER NULL,

  is_s3                 INTEGER DEFAULT 0,
  is_inline             INTEGER DEFAULT 0,
  is_image              INTEGER DEFAULT 0,

  fname                 TEXT NOT NULL DEFAULT '',
  fsize                 INTEGER DEFAULT 0,
  ext                   TEXT NOT NULL DEFAULT '',
  iname                 TEXT NOT NULL DEFAULT '',

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_att_icode ON att (icode);
CREATE INDEX IX_att_categories ON att (att_categories_id);
CREATE INDEX IX_att_fwentities ON att (fwentities_id, item_id);

/* junction to link multiple att files to multiple entity items */
CREATE TABLE att_links (
  att_id                INTEGER NOT NULL REFERENCES att(id),
  fwentities_id         INTEGER NOT NULL REFERENCES fwentities(id),
  item_id               INTEGER NOT NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_att_links ON att_links (fwentities_id, item_id, att_id);
CREATE INDEX IX_att_links_att ON att_links (att_id, fwentities_id, item_id);

CREATE TABLE users (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,

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
  att_id                INTEGER NULL REFERENCES att(id),

  login_time            DATETIME,
  pwd_reset             TEXT NULL,
  pwd_reset_time        DATETIME NULL,
  mfa_secret            TEXT,
  mfa_recovery          TEXT,
  mfa_added             DATETIME,
  login                 TEXT NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_users_email ON users (email);
CREATE UNIQUE INDEX UX_users_login ON users (login) WHERE login IS NOT NULL;

INSERT INTO users (fname, lname, email, pwd, access_level)
VALUES ('Website','Admin','admin@admin.com','CHANGE_ME',100);

/* user cookies (for permanent sessions) */
CREATE TABLE users_cookies (
  cookie_id             TEXT NOT NULL PRIMARY KEY,
  users_id              INTEGER NOT NULL REFERENCES users(id),

  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

/* Site Settings - special table for misc site settings */
CREATE TABLE settings (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  icat                  TEXT NOT NULL DEFAULT '',
  icode                 TEXT NOT NULL DEFAULT '',
  ivalue                TEXT NOT NULL DEFAULT '',

  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,
  input                 INTEGER NOT NULL DEFAULT 0,
  allowed_values        TEXT,

  is_user_edit          INTEGER DEFAULT 0,

  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_settings_icode ON settings (icode);
CREATE INDEX IX_settings_icat ON settings (icat);

INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc) VALUES
(1, 10, '', 'test', 'novalue', 'test settings', 'description');

/* Static pages */
CREATE TABLE spages (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  parent_id             INTEGER NOT NULL DEFAULT 0,

  url                   TEXT NOT NULL DEFAULT '',
  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,
  head_att_id           INTEGER NULL REFERENCES att(id),

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
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE INDEX IX_spages_parent_id ON spages (parent_id, prio);
CREATE INDEX IX_spages_url ON spages (url);

INSERT INTO spages (parent_id, url, iname) VALUES
(0,'','Home'),
(0,'test-page','Test  page');
UPDATE spages SET is_home=1 WHERE id=1;

/* Logs types */
CREATE TABLE log_types (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  itype                 INTEGER NOT NULL DEFAULT 0,
  icode                 TEXT NOT NULL DEFAULT '',

  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,
  prio                  INTEGER NOT NULL DEFAULT 0,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE INDEX IX_log_types_icode ON log_types (icode);

/* Activity Logs */
CREATE TABLE activity_logs (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  reply_id              INTEGER NULL,
  log_types_id          INTEGER NOT NULL REFERENCES log_types(id),
  fwentities_id         INTEGER NOT NULL REFERENCES fwentities(id),
  item_id               INTEGER NULL,

  idate                 DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  users_id              INTEGER NULL REFERENCES users(id),
  idesc                 TEXT,
  payload               TEXT,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE INDEX IX_activity_logs_reply_id ON activity_logs (reply_id);
CREATE INDEX IX_activity_logs_log_types_id ON activity_logs (log_types_id);
CREATE INDEX IX_activity_logs_fwentities_id ON activity_logs (fwentities_id);
CREATE INDEX IX_activity_logs_item_id ON activity_logs (item_id);
CREATE INDEX IX_activity_logs_idate ON activity_logs (idate);
CREATE INDEX IX_activity_logs_users_id ON activity_logs (users_id);

/* user custom views */
CREATE TABLE user_views (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  icode                 TEXT NOT NULL,
  fields                TEXT,

  iname                 TEXT NOT NULL DEFAULT '',
  is_system             INTEGER NOT NULL DEFAULT 0,
  is_shared             INTEGER NOT NULL DEFAULT 0,
  density               TEXT NOT NULL DEFAULT '',

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_user_views ON user_views (add_users_id, icode, iname);

/* user lists */
CREATE TABLE user_lists (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  entity                TEXT NOT NULL,

  iname                 TEXT NOT NULL,
  idesc                 TEXT,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE INDEX IX_user_lists ON user_lists (add_users_id, entity);

/* items linked to user lists */
CREATE TABLE user_lists_items (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  user_lists_id         INTEGER NOT NULL REFERENCES user_lists(id),
  item_id               INTEGER NOT NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_user_lists_items ON user_lists_items (user_lists_id, item_id);

/* Custom menu items for sidebar */
CREATE TABLE menu_items (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,

  iname                 TEXT NOT NULL DEFAULT '',
  url                   TEXT NOT NULL DEFAULT '',
  icon                  TEXT NOT NULL DEFAULT '',
  controller            TEXT NOT NULL DEFAULT '',
  access_level          INTEGER NOT NULL DEFAULT 0,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);

CREATE TABLE user_filters (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  icode                 TEXT NOT NULL,

  iname                 TEXT NOT NULL,
  idesc                 TEXT,
  is_system             INTEGER NOT NULL DEFAULT 0,
  is_shared             INTEGER NOT NULL DEFAULT 0,

  status                INTEGER DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);

PRAGMA foreign_keys = ON;

-- run roles.sql if roles support required and also uncomment #define isRoles in Users model
-- after this file - run lookups.sql
