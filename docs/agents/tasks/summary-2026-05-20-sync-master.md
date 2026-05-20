## What changed
- Merged local `master` into `support-sqllite` and resolved conflicts.
- Preserved SQLite provider support while taking master updates for timezone auto-detection, init DB behavior, common UI controls, design-system docs, and frontend assets.
- Kept provider-specific SQL script roots via `FwUpdates.sqlScriptRoot()` and updated stale tests to cover the moved helper.
- Fixed merge regressions caught by tests: restored `FwModel.listByWhere` offset/limit pass-through and aligned the autocomplete id parsing test with the current `label ::: id` formatter.
- Review loop fixes added SQLite demo parity for master range/switch fields and restored `listByWhere` to the offset-before-limit public contract.
- Follow-up reviewer feedback fixed SQLite composite-PK identity detection, SQLite cache expiration test coverage, and MySQL update discovery so legacy root updates remain visible while provider overrides are supported.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- Current branch/status and recent branch graph.
- Conflict groups in DB/provider code, DevConfigure, FwModel, DB tests, provider docs, demo schema/config, shared Vue templates, and frontend assets.

## Commands used / verification
- `git merge master` - produced conflicts, resolved manually.
- Conflict marker scan with `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs osafw-app osafw-tests` - clean after resolution.
- JSON validation via PowerShell `ConvertFrom-Json` for `admin/demosdynamic/config.json` and `admin/demosvue/config.json` - passed.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build\` - passed; latest rerun had one transient apphost copy retry warning due to a file lock.
- `dotnet build osafw-app\osafw-app.csproj -p:DefineConstants=isSQLite -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build_sqlite\` - passed.
- `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_build\ --filter "FullyQualifiedName~DBOperationTests|FullyQualifiedName~DBTests|FullyQualifiedName~FwKeysTests|FullyQualifiedName~FwUpdatesTests|FullyQualifiedName~FwTests|FullyQualifiedName~UtilsTests|FullyQualifiedName~UsersTimezoneTests|FullyQualifiedName~MySettingsControllerTests"` - passed, 233 tests; existing nullable warning in `ConvUtilsTests.cs`.
- `dotnet test osafw-tests\osafw-tests.csproj -p:DefineConstants=isSQLite -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_build_sqlite\ --filter "FullyQualifiedName~DBOperationTests|FullyQualifiedName~DBTests|FullyQualifiedName~FwKeysTests|FullyQualifiedName~FwUpdatesTests|FullyQualifiedName~SQLiteDBTests"` - passed, 91 tests; existing nullable warning in `ConvUtilsTests.cs`.
- `git diff --check` and `git diff --cached --check` - passed.
- Code review sub-agent found SQLite demo schema/update parity and `listByWhere` signature issues; both were fixed and verification was rerun.
- User-provided follow-up review feedback found three valid issues; fixed SQLite composite-PK identity detection, cache expiration coverage, and MySQL update root behavior, then reran build/test verification.

## Decisions - why
- In DB conflicts, kept SQLite date metadata detection and directory creation, and kept master timezone detection status tuple and UTC expanded-parameter suffix handling.
- In DevConfigure, combined provider-specific SQL root resolution with master FK cleanup, demo.sql initialization, and explicit timezone detection status.
- In demo docs/templates/config, took master range/switch additions because they match the new schema and generated UI controls.
- In agent/datetime docs, kept both SQLite provider facts and master user-timezone-auto facts.

## Pitfalls - fixes
- A build attempt with `BaseIntermediateOutputPath` under/outside the repo caused duplicate assembly attributes because the project compile glob still picked up `osafw-app/obj`; cleaned that generated folder and used isolated `OutDir` only.
- `git diff --cached --check` found one trailing whitespace line in merged `fw.js`; removed it.
- A stale test still referenced removed `DB.sqlScriptSubdir()`; moved coverage to `FwUpdates.sqlScriptRoot()`.
- SQLite demo scripts missed master `frange`/`is_switch` fields; added them to fresh SQLite demo schema and provider-specific update script.
- SQLite `PRAGMA table_xinfo` marks each composite-PK column with `pk`; identity detection now only treats a single-column `INTEGER` primary key as identity.
- MySQL updates now scan both root updates and `mysql/updates`, with provider-specific files overriding root files of the same name.

## Risks / follow-ups
- Browser smoke was not rerun after the merge; automated builds/tests passed.
- Unrelated untracked local files remain unstaged, including `.jshintrc`.

## Heuristics (keep terse)
- No new reusable heuristic added for this merge.

## Testing instructions
- Re-run the two build commands and two targeted test commands listed above after any further conflict-resolution edits.

## Reflection
- Stable facts/heuristics from master and the SQLite branch were preserved. No AGENTS.md change was made, so no `.github/copilot-instructions.md` sync was required.
