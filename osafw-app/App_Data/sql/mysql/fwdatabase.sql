-- core framework tables only, create app-specific tables in database.sql

/* net core sessions */
DROP TABLE IF EXISTS fwsessions;
CREATE TABLE fwsessions (
  Id                    VARCHAR(449) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  Value                 LONGBLOB NOT NULL,
  ExpiresAtTime         DATETIME NOT NULL,
  SlidingExpirationInSeconds BIGINT NULL,
  AbsoluteExpiration    DATETIME NULL,

  PRIMARY KEY (Id),
  KEY IX_ExpiresAtTime (ExpiresAtTime)
) DEFAULT CHARSET=utf8mb4;

/* upload categories */
DROP TABLE IF EXISTS att_categories;
CREATE TABLE att_categories (
  id                    INT NOT NULL auto_increment,

  icode                 VARCHAR(64) NOT NULL DEFAULT '', /*to use from code*/
  iname                 VARCHAR(64) NOT NULL DEFAULT '',
  idesc                 TEXT,
  prio                  INT NOT NULL DEFAULT 0,     /*0-on insert, then =id, default order by prio asc,iname*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id)
) DEFAULT CHARSET=utf8mb4;

DROP TABLE IF EXISTS att;
CREATE TABLE att (
  id                    INT NOT NULL auto_increment, /* files stored on disk under 0/0/0/id.dat */
  att_categories_id     INT NULL,

  table_name            VARCHAR(128) NOT NULL DEFAULT '',
  item_id               INT NOT NULL DEFAULT 0,

  is_s3                 TINYINT DEFAULT 0, /* 1 if file is in S3 - default key att/{icode}, S3.IS_ATT_KEY_BY_ID uses att/{id} */
  is_inline             TINYINT DEFAULT 0, /* if uploaded with wysiwyg */
  is_image              TINYINT DEFAULT 0, /* 1 if this is supported image */

  fname                 VARCHAR(255) NOT NULL DEFAULT '',              /*original file name*/
  fsize                 INT DEFAULT 0,                   /*file size*/
  ext                   VARCHAR(16) NOT NULL DEFAULT '',                 /*extension*/
  iname                 VARCHAR(255) NOT NULL DEFAULT '',   /*attachment name*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under upload, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  KEY IX_att_table_name_item_id (table_name, item_id),
  FOREIGN KEY (att_categories_id) REFERENCES att_categories(id)
) DEFAULT CHARSET=utf8mb4;

/* link att files to table items*/
DROP TABLE IF EXISTS att_table_link;
CREATE TABLE att_table_link (
  id                    INT NOT NULL auto_increment,
  att_id                INT NOT NULL,

  table_name            VARCHAR(128) NOT NULL DEFAULT '',
  item_id               INT NOT NULL,

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under update*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY UX_att_table_link (table_name, item_id, att_id),
  FOREIGN KEY (att_id) REFERENCES att(id)
) DEFAULT CHARSET=utf8mb4;


DROP TABLE IF EXISTS users;
CREATE TABLE users (
  id                    INT NOT NULL auto_increment,

  email                 VARCHAR(128) NOT NULL DEFAULT '',
  pwd                   VARCHAR(255) NOT NULL DEFAULT '', -- hashed password
  access_level          TINYINT NOT NULL,                 -- 0 - visitor, 1 - usual user, 80 - moderator, 100 - admin

  iname                 AS CONCAT(fname,' ', lname) STORED, -- standard iname field, calculated from first+last name
  fname                 VARCHAR(32) NOT NULL DEFAULT '',
  lname                 VARCHAR(32) NOT NULL DEFAULT '',
  title                 VARCHAR(128) NOT NULL DEFAULT '',

  address1              VARCHAR(128) NOT NULL DEFAULT '',
  address2              VARCHAR(64) NOT NULL DEFAULT '',
  city                  VARCHAR(64) NOT NULL DEFAULT '',
  state                 VARCHAR(4) NOT NULL DEFAULT '',
  zip                   VARCHAR(16) NOT NULL DEFAULT '',
  phone                 VARCHAR(16) NOT NULL DEFAULT '',
  lang                  VARCHAR(16) NOT NULL DEFAULT 'en', -- user interface language

  ui_theme              TINYINT NOT NULL DEFAULT 0, -- 0--default theme
  ui_mode               TINYINT NOT NULL DEFAULT 0, -- 0--auto, 10-light, 20-dark

  date_format           TINYINT NOT NULL DEFAULT 0, -- 0-MM/DD/YYYY, 10-DD/MM/YYYY
  time_format           TINYINT NOT NULL DEFAULT 0, -- 0-12h, 10-24h
  timezone              VARCHAR(64) NOT NULL DEFAULT '', -- empty means auto; see /common/sel/timezone.sel

  idesc                 TEXT,
  att_id                INT NULL,                -- avatar

  login_time            TIMESTAMP,
  pwd_reset             VARCHAR(255) NULL,
  pwd_reset_time        TIMESTAMP NULL,

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY UX_users_email (email),
  FOREIGN KEY (att_id) REFERENCES att(id)
) DEFAULT CHARSET=utf8mb4;
INSERT INTO users (fname, lname, email, pwd, access_level)
VALUES ('Website','Admin','admin@admin.com',UUID(),100);

/*user cookies (for permanent sessions)*/
DROP TABLE IF EXISTS users_cookies;
CREATE TABLE users_cookies (
  cookie_id           VARCHAR(32) NOT NULL,      /*cookie id: time(secs)+rand(16)*/
  users_id            INT NOT NULL,

  add_time            TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

  PRIMARY KEY (cookie_id),
  FOREIGN KEY (users_id) REFERENCES users(id)
) DEFAULT CHARSET=utf8mb4;

/*Site Settings - special table for misc site settings*/
DROP TABLE IF EXISTS settings;
CREATE TABLE settings (
  id                    INT NOT NULL auto_increment,
  icat                  VARCHAR(64) NOT NULL DEFAULT '', /*settings category: ''-system, 'other' -site specific*/
  icode                 VARCHAR(64) NOT NULL DEFAULT '', /*settings internal code*/
  ivalue                TEXT, /*value*/

  iname                 VARCHAR(64) NOT NULL DEFAULT '', /*settings visible name*/
  idesc                 TEXT,                    /*settings visible description*/
  input                 TINYINT NOT NULL default 0,       /*form input type: 0-input, 10-textarea, 20-select, 21-select multi, 30-checkbox, 40-radio, 50-date, 60-number, 70-switch, 80-range, 90-credential*/
  allowed_values        TEXT,                    /*space-separated value|label options or key|value metadata, use &nbsp; for spaces*/

  is_user_edit          TINYINT DEFAULT 0,  /* if 1 - use can edit this value*/

  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY UX_settings_icode (icode),
  KEY IX_settings_icat (icat)
) DEFAULT CHARSET=utf8mb4;
INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc, allowed_values) VALUES
(1, 10, '', 'test', 'novalue', 'test settings', 'description', ''),
(1, 90, 'AI', 'OPENAI_API_KEY', '', 'OpenAI API Key', 'API key used by Assistant and LLM features.', ''),
(1, 70, 'AI', 'ASSISTANT_ENABLED', '0', 'Assistant Enabled', 'Set to 1 to enable the assistant UI and queued runs.', ''),
(1, 20, 'AI', 'ASSISTANT_VECTOR_MODE', 'auto', 'Assistant Vector Mode', 'Use auto, json, or native. Auto uses SQL Server native vectors when available.', 'auto|Auto json|JSON native|Native'),
(1, 0, 'AI', 'ASSISTANT_MODEL', 'gpt-5-mini', 'Assistant Model', 'Chat model used for assistant responses.', ''),
(1, 70, 'AI', 'ASSISTANT_MEMORY_ENABLED', '0', 'Assistant Memory Enabled', 'Set to 1 to save optional per-user assistant memory summaries.', ''),
(1, 60, 'AI', 'ASSISTANT_RUN_TIMEOUT_SECONDS', '120', 'Assistant Run Timeout Seconds', 'Maximum queued or processing time for UI-facing assistant responses before they fail and can be retried.', 'min|30 step|1'),
(1, 60, 'AI', 'ASSISTANT_MAX_FILES_PER_MESSAGE', '5', 'Assistant Max Files Per Message', 'Maximum number of files accepted with one assistant message.', 'min|1 step|1'),
(1, 60, 'AI', 'ASSISTANT_MAX_INDEXED_FILE_BYTES', '5242880', 'Assistant Max Indexed File Bytes', 'Maximum supported attachment size for queued indexing. Larger files remain attached but are not indexed.', 'min|1 step|1'),
(1, 60, 'AI', 'ASSISTANT_MAX_INDEX_CHARS', '200000', 'Assistant Max Index Characters', 'Maximum parsed characters indexed per document.', 'min|1 step|1'),
(1, 60, 'AI', 'ASSISTANT_MAX_INDEX_CHUNKS', '80', 'Assistant Max Index Chunks', 'Maximum embedding chunks indexed per document.', 'min|1 step|1');

/*Static pages*/
DROP TABLE IF EXISTS spages;
CREATE TABLE spages (
  id                    INT NOT NULL auto_increment,
  parent_id             INT NOT NULL DEFAULT 0,  /*parent page id*/

  url                   VARCHAR(255) NOT NULL DEFAULT '',      /*sub-url from parent page*/
  iname                 VARCHAR(64) NOT NULL DEFAULT '',       /*page name-title*/
  idesc                 TEXT,                          /*page contents, markdown*/
  head_att_id           INT NULL, /*optional head banner image*/

  idesc_left            TEXT,                          /*left sidebar content, markdown*/
  idesc_right           TEXT,                          /*right sidebar content, markdown*/
  meta_keywords         VARCHAR(255) NOT NULL DEFAULT '',      /*meta keywords*/
  meta_description      VARCHAR(255) NOT NULL DEFAULT '',      /*meta description*/

  pub_time              DATETIME,                               /*publish date-time*/
  template              VARCHAR(64),                           /*template to use, if not defined - default site template used*/
  prio                  INT NOT NULL DEFAULT 0,                 /*0-on insert, then =id, default order by prio asc,iname*/
  is_home               INT DEFAULT 0,                          /* 1 is for home page (non-deletable page*/
  redirect_url          VARCHAR(255) NOT NULL DEFAULT '',      /*if set - redirect to this url instead displaying page*/

  custom_css            TEXT,                          /*custom page css*/
  custom_js             TEXT,                          /*custom page js*/

  status                TINYINT NOT NULL DEFAULT 0,    /*0-ok, 10-not published, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  KEY IX_spages_parent_id (parent_id, prio),
  KEY IX_spages_url (url),
  FOREIGN KEY (head_att_id) REFERENCES att(id)
) DEFAULT CHARSET=utf8mb4;
--TRUNCATE TABLE spages;
INSERT INTO spages (parent_id, url, iname) VALUES
(0,'','Home') --1
,(0,'test-page','Test  page') --2
;
update spages set is_home=1 where id=1;



/*event types for log*/
DROP TABLE IF EXISTS events;
CREATE TABLE events (
  id                    INT NOT NULL auto_increment,
  icode                 VARCHAR(64) NOT NULL default '',

  iname                 VARCHAR(255) NOT NULL default '',
  idesc                 TEXT,

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY UX_events_icode (icode)
) DEFAULT CHARSET=utf8mb4;

/* log of all user-initiated events */
DROP TABLE IF EXISTS event_log;
CREATE TABLE event_log (
  id                    BIGINT NOT NULL auto_increment,
  events_id             INT NOT NULL,           /* event id */

  item_id               INT NOT NULL DEFAULT 0,           /*related id*/
  item_id2              INT NOT NULL DEFAULT 0,           /*related id (if another)*/

  iname                 VARCHAR(255) NOT NULL DEFAULT '', /*short description of what's happened or additional data*/

  records_affected      INT NOT NULL DEFAULT 0,
  fields                TEXT,       /*serialized json with related fields data (for history) in form {fieldname: data, fieldname: data}*/

  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,  /*date record added*/
  add_users_id          INT DEFAULT 0,                        /*user added record, 0 if sent by cron module*/

  PRIMARY KEY (id),
  KEY IX_event_log_events_id (events_id),
  KEY IX_event_log_item_id (item_id),
  KEY IX_event_log_item_id2 (item_id2),
  KEY IX_event_log_add_users_id (add_users_id),
  KEY IX_event_log_add_time (add_time),
  FOREIGN KEY (events_id) REFERENCES events(id)
) DEFAULT CHARSET=utf8mb4;

/*user custom views*/
DROP TABLE IF EXISTS user_views;
CREATE TABLE user_views (
  id                    INT NOT NULL auto_increment,
  icode                 VARCHAR(128) NOT NULL, --related screen url, ex: "/Admin/Demos"
  fields                TEXT, -- comma-separated list of fields to display, order kept

  iname                 VARCHAR(255) NOT NULL DEFAULT '', -- if empty - it's a "default" view
  is_system             TINYINT NOT NULL DEFAULT 0, -- 1 - system - visible for all
  is_shared             TINYINT NOT NULL DEFAULT 0, -- 1 if shared/published

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0, -- related user
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  KEY UX_user_views (add_users_id, icode, iname)
) DEFAULT CHARSET=utf8mb4;

/*user lists*/
DROP TABLE IF EXISTS user_lists;
CREATE TABLE user_lists (
  id                    INT NOT NULL auto_increment,
  entity                VARCHAR(128) NOT NULL, -- usually table name or base_url, ex: 'demos' or /Admin/Demos

  iname                 VARCHAR(255) NOT NULL,
  idesc                 TEXT, -- description

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0, -- related owner user
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  KEY IX_user_lists (add_users_id, entity)
) DEFAULT CHARSET=utf8mb4;

/*items linked to user lists */
DROP TABLE IF EXISTS user_lists_items;
CREATE TABLE user_lists_items (
  id                    INT NOT NULL auto_increment,
  user_lists_id         INT NOT NULL,
  item_id               INT NOT NULL, -- related item id, example demos.id

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0, -- related owner user
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  KEY UX_user_lists_items (user_lists_id, item_id),
  FOREIGN KEY (user_lists_id) REFERENCES user_lists(id)
) DEFAULT CHARSET=utf8mb4;

/*Custom menu items for sidebar*/
DROP TABLE IF EXISTS menu_items;
CREATE TABLE menu_items (
  id                    INT NOT NULL auto_increment,

  iname                 VARCHAR(64) NOT NULL default '',
  url                   VARCHAR(255) NOT NULL default '',  -- menu url
  icon                  VARCHAR(64) NOT NULL default '',   -- menu icon
  controller            VARCHAR(255) NOT NULL default '',  -- controller class name for UI highlighting
  access_level          TINYINT NOT NULL DEFAULT 0,         -- min access level

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 10-hidden, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id)
) DEFAULT CHARSET=utf8mb4;
-- INSERT INTO menu_items (iname, url, icon, controller) VALUES ('Test Menu Item', '/Admin/Demos', 'list-ul', 'AdminDemos');

/*Site Admin-managed custom reports*/
DROP TABLE IF EXISTS fwreports;
CREATE TABLE fwreports (
  id                    INT NOT NULL auto_increment,
  icode                 VARCHAR(50) NOT NULL,
  iname                 VARCHAR(255) NOT NULL default '',
  idesc                 TEXT,
  icon                  VARCHAR(64) NOT NULL DEFAULT '',
  access_level          TINYINT NOT NULL DEFAULT 80,
  sql_template          TEXT NOT NULL,
  params_json           TEXT,
  render_options_json   TEXT,

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY UX_fwreports_icode (icode)
) DEFAULT CHARSET=utf8mb4;

/* Knowledge base and assistant RAG */
DROP TABLE IF EXISTS kb_articles;
CREATE TABLE kb_articles (
  id                    INT NOT NULL auto_increment,
  icode                 VARCHAR(80) NOT NULL DEFAULT '',
  iname                 VARCHAR(255) NOT NULL DEFAULT '',
  idesc                 TEXT,
  content_markdown      MEDIUMTEXT NOT NULL,
  access_level          TINYINT NOT NULL DEFAULT 1,

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY UX_kb_articles_icode (icode),
  KEY IX_kb_articles_access (access_level, status)
) DEFAULT CHARSET=utf8mb4;

DROP TABLE IF EXISTS rag_sources;
CREATE TABLE rag_sources (
  id                    INT NOT NULL auto_increment,
  source_type           VARCHAR(64) NOT NULL DEFAULT '',
  source_key            VARCHAR(255) NOT NULL DEFAULT '',
  fwentities_id         INT NOT NULL DEFAULT 0,
  item_id               INT NOT NULL DEFAULT 0,
  att_id                INT NOT NULL DEFAULT 0,
  iname                 VARCHAR(255) NOT NULL DEFAULT '',
  url                   VARCHAR(1024) NOT NULL DEFAULT '',
  content_hash          VARCHAR(64) NOT NULL DEFAULT '',
  source_version        VARCHAR(64) NOT NULL DEFAULT '',
  acl_snapshot          TEXT,
  index_status          VARCHAR(32) NOT NULL DEFAULT 'pending',
  index_attempt_no      INT NOT NULL DEFAULT 0,
  queued_at             TIMESTAMP NULL,
  next_retry_at         TIMESTAMP NULL,
  last_indexed_at       TIMESTAMP NULL,
  last_error            TEXT,
  metadata_json         TEXT,

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY UX_rag_sources_source_key (source_key),
  KEY IX_rag_sources_queue (index_status, status, next_retry_at, queued_at, id),
  KEY IX_rag_sources_entity (fwentities_id, item_id, att_id, status)
) DEFAULT CHARSET=utf8mb4;

DROP TABLE IF EXISTS rag_chunks;
CREATE TABLE rag_chunks (
  id                    INT NOT NULL auto_increment,
  rag_sources_id        INT NOT NULL DEFAULT 0,
  fwentities_id         INT NOT NULL DEFAULT 0,
  item_id               INT NOT NULL DEFAULT 0,
  att_id                INT NOT NULL DEFAULT 0,
  source_type           VARCHAR(64) NOT NULL DEFAULT '',
  source_title          VARCHAR(255) NOT NULL DEFAULT '',
  source_url            VARCHAR(1024) NOT NULL DEFAULT '',
  chunk_index           INT NOT NULL DEFAULT 0,
  iname                 VARCHAR(255) NOT NULL DEFAULT '',
  idesc                 MEDIUMTEXT NOT NULL,
  page                  INT NOT NULL DEFAULT 0,
  section               VARCHAR(255) NOT NULL DEFAULT '',
  embedding_json        MEDIUMTEXT NOT NULL,
  embedding_norm        DOUBLE NOT NULL DEFAULT 0,
  embedding_dim         INT NOT NULL DEFAULT 0,
  embedding_model       VARCHAR(128) NOT NULL DEFAULT '',
  vector_backend        VARCHAR(32) NOT NULL DEFAULT '',
  metadata_json         TEXT,

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  KEY IX_rag_chunks_source (rag_sources_id, chunk_index, status),
  KEY IX_rag_chunks_entity (fwentities_id, item_id, att_id, status),
  KEY IX_rag_chunks_embedding (embedding_model, embedding_dim, status),
  KEY IX_rag_chunks_backend (vector_backend, status)
) DEFAULT CHARSET=utf8mb4;

DROP TABLE IF EXISTS assistant_threads;
CREATE TABLE assistant_threads (
  id                    INT NOT NULL auto_increment,
  icode                 VARCHAR(64) NOT NULL DEFAULT '',
  icode_share           VARCHAR(64) GENERATED ALWAYS AS (NULLIF(icode, '')) STORED,
  users_id              INT NULL,
  owner_token           VARCHAR(64) NOT NULL DEFAULT '',
  iname                 VARCHAR(255) NOT NULL DEFAULT '',
  provider_thread_id    VARCHAR(255) NOT NULL DEFAULT '',
  last_run_status       TINYINT NULL,
  last_message_at       TIMESTAMP NULL,

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY UX_assistant_threads_icode (icode_share),
  KEY IX_assistant_threads_icode (icode),
  KEY IX_assistant_threads_owner (users_id, owner_token, status, last_message_at)
) DEFAULT CHARSET=utf8mb4;

DROP TABLE IF EXISTS assistant_messages;
CREATE TABLE assistant_messages (
  id                    INT NOT NULL auto_increment,
  assistant_threads_id  INT NOT NULL,
  role                  VARCHAR(32) NOT NULL DEFAULT '',
  message_type          VARCHAR(32) NOT NULL DEFAULT '',
  preview_text          VARCHAR(700) NOT NULL DEFAULT '',
  content_markdown      MEDIUMTEXT NOT NULL,
  payload_json          MEDIUMTEXT,
  sources_json          MEDIUMTEXT,
  confidence            DOUBLE NULL,

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  KEY IX_assistant_messages_thread (assistant_threads_id, id, status),
  KEY IX_assistant_messages_role (assistant_threads_id, role, status)
) DEFAULT CHARSET=utf8mb4;

DROP TABLE IF EXISTS assistant_runs;
CREATE TABLE assistant_runs (
  id                    INT NOT NULL auto_increment,
  assistant_threads_id  INT NOT NULL,
  assistant_messages_id INT NOT NULL,
  result_messages_id    INT NULL,
  activity_logs_id      INT NULL,
  worker_id             VARCHAR(128) NOT NULL DEFAULT '',
  error_message         TEXT,
  clarification_json    MEDIUMTEXT,
  attempt_no            INT NOT NULL DEFAULT 0,
  claimed_at            TIMESTAMP NULL,
  started_at            TIMESTAMP NULL,
  completed_at          TIMESTAMP NULL,

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  KEY IX_assistant_runs_queue (status, id),
  KEY IX_assistant_runs_thread (assistant_threads_id, id)
) DEFAULT CHARSET=utf8mb4;

DROP TABLE IF EXISTS assistant_runs_events;
CREATE TABLE assistant_runs_events (
  id                    INT NOT NULL auto_increment,
  assistant_runs_id     INT NOT NULL,
  event_type            VARCHAR(32) NOT NULL DEFAULT '',
  content               TEXT,
  payload_json          MEDIUMTEXT,

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  KEY IX_assistant_runs_events_run (assistant_runs_id, id, status)
) DEFAULT CHARSET=utf8mb4;

DROP TABLE IF EXISTS assistant_memories;
CREATE TABLE assistant_memories (
  id                    INT NOT NULL auto_increment,
  users_id              INT NOT NULL,
  summary               MEDIUMTEXT,
  terminology_json      MEDIUMTEXT,
  preferences_json      MEDIUMTEXT,
  last_compacted_at     TIMESTAMP NULL,
  source_threads_id     INT NULL,

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY UX_assistant_memories_user (users_id)
) DEFAULT CHARSET=utf8mb4;

DROP TABLE IF EXISTS assistant_feedback;
CREATE TABLE assistant_feedback (
  id                    INT NOT NULL auto_increment,
  assistant_threads_id  INT NULL,
  assistant_runs_id     INT NULL,
  assistant_messages_id INT NULL,
  feedback_type         VARCHAR(32) NOT NULL DEFAULT '',
  comment               TEXT,

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  KEY IX_assistant_feedback_thread (assistant_threads_id, assistant_runs_id, assistant_messages_id)
) DEFAULT CHARSET=utf8mb4;

DROP TABLE IF EXISTS user_filters;
CREATE TABLE user_filters (
  id                    INT NOT NULL auto_increment,
  icode                 VARCHAR(128) NOT NULL, -- related screen, ex: GLOBAL[controller.action]

  iname                 VARCHAR(255) NOT NULL,
  idesc                 TEXT, -- json with filter data
  is_system             TINYINT NOT NULL DEFAULT 0, -- 1 - system - visible for all
  is_shared             TINYINT NOT NULL DEFAULT 0, -- 1 if shared/published

  status                TINYINT DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0, -- related owner user
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id)
) DEFAULT CHARSET=utf8mb4;

-- after this file - run lookups.sql
