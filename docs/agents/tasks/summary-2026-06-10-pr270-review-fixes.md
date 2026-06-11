## What changed
- Fixed PR #270 review findings using focused sub-agents and main-agent integration.
- Vue list requests now keep active `list_headers[].search_value` filters on post-initial `dofilter=1` requests even when the search row is closed.
- Removed the duplicate `FwVueController.collectFormFields()` override; Vue now uses the identical `FwDynamicController` implementation.
- Reduced repeated column-filter definition work by collecting form definitions and field overrides once per `getListColumnFilterDefs()` call.
- Removed the one-use `getListColumnFilterFieldOverrides()` wrapper.
- Added request-level lookup option caching for list column filters so repeated `lookup_model` / `lookup_tpl` sources are loaded once while dropdowns still receive usable options.
- Added focused tests for repeated lookup-backed multi-select filter option caching, selected-value display, and inline options.
- Follow-up review loop fixed saved-filter restore compatibility: `initFilter()` now treats saved filters as split `{ f, search }` payloads only when both values are dictionaries, preserving legacy saved filters with normal `f` or `search` fields.
- Vue typed text filters now rehydrate non-JSON legacy search values from server-prepared `header.filter.op` / `header.filter.value`, so restored `^`, `=`, `!=`, `!`, `$`, and related legacy operators are not converted to contains.
- Added regression tests for legacy saved-filter key collisions and Vue legacy text filter metadata/template usage.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/agents/code_reviewer.md`
- PR #270 base/head diff: `923d442b2fdf891a72af2e36e96d9ffc3337da99..c4929ee6c262b2950ed925e2e9729790f8d18f09`
- `osafw-app/App_Code/fw/FwController.ColumnFilters.cs`
- `osafw-app/App_Code/fw/FwVueController.cs`
- `osafw-app/App_Data/template/common/vue/store.js`
- `osafw-tests/App_Code/fw/FwControllerColumnFilterTests.cs`
- Current branch diff through `04655a71` against `923d442b2fdf891a72af2e36e96d9ffc3337da99`.
- `osafw-app/App_Code/fw/FwDynamicController.ColumnFilters.cs`
- `osafw-app/App_Code/fw/FwController.cs`
- `osafw-app/App_Data/template/common/vue/list-column-filter.html`
- `osafw-tests/App_Code/fw/FwDynamicControllerColumnFilterTests.cs`
- `osafw-tests/App_Code/security/UserOwnedPreferencesSecurityTests.cs`
- Final reviewer sub-agent checked the four-file follow-up diff and found no remaining issues.

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
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_build\` - passed, 0 warnings/errors.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FwDynamicControllerColumnFilterTests -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_tests_column\` - passed, 19 tests after fixing the new test fixture to register `LookupOptionsModel`.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "ClassName=osafw.Tests.UtilsTests|ClassName=osafw.Tests.FwReportsTests|ClassName=osafw.Tests.UserOwnedPreferencesSecurityTests" -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_tests_related\` - passed, 133 tests.
- `git diff --check` - passed.
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check osafw-app\App_Code\fw\FwController.cs osafw-app\App_Data\template\common\vue\list-column-filter.html osafw-tests\App_Code\fw\FwDynamicControllerColumnFilterTests.cs osafw-tests\App_Code\security\UserOwnedPreferencesSecurityTests.cs` - passed.

## Decisions - why
- Keep `FwController.ColumnFilters.cs` for now per user direction.
- Use sub-agents for independent findings, with final integration and verification in the main workspace.
- Kept multi-select options available in initial header payload because server-rendered and Vue dropdowns have no lazy option endpoint yet; an option-skipping implementation made lookup-backed filters unusable.
- Used request-level option caching as the lower-risk performance fix for this pass.
- Passed form definitions and field overrides into filter definition inference instead of adding more base-controller cache fields.
- No changelog entry was added because this fixes unreleased PR behavior and does not introduce a new breaking upgrade requirement.
- Required both split saved-filter members to be dictionaries instead of using key presence as a discriminator because legacy saved filters may legitimately contain scalar `f` or `search` fields.
- Kept Vue legacy text restore in the template component rather than adding flattened header compatibility fields; the intended contract remains nested under `header.filter`.
- No docs/changelog update was needed for the follow-up fixes because they preserve documented behavior and fix unreleased PR regressions.

## Pitfalls - fixes
- A sub-agent's first option-loading fix prevented lookup-backed multi-select dropdowns from rendering choices. Sent it back through a review-fix loop and accepted the revised request-cache approach instead.
- `apply_patch` left bare LF in the edited partial and new task summary. Ran the repo normalizer and rechecked CRLF.
- Worker implementation initially added a Vue regression test without registering the test lookup model. The focused test failed, then the fixture was corrected and rerun successfully.
- Worker test attempts created an untracked `osafw-tests\TestResults` folder. Verified the absolute path was under the repo and removed only that generated folder.

## Risks / follow-ups
- True open-time lazy option loading remains a follow-up; it needs a small endpoint plus client loading/error states for both server-rendered and Vue filters.
- Full suite was not run; focused build/tests covered the touched contracts.
- The Vue template regression test includes a small source-contract assertion because the legacy-text restore behavior lives in client template code that the .NET test suite does not execute.
- Manual browser smoke testing against the running Visual Studio app was not performed; automated checks covered the restored server/Vue metadata contract.

## Heuristics (keep terse)
- Do not skip lookup-backed filter options unless the UI has a lazy path; otherwise the dropdown is empty.
- For base-controller helpers, prefer local per-call caches over extra instance fields unless the cached state must survive across calls.

## Testing instructions
- Build: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_build\`
- Focused tests: `dotnet test osafw-tests\osafw-tests.csproj --filter FwDynamicControllerColumnFilterTests -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_tests_column\`
- Related tests: `dotnet test osafw-tests\osafw-tests.csproj --filter "ClassName=osafw.Tests.UtilsTests|ClassName=osafw.Tests.FwReportsTests|ClassName=osafw.Tests.UserOwnedPreferencesSecurityTests" -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_tests_related\`
- Whitespace: `git diff --check`
- CRLF/UTF-8: `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check <touched files>`

## Reflection
Sub-agent delegation helped isolate the four requested findings quickly, but the option-loading worker's first pass shows that performance fixes need immediate UI-contract review when there is no lazy data path. Future agents should explicitly distinguish "dedupe repeated eager work" from "make eager work lazy"; the latter needs endpoint and client-state design, not just server omission. The main-agent line-ending check was necessary after manual patches. No stable framework facts, shared heuristics, or ADRs were added beyond this task summary because the remaining lazy-loading design is still a follow-up, not a settled framework rule.

Follow-up reviewers were useful: they found two restore regressions missed in the previous pass, one in saved-filter shape detection and one in Vue legacy text hydration. The worker was useful for quick implementation but needed main-agent integration because its first test fixture was incomplete and generated a local test-results folder. Future agents should treat saved preference JSON shape changes as backward-compatibility work and add collision tests for plausible legacy keys. No stable framework facts, shared heuristics, ADRs, or AGENTS changes were added; this pass fixed PR-specific regressions and preserved the existing documented nested filter contract.
