-- Stop workers first. Developers must migrate legacy schedule dates to UTC before restart.
-- Old columns and values remain untouched; see docs/CHANGELOG.md (2026-11-07).
ALTER TABLE fwcron ADD COLUMN next_run_utc DATETIME NULL;
ALTER TABLE fwcron ADD COLUMN start_date_utc DATETIME NULL;
ALTER TABLE fwcron ADD COLUMN end_date_utc DATETIME NULL;
CREATE INDEX IX_fwcron_next_run_utc ON fwcron (next_run_utc);
ALTER TABLE fwcron ADD COLUMN timezone TEXT NOT NULL DEFAULT 'UTC';
ALTER TABLE fwcron ADD COLUMN payload TEXT NULL;
ALTER TABLE fwcron ADD COLUMN run_requested_utc DATETIME NULL;
ALTER TABLE fwcron ADD COLUMN run_token TEXT NULL;
ALTER TABLE fwcron ADD COLUMN last_started_utc DATETIME NULL;
ALTER TABLE fwcron ADD COLUMN last_finished_utc DATETIME NULL;
ALTER TABLE fwcron ADD COLUMN last_result TEXT NULL;

INSERT OR IGNORE INTO fwentities (icode, iname) VALUES ('fwcron', 'Scheduled Tasks');
INSERT OR IGNORE INTO log_types (itype, icode, iname) VALUES (0, 'cron_event', 'Scheduled Task Event');
