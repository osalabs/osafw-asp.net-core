## What changed
- Fixed ParsePage date rendering so date-only values no longer get shifted across time zones during template formatting.
- Replaced ParsePage's culture-dependent date parsing with explicit SQL/current-user-format parsing only.
- Reset the whole session when a user changes date/time/timezone preferences in profile settings, then rebuilt the active user session data.
- Normalized `SampleReport` date filters through `DateUtils.Str2SQL` instead of ad hoc `DateTime.TryParse`.
- Added ParsePage regression tests covering date-only user strings, date-only `DateTime` values, and true UTC datetimes.
- Added session regression tests covering `Users.reloadSession(isClearSession: true)` behavior.

## Commands that worked (build/test/run)
- `dotnet vstest osafw-tests\bin\Debug\net10.0\osafw-tests.dll --Tests:parse_string_date_preserves_date_only_values_across_timezones`
- `dotnet vstest osafw-tests\bin\Debug\net10.0\osafw-tests.dll --TestCaseFilter:"ClassName=osafw.Tests.ParsePageTests"`
- `dotnet vstest osafw-tests\bin\Debug\net10.0\osafw-tests.dll --TestCaseFilter:"ClassName=osafw.Tests.ParsePageTests|ClassName=osafw.Tests.FwTests|ClassName=osafw.Tests.DateUtilsTests"`
- Visual Studio MCP `build_project` for `osafw-tests\osafw-tests.csproj`

## Pitfalls - fixes
- Playwright MCP failed to initialize its profile under `C:\Windows\System32\.playwright-mcp`; investigation proceeded via framework code path and regression tests.
- `dotnet test` hit sandbox/home and offline LibMan restore issues; used Visual Studio build plus `dotnet vstest` against the compiled test assembly instead.

## Decisions - why
- Fixed the root cause in `ParsePage` instead of special-casing reports because the same `<~value date>` formatter is used across report filters and date form inputs.
- Kept timezone conversion for real datetimes, but skipped it for date-only inputs so calendar dates survive round-trips unchanged.
- Removed culture-dependent `DateTime.TryParse` from shared date rendering paths to avoid silent MDY/DMY reinterpretation.
- Chose `reloadSession(isClearSession: true)` over a FW filter helper to keep the reset behavior local to user session management.
- Moved the profile success flash after `reloadSession` so it survives the session reset.

## Heuristics (keep terse)
- In ParsePage, treat date-only strings and `DateTimeKind.Unspecified` midnight values as calendar dates, not UTC datetimes.
- When date/time/timezone preferences change, reset the session and rebuild current user state rather than trying to reinterpret old user-formatted date strings.
