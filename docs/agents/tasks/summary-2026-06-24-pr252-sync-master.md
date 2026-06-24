## What changed
Synced PR #252 (`patch-fwcron`) with current `master` on local branch `pr-252-sync` and resolved all merge conflicts.
- Kept current `master` DB timezone/UTC normalization and paging helper contracts instead of the older PR-local `convertParamValue` path.
- Kept PR #252 activity-log pagination support and `FwEntities.ICODE_CRON`, while preserving master system-user handling and current activity-log UI shaping.
- Combined cron startup gates so hosted fwcron requires `isFwCronService` and `appSettings.is_cron_enabled=true`; kept master Assistant worker registration.
- Kept `CronExpressionDescriptor` because `FwCron` uses it for human cron descriptions; kept master conditional package references.
- Simplified Dev Configure DB timezone display back to the current DB API contract and removed the older direct SQL probe.
- Added a `docs/CHANGELOG.md` breaking-upgrade note for the combined cron compile/runtime gate.

## Scope reviewed
- PR metadata from GitHub: base `master`, head `patch-fwcron`, mergeable false.
- Local checkout state before sync: clean `master`; switched to local `pr-252-sync` from `origin/pr-252`.
- Conflict files: `DB.cs`, `FwActivityLogs.cs`, `FwController.cs`, `FwEntities.cs`, Dev Configure template, `Program.cs`, `appsettings.json`, `osafw-app.csproj`, and `FwCronTests.cs`.
- Auto-merge sanity check: `DevConfigure.cs` timezone block and `fw-modal.js` EOF whitespace.

## Commands used / verification
- `git -c core.quotepath=false status --short --branch`
- `git -c core.quotepath=false fetch origin master`
- `git -c core.quotepath=false fetch origin pull/252/head:refs/remotes/origin/pr-252`
- `git -c safe.directory=C:/DOCS_PROJ/github/_core_master grep -n -E "^(<<<<<<<|=======|>>>>>>>)" -- .` - no tracked conflict markers.
- `git -c safe.directory=C:/DOCS_PROJ/github/_core_master -c core.quotepath=false diff --cached --check` - clean.
- `powershell -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check ...` - edited/resolved files are UTF-8 no BOM with CRLF.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\_core_master\artifacts\assistant_pr252_sync_build\` - passed; one existing nullable warning in `FwCron.cs`.
- `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=C:\DOCS_PROJ\github\_core_master\artifacts\assistant_pr252_sync_tests\` - passed, 692/692.

## Decisions - why
- Used the PR ref because `origin/patch-fwcron` was not available from the upstream remote.
- Created a local working branch `pr-252-sync` so the PR head can be merged with `master` without disturbing local `master`.
- Resolved `DB.cs` to master behavior because master already contains the PR's required `_utc` and `selectRaw` concepts in provider-aware helpers.
- Kept both cron gates to preserve master optional-feature policy and PR runtime enablement.
- Changelog update was needed because apps enabling scheduled tasks now need both the compile symbol and runtime setting.

## Pitfalls - fixes
- `git fetch origin master patch-fwcron pull/252/head:refs/remotes/origin/pr-252` failed because `patch-fwcron` is not an upstream branch; refetched `master` and the PR ref separately.
- `DevConfigure.cs` auto-merged into a valid but duplicated timezone check; simplified it to `db.getTimezoneId()` / `db.isTimezoneDetectionOk()` and the matching master template fields.
- `diff --cached --check` flagged a trailing blank line in merged `fw-modal.js`; removed only the extra EOF blank line.

## Risks / follow-ups
- The merge is large because PR #252 was far behind `master`; final confidence comes from full tests, app build, marker scan, and focused manual review of conflict choices.
- Hosted cron now needs both the compile constant and `is_cron_enabled=true`; this is documented in the changelog.

## Heuristics (keep terse)
- No stable heuristics added; this was a one-off PR sync.

## Testing instructions
Use the commands above for verification. For a runtime smoke of cron hosting, enable `isFwCronService`, set `appSettings.is_cron_enabled=true`, configure a development database with fwcron tables, and run the app long enough to observe due job execution.

## Reflection
Sub-agent review was attempted with a bounded code-review prompt, but the agent did not finish within the wait window and was closed; self-review followed `docs/agents/code_reviewer.md` and found the changelog gap. The slowest part was separating true conflict choices from the very large master merge; targeted conflict-file reads plus full tests were more useful than broad diff review. No domain facts, reusable heuristics, or ADRs were added because the only durable user-facing fact was the cron startup gate, captured in `docs/CHANGELOG.md`.
