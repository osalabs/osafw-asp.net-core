# ADR 20250908: Per-user Date/Time Formats and Timezone Handling

Date: 2025-09-08
Status: Accepted

## Context
- Databases store timestamps as SQL types (`date`, `datetime`) and each connection can have its own timezone configured via `appSettings.db.<name>.timezone` or autodetected at runtime.
- Defaults for user-facing formatting come from `appsettings.json` (`appSettings.date_format`, `time_format`, `timezone`).

## Decision
- Resolve the source timezone per database connection (cached for the lifetime of the app). Use optional `appsettings.db.<name>.timezone` overrides when provided, otherwise autodetect from the provider (fallback `UTC`).
- Provide per-user overrides for:
  - Date format: `FW.userDateFormat` (backed by Session `date_format`)
  - Time format: `FW.userTimeFormat` (Session `time_format`)
  - Timezone: `FW.userTimezone` (Session `timezone`)
- Defaults come from `appsettings.json` (`appSettings.date_format`, `time_format`, `timezone`).
- ParsePage is configured per-request with these formats; `InputTimezone` defaults to UTC while `OutputTimezone = FW.userTimezone`.
- Controllers/Models:
  - For display: `fw.formatUserDateTime(value)` converts internal UTC values to the user's timezone/format, but leaves date-only values on the same calendar day.
  - For saving user input: models convert `datetime` fields to UTC inside `FwModel.convertUserInput(item)`, parsing strings with the user's formats/timezone and normalizing `date` strings.
  - Dynamic `date`/`date_popup`/`date_combo` inputs are normalized to SQL `YYYY-MM-DD` so date-only fields stay calendar-stable even when backed by a `datetime` column.
- Field names ending in `_utc` explicitly declare UTC instant storage. The DB helper does not apply database timezone conversion to those fields.
- SQL Server `datetimeoffset` is supported as an offset-aware instant. Untyped framework rows/scalars normalize it into the existing UTC display pipeline, while typed DTOs may use `DateTimeOffset`.

## Rationale
- Removes ambiguity in date parsing.
- Centralizes conversion logic and reduces controller-level boilerplate.
- Keeps DB storage normalized to UTC while still presenting local times to users.
- Makes date-only fields behave as calendar values instead of time instants.

- DB values are normalized to UTC by `DB`; all UI/API output should convert to the user's timezone where appropriate.
- SQL `date` values and other explicit date-only inputs are treated as calendar values rather than instants in time.
- Models convert user `datetime` input to UTC before saving, and controllers should use `fw.formatUserDateTime` for presentation.
- Use `_utc` field names or `datetimeoffset` for fields that are already instants and should not be interpreted as database-local wall time.
- Timezone IDs must be valid Windows time zone IDs; invalid IDs are logged and fallback preserves the original `DateTime`.

## Alternatives Considered
- Making `DateTimeOffset` the default timestamp type: still rejected because most existing schemas and UI flows use `datetime2` plus user formatting preferences. Targeted `datetimeoffset` support is accepted for fields that need offset-aware instants.
- Using server-local time: brittle and environment-dependent; complicates multi-region scenarios.

## References
- FW accessors and setup: `osafw-app/App_Code/fw/FW.cs`
- Input conversion: `osafw-app/App_Code/fw/FwModel.cs` (`convertUserInput`)
- Dynamic date normalization: `osafw-app/App_Code/fw/FwDynamicController.cs`
- Date helpers: `osafw-app/App_Code/fw/DateUtils.cs`
- User guide: `docs/datetime.md`
