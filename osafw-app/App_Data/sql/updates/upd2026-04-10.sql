-- Cron updates

ALTER TABLE fwcron ADD last_run              DATETIME2 NULL;
ALTER TABLE fwcron ADD is_running            BIT NOT NULL DEFAULT 0;                 -- Job "currently running" flag. Avoid simultaneous runs.
GO

CREATE INDEX IX_fwcron_last_run ON fwcron (last_run);
GO

INSERT INTO log_types (itype, icode, iname) VALUES (0, 'cron_job_run_start', 'Cron Job Run Start');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'cron_job_run_end', 'Cron Job Run End');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'cron_job_manual_run_start', 'Cron Job Manual Run Start');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'cron_job_manual_run_end', 'Cron Job Manual Run End');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'cron_job_run_error', 'Cron Job Run Error');
GO

CREATE INDEX IX_activity_logs_fwentities_id_item_id ON activity_logs (fwentities_id, item_id, idate DESC, id DESC);
GO