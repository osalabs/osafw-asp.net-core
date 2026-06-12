IF OBJECT_ID(N'dbo.kb_articles', N'U') IS NULL
BEGIN
  CREATE TABLE kb_articles (
    id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
    icode                 NVARCHAR(80) NOT NULL DEFAULT '',
    iname                 NVARCHAR(255) NOT NULL DEFAULT '',
    idesc                 NVARCHAR(MAX),
    content_markdown      NVARCHAR(MAX) NOT NULL DEFAULT '',
    access_level          TINYINT NOT NULL DEFAULT 1,

    status                TINYINT NOT NULL DEFAULT 0,
    add_time              DATETIME2 NOT NULL DEFAULT getdate(),
    add_users_id          INT DEFAULT 0,
    upd_time              DATETIME2,
    upd_users_id          INT DEFAULT 0
  );

  CREATE UNIQUE INDEX UX_kb_articles_icode ON kb_articles(icode);
  CREATE INDEX IX_kb_articles_access ON kb_articles(access_level, status);
END
GO

IF OBJECT_ID(N'dbo.doc_chunks', N'U') IS NULL
BEGIN
  CREATE TABLE doc_chunks (
    id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
    fwentities_id         INT NOT NULL DEFAULT 0,
    item_id               INT NOT NULL DEFAULT 0,
    chunk_index           INT NOT NULL DEFAULT 0,
    iname                 NVARCHAR(255) NOT NULL DEFAULT '',
    idesc                 NVARCHAR(MAX) NOT NULL DEFAULT '',
    page                  INT NOT NULL DEFAULT 0,
    section               NVARCHAR(255) NOT NULL DEFAULT '',
    embedding_json        NVARCHAR(MAX) NOT NULL,
    embedding_norm        FLOAT NOT NULL DEFAULT 0,
    embedding_dim         INT NOT NULL DEFAULT 0,
    embedding_model       NVARCHAR(128) NOT NULL DEFAULT '',
    vector_backend        NVARCHAR(32) NOT NULL DEFAULT '',
    metadata_json         NVARCHAR(MAX),

    status                TINYINT NOT NULL DEFAULT 0,
    add_time              DATETIME2 NOT NULL DEFAULT getdate(),
    add_users_id          INT DEFAULT 0,
    upd_time              DATETIME2,
    upd_users_id          INT DEFAULT 0
  );

  CREATE INDEX IX_doc_chunks_entity ON doc_chunks(fwentities_id, item_id, status);
  CREATE INDEX IX_doc_chunks_embedding ON doc_chunks(embedding_model, embedding_dim, status);
  CREATE INDEX IX_doc_chunks_backend ON doc_chunks(vector_backend, status);
END
GO

IF OBJECT_ID(N'dbo.assistant_threads', N'U') IS NULL
BEGIN
  CREATE TABLE assistant_threads (
    id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
    icode                 NVARCHAR(64) NOT NULL DEFAULT '',
    users_id              INT NULL,
    owner_token           NVARCHAR(64) NOT NULL DEFAULT '',
    iname                 NVARCHAR(255) NOT NULL DEFAULT '',
    provider_thread_id    NVARCHAR(255) NOT NULL DEFAULT '',
    last_run_status       TINYINT NULL,
    last_message_at       DATETIME2 NULL,

    status                TINYINT NOT NULL DEFAULT 0,
    add_time              DATETIME2 NOT NULL DEFAULT getdate(),
    add_users_id          INT DEFAULT 0,
    upd_time              DATETIME2,
    upd_users_id          INT DEFAULT 0
  );

  CREATE UNIQUE INDEX UX_assistant_threads_icode ON assistant_threads(icode) WHERE icode <> '';
  CREATE INDEX IX_assistant_threads_owner ON assistant_threads(users_id, owner_token, status, last_message_at);
END
GO

IF OBJECT_ID(N'dbo.assistant_messages', N'U') IS NULL
BEGIN
  CREATE TABLE assistant_messages (
    id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
    assistant_threads_id  INT NOT NULL,
    role                  NVARCHAR(32) NOT NULL DEFAULT '',
    message_type          NVARCHAR(32) NOT NULL DEFAULT '',
    preview_text          NVARCHAR(700) NOT NULL DEFAULT '',
    content_markdown      NVARCHAR(MAX) NOT NULL DEFAULT '',
    payload_json          NVARCHAR(MAX),
    sources_json          NVARCHAR(MAX),
    confidence            FLOAT NULL,

    status                TINYINT NOT NULL DEFAULT 0,
    add_time              DATETIME2 NOT NULL DEFAULT getdate(),
    add_users_id          INT DEFAULT 0,
    upd_time              DATETIME2,
    upd_users_id          INT DEFAULT 0
  );

  CREATE INDEX IX_assistant_messages_thread ON assistant_messages(assistant_threads_id, id, status);
  CREATE INDEX IX_assistant_messages_role ON assistant_messages(assistant_threads_id, role, status);
END
GO

IF OBJECT_ID(N'dbo.assistant_runs', N'U') IS NULL
BEGIN
  CREATE TABLE assistant_runs (
    id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
    assistant_threads_id  INT NOT NULL,
    assistant_messages_id INT NOT NULL,
    result_messages_id    INT NULL,
    activity_logs_id      INT NULL,
    worker_id             NVARCHAR(128) NOT NULL DEFAULT '',
    error_message         NVARCHAR(MAX),
    clarification_json    NVARCHAR(MAX),
    attempt_no            INT NOT NULL DEFAULT 0,
    claimed_at            DATETIME2 NULL,
    started_at            DATETIME2 NULL,
    completed_at          DATETIME2 NULL,

    status                TINYINT NOT NULL DEFAULT 0,
    add_time              DATETIME2 NOT NULL DEFAULT getdate(),
    add_users_id          INT DEFAULT 0,
    upd_time              DATETIME2,
    upd_users_id          INT DEFAULT 0
  );

  CREATE INDEX IX_assistant_runs_queue ON assistant_runs(status, id);
  CREATE INDEX IX_assistant_runs_thread ON assistant_runs(assistant_threads_id, id);
END
GO

IF OBJECT_ID(N'dbo.assistant_runs_events', N'U') IS NULL
BEGIN
  CREATE TABLE assistant_runs_events (
    id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
    assistant_runs_id     INT NOT NULL,
    event_type            NVARCHAR(32) NOT NULL DEFAULT '',
    content               NVARCHAR(MAX),
    payload_json          NVARCHAR(MAX),

    status                TINYINT NOT NULL DEFAULT 0,
    add_time              DATETIME2 NOT NULL DEFAULT getdate(),
    add_users_id          INT DEFAULT 0,
    upd_time              DATETIME2,
    upd_users_id          INT DEFAULT 0
  );

  CREATE INDEX IX_assistant_runs_events_run ON assistant_runs_events(assistant_runs_id, id, status);
END
GO

IF OBJECT_ID(N'dbo.assistant_memories', N'U') IS NULL
BEGIN
  CREATE TABLE assistant_memories (
    id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
    users_id              INT NOT NULL,
    summary               NVARCHAR(MAX),
    terminology_json      NVARCHAR(MAX),
    preferences_json      NVARCHAR(MAX),
    last_compacted_at     DATETIME2 NULL,
    source_threads_id     INT NULL,

    status                TINYINT NOT NULL DEFAULT 0,
    add_time              DATETIME2 NOT NULL DEFAULT getdate(),
    add_users_id          INT DEFAULT 0,
    upd_time              DATETIME2,
    upd_users_id          INT DEFAULT 0
  );

  CREATE UNIQUE INDEX UX_assistant_memories_user ON assistant_memories(users_id);
END
GO

IF OBJECT_ID(N'dbo.assistant_feedback', N'U') IS NULL
BEGIN
  CREATE TABLE assistant_feedback (
    id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
    assistant_threads_id  INT NULL,
    assistant_runs_id     INT NULL,
    assistant_messages_id INT NULL,
    feedback_type         NVARCHAR(32) NOT NULL DEFAULT '',
    comment               NVARCHAR(MAX),

    status                TINYINT NOT NULL DEFAULT 0,
    add_time              DATETIME2 NOT NULL DEFAULT getdate(),
    add_users_id          INT DEFAULT 0,
    upd_time              DATETIME2,
    upd_users_id          INT DEFAULT 0
  );

  CREATE INDEX IX_assistant_feedback_thread ON assistant_feedback(assistant_threads_id, assistant_runs_id, assistant_messages_id);
END
GO

INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc)
SELECT 1, 0, 'AI', 'OPENAI_API_KEY', '', 'OpenAI API Key', 'API key used by Assistant and LLM features.'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE icode='OPENAI_API_KEY');
GO

INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc)
SELECT 1, 0, 'AI', 'ASSISTANT_ENABLED', '0', 'Assistant Enabled', 'Set to 1 to enable the assistant UI and queued runs.'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE icode='ASSISTANT_ENABLED');
GO

INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc)
SELECT 1, 0, 'AI', 'ASSISTANT_VECTOR_MODE', 'auto', 'Assistant Vector Mode', 'Use auto, json, or native. Auto uses SQL Server native vectors when available.'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE icode='ASSISTANT_VECTOR_MODE');
GO

INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc)
SELECT 1, 0, 'AI', 'ASSISTANT_MODEL', 'gpt-5-mini', 'Assistant Model', 'Chat model used for assistant responses.'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE icode='ASSISTANT_MODEL');
GO

INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc)
SELECT 1, 0, 'AI', 'ASSISTANT_MEMORY_ENABLED', '0', 'Assistant Memory Enabled', 'Set to 1 to save optional per-user assistant memory summaries.'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE icode='ASSISTANT_MEMORY_ENABLED');
GO

INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc)
SELECT 1, 0, 'AI', 'ASSISTANT_MAX_FILES_PER_MESSAGE', '5', 'Assistant Max Files Per Message', 'Maximum number of files accepted with one assistant message.'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE icode='ASSISTANT_MAX_FILES_PER_MESSAGE');
GO

INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc)
SELECT 1, 0, 'AI', 'ASSISTANT_MAX_INDEXED_FILE_BYTES', '5242880', 'Assistant Max Indexed File Bytes', 'Maximum supported attachment size for inline indexing. Larger files remain attached but are not indexed.'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE icode='ASSISTANT_MAX_INDEXED_FILE_BYTES');
GO

INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc)
SELECT 1, 0, 'AI', 'ASSISTANT_MAX_INDEX_CHARS', '200000', 'Assistant Max Index Characters', 'Maximum parsed characters indexed per document.'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE icode='ASSISTANT_MAX_INDEX_CHARS');
GO

INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc)
SELECT 1, 0, 'AI', 'ASSISTANT_MAX_INDEX_CHUNKS', '80', 'Assistant Max Index Chunks', 'Maximum embedding chunks indexed per document.'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE icode='ASSISTANT_MAX_INDEX_CHUNKS');
GO
