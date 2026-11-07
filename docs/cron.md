# Scheduled Tasks

Site Admins manage recurring application jobs and report emails at `/Admin/Cron`. Reports that support scheduling offer **Schedule email** beside **Email To**, and **Report schedules** opens the same task list filtered to that report.

## Enabling the runner

Apply the active provider's `upd2026-11-07-scheduled-tasks.sql` update and complete any existing-row UTC migration below before using the new runtime. The `isFwCronService` compile symbol registers the existing hosted worker; it remains opt-in. Without it the UI can manage definitions but cannot run tasks. IIS/application availability controls worker availability. Configure the application's hosting lifetime appropriately; this feature does not install a Windows service.

SQL Server is the primary provider. SQLite has matching fresh and additive schemas for embedded deployments and disposable tests. The claiming implementation requires SQL Server or SQLite; other providers are not supported by this runner.

## Creating a report schedule

Choose the report filters, then select **Schedule email**. Enter a name, recipient addresses, format, recurrence, and timezone. Recipients default to your email. PDF attachment is the default; HTML body is also available. Multiple schedules may use the same report with different filters and recipients.

Site Admins authorize distribution of one shared report. Recipients do not need application accounts; all addresses in the To list are visible to the recipients. Addresses are saved literally and do not follow user-profile email changes. Rendering uses the saved active Site Admin identity and normal report authorization. Reassign the rendering user explicitly if that account becomes unavailable.

Selected dates stay fixed. The scheduler never infers a relative period from a date range. Reports needing previous week/month should expose an explicit period selector and resolve it in their own filter logic. Saved parameters use the custom-report `name`, `type`, `value` shape; definitions/defaults remain with the report. Changing a parameter contract requires reviewing affected schedules. Sample preserves its declared sort fields. Custom reports use their report-defined SQL order; interactive column sorting is not part of their saved schedule.

## Time and execution

The recurrence uses its saved timezone. New schedules default to the current admin's resolved zone; old schedules retain UTC. List timestamps show the viewing admin's local time alongside UTC. Changing a user's preferences does not change a recurrence. Start is inclusive and end is exclusive.

All scheduling and execution instants are stored as UTC: `start_date_utc`, `end_date_utc`, `next_run_utc`, `run_requested_utc`, `last_started_utc`, and `last_finished_utc`. The `_utc` names use the framework's existing UTC field handling, independently of the database timezone. Raw SQL datetime parameters must also use `_utc` names (for example, `@now_utc`); see [date/time handling](datetime.md). Standard `add_time`/`upd_time` audit fields retain the shared framework convention.

The saved recurrence timezone still determines what “7 a.m.” means. Cronos handles its daylight-saving transitions; UTC storage preserves distinct instants even when a local clock hour repeats. No database-local DST rejection or pause is needed. Form bounds are entered in the viewing admin's timezone and converted to UTC; nonexistent local clock times remain invalid inputs.

**Run now** queues one request for the existing worker. Repeated requests while queued/running do not enqueue additional work. A paused task can be run manually without resuming its regular schedule. The worker uses the same database claim for timer and manual execution.

Report occurrences delayed more than five minutes are skipped, and the next future occurrence is calculated. Manual requests also expire after five minutes. The scheduler does not send a backlog after downtime. A simultaneous manual request and eligible scheduled occurrence are covered by one run.

## Results and recovery

The latest attempt has start/end timestamps and a small JSON result with common `status`, `message`, and optional task-specific `data`. Activity history retains execution and administrative events. Results are diagnostic summaries; do not store report bodies, secrets, large datasets, or temporary paths in them.

A successful mail submission means the mail server accepted the message, not that every inbox received it. A failed or interrupted send may already have reached recipients. Such outcomes require review rather than automatic resend.

An interrupted worker leaves its claim held. First establish that the original execution has stopped on all workers. Then use the Site Admin recovery action, which clears the observed claim and leaves the task paused. Check mail/provider logs before deliberately resending. No startup lock reset or automatic timeout takeover occurs.

## Application development

Keep existing application job codes and dispatch in `FwCron.runJobAction(TFwCron)`, and list selectable codes in `legacyCodes()`. The handler returns null for a generic success, or a dictionary with `message` and optional JSON-compatible `data`. The framework owns the result status. Throw `FwCronPauseException` for invalid task configuration or `FwCronUncertainException` when an external effect may have happened; their messages must be safe for display and activity logs. Report task codes use `report_<code>:<short NanoID>`, generated with `Utils.nanoid(10)` and protected by the unique index. This reserved family calls one shared report handler; each schedule remains an independent row.

Hardcoded reports opt in by overriding `isSchedulingSupported` and `schedulingParameters()` on `FwReportsBase`. Return the existing custom-report metadata shape with `name`, `type`, and optional `label`/`required`; see `models/Reports/Sample.cs`. Scheduling captures the effective values after normal validation. During replay, `is_scheduled_snapshot` distinguishes explicit blanks from ordinary interactive defaults; `scheduled_run_utc` provides the execution instant for report-owned relative periods. Custom reports use their existing parameter metadata. Preserve normal report validation, query limits, rendering templates, and access checks. Scheduling must not capture browser sessions, tokens, SQL definitions, result rows, or arbitrary request fields.

Use existing PDF/email layouts. HTML email links/images need recipient-accessible application URLs; PDF-only local file URLs are unsuitable for email images. Test mail routing continues through the framework mail API. Do not enable real sends during development verification.

## Upgrading existing tasks

Stop all old workers and keep task editing disabled until the schema, application code, and data agree. Back up the task definitions. Old binaries do not understand the new claims or report task codes.

The provider update adds nullable UTC columns and their next-run index. It deliberately leaves the old `start_date`, `end_date`, and `next_run` columns and values untouched. The application developer must migrate existing rows before enabling the new UI or worker:

- Convert `start_date` → `start_date_utc`, `end_date` → `end_date_utc`, and `next_run` → `next_run_utc` using the original database timezone and each date's applicable offset. Preserve nulls. If storage was already UTC, copy the instants without shifting them. Do not simply relabel local clock values as UTC or change the global database timezone.
- Resolve any ambiguous or invalid historical local values explicitly. A repeated local clock time does not contain enough information to infer the original instant. Where appropriate, recalculate the next future occurrence from the saved recurrence and bounds instead of migrating an overdue `next_run`.
- Update copied handlers, SQL, DTO properties, form fields, and template/config overrides to use the new names. Keep `timezone` as the recurrence zone. Check migrated bounds, next occurrence, local/UTC display, and one controlled run before resuming workers.

There is no automatic data conversion or compatibility reader. Starting before migration treats the new null bounds as unbounded and can initialize a different next occurrence. Remove obsolete columns/indexes only after the developer has verified the migration and rollback requirements. If an earlier version of this PR's update was registered in `fwupdates` (pending, failed, or applied), inspect its stored SQL and the actual schema. `FwUpdates.loadUpdates()` does not refresh an existing filename; applying a pending entry executes the previously stored SQL, not the latest file. Before startup, verify that all three UTC columns and `IX_fwcron_next_run_utc` exist. For a stale entry, use an application-owned upgrade to add any missing columns/index and migrate the rows; do not blindly rerun the SQLite update.

Before rollback, stop the upgraded worker and disable new report tasks. Account for UTC-to-legacy conversion if returning to an old runtime; retained legacy columns do not track later edits. Restoring a database does not undo emails already sent.
