-- Cron updates

EXEC sp_rename 'fwcron.next_run', 'next_run_utc', 'COLUMN';
EXEC sp_rename 'fwcron.start_date', 'start_date_utc', 'COLUMN';
EXEC sp_rename 'fwcron.end_date', 'end_date_utc', 'COLUMN';
GO

ALTER TABLE fwcron ADD last_run_utc          DATETIME2 NULL;
ALTER TABLE fwcron ADD is_log_run            BIT NOT NULL DEFAULT 0;                 -- Log start/end to activity logs; we don't want to log for every minute mailer job, for example
ALTER TABLE fwcron ADD is_log_run_result     BIT NOT NULL DEFAULT 0;                 -- Log job resuls to activity logs if needed
ALTER TABLE fwcron ADD is_running            BIT NOT NULL DEFAULT 0;                 -- Job "currently running" flag. Avoid simultaneous runs.
GO

INSERT INTO log_types (itype, icode, iname) VALUES (0, 'cron_job_run_start', 'Cron Job Run Start');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'cron_job_run_end', 'Cron Job Run End');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'cron_job_manual_run_start', 'Cron Job Manual Run Start');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'cron_job_manual_run_end', 'Cron Job Manual Run End');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'cron_job_run_error', 'Cron Job Run Error');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'cron_job_run_result', 'Cron Job Run Result');
GO

DROP INDEX IX_activity_logs_item_id ON activity_logs;
DROP INDEX IX_activity_logs_fwentities_id ON activity_logs;
CREATE INDEX IX_activity_logs_fwentities_id_item_id ON activity_logs (fwentities_id, item_id, idate DESC, id DESC) INCLUDE (status);
CREATE INDEX IX_activity_logs_fwentities_id_item_id_log_types_id ON activity_logs (fwentities_id, item_id, log_types_id, idate DESC, id DESC) INCLUDE (status);
GO