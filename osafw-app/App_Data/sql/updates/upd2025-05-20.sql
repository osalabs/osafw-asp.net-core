-- for scheduled tasks
DROP TABLE IF EXISTS fwcron;
CREATE TABLE fwcron
(
  id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,

  icode                 NVARCHAR(128) NOT NULL DEFAULT '',      -- Internal job code (used in switch/logic)
  iname                 NVARCHAR(255) NOT NULL DEFAULT '',      -- Human-readable name/title of the job
  idesc                 NVARCHAR(MAX),                          -- Optional full description of the job

  cron                  NVARCHAR(255) NOT NULL,                 -- Cron expression string
  next_run              DATETIME2 NULL,                         -- When the job should run next (UTC)

  start_date            DATETIME2 NULL,                         -- When job becomes active (inclusive)
  end_date              DATETIME2 NULL,                         -- Optional: when job expires (exclusive)

  status                TINYINT NOT NULL DEFAULT 0,             -- Job status (0=Active, 10=Inactive, 20=Completed, 127=Deleted)

  add_time              DATETIME2 NOT NULL DEFAULT GETDATE(),   -- Timestamp when job was created
  add_users_id          INT DEFAULT 0,                          -- User ID who created the job (0 = system)

  upd_time              DATETIME2,                              -- Last update timestamp
  upd_users_id          INT DEFAULT 0                           -- User ID who last updated the job (0 = system)
);
CREATE UNIQUE INDEX UX_fwcron_icode ON fwcron (icode);
CREATE INDEX IX_fwcron_next_run ON fwcron (next_run);

INSERT INTO log_types (itype, icode, iname) VALUES (0, 'executed', 'Record Executed');