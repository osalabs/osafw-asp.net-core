## What changed
- Fixed PR #270 review findings using focused sub-agents and main-agent integration.
- Vue list requests now keep active `list_headers[].search_value` filters on post-initial `dofilter=1` requests even when the search row is closed.
- Removed the duplicate `FwVueController.collectFormFields()` override; Vue now uses the identical `FwDynamicController` implementation.
- Reduced repeated column-filter definition work by collecting form definitions and field overrides once per `getListColumnFilterDefs()` call.
- Removed the one-use `getListColumnFilterFieldOverrides()` wrapper.
- Added request-level lookup option caching for list column filters so repeated `lookup_model` / `lookup_tpl` sources are loaded once while dropdowns still receive usable options.
- Added focused tests for repeated lookup-backed multi-select filter option caching, selected-value display, and inline options.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/agents/code_reviewer.md`
- PR #270 base/head diff: `923d442b2fdf891a72af2e36e96d9ffc3337da99..c4929ee6c262b2950ed925e2e9729790f8d18f09`
- `osafw-app/App_Code/fw/FwController.ColumnFilters.cs`
- `osafw-app/App_Code/fw/FwVueController.cs`
- `osafw-app/App_Data/template/common/vue/store.js`
- `osafw-tests/App_Code/fw/FwControllerColumnFilterTests.cs`

## Commands used / verification
- Sub-agent worker for Vue search persistence: changed `store.js`; reported focused Node extraction check, `git diff --check`, and CRLF check passed.
- Sub-agent worker for duplicate Vue method: changed `FwVueController.cs`; reported isolated app build and `git diff --check` passed.
- Sub-agent worker for inference cleanup: changed `FwController.ColumnFilters.cs`; reported app build and focused column-filter tests passed.
- Sub-agent worker for option loading: changed `FwController.ColumnFilters.cs` and `FwControllerColumnFilterTests.cs`; initial empty-dropdown approach was rejected in review, then revised to request-level option caching with focused tests.
- `git diff --check` - passed.
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check ...` - touched files are UTF-8 without BOM and CRLF.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\review_fixes_build\` - passed, 0 warnings/errors.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FwControllerColumnFilterTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\review_fixes_column_tests\` - passed, 12 tests.
- `dotnet test osafw-tests\osafw-tests.csproj --filter UserOwnedPreferencesSecurityTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\review_fixes_prefs_tests\` - passed, 25 tests.

## Decisions - why
- Keep `FwController.ColumnFilters.cs` for now per user direction.
- Use sub-agents for independent findings, with final integration and verification in the main workspace.
- Kept multi-select options available in initial header payload because server-rendered and Vue dropdowns have no lazy option endpoint yet; an option-skipping implementation made lookup-backed filters unusable.
- Used request-level option caching as the lower-risk performance fix for this pass.
- Passed form definitions and field overrides into filter definition inference instead of adding more base-controller cache fields.
- No changelog entry was added because this fixes unreleased PR behavior and does not introduce a new breaking upgrade requirement.

## Pitfalls - fixes
- A sub-agent's first option-loading fix prevented lookup-backed multi-select dropdowns from rendering choices. Sent it back through a review-fix loop and accepted the revised request-cache approach instead.
- `apply_patch` left bare LF in the edited partial and new task summary. Ran the repo normalizer and rechecked CRLF.

## Risks / follow-ups
- True open-time lazy option loading remains a follow-up; it needs a small endpoint plus client loading/error states for both server-rendered and Vue filters.
- Full suite was not run; focused build/tests covered the touched contracts.

## Heuristics (keep terse)
- Do not skip lookup-backed filter options unless the UI has a lazy path; otherwise the dropdown is empty.
- For base-controller helpers, prefer local per-call caches over extra instance fields unless the cached state must survive across calls.

## Testing instructions
- Build: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\review_fixes_build\`
- Focused tests: `dotnet test osafw-tests\osafw-tests.csproj --filter FwControllerColumnFilterTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\review_fixes_column_tests\`
- Focused tests: `dotnet test osafw-tests\osafw-tests.csproj --filter UserOwnedPreferencesSecurityTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\review_fixes_prefs_tests\`

## Reflection
Sub-agent delegation helped isolate the four requested findings quickly, but the option-loading worker's first pass shows that performance fixes need immediate UI-contract review when there is no lazy data path. Future agents should explicitly distinguish "dedupe repeated eager work" from "make eager work lazy"; the latter needs endpoint and client-state design, not just server omission. The main-agent line-ending check was necessary after manual patches. No stable framework facts, shared heuristics, or ADRs were added beyond this task summary because the remaining lazy-loading design is still a follow-up, not a settled framework rule.
