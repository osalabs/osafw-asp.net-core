## What changed
- Addressed PR #273 review findings for assistant queue claiming, stale run recovery, claimed-run failure handling, shared-thread event exposure, feedback/run association, and share-code uniqueness across providers.
- Left member-level contact search unchanged because the user clarified it is an internal logged-user directory tool.
- Follow-up feedback: changed base `FwModel.update()` to return whether DB rows were affected, added cache cleanup for assistant direct conditional writes, and removed MySQL/SQLite update-script changes because those providers are not deployed yet.
- Added `docs/CHANGELOG.md` entry for the public `FwModel.update()` return-value behavior change.

## Scope reviewed
- `docs/agents/local_instructions.md`, `docs/README.md`, `docs/assistant.md`, `docs/db.md`, `docs/templates.md`, `docs/CHANGELOG.md`, and `docs/agents/code_reviewer.md`.
- Assistant runtime/model/template files under `osafw-app/App_Code/models/AI` and `osafw-app/App_Data/template/assistant/index/main.html`.
- Assistant SQL Server/MySQL/SQLite schema/update scripts, with edits only needed for MySQL/SQLite share-code uniqueness because SQL Server already had a filtered unique index.
- Assistant focused tests in `osafw-tests/App_Code/fw/AssistantFeatureTests.cs` and runtime status tests.
- Follow-up review covered `FwModel.update()` return semantics and confirmed MySQL/SQLite update scripts should remain unchanged.

## Commands used / verification
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~AssistantFeatureTests|FullyQualifiedName~AssistantRuntimeStatusTests" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_pr273_fixes_test\` - passed, 28/28.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_pr273_fixes_build\` - passed, 0 warnings/errors.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~AssistantFeatureTests|FullyQualifiedName~AssistantRuntimeStatusTests" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_pr273_fixes_test\` - passed again after normalization, 28/28.
- Code reviewer sub-agent found follow-up issues around stale recovery time source, duplicate share-code migration handling, feedback fallback integrity, and source-heavy tests; all were addressed in the main workspace.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~AssistantFeatureTests|FullyQualifiedName~AssistantRuntimeStatusTests" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_pr273_fixes_test\` - passed after reviewer fixes, 28/28.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_pr273_fixes_build\` - passed after reviewer fixes, 0 warnings/errors.
- Final pass after tightening message-free `run_id` validation: focused assistant tests passed 28/28; app build passed with 0 warnings/errors.
- `docs/agents/tools/Normalize-TextFiles.ps1 -Check ...` on touched files - passed.
- `git diff --check` - passed.
- Follow-up feedback pass: `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~AssistantFeatureTests|FullyQualifiedName~AssistantRuntimeStatusTests|FullyQualifiedName~FwModelLookupTests.Update_ReturnsWhetherDatabaseAffectedRows" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_pr273_fixes_test\` - passed, 29/29.
- Follow-up feedback pass: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_pr273_fixes_build\` - passed, 0 warnings/errors.

## Decisions - why
- Kept `assistant_runs` and `assistant_runs_events`; they remain distinct execution/queue/evidence stores and are not suitable for `activity_logs`.
- Used conditional update in the portable run-claim path to prevent duplicate workers from processing the same queued run.
- Kept direct `db.update()` only where assistant claiming needs an atomic `id + status=queued` predicate; added explicit cache cleanup after those direct writes.
- Changed `FwModel.update()` to return `false` when the DB reports no affected row, instead of always returning `true`.
- Added portable stale processing run recovery using `db.Now()` for the cutoff so claim and recovery use the database time source.
- Suppressed run events entirely for shared-thread reads and polling to match the materialized-content sharing contract.
- Resolved feedback run association server-side from `assistant_runs.result_messages_id`, reject message-based feedback when the run cannot be resolved, and validate message-free `run_id` against the owned thread before storing it.
- Added MySQL `icode_share` generated column plus a unique key in `fwdatabase.sql` because MySQL does not support SQL Server-style filtered indexes; SQLite from-scratch schema uses a partial unique index. MySQL/SQLite update scripts were intentionally left unchanged per user feedback.

## Pitfalls - fixes
- `apply_patch` wrote LF endings; ran `docs/agents/tools/Normalize-TextFiles.ps1` on touched files to restore CRLF.
- Existing local `osafw-app/appsettings.json` and `.jshintrc` were pre-existing unrelated worktree changes and were not touched.

## Risks / follow-ups
- MySQL generated-column syntax is covered by source/schema tests but was not executed against a live MySQL server in this pass.
- Source/schema contract tests still guard private queue and migration details where behavior-level coverage would require production test hooks; DTO/event suppression behavior is now covered directly.
- `FwModel.update()` returning affected-row truth is a framework behavior change; existing callers that ignored the result are unaffected, and callers that check it now get a meaningful not-updated signal.
- Breaking changelog entry added for the `FwModel.update()` return-value behavior change.

## Heuristics (keep terse)
- No reusable heuristics added.

## Testing instructions
- Build app with isolated output: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_pr273_fixes_build\`.
- Run focused assistant/base-model tests: `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~AssistantFeatureTests|FullyQualifiedName~AssistantRuntimeStatusTests|FullyQualifiedName~FwModelLookupTests.Update_ReturnsWhetherDatabaseAffectedRows" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_pr273_fixes_test\`.

## Reflection
- The review findings were precise enough to avoid a broad assistant rewrite. Future similar runs should preserve that bounded approach: fix the execution contracts and sharing/feedback data shapes directly, then add narrow regression checks.
- The line-ending helper was useful and should be run immediately after `apply_patch` on Windows repos with CRLF requirements.
- No stable framework facts, heuristics, or ADRs were added; the fixes are task-specific within the assistant PR.
