-- Stop workers first. Developers must migrate legacy schedule dates to UTC before restart.
-- Old columns and values remain untouched; see docs/CHANGELOG.md (2026-11-07).
IF COL_LENGTH('fwcron', 'next_run_utc') IS NULL
BEGIN
    ALTER TABLE fwcron ADD
        next_run_utc DATETIME2 NULL,
        start_date_utc DATETIME2 NULL,
        end_date_utc DATETIME2 NULL;
    CREATE INDEX IX_fwcron_next_run_utc ON fwcron (next_run_utc);
END
GO

IF COL_LENGTH('fwcron', 'timezone') IS NULL
BEGIN
    ALTER TABLE fwcron ADD
        timezone NVARCHAR(64) NOT NULL CONSTRAINT DF_fwcron_timezone DEFAULT 'UTC',
        payload NVARCHAR(MAX) NULL,
        run_requested_utc DATETIME2 NULL,
        run_token NVARCHAR(32) NULL,
        last_started_utc DATETIME2 NULL,
        last_finished_utc DATETIME2 NULL,
        last_result NVARCHAR(MAX) NULL;
END
GO

INSERT INTO fwentities (icode, iname)
SELECT 'fwcron', 'Scheduled Tasks'
WHERE NOT EXISTS (SELECT 1 FROM fwentities WHERE icode = 'fwcron');

INSERT INTO log_types (itype, icode, iname)
SELECT 0, 'cron_event', 'Scheduled Task Event'
WHERE NOT EXISTS (SELECT 1 FROM log_types WHERE icode = 'cron_event');
