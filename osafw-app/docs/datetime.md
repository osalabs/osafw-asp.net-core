# Per-user Date/Time and Timezone Support

- Each database connection resolves its own timezone. Configure it under `appSettings.db.<name>.timezone` or omit it and let `DB` autodetect (cached per connection string, falls back to `UTC`).
- `DB` converts all datetime values from the database timezone to UTC on read and from UTC to the database timezone on write.
- Database values are read in SQL formats:
  - Date: `YYYY-MM-DD`
  - Datetime: `YYYY-MM-DD HH:mm:ss`
- On output, values can be converted to the user’s timezone and formatted with the user’s date/time formats.

## Global defaults (appsettings.json)
`appSettings` keys define defaults applied for anonymous users or when there’s no per-user override:
- `appSettings.date_format` – default date format id (see constants below). Example: `0` (MDY)
- `appSettings.time_format` – default time format id. Example: `0` (12h)
- `appSettings.timezone` – default timezone id. Example: `"UTC"`

These values are available at runtime via `fw.config("date_format")`, `fw.config("time_format")`, `fw.config("timezone")` and are copied into `fw.G`.

## Per-user overrides
On each request FW pulls user-specific settings from session, if present:
- `Session("date_format")`
- `Session("time_format")`
- `Session("timezone")`

Accessors:
- `FW.userDateFormat`
- `FW.userTimeFormat`
- `FW.userTimezone`

Set these session keys at login or on profile save to personalize formatting and conversions for a user.

## Constants and formats
DateUtils exposes constants and helpers:
- Formats
  - `DateUtils.DATE_FORMAT_MDY` = 0  ? `M/d/yyyy`
  - `DateUtils.DATE_FORMAT_DMY` = 10 ? `d/M/yyyy`
  - `DateUtils.TIME_FORMAT_12` = 0   ? `h:mm tt`
  - `DateUtils.TIME_FORMAT_24` = 10  ? `H:mm`
- Timezone
  - `DateUtils.TZ_UTC` = `"UTC"`
- Mapping helpers
  - `DateUtils.mapDateFormat(int)`
  - `DateUtils.mapTimeFormat(int)`
  - `DateUtils.mapTimeWithSecondsFormat(int)`

- `FW.formatUserDateTime(dbValue)` – takes a UTC `DateTime` (or SQL string) and returns a user-formatted string with timezone conversion from UTC to the user’s timezone.
- ParsePage templates are preconfigured from FW:
  - `DateFormat` = `mapDateFormat(userDateFormat)`
  - `DateFormatShort` = `DateFormat + " " + mapTimeFormat(userTimeFormat)`
  - `DateFormatLong` = `DateFormat + " " + mapTimeWithSecondsFormat(userTimeFormat)`
  - InputTimezone defaults to `UTC`
  - OutputTimezone = `fw.userTimezone`

## Converting user input for saving
Use `FwController.modelAddOrUpdate` plus `FwModel.convertUserInput(item)` before `add`/`update`. It converts human-entered strings while keeping internals in UTC:
- `date` fields → `YYYY-MM-DD` via `DateUtils.Str2SQL(str, fw.userDateFormat)`
- `datetime` fields → parsed with the user’s formats, converted from `fw.userTimezone` to UTC, and passed to DB as UTC `DateTime` values.

Notes:
- If a value is already a `DateTime` (UTC) or `DB.NOW`, it is left unchanged.
- If the string is already in SQL format, it is parsed as UTC and passed through.

## Timezone conversion utilities
- Resolved DB timezone is cached per connection (configured or autodetected).
- `DateUtils.convertTimezone(DateTime dt, string from_tz, string to_tz)` – low-level helper reused by models.

- Show a timestamp from DB:
  - C#: `ps["created_on"] = fw.formatUserDateTime(row["created_on"]);`
- Accept a date from a form and save:
  - Controller builds `item` from request, Model calls `convertUserInput(item)` to normalize and convert to UTC, then `add/update`.

## Choosing the user’s timezone
Timezones must match Windows time zone IDs (used by `TimeZoneInfo.FindSystemTimeZoneById`). Examples: `"UTC"`, `"Pacific Standard Time"`, `"Europe/Berlin"`.

If an invalid timezone is supplied, `DateUtils.convertTimezone` logs the issue and returns the original `DateTime`.

- Ensure `appsettings.json` has sensible defaults for `date_format`, `time_format`, `timezone`, and optional per-DB `timezone` overrides.
- Set session values to emulate a user preference and verify formatting on pages and API responses.

## Related source
- FW accessors and initialization: `App_Code/fw/FW.cs`
- Input conversion: `App_Code/fw/FwModel.cs` – `convertUserInput`
- Date helpers and constants: `App_Code/fw/DateUtils.cs`
