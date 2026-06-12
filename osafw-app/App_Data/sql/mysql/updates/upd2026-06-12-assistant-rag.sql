CREATE TABLE IF NOT EXISTS kb_articles (
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

CREATE TABLE IF NOT EXISTS doc_chunks (
  id                    INT NOT NULL auto_increment,
  fwentities_id         INT NOT NULL DEFAULT 0,
  item_id               INT NOT NULL DEFAULT 0,
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
  KEY IX_doc_chunks_entity (fwentities_id, item_id, status),
  KEY IX_doc_chunks_embedding (embedding_model, embedding_dim, status),
  KEY IX_doc_chunks_backend (vector_backend, status)
) DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS assistant_threads (
  id                    INT NOT NULL auto_increment,
  icode                 VARCHAR(64) NOT NULL DEFAULT '',
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
  KEY IX_assistant_threads_icode (icode),
  KEY IX_assistant_threads_owner (users_id, owner_token, status, last_message_at)
) DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS assistant_messages (
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

CREATE TABLE IF NOT EXISTS assistant_runs (
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

CREATE TABLE IF NOT EXISTS assistant_runs_events (
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

CREATE TABLE IF NOT EXISTS assistant_memories (
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

CREATE TABLE IF NOT EXISTS assistant_feedback (
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
