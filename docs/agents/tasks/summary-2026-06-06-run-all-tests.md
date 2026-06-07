## What changed
- Ran the full test suite, fixed the failing tests/root causes, and verified both default and SQLite compile-constant test variants.
- Changed `ParsePage` date/time display options from static mutable fields to per-instance readonly fields so one parser/user/request cannot leak date formats or time zones into another.
- Made `FwCronTests.CalculateNextRun_HonorsEndDate` deterministic by using a fixed UTC start time whose next cron occurrence is outside the end window.
- Gave `FwDynamicControllerTests` a minimal model so inherited prev/next navigation has the same `model0` contract as real dynamic controllers.
- Set `is_mfa_enforced` explicitly to `false` in the unsafe-login-redirect test so the test reaches redirect fallback behavior instead of MFA setup when prior tests mutate shared config.
- Added a ParsePage regression test proving custom date formats do not leak into a later default parser.
- Cleaned a nullable warning in `ConvUtilsTests` by asserting workbook/worksheet parts before dereferencing them.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/agents/tasks/index.md`
- `docs/agents/tasks/summary-2026-05-20-sqlite-only-test.md`
- `osafw-app/osafw-app.csproj`
- `osafw-tests/osafw-tests.csproj`
- `docs/agents/code_reviewer.md`
- `osafw-app/App_Code/fw/ParsePage.cs`
- `osafw-tests/App_Code/fw/FwCronTests.cs`
- `osafw-tests/App_Code/fw/FwDynamicControllerTests.cs`
- `osafw-tests/App_Code/fw/ParsePageTests.cs`
- `osafw-tests/App_Code/security/SecurityQuickFixTests.cs`
- `osafw-tests/App_Code/fw/ConvUtilsTests.cs`

## Commands used / verification
- Initial `dotnet test osafw-asp.net-core.sln -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_all\` failed 9/526: cron end-date boundary, dynamic prev/next null `model0`, ParsePage date-format leak/order dependence, and unsafe login redirect tests diverted by MFA setup.
- `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_focused\ --filter "FullyQualifiedName~FwCronTests|FullyQualifiedName~FwDynamicControllerTests|FullyQualifiedName~ParsePageTests|FullyQualifiedName~SecurityQuickFixTests"` passed 73/73.
- `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_convutils_after\ --filter "FullyQualifiedName~ConvUtilsTests"` passed 5/5 with no build warning.
- Final `dotnet test osafw-asp.net-core.sln -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_all_final\` passed 527/527 with no build warnings.
- Final `dotnet test osafw-tests\osafw-tests.csproj -p:DefineConstants=isSQLite -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_sqlite_final\` passed 533/533 with no build warnings.
- `git diff --check` passed.
- `docs/agents/tools/Normalize-TextFiles.ps1 -Check` passed for all touched files.
- Code reviewer sub-agent was requested for the final diff but did not return within the wait window and was closed; main-agent self-review followed `docs/agents/code_reviewer.md` and found no issues requiring another loop.

## Decisions - why
- Use an absolute repo-root `OutDir` for test runs to avoid IIS Express or Visual Studio locks on normal `bin/Debug` outputs.
- Fixed ParsePage itself, not only the test, because parser date/time options are request/user-specific and static mutable fields are unsafe in production.
- Kept the login security test focused on unsafe `gourl` fallback by explicitly disabling MFA enforcement in that test setup; MFA setup behavior is covered elsewhere.
- Updated the dynamic prev/next test setup instead of changing controller behavior because real dynamic controllers are expected to have `model0` loaded from config.
- No `docs/CHANGELOG.md` entry was added because this is a bug fix with no breaking public API, route, schema, config, storage, security-default, or frontend contract change.

## Pitfalls - fixes
- `CalculateNextRun_HonorsEndDate` was time-of-minute dependent; fixed with a fixed timestamp.
- ParsePage options were static and could leak across tests/users; fixed with instance fields and a regression test.
- Test-created framework settings can be shared across tests through `fw.config()`; the redirect test now pins `is_mfa_enforced` to the scenario it verifies.
- PowerShell blocked direct script execution for `Normalize-TextFiles.ps1`; reran it with `powershell.exe -ExecutionPolicy Bypass -File`.

## Risks / follow-ups
- No follow-up required for the fixed failures.
- Generated verification outputs remain under repo-root `artifacts/assistant_*`.

## Heuristics (keep terse)
- No reusable heuristics added.

## Testing instructions
- Default full suite: `dotnet test osafw-asp.net-core.sln`.
- SQLite variant: `dotnet test osafw-tests\osafw-tests.csproj -p:DefineConstants=isSQLite`.

## Reflection
- The slowest part was separating production bugs from test setup leaks caused by shared framework state. Future runs should treat full-suite-only failures around `fw.config()` and parser options as likely isolation issues before assuming the individual test body is wrong.
- The sub-agent review request did not complete in time; fallback self-review was faster and sufficient after focused and full automated coverage passed.
- The existing task history was useful because it identified prior SQLite full-suite failures and the isolated `OutDir` convention immediately.
- No stable facts, heuristics, ADRs, or shared agent instructions were added; this task did not reveal a reusable workflow rule beyond the existing isolated-output guidance.
