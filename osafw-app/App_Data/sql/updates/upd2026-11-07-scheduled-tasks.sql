-- Existing jobs retain their codes, recurrence, status and timestamps.
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
