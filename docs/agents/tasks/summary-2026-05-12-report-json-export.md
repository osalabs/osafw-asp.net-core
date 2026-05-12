## What changed
- Added internal report JSON export support for `f[format]=json`.
- JSON report requests now run the report data path even when `dofilter`/`is_run` is not passed.

## Scope reviewed
- Reviewed `AdminReportsController.ShowAction`, `FwReports.render`, existing report templates, sample report data flow, and report tests.

## Commands used / verification
- `dotnet test osafw-tests/osafw-tests.csproj --filter FwReportsTests --no-restore -p:LibraryRestore=false -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\`
- `dotnet build osafw-app/osafw-app.csproj --no-restore -p:LibraryRestore=false -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build\`
- Code reviewer sub-agent reviewed the task diff and found no issues; after the final compatibility tweak, a second reviewer pass also found no issues.

## Decisions - why
- Returned the same `ps` payload that report templates receive so API callers can access `report_code`, `f`, `filter`, `count`, `list_rows`, and report-specific calculated values consistently.
- Kept `.json` out of the download dropdown per request.

## Pitfalls - fixes
- `f[format]` was previously read after the run decision, so a direct API call with only `f[format]=json` would not populate report rows. The controller now treats JSON format as a run request.

## Risks / follow-ups
- JSON exposes the raw report row keys and any extra `ps` values each report prepares; that matches an internal API-style contract but should be considered when adding sensitive report fields.

## Heuristics (keep terse)
- None added.

## Testing instructions
- Automated coverage: `FwReportsTests` covers JSON extension mapping and JSON response payload/content type.
- Build verification: app project build with LibMan restore disabled and isolated output.

## Reflection
- No shared docs, domain facts, heuristics, or ADRs were added. The change is a small extension of existing report format handling, and the task summary captures the API payload decision.
