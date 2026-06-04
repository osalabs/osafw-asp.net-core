## What changed
- Renamed the report runtime/base class to `FwReportsBase` and moved the `fwreports` table model to `osafw-app/App_Code/fw/FwReports.cs` as `FwReports`.
- Updated hardcoded reports, report helpers, controller/adapters, codegen, docs, and tests for the new type split.
- Added runtime custom-report SQL validation before preview/report execution, hardcoded-code collision validation, custom report run context, and a shared generic result table partial.
- Removed the unreleased `ALTER TABLE fwreports ADD icon` fallback from the custom reports SQL update.
- Follow-up feedback: staged the two report file moves through `git mv` so `git diff --cached -B -M` reports both as renames, and removed custom report `Code` / `Rows shown` labels from the run context.

## Scope reviewed
- Read required docs: `AGENTS.md`, `docs/agents/local_instructions.md`, `docs/README.md`, `docs/drafts/custom_reports_review.md`, `docs/reports.md`, `docs/naming.md`, `docs/templates.md`, and `docs/fw_upgrade_prompt.md`.
- Reviewed report runtime/controller/model/tests/templates and SQL update paths: `FwReportsBase`, `FwReports`, `FwCustomReport`, `AdminReportsController`, `SampleReport`, report templates, custom report SQL update, and `FwReportsTests`.
- Fresh SQL schemas already contained the final `fwreports` shape; only the unreleased additive SQL Server update fallback needed removal.

## Commands used / verification
- `rg -n "\bFwReportsModel\b" osafw-app osafw-tests docs README.md --glob '!osafw-tests/bin/**' --glob '!docs/agents/tasks/**' --glob '!docs/drafts/custom_reports_review.md'` - no active code/doc matches.
- `rg -n "\bFwReports\.(cleanupRepcode|createInstance|createHtml|createFile|format2ext|filterSessionKey|repcodeToClass|isHardcodedReport)" ...` - no active stale runtime-helper callers.
- `rg -n ":\s*FwReports\b|class .*: FwReports\b|preview_headers|preview_rows|preview_totals|has_preview_totals|cleanupIcode|normalizeForSave\([^)]*,|ALTER TABLE fwreports ADD icon" ...` - no active matches.
- `git diff --check` - passed.
- `dotnet build osafw-app/osafw-app.csproj` - failed only because IIS Express process 42252 locked `osafw-app/bin/Debug/net10.0/osafw-app.dll`.
- `dotnet build osafw-app/osafw-app.csproj -p:OutDir=artifacts/assistant_build/` - passed, 0 warnings/errors.
- `dotnet test` - failed only because the same locked app DLL blocked normal build output.
- `dotnet test osafw-tests/osafw-tests.csproj -p:OutDir=artifacts/assistant_test/` - compiled and ran; 483 passed, 8 failed in pre-existing unrelated dynamic/navigation/date/login tests.
- `dotnet test osafw-tests/osafw-tests.csproj -p:OutDir=artifacts/assistant_test/ --filter FullyQualifiedName~FwReportsTests` - passed, 29/29.
- CRLF byte scan over touched files - no lone LF found.
- Review loop: attempted code reviewer sub-agent, but it did not return after two 3-minute waits and was closed; performed local review using `docs/agents/code_reviewer.md` with no findings requiring another fix loop.
- Feedback pass: `git diff --cached -B -M --summary -- osafw-app/App_Code/fw/FwReports.cs osafw-app/App_Code/fw/FwReportsBase.cs osafw-app/App_Code/models/FwReportsModel.cs` reports `FwReportsModel.cs => fw/FwReports.cs` and `fw/FwReports.cs => FwReportsBase.cs` as 97% renames.
- Feedback pass: reran `dotnet test osafw-tests/osafw-tests.csproj -p:OutDir=artifacts/assistant_test/ --filter FullyQualifiedName~FwReportsTests` - passed, 29/29.

## Decisions - why
- Used existing `docs/CHANGELOG.md` rather than adding a root changelog because the repo already had a changelog under `docs/`.
- Recorded fixed review items here instead of editing `docs/drafts/custom_reports_review.md`, per the accepted plan.
- Kept Site Admin table access, render-option control, manual RBAC grants, and Site Admin bypass unchanged as intentional framework behavior.

## Pitfalls - fixes
- `docs/fw_upgrade_prompt.md` was Windows-encoded and failed `apply_patch`; rewrote that single file as UTF-8 with CRLF and ASCII punctuation.

## Risks / follow-ups
- Full suite currently has unrelated failures in `FwDynamicControllerTests.NextAction_*`, `ParsePageTests.parse_string_dateTest`, and `SecurityQuickFixTests.Login_UnsafeGourlFallsBackToDefault`; report-focused tests pass.
- Normal `bin/Debug` app build/test output is locked by IIS Express process 42252; isolated output builds are clean.
- Historical task summaries and ignored draft/security artifacts may still mention old report names; active source, tests, and public docs were updated.
- Plain `git status` still shows the path swap as added/deleted because `fw/FwReports.cs` remains in the final tree with different content; staged break-rewrite rename detection shows both history-preserving moves.

## Heuristics (keep terse)
- No stable heuristics, domain facts, or ADRs added; this was a scoped framework rename/fix.

## Testing instructions
- Use isolated output while the local IIS Express app is running: `dotnet build osafw-app/osafw-app.csproj -p:OutDir=artifacts/assistant_build/`.
- Run focused report regressions: `dotnet test osafw-tests/osafw-tests.csproj -p:OutDir=artifacts/assistant_test/ --filter FullyQualifiedName~FwReportsTests`.

## Reflection
The rename touched many call sites; targeted `rg` checks were more useful than broad manual inspection for catching stale public helper usage. The delegated reviewer did not complete in time, so future runs should set a shorter initial review timeout and fall back sooner when the diff is already locally verified. The encoding issue in `docs/fw_upgrade_prompt.md` slowed edits; keeping shared docs UTF-8 would avoid patch-tool fallback writes.
