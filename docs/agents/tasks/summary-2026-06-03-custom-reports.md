## What changed
- Added Site Admin-managed custom reports on top of the existing `/Admin/Reports` module.
- Added `fwreports` schema, model validation, `FwCustomReport` runtime adapter, generic report templates, management templates, and docs.
- Kept hardcoded report resolution first; custom reports are the fallback by active `fwreports.icode`.
- Applied feedback for report breadcrumbs/header actions, default `repN` code, Site Admin default access, form focus/help placement, preview rendering, optional icons, and generic numeric alignment/totals.
## Scope reviewed
- Existing `AdminReportsController`, `FwReports`, sample report templates, report tests, DB helper parameter/limit behavior, RBAC resource checks, SQL schema scripts, and docs map.
- FPF draft was used only as planning discipline input; no project-facing docs mention it.
- Feedback pass also reviewed shared page-header/breadcrumb templates, report list layout, local SQL Server update path, Visual Studio launch behavior, and Playwright smoke flows on `https://localhost:44315`.
## Commands used / verification
- `dotnet build osafw-app\osafw-app.csproj` failed because IIS Express locked `bin\Debug\net10.0\osafw-app.dll`.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build\` passed with 0 warnings/errors after final changes.
- `dotnet build osafw-app\osafw-app.csproj` passed with 0 warnings/errors after stopping IIS Express and rebuilding normal VS output.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FwReportsTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_build\` passed: 18 tests.
- Full `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_build\` compiled and ran but failed 8 existing/environment-sensitive tests outside reports: dynamic prev/next null setup, ParsePage date format, and login MFA redirect expectations.
- Applied `osafw-app\App_Data\sql\updates\upd2026-06-03-custom-reports.sql` to local SQL Server `demo` via `sqlcmd` so Playwright could test the added `icon` column.
- Playwright MCP smoke: logged in locally, verified `/Admin/Reports/new` breadcrumbs/default code/name focus/access/icon/help, preview rendering for new and edit forms, `/Admin/Reports/test1` view header/breadcrumb/Edit action, run table numeric alignment, and `/Admin/Reports` custom section create button placement.
- `git diff --check` passed.
- CRLF check passed for all files changed in this task.
## Decisions - why
- Custom reports use a `FwCustomReport` adapter instead of duplicating report rendering/export logic in the controller.
- `FwReports.createInstance()` now resolves hardcoded classes first, then active `fwreports` rows, so existing URLs and helper APIs keep working.
- SQL templates are limited to one SELECT/CTE-shaped statement with bound `@params`; writes/procs/DDL/multiple statements are rejected centrally.
- Parameter JSON is normalized on save so auto-detected placeholders have stable metadata.
- RBAC resources use the existing `{icode}Report` convention; grants remain manual.
- `AdminReportsController` now allows logged-in users to reach the module; individual hardcoded/custom reports enforce their own access and RBAC resources.
- New custom reports default to Site Admin access because report authorship starts with SQL and can be lowered intentionally after review.
- Generic totals are intentionally conservative: numeric columns align right, but identifier/status/config fields are excluded from footer sums.
## Pitfalls - fixes
- Normal app build output was locked by IIS Express; used isolated output per repo guidance.
- Initial template patch needed new directories; created only the required report template directories.
- A missing `gear-fill` icon partial was replaced with existing `gear`.
- Edit POST to a missing report code was tightened to return not found instead of creating a new report.
- Runtime row limits were moved into `DB.arrayp(..., maxRows)` as a separate overload to cap CTE reads without breaking existing test doubles that override the original method.
- Local review found and fixed the old controller-level RBAC gate requiring `AdminReports` grants in addition to report-specific grants.
- Final local review found no remaining issues worth another loop; review loop can stop.
- Preview Run returned form state from `SaveAction`, so parser originally looked for `/admin/reports/save`; fixed by setting `_basedir` to `/admin/reports/showform` on form responses.
- Preview form actions now post to `/Admin/Reports/new` and `/Admin/Reports/{icode}/edit`, keeping browser location aligned with the create/edit screen after preview.
- IIS Express had to be stopped before rebuilding normal VS output; isolated builds alone did not update the live browser smoke target.
- Generated build artifacts were removed after verification.
## Risks / follow-ups
- CTE queries are capped at the reader level, but SQL-level limiting is only added automatically for simple `SELECT`; admins should still include provider-specific limits for large CTEs to reduce database work.
- Full test suite has unrelated failures in this environment; focused report tests passed.
- Browser/UI smoke used the local SQL Server `demo` DB after applying the additive update script.
## Heuristics (keep terse)
- No shared heuristics added.
## Testing instructions
- Apply `osafw-app/App_Data/sql/updates/upd2026-06-03-custom-reports.sql` before using the UI against an existing SQL Server DB.
- Run `dotnet build osafw-app/osafw-app.csproj -p:OutDir=artifacts/assistant_build/` if normal output is locked.
- Run `dotnet test osafw-tests/osafw-tests.csproj --filter FwReportsTests -p:OutDir=artifacts/assistant_test_build/`.
## Reflection
The main slowdown was distinguishing template hot-reload from stale compiled C# in the VS-hosted IIS Express app. Future browser smoke after C# edits should stop IIS Express, run a normal project build, then launch without debugging before trusting the page. A reviewer sub-agent was requested for the feedback diff but did not finish before shutdown, so the final review used `docs/agents/code_reviewer.md` locally and found no remaining issues worth another loop. No recurring instruction change is recommended yet; this task is too feature-specific.
