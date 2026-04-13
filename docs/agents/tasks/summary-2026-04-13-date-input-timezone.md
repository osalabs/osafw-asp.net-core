## What changed
- Fixed ParsePage date rendering so date-only values no longer get shifted across time zones during template formatting.
- Added ParsePage regression tests covering date-only user strings, date-only `DateTime` values, and true UTC datetimes.

## Commands that worked (build/test/run)
- `dotnet vstest osafw-tests\bin\Debug\net10.0\osafw-tests.dll --Tests:parse_string_date_preserves_date_only_values_across_timezones`
- `dotnet vstest osafw-tests\bin\Debug\net10.0\osafw-tests.dll --TestCaseFilter:"ClassName=osafw.Tests.ParsePageTests"`
- Visual Studio MCP `build_project` for `osafw-tests\osafw-tests.csproj`

## Pitfalls - fixes
- Playwright MCP failed to initialize its profile under `C:\Windows\System32\.playwright-mcp`; investigation proceeded via framework code path and regression tests.
- `dotnet test` hit sandbox/home and offline LibMan restore issues; used Visual Studio build plus `dotnet vstest` against the compiled test assembly instead.

## Decisions - why
- Fixed the root cause in `ParsePage` instead of special-casing reports because the same `<~value date>` formatter is used across report filters and date form inputs.
- Kept timezone conversion for real datetimes, but skipped it for date-only inputs so calendar dates survive round-trips unchanged.

## Heuristics (keep terse)
- In ParsePage, treat date-only strings and `DateTimeKind.Unspecified` midnight values as calendar dates, not UTC datetimes.
