# ADR 20250908: Per-user Date/Time Formats and Timezone Handling

Date: 2025-09-08
Status: Accepted

## Context
- Read dates/times from database in SQL string formats (`YYYY-MM-DD`, `YYYY-MM-DD HH:mm:ss`) and treat them as the configured `appSettings.timezone_db` (`DateUtils.DATABASE_TZ`, default `UTC`).
- Defaults come from `appsettings.json` (`appSettings.date_format`, `time_format`, `timezone`, `timezone_db`).
## Decision
- Read dates/times from database in SQL string formats (`YYYY-MM-DD`, `YYYY-MM-DD HH:mm:ss`) and treat them as `DateUtils.DATABASE_TZ` (default `UTC`) timezone.
- Provide per-user overrides for:
  - Date format: `FW.userDateFormat` (backed by Session `date_format`)
  - Time format: `FW.userTimeFormat` (Session `time_format`)
  - Timezone: `FW.userTimezone` (Session `timezone`)
- Defaults come from `appsettings.json` (`appSettings.date_format`, `time_format`, `timezone`).
- ParsePage is configured per-request with these formats and `InputTimezone = DATABASE_TZ`, `OutputTimezone = FW.userTimezone`.
- Controllers/Models:
  - For display: `fw.formatUserDateTime(value)` or `DateUtils.SQL2Str(value, fw.userDateFormat, fw.userTimeFormat, fw.userTimezone)`
  - For saving user input: models call `FwModel.convertUserInput(item)` to convert `date`/`datetime` string fields into SQL formats based on the user’s formats.

## Rationale
- Removes ambiguity in date parsing.
- Centralizes conversion logic and reduces controller-level boilerplate.
- Keeps DB storage normalized (UTC) while still presenting local times to users.

## Consequences
- DB values are timezone-naive strings but treated as UTC (or `DATABASE_TZ`); all UI/API output should convert to the user’s timezone where appropriate.
- Developers must use `convertUserInput` when persisting form data and `formatUserDateTime` (or `SQL2Str`) for presentation.
- Timezone IDs must be valid Windows time zone IDs; invalid IDs are logged and fallback preserves the original `DateTime`.

## Alternatives Considered
- Storing `DateTimeOffset` in DB: increases complexity of schema/driver support and does not address user formatting preferences directly.
- Using server-local time: brittle and environment-dependent; complicates multi-region scenarios.

## References
- FW accessors and setup: `osafw-app/App_Code/fw/FW.cs`
- Input conversion: `osafw-app/App_Code/fw/FwModel.cs` (`convertUserInput`)
- Date helpers: `osafw-app/App_Code/fw/DateUtils.cs`
- User guide: `docs/datetime.md`
