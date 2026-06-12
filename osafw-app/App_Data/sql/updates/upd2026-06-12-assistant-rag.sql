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

IF OBJECT_ID(N'dbo.rag_sources', N'U') IS NULL
BEGIN
  CREATE TABLE rag_sources (
    id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
    source_type           NVARCHAR(64) NOT NULL DEFAULT '',
    source_key            NVARCHAR(255) NOT NULL DEFAULT '',
    fwentities_id         INT NOT NULL DEFAULT 0,
    item_id               INT NOT NULL DEFAULT 0,
    att_id                INT NOT NULL DEFAULT 0,
    iname                 NVARCHAR(255) NOT NULL DEFAULT '',
    url                   NVARCHAR(1024) NOT NULL DEFAULT '',
    content_hash          NVARCHAR(64) NOT NULL DEFAULT '',
    source_version        NVARCHAR(64) NOT NULL DEFAULT '',
    acl_snapshot          NVARCHAR(MAX),
    index_status          NVARCHAR(32) NOT NULL DEFAULT 'pending',
    queued_at             DATETIME2 NULL,
    last_indexed_at       DATETIME2 NULL,
    last_error            NVARCHAR(MAX),
    metadata_json         NVARCHAR(MAX),

    status                TINYINT NOT NULL DEFAULT 0,
    add_time              DATETIME2 NOT NULL DEFAULT getdate(),
    add_users_id          INT DEFAULT 0,
    upd_time              DATETIME2,
    upd_users_id          INT DEFAULT 0
  );

  CREATE UNIQUE INDEX UX_rag_sources_source_key ON rag_sources(source_key);
  CREATE INDEX IX_rag_sources_queue ON rag_sources(index_status, status, queued_at, id);
  CREATE INDEX IX_rag_sources_entity ON rag_sources(fwentities_id, item_id, att_id, status);
END
GO

IF OBJECT_ID(N'dbo.rag_chunks', N'U') IS NULL
BEGIN
  CREATE TABLE rag_chunks (
    id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
    rag_sources_id        INT NOT NULL DEFAULT 0,
    fwentities_id         INT NOT NULL DEFAULT 0,
    item_id               INT NOT NULL DEFAULT 0,
    att_id                INT NOT NULL DEFAULT 0,
    source_type           NVARCHAR(64) NOT NULL DEFAULT '',
    source_title          NVARCHAR(255) NOT NULL DEFAULT '',
    source_url            NVARCHAR(1024) NOT NULL DEFAULT '',
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

  CREATE INDEX IX_rag_chunks_source ON rag_chunks(rag_sources_id, chunk_index, status);
  CREATE INDEX IX_rag_chunks_entity ON rag_chunks(fwentities_id, item_id, att_id, status);
  CREATE INDEX IX_rag_chunks_embedding ON rag_chunks(embedding_model, embedding_dim, status);
  CREATE INDEX IX_rag_chunks_backend ON rag_chunks(vector_backend, status);
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
