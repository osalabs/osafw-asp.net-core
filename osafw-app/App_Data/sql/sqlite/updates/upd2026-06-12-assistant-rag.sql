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

CREATE TABLE IF NOT EXISTS doc_chunks (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  fwentities_id         INTEGER NOT NULL DEFAULT 0,
  item_id               INTEGER NOT NULL DEFAULT 0,
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
CREATE INDEX IF NOT EXISTS IX_doc_chunks_entity ON doc_chunks (fwentities_id, item_id, status);
CREATE INDEX IF NOT EXISTS IX_doc_chunks_embedding ON doc_chunks (embedding_model, embedding_dim, status);
CREATE INDEX IF NOT EXISTS IX_doc_chunks_backend ON doc_chunks (vector_backend, status);

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

INSERT OR IGNORE INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc) VALUES
(1, 0, 'AI', 'OPENAI_API_KEY', '', 'OpenAI API Key', 'API key used by Assistant and LLM features.'),
(1, 0, 'AI', 'ASSISTANT_ENABLED', '0', 'Assistant Enabled', 'Set to 1 to enable the assistant UI and queued runs.'),
(1, 0, 'AI', 'ASSISTANT_VECTOR_MODE', 'auto', 'Assistant Vector Mode', 'Use auto, json, or native. Auto uses SQL Server native vectors when available.'),
(1, 0, 'AI', 'ASSISTANT_MODEL', 'gpt-5-mini', 'Assistant Model', 'Chat model used for assistant responses.'),
(1, 0, 'AI', 'ASSISTANT_MEMORY_ENABLED', '0', 'Assistant Memory Enabled', 'Set to 1 to save optional per-user assistant memory summaries.'),
(1, 0, 'AI', 'ASSISTANT_MAX_FILES_PER_MESSAGE', '5', 'Assistant Max Files Per Message', 'Maximum number of files accepted with one assistant message.'),
(1, 0, 'AI', 'ASSISTANT_MAX_INDEXED_FILE_BYTES', '5242880', 'Assistant Max Indexed File Bytes', 'Maximum supported attachment size for inline indexing. Larger files remain attached but are not indexed.'),
(1, 0, 'AI', 'ASSISTANT_MAX_INDEX_CHARS', '200000', 'Assistant Max Index Characters', 'Maximum parsed characters indexed per document.'),
(1, 0, 'AI', 'ASSISTANT_MAX_INDEX_CHUNKS', '80', 'Assistant Max Index Chunks', 'Maximum embedding chunks indexed per document.');
