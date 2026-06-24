## What changed
- Added Site Settings admin input constants for text, textarea, select, multi-select, checkbox, radio, date, number, switch, range, and credential controls.
- Updated `AdminSettingsController` to prepare option/metadata view data, mask credential values, normalize submitted values, validate allowed options, validate numeric min/max/step metadata, and preserve existing credentials on blank submissions.
- Added settings form partials for number, switch, range, and credential; replaced checkbox/radio TODO partials; updated select partials and the list table to use display-safe values.
- Updated SQL Server/MySQL/SQLite seed metadata and unreleased assistant update scripts so assistant settings use switch/select/number/credential controls.
- Added SQL Server local metadata update `osafw-app/App_Data/sql/updates/upd2026-06-17-settings-input-metadata.sql` and applied it to local `demo`.
- Updated `docs/settings.md` with the control catalog, `allowed_values` formats, validation behavior, and credential masking semantics.
- Added focused `AdminSettingsControllerTests` coverage for credential, switch, number/range, select/radio, and checkbox behavior.
- Follow-up UI feedback: removed Add New from the Site Settings list header, removed non-functional bulk selection checkboxes from the list table, and rendered switch settings as disabled list-screen switches.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/agents/tasks/index.md` keyword search for related history
- `osafw-app/App_Code/controllers/AdminSettings.cs`
- `osafw-app/App_Code/models/Settings.cs`
- `osafw-app/App_Data/template/admin/settings/**`
- Shared form control partials under `osafw-app/App_Data/template/common/form/showform/`
- `osafw-tests/App_Code/fw/TestHelpers.cs`
- SQL seed/update scripts under `osafw-app/App_Data/sql/**`
- `docs/settings.md`
- `docs/agents/code_reviewer.md`
- Static review by sub-agent Nash of settings controller/model/templates/SQL/docs/tests
- Follow-up review of settings index/header/switch templates

## Commands used / verification
- `Get-Content docs\agents\local_instructions.md | Select-Object -Index (0..220)`
- `Get-Content docs\README.md | Select-Object -Index (0..120)`
- `rg -n "site settings|settings input|AdminSettings|settings" docs\agents\tasks\index.md`
- `git -c core.safecrlf=false status --short`
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~AdminSettingsControllerTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_settings_tests\` - passed, 13 tests.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_settings_build\` - passed, 0 warnings/errors.
- `sqlcmd -S "(local)" -d demo -E -C -i osafw-app\App_Data\sql\updates\upd2026-06-17-settings-input-metadata.sql` - applied local SQL Server metadata update.
- `Test-NetConnection localhost -Port 44315 | Select-Object TcpTestSucceeded` - timed out/port not listening, so browser smoke was not run.
- `powershell -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 ... -Check` - CRLF check passed for touched tracked and new files.
- `git -c core.quotepath=false diff --check` - passed.
- Follow-up checks: `rg -n 'btn_std|Add New|on-list-chkall|multicb' osafw-app\App_Data\template\admin\settings\index` - no matches.
- Follow-up `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~AdminSettingsControllerTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_settings_tests\` - passed, 13 tests.
- Follow-up `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_settings_build\` - passed, 0 warnings/errors.
- Follow-up CRLF check for changed settings templates and task summary - passed.
- Follow-up `git -c core.quotepath=false diff --check` - passed.

## Decisions - why
- Use `credential` as the canonical control name and preserve existing credential values on blank submissions, per user-approved plan.
- Keep the existing `settings` schema and reuse `allowed_values` for option and numeric metadata.
- Number controls enforce `min`, `max`, and `step` metadata when present so operational integer settings can be trusted by callers after save.
- Credential replacement preserves the exact nonblank submitted text instead of trimming, because credential text may be whitespace-sensitive.
- Assistant `MAX_*` settings use `number` with integer-oriented metadata instead of `range`, because these are large operational limits and direct entry is clearer.
- Settings list actions are overridden locally with an empty `page_header_actions.html` so Add New is suppressed only for settings.
- Switch list rows use a disabled Bootstrap switch and still route edits through the existing Edit link.
- No changelog entry was added: this is additive admin settings behavior plus metadata for unreleased assistant settings, with no schema change or end-user-app breaking upgrade identified.

## Pitfalls - fixes
- Native HTML multi-select posts as a comma string through the framework request parser; validation handles that shape separately from checkbox groups.
- Initial numeric validation only enforced metadata for range controls; changed it to enforce metadata for number controls too.
- Build/test commands must use absolute repo-root `OutDir` paths to avoid writing under project-local `artifacts` folders.
- The switch edit partial was tightened to mark checked only when stored value is `1`, matching the list rendering.

## Risks / follow-ups
- Browser smoke for `/Admin/Settings` was not run because the local app was not listening on the checked port.
- Template behavior was reviewed statically and covered by controller tests, but not rendered in a browser in this run.
- Existing unrelated dirty files remain outside this task, including assistant/RAG/KB files, deployment docs/scripts, `osafw-app/App_Data/template/dev/manage/docs/tab_installation.md`, `.jshintrc`, and `CLAUDE.md`.
- No stable domain facts, heuristics, or ADRs were added; this task did not introduce a durable architecture decision beyond the requested settings-control contract documented in `docs/settings.md`.

## Heuristics (keep terse)
- For settings controls, treat `allowed_values` as both option-source and numeric metadata; keep parser assumptions documented next to controller tests.
- For untracked new files, pair `git diff --check` with explicit CRLF/text normalization checks because normal Git diff checks do not inspect untracked content.

## Testing instructions
- Re-run focused tests: `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~AdminSettingsControllerTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_settings_tests\`
- Re-run app build: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_settings_build\`
- If a local app is running, smoke `/Admin/Settings` and edit the assistant settings to confirm rendered switch/select/number/credential controls.

## Reflection
- Static review was useful for catching the stale task summary, but implementation review was more efficient in the main agent after tests were already available.
- The biggest slowdown was making sure SQL update scope stayed aligned across from-scratch scripts, unreleased update scripts, and the one local SQL Server metadata update.
- Future similar work should inspect the request parser before choosing multi-value template names; that avoids guessing between `item[field]`, `field_multi`, and native multi-select submission shapes.
