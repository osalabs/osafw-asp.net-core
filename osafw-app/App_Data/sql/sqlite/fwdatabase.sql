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
(1, 10, '', 'test', 'novalue', 'test settings', 'description'),
(1, 0, 'AI', 'OPENAI_API_KEY', '', 'OpenAI API Key', 'API key used by Assistant and LLM features.'),
(1, 0, 'AI', 'ASSISTANT_ENABLED', '0', 'Assistant Enabled', 'Set to 1 to enable the assistant UI and queued runs.'),
(1, 0, 'AI', 'ASSISTANT_VECTOR_MODE', 'auto', 'Assistant Vector Mode', 'Use auto, json, or native. Auto uses SQL Server native vectors when available.'),
(1, 0, 'AI', 'ASSISTANT_MODEL', 'gpt-5-mini', 'Assistant Model', 'Chat model used for assistant responses.'),
(1, 0, 'AI', 'ASSISTANT_MEMORY_ENABLED', '0', 'Assistant Memory Enabled', 'Set to 1 to save optional per-user assistant memory summaries.'),
(1, 0, 'AI', 'ASSISTANT_MAX_FILES_PER_MESSAGE', '5', 'Assistant Max Files Per Message', 'Maximum number of files accepted with one assistant message.'),
(1, 0, 'AI', 'ASSISTANT_MAX_INDEXED_FILE_BYTES', '5242880', 'Assistant Max Indexed File Bytes', 'Maximum supported attachment size for queued indexing. Larger files remain attached but are not indexed.'),
(1, 0, 'AI', 'ASSISTANT_MAX_INDEX_CHARS', '200000', 'Assistant Max Index Characters', 'Maximum parsed characters indexed per document.'),
(1, 0, 'AI', 'ASSISTANT_MAX_INDEX_CHUNKS', '80', 'Assistant Max Index Chunks', 'Maximum embedding chunks indexed per document.');

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

/* Site Admin-managed custom reports */
CREATE TABLE fwreports (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  icode                 TEXT NOT NULL,
  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,
  icon                  TEXT NOT NULL DEFAULT '',
  access_level          INTEGER NOT NULL DEFAULT 80,
  sql_template          TEXT NOT NULL DEFAULT '',
  params_json           TEXT,
  render_options_json   TEXT,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_fwreports_icode ON fwreports (icode);

/* Knowledge base and assistant RAG */
CREATE TABLE kb_articles (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  icode                 TEXT NOT NULL DEFAULT '',
  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,
  content_markdown      TEXT NOT NULL DEFAULT '',
  access_level          INTEGER NOT NULL DEFAULT 1,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_kb_articles_icode ON kb_articles (icode);
CREATE INDEX IX_kb_articles_access ON kb_articles (access_level, status);

CREATE TABLE rag_sources (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  source_type           TEXT NOT NULL DEFAULT '',
  source_key            TEXT NOT NULL DEFAULT '',
  fwentities_id         INTEGER NOT NULL DEFAULT 0,
  item_id               INTEGER NOT NULL DEFAULT 0,
  att_id                INTEGER NOT NULL DEFAULT 0,
  iname                 TEXT NOT NULL DEFAULT '',
  url                   TEXT NOT NULL DEFAULT '',
  content_hash          TEXT NOT NULL DEFAULT '',
  source_version        TEXT NOT NULL DEFAULT '',
  acl_snapshot          TEXT,
  index_status          TEXT NOT NULL DEFAULT 'pending',
  queued_at             DATETIME NULL,
  last_indexed_at       DATETIME NULL,
  last_error            TEXT,
  metadata_json         TEXT,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_rag_sources_source_key ON rag_sources (source_key);
CREATE INDEX IX_rag_sources_queue ON rag_sources (index_status, status, queued_at, id);
CREATE INDEX IX_rag_sources_entity ON rag_sources (fwentities_id, item_id, att_id, status);

CREATE TABLE rag_chunks (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  rag_sources_id        INTEGER NOT NULL DEFAULT 0,
  fwentities_id         INTEGER NOT NULL DEFAULT 0,
  item_id               INTEGER NOT NULL DEFAULT 0,
  att_id                INTEGER NOT NULL DEFAULT 0,
  source_type           TEXT NOT NULL DEFAULT '',
  source_title          TEXT NOT NULL DEFAULT '',
  source_url            TEXT NOT NULL DEFAULT '',
  chunk_index           INTEGER NOT NULL DEFAULT 0,
  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT NOT NULL DEFAULT '',
  page                  INTEGER NOT NULL DEFAULT 0,
  section               TEXT NOT NULL DEFAULT '',
  embedding_json        TEXT NOT NULL,
  embedding_norm        REAL NOT NULL DEFAULT 0,
  embedding_dim         INTEGER NOT NULL DEFAULT 0,
  embedding_model       TEXT NOT NULL DEFAULT '',
  vector_backend        TEXT NOT NULL DEFAULT '',
  metadata_json         TEXT,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE INDEX IX_rag_chunks_source ON rag_chunks (rag_sources_id, chunk_index, status);
CREATE INDEX IX_rag_chunks_entity ON rag_chunks (fwentities_id, item_id, att_id, status);
CREATE INDEX IX_rag_chunks_embedding ON rag_chunks (embedding_model, embedding_dim, status);
CREATE INDEX IX_rag_chunks_backend ON rag_chunks (vector_backend, status);

CREATE TABLE assistant_threads (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  icode                 TEXT NOT NULL DEFAULT '',
  users_id              INTEGER NULL,
  owner_token           TEXT NOT NULL DEFAULT '',
  iname                 TEXT NOT NULL DEFAULT '',
  provider_thread_id    TEXT NOT NULL DEFAULT '',
  last_run_status       INTEGER NULL,
  last_message_at       DATETIME NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE INDEX IX_assistant_threads_icode ON assistant_threads (icode);
CREATE INDEX IX_assistant_threads_owner ON assistant_threads (users_id, owner_token, status, last_message_at);

CREATE TABLE assistant_messages (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  assistant_threads_id  INTEGER NOT NULL,
  role                  TEXT NOT NULL DEFAULT '',
  message_type          TEXT NOT NULL DEFAULT '',
  preview_text          TEXT NOT NULL DEFAULT '',
  content_markdown      TEXT NOT NULL DEFAULT '',
  payload_json          TEXT,
  sources_json          TEXT,
  confidence            REAL NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE INDEX IX_assistant_messages_thread ON assistant_messages (assistant_threads_id, id, status);
CREATE INDEX IX_assistant_messages_role ON assistant_messages (assistant_threads_id, role, status);

CREATE TABLE assistant_runs (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  assistant_threads_id  INTEGER NOT NULL,
  assistant_messages_id INTEGER NOT NULL,
  result_messages_id    INTEGER NULL,
  activity_logs_id      INTEGER NULL,
  worker_id             TEXT NOT NULL DEFAULT '',
  error_message         TEXT,
  clarification_json    TEXT,
  attempt_no            INTEGER NOT NULL DEFAULT 0,
  claimed_at            DATETIME NULL,
  started_at            DATETIME NULL,
  completed_at          DATETIME NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE INDEX IX_assistant_runs_queue ON assistant_runs (status, id);
CREATE INDEX IX_assistant_runs_thread ON assistant_runs (assistant_threads_id, id);

CREATE TABLE assistant_runs_events (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  assistant_runs_id     INTEGER NOT NULL,
  event_type            TEXT NOT NULL DEFAULT '',
  content               TEXT,
  payload_json          TEXT,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE INDEX IX_assistant_runs_events_run ON assistant_runs_events (assistant_runs_id, id, status);

CREATE TABLE assistant_memories (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  users_id              INTEGER NOT NULL,
  summary               TEXT,
  terminology_json      TEXT,
  preferences_json      TEXT,
  last_compacted_at     DATETIME NULL,
  source_threads_id     INTEGER NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_assistant_memories_user ON assistant_memories (users_id);

CREATE TABLE assistant_feedback (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  assistant_threads_id  INTEGER NULL,
  assistant_runs_id     INTEGER NULL,
  assistant_messages_id INTEGER NULL,
  feedback_type         TEXT NOT NULL DEFAULT '',
  comment               TEXT,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE INDEX IX_assistant_feedback_thread ON assistant_feedback (assistant_threads_id, assistant_runs_id, assistant_messages_id);

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

-- run roles.sql if roles support is enabled with isRoles in osafw-app.csproj
-- after this file - run lookups.sql
