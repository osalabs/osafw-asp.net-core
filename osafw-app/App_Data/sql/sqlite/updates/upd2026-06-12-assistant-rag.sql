CREATE TABLE IF NOT EXISTS kb_articles (
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
CREATE UNIQUE INDEX IF NOT EXISTS UX_kb_articles_icode ON kb_articles (icode);
CREATE INDEX IF NOT EXISTS IX_kb_articles_access ON kb_articles (access_level, status);

CREATE TABLE IF NOT EXISTS rag_sources (
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
CREATE UNIQUE INDEX IF NOT EXISTS UX_rag_sources_source_key ON rag_sources (source_key);
CREATE INDEX IF NOT EXISTS IX_rag_sources_queue ON rag_sources (index_status, status, queued_at, id);
CREATE INDEX IF NOT EXISTS IX_rag_sources_entity ON rag_sources (fwentities_id, item_id, att_id, status);

CREATE TABLE IF NOT EXISTS rag_chunks (
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
CREATE INDEX IF NOT EXISTS IX_rag_chunks_source ON rag_chunks (rag_sources_id, chunk_index, status);
CREATE INDEX IF NOT EXISTS IX_rag_chunks_entity ON rag_chunks (fwentities_id, item_id, att_id, status);
CREATE INDEX IF NOT EXISTS IX_rag_chunks_embedding ON rag_chunks (embedding_model, embedding_dim, status);
CREATE INDEX IF NOT EXISTS IX_rag_chunks_backend ON rag_chunks (vector_backend, status);

CREATE TABLE IF NOT EXISTS assistant_threads (
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
CREATE INDEX IF NOT EXISTS IX_assistant_threads_icode ON assistant_threads (icode);
CREATE INDEX IF NOT EXISTS IX_assistant_threads_owner ON assistant_threads (users_id, owner_token, status, last_message_at);

CREATE TABLE IF NOT EXISTS assistant_messages (
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
CREATE INDEX IF NOT EXISTS IX_assistant_messages_thread ON assistant_messages (assistant_threads_id, id, status);
CREATE INDEX IF NOT EXISTS IX_assistant_messages_role ON assistant_messages (assistant_threads_id, role, status);

CREATE TABLE IF NOT EXISTS assistant_runs (
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
CREATE INDEX IF NOT EXISTS IX_assistant_runs_queue ON assistant_runs (status, id);
CREATE INDEX IF NOT EXISTS IX_assistant_runs_thread ON assistant_runs (assistant_threads_id, id);

CREATE TABLE IF NOT EXISTS assistant_runs_events (
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
CREATE INDEX IF NOT EXISTS IX_assistant_runs_events_run ON assistant_runs_events (assistant_runs_id, id, status);

CREATE TABLE IF NOT EXISTS assistant_memories (
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
CREATE UNIQUE INDEX IF NOT EXISTS UX_assistant_memories_user ON assistant_memories (users_id);

CREATE TABLE IF NOT EXISTS assistant_feedback (
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
CREATE INDEX IF NOT EXISTS IX_assistant_feedback_thread ON assistant_feedback (assistant_threads_id, assistant_runs_id, assistant_messages_id);
