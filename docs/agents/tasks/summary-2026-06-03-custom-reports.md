## What changed
- Added Site Admin-managed custom reports on top of the existing `/Admin/Reports` module.
- Added `fwreports` schema, model validation, `FwCustomReport` runtime adapter, generic report templates, management templates, and docs.
- Kept hardcoded report resolution first; custom reports are the fallback by active `fwreports.icode`.
- Applied feedback for report breadcrumbs/header actions, default `repN` code, Site Admin default access, form focus/help placement, preview routing, optional icons, report count badges, and generic numeric alignment/totals.
- Moved custom report form Help examples into templates, removed controller-built form/help state, and changed Preview Run to reroute through `ShowFormAction`.
- Applied follow-up feedback for report breadcrumbs without View/Edit labels, inline title count badges, title icons, custom report sort headers, preview count badge/totals, and clearer `Edit Custom Report` action text.
- Applied the latest feedback for edit breadcrumbs linking back to the report run screen, no-param custom report autorun links, `Modify Report` action styling/placement, icon input prepend, expanded parameter Help, model-based lookup sources, and local seeded reports for UI coverage.
## Scope reviewed
- Existing `AdminReportsController`, `FwReports`, sample report templates, report tests, DB helper parameter/limit behavior, RBAC resource checks, SQL schema scripts, and docs map.
- Feedback pass also reviewed shared page-header/breadcrumb templates, report list layout, local SQL Server update path, Visual Studio launch behavior, and Playwright smoke flows on `https://localhost:44315`.
## Commands used / verification
- `dotnet build osafw-app\osafw-app.csproj` failed because IIS Express locked `bin\Debug\net10.0\osafw-app.dll`.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build\` passed with 0 warnings/errors after final changes.
- `dotnet build osafw-app\osafw-app.csproj` passed with 0 warnings/errors after stopping IIS Express and rebuilding normal VS output.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FwReportsTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_build\` passed: 18 tests.
- Follow-up build `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\` passed with 0 warnings/errors.
- Follow-up focused test `dotnet test osafw-tests\osafw-tests.csproj --filter FwReportsTests -p:OutDir=artifacts\assistant_test_build\` passed: 18 tests, 1 existing nullable warning in `ConvUtilsTests.cs`.
- Follow-up normal build `dotnet build osafw-app\osafw-app.csproj` passed with 0 warnings/errors after stopping detached IIS Express and relaunching through Visual Studio without debugging.
- Second follow-up build `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\` passed with 0 warnings/errors.
- Second follow-up focused test `dotnet test osafw-tests\osafw-tests.csproj --filter FwReportsTests -p:OutDir=artifacts\assistant_test_build\` passed: 19 tests.
- Second follow-up normal build `dotnet build osafw-app\osafw-app.csproj` passed with 0 warnings/errors after stopping detached IIS Express and relaunching through Visual Studio without debugging.
- Full `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_build\` compiled and ran but failed 8 existing/environment-sensitive tests outside reports: dynamic prev/next null setup, ParsePage date format, and login MFA redirect expectations.
- Applied `osafw-app\App_Data\sql\updates\upd2026-06-03-custom-reports.sql` to local SQL Server `demo` via `sqlcmd` so Playwright could test the added `icon` column.
- Playwright MCP smoke: logged in locally, verified `/Admin/Reports/new` breadcrumbs/default code/name focus/access/icon/help, preview rendering for new and edit forms, `/Admin/Reports/test1` view header/breadcrumb/Edit action, run table numeric alignment, and `/Admin/Reports` custom section create button placement.
- Follow-up Playwright MCP smoke verified `/Admin/Reports/test1?dofilter=1` title row with separate record-count badge and inline Edit button, `/Admin/Reports` Create New Report beside the Custom Reports heading, `/Admin/Reports/new` Icon under Access Level and Preview above Help, `/Admin/Reports/test1/edit` preview staying on the edit URL, and `/Admin/Reports/Sample?dofilter=1` using a separate count badge instead of count text in the title.
- Second follow-up Playwright MCP smoke verified custom and hardcoded report breadcrumbs without View/Edit text, title icon before the custom report title, inline title count badges, `Edit Custom Report` button text, Custom Reports list icon rendering through Bootstrap Icons, generated custom report column sorting via `f[sortby]`/`f[sortdir]`, and Preview count/totals footer with a metric query.
- Third follow-up Playwright MCP smoke verified `/Admin/Reports/new` breadcrumb/focus/defaults/help, `/Admin/Reports/rep1/edit` breadcrumb report-name link and icon prepend, `/Admin/Reports` custom report count/icon rendering/autorun URLs, `/Admin/Reports/test1?dofilter=1` title icon/count/Modify Report button/totals/sorting, parameter-heavy generated filters including `model:Users`, edit Preview Run totals above Help, render options row limiting, and hardcoded Sample breadcrumb/title behavior.
- Third follow-up final build `dotnet build osafw-app\osafw-app.csproj` passed with 0 warnings/errors after stopping IIS Express.
- Third follow-up focused test `dotnet test osafw-tests\osafw-tests.csproj --filter FwReportsTests -p:OutDir=artifacts\assistant_test_build\` passed: 21 tests, 1 existing nullable warning in `ConvUtilsTests.cs`.
- `git diff --check` passed.
- CRLF check passed for all files changed in this task.
- Follow-up review loop: reviewer sub-agent timed out and was closed; local review using `docs/agents/code_reviewer.md` found no remaining issues worth another loop.
- Second follow-up review loop: reviewer sub-agent timed out and was closed; local review using `docs/agents/code_reviewer.md` found and fixed descending-sort empty-value ordering, then focused build/tests passed again.
- Third follow-up review loop: local review using `docs/agents/code_reviewer.md` found no remaining issues worth another loop; independent reviewer sub-agent also found no issues and said the review loop can stop.
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
- Runtime row limits were moved into `DB.arrayp(..., limit)` as a separate overload to cap CTE reads without breaking existing test doubles that override the original method.
- Local review found and fixed the old controller-level RBAC gate requiring `AdminReports` grants in addition to report-specific grants.
- Final local review found no remaining issues worth another loop; review loop can stop.
- Preview Run returned form state from `SaveAction`, so parser originally looked for `/admin/reports/save`; fixed first with a template-directory override, then replaced with `routeRedirect(FW.ACTION_SHOW_FORM)` so preview uses the normal form route without `_basedir`.
- Form and delete actions now use report URL templates instead of controller-supplied URL values.
- Custom report sorting is intentionally applied after the stored SQL returns rows, so it stays within the validated result shape and avoids rewriting Site Admin-authored SQL.
- Local review fixed custom report descending sort so null/empty values remain last instead of moving to the top after descending reversal.
- The report header metadata area inherited shared `margin-left:auto`; fixed the report-specific header with `ms-0` so `Modify Report` sits next to the title without changing shared form/list headers.
- IIS Express had to be stopped before rebuilding normal VS output; isolated builds alone did not update the live browser smoke target.
- Generated build artifacts were removed after verification.
## Risks / follow-ups
- CTE queries are capped at the reader level, but SQL-level limiting is only added automatically for simple `SELECT`; admins should still include provider-specific limits for large CTEs to reduce database work.
- Full test suite has unrelated failures in this environment; focused report tests passed.
- Browser/UI smoke used the local SQL Server `demo` DB after applying the additive update script.
- Local UI smoke also applied an ignored agent-artifact seed script that created or updated 12 active custom reports in `demo`; this data is for verification, not shared schema/data setup.
## Heuristics (keep terse)
- No shared heuristics added.
## Testing instructions
- Apply `osafw-app/App_Data/sql/updates/upd2026-06-03-custom-reports.sql` before using the UI against an existing SQL Server DB.
- Run `dotnet build osafw-app/osafw-app.csproj -p:OutDir=artifacts/assistant_build/` if normal output is locked.
- Run `dotnet test osafw-tests/osafw-tests.csproj --filter FwReportsTests -p:OutDir=artifacts/assistant_test_build/`.
## Reflection
The main slowdown was distinguishing template hot-reload from stale compiled C# in the VS-hosted IIS Express app. Future browser smoke after C# edits should stop IIS Express, run a normal project build, then launch without debugging before trusting the page. A reviewer sub-agent was requested for the feedback diff but timed out and was closed, so the final review used `docs/agents/code_reviewer.md` locally and found no remaining issues worth another loop. No recurring instruction change is recommended yet; this task is too feature-specific.
