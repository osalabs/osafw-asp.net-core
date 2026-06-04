## What changed
- Added Site Admin-managed custom reports on top of the existing `/Admin/Reports` module.
- Added `fwreports` schema, model validation, `FwCustomReport` runtime adapter, generic report templates, management templates, and docs.
- Kept hardcoded report resolution first; custom reports are the fallback by active `fwreports.icode`.
- Applied feedback for report breadcrumbs/header actions, default `repN` code, Site Admin default access, form focus/help placement, preview routing, optional icons, report count badges, and generic numeric alignment/totals.
- Moved custom report form Help examples into templates, removed controller-built form/help state, and changed Preview Run to reroute through `ShowFormAction`.
- Applied follow-up feedback for report breadcrumbs without View/Edit labels, inline title count badges, title icons, custom report sort headers, preview count badge/totals, and clearer `Edit Custom Report` action text.
- Applied the latest feedback for edit breadcrumbs linking back to the report run screen, no-param custom report autorun links, `Modify Report` action styling/placement, icon input prepend, expanded parameter Help, model-based lookup sources, and local seeded reports for UI coverage.
- Applied the latest report index and runtime UI feedback: responsive multi-column report lists, client-side fuzzy report filtering, right-aligned Modify Report action, Bootstrap-styled result table header/footer/dividers, `Totals` footer label, split lookup parameter types, `.sel` template lookups, and custom report calendar loading.
- Applied the latest follow-up feedback: removed the redundant onload wrapper, restored Bootstrap list-group styling while keeping report columns, and handled custom report SQL execution errors inline for preview and run screens with Site Admin-only detail.
- Applied this follow-up feedback: report list items now keep independent rounded Bootstrap borders inside columns and use standard hover feedback, date-only values are accepted for custom `datetime` params as midnight, `Modify Report` is hidden in print media, and the totals label moved from C# into templates.
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
- Fourth follow-up isolated build `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\` passed with 0 warnings/errors.
- Fourth follow-up focused test initially hit a locked app intermediate from IIS Express, then passed after stopping IIS Express: `dotnet test osafw-tests\osafw-tests.csproj --filter FwReportsTests -p:OutDir=artifacts\assistant_test_build\` passed 22 tests with 1 existing nullable warning in `ConvUtilsTests.cs`.
- Fourth follow-up normal build `dotnet build osafw-app\osafw-app.csproj` passed with 0 warnings/errors, then the app was relaunched through Visual Studio without debugging.
- Fourth follow-up Playwright MCP smoke verified `/Admin/Reports` 3-column layout on wide viewport, fuzzy filter `testrep` leaving only `Test 1 report`, `/Admin/Reports/test1?dofilter=1` right-side Modify Report action and table header/body/footer styling, `/Admin/Reports/Sample?dofilter=1` hardcoded table styling, split lookup filters for table/model/sql/template sources, custom report calendar loading, and updated Help examples.
- Fifth follow-up focused test `dotnet test osafw-tests\osafw-tests.csproj --filter FwReportsTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\` passed: 23 tests, 1 existing nullable warning in `ConvUtilsTests.cs`.
- Fifth follow-up normal build `dotnet build osafw-app\osafw-app.csproj` passed with 0 warnings/errors after stopping IIS Express.
- Fifth follow-up Playwright MCP smoke verified invalid SQL preview stays on `/Admin/Reports/new` and shows an inline detailed alert, invalid SQL run screen shows the detailed alert without the results table, `/Admin/Reports` renders custom reports as 3 Bootstrap list-group columns, fuzzy filter `testrep` visually leaves only `Test 1 report`, and the temporary invalid test report was soft-deleted through the UI.
- Fifth follow-up review loop: reviewer sub-agent timed out twice and was closed; local review using `docs/agents/code_reviewer.md` found no remaining issues worth another loop.
- Cleanup follow-up stopped the stray direct-run `dotnet`/`osafw-app` processes that were locking `bin\Debug\net10.0\osafw-app.exe`, then used Visual Studio MCP `build_project` and `debugger_launch_without_debugging` to build and restart the startup project through VS/IIS Express.
- Sixth follow-up focused test `dotnet test osafw-tests\osafw-tests.csproj --filter FwReportsTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\` passed: 25 tests, 1 existing nullable warning in `ConvUtilsTests.cs`.
- Sixth follow-up Visual Studio MCP `build_project` for `osafw-app` completed with `FailedProjects: 0`.
- Sixth follow-up Playwright MCP smoke verified `/Admin/Reports` visual columns with rounded bordered action items, Bootstrap hover background change, the provided `qa_all_params` parameter shape without the stale XSS token no longer produces `Invalid date for @to_datetime`, `To Time` renders as `6/17/2026 12:00 AM`, and `Modify Report` computes `display:none` under print media.
- Sixth follow-up review loop: independent reviewer sub-agent found no issues and said the review loop can stop.
- `git diff --check` passed.
- CRLF check passed for all files changed in this task.
- Follow-up review loop: reviewer sub-agent timed out and was closed; local review using `docs/agents/code_reviewer.md` found no remaining issues worth another loop.
- Second follow-up review loop: reviewer sub-agent timed out and was closed; local review using `docs/agents/code_reviewer.md` found and fixed descending-sort empty-value ordering, then focused build/tests passed again.
- Third follow-up review loop: local review using `docs/agents/code_reviewer.md` found no remaining issues worth another loop; independent reviewer sub-agent also found no issues and said the review loop can stop.
- Fourth follow-up review loop: local review found no issues; independent reviewer sub-agent also found no blocking correctness/security/template-contract regressions.
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
- Later feedback wanted `Modify Report` back on the right side of the same header row, so the report-specific header returned to the standard flex-grow title plus auto-margin metadata layout.
- IIS Express had to be stopped before rebuilding normal VS output; isolated builds alone did not update the live browser smoke target.
- Generated build artifacts were removed after verification.
- Combined `if`/`unless` on the custom report include did not suppress the no-rows results block after an execution error; added an explicit `is_report_results_visible` flag for normal result rendering.
- Bootstrap `.list-group` is flex by default, so CSS columns did not apply until the report-specific list container was changed to `display:block` while retaining `list-group-item` styling.
- Manual IIS Express launch from `.vs` config failed without Visual Studio's launcher environment variables; used `dotnet run --no-build --urls https://localhost:44315;http://localhost:57921` for the final browser smoke.
- Report list columns and Bootstrap list groups conflict if one list-group spans multiple CSS columns; giving each item Bootstrap `rounded border mb-2 list-group-item-action` utilities preserves column flow and per-card borders with standard hover behavior.
- Custom report datetime params originally required a full time string; accepting date-only input as midnight matches the datepicker behavior seen in the report filter UI.
## Risks / follow-ups
- CTE queries are capped at the reader level, but SQL-level limiting is only added automatically for simple `SELECT`; admins should still include provider-specific limits for large CTEs to reduce database work.
- Full test suite has unrelated failures in this environment; focused report tests passed.
- Browser/UI smoke used the local SQL Server `demo` DB after applying the additive update script.
- Local UI smoke also applied an ignored agent-artifact seed script that created or updated 12 active custom reports in `demo`; this data is for verification, not shared schema/data setup.
- The final browser smoke temporarily left the app running via a hidden `dotnet run --no-build` process on port 44315 because the VS-only IIS Express launcher environment was unavailable from the shell; that process was later stopped and the app relaunched through Visual Studio MCP.
## Heuristics (keep terse)
- No shared heuristics added.
## Testing instructions
- Apply `osafw-app/App_Data/sql/updates/upd2026-06-03-custom-reports.sql` before using the UI against an existing SQL Server DB.
- Run `dotnet build osafw-app/osafw-app.csproj -p:OutDir=artifacts/assistant_build/` if normal output is locked.
- Run `dotnet test osafw-tests/osafw-tests.csproj --filter FwReportsTests -p:OutDir=artifacts/assistant_test/`.
- For app rebuild/restart, prefer Visual Studio MCP `build_project` and `debugger_launch_without_debugging`; do not start a separate unmanaged `dotnet run` on the VS app port.
## Reflection
The main slowdown was distinguishing template hot-reload from stale compiled C# in the VS-hosted IIS Express app. Future browser smoke after C# edits should prefer Visual Studio MCP build/restart, using `debugger_stop` only when VS reports an active debug session, and should not start an unmanaged `dotnet run` process on the same VS port. A reviewer sub-agent was useful in prior passes but timed out in this round; the local fallback review was enough for the narrow diff. No recurring instruction change is recommended yet; this task is too feature-specific.
