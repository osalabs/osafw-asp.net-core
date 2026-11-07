-- Existing jobs retain their codes, recurrence, status and timestamps.
ALTER TABLE fwcron ADD COLUMN timezone TEXT NOT NULL DEFAULT 'UTC';
ALTER TABLE fwcron ADD COLUMN payload TEXT NULL;
ALTER TABLE fwcron ADD COLUMN run_requested_utc DATETIME NULL;
ALTER TABLE fwcron ADD COLUMN run_token TEXT NULL;
ALTER TABLE fwcron ADD COLUMN last_started_utc DATETIME NULL;
ALTER TABLE fwcron ADD COLUMN last_finished_utc DATETIME NULL;
ALTER TABLE fwcron ADD COLUMN last_result TEXT NULL;

INSERT OR IGNORE INTO fwentities (icode, iname) VALUES ('fwcron', 'Scheduled Tasks');
INSERT OR IGNORE INTO log_types (itype, icode, iname) VALUES (0, 'cron_event', 'Scheduled Task Event');
