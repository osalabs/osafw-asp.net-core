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
- Follow-up manual feedback on `/Admin/DemosVue`: lookup-backed `_id` blank/not-blank filters now treat stored `0` as blank, so `parent_id = 0` rows match Blank and are excluded by Not blank.
- Vue inline-edit list refreshes now request `lookups` with `list_rows` and the server includes selected ids from current rows when loading lookup-model options. This keeps nonzero selected ids displayable when they are outside the default active lookup page/options.
- Added a model-specific selected-lookup filter bypass hook and opted in only the `Demos` parent lookup, so saved parent selections outside the top-level parent option filter can render while generic lookup `baseWhere` constraints remain intact by default.
- Renamed the boolean selected-lookup bypass hook to `isSelectedLookupFilterBypassAllowed()` to follow the project `is*` predicate naming convention.
- 2026-06-11 follow-up review loop: number `from`/`to` and `not_between_from`/`not_between_to` filters now serialize, display, and mark active only when both endpoints are present.
- Invalid boolean typed-filter values are ignored instead of silently filtering as `false`.
- Parseable but unrecognized JSON search text now falls back to legacy search and restores as legacy text, while native `FwDict`/`IDictionary` typed filter payloads still apply without stringifying to a type name.

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
- Manual feedback reproduction notes for `https://localhost:44315/Admin/DemosVue`: `parent_id` and `demo_dicts_id` are lookup-backed raw id columns in inline edit; empty-looking affected rows had `0` or nonzero ids missing from the loaded select options.
- Follow-up diff in `FwDynamicController.ColumnFilters.cs`, `FwVueController.cs`, `common/vue/store.js`, and `FwDynamicControllerColumnFilterTests.cs`.
- Final manual-feedback diff also covered `FwModel.cs`, `Demos.cs`, and `FwModelLookupTests.cs` after the review loop identified that the selected-id option exception should not be global.
- Final reviewer sub-agent checked the corrected generic/default lookup scoping plus Demos-specific opt-in and found no remaining issues.
- 2026-06-11 review loop covered `FwDynamicController.ColumnFilters.cs`, `common/vue/list-column-filter.html`, `assets/js/fw.js`, and `FwDynamicControllerColumnFilterTests.cs`.
- C# explorer sub-agent reviewed Dynamic column-filter engine/contracts and found the parseable JSON fallback/native payload gaps; a second template/client explorer was closed after repeated unproductive waits and the main agent performed the fallback review.

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
- Follow-up focused reruns during manual-feedback fix: `dotnet test osafw-tests\osafw-tests.csproj --filter FwDynamicControllerColumnFilterTests -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_tests_column\` - passed, 22 tests.
- Temporary updated app process was started from isolated output on `localhost:5127`; it could execute but rendered empty pages because the artifact runtime base lacked `App_Data` templates, so it was stopped and not used as final browser evidence.
- VS-hosted browser check after the first manual-feedback fix: `/Admin/DemosVue` Parent Blank returned count 34 and included ID 1128 with only `parent_id=0` rows; Parent Not blank excluded `parent_id=0` but still showed some nonzero parent ids as visually blank, which led to the final Demos parent lookup option fix.
- Final required verification after all fixes:
  - `dotnet build osafw-app\osafw-app.csproj -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_build\` - passed, 0 warnings/errors.
  - `dotnet test osafw-tests\osafw-tests.csproj --filter FwDynamicControllerColumnFilterTests -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_tests_column\` - passed, 23 tests.
  - `dotnet test osafw-tests\osafw-tests.csproj --filter "ClassName=osafw.Tests.UtilsTests|ClassName=osafw.Tests.FwReportsTests|ClassName=osafw.Tests.UserOwnedPreferencesSecurityTests" -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_tests_related\` - passed, 133 tests.
  - `dotnet test osafw-tests\osafw-tests.csproj --filter FwModelLookupTests -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_tests_lookup\` - passed, 11 tests.
  - `git diff --check` - passed.
  - `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check ...` - touched files are UTF-8 without BOM and CRLF.
- 2026-06-11 required verification after follow-up fixes:
  - `dotnet build osafw-app\osafw-app.csproj -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_build\` - passed, 0 warnings/errors.
  - `dotnet test osafw-tests\osafw-tests.csproj --filter FwDynamicControllerColumnFilterTests -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_tests_column\` - passed, 27 tests.
  - `dotnet test osafw-tests\osafw-tests.csproj --filter "ClassName=osafw.Tests.UtilsTests|ClassName=osafw.Tests.FwReportsTests|ClassName=osafw.Tests.UserOwnedPreferencesSecurityTests" -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_tests_related\` - passed, 133 tests.
  - `git diff --check` - passed.
  - `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check osafw-app\App_Code\fw\FwDynamicController.ColumnFilters.cs osafw-app\App_Data\template\common\vue\list-column-filter.html osafw-app\wwwroot\assets\js\fw.js osafw-tests\App_Code\fw\FwDynamicControllerColumnFilterTests.cs` - passed; touched files are UTF-8 without BOM and CRLF.

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
- Limited zero-as-blank behavior to lookup-backed `_id` filters. Status/radio/numeric fields can legitimately use `0`, so their blank predicates remain null/empty-string only.
- Aggregated Vue selected lookup ids by `lookup_model`, matching the existing lookup payload key and preserving selections across multiple fields sharing one option source without changing the Vue data contract.
- Kept selected-id bypass disabled for generic `FwModel.listSelectOptions()` because `baseWhere` can carry real relationship or security constraints. The opt-in hook is only enabled by `Demos` parent lookup for the UI presentation filter `parent_id=0`.
- Kept incomplete typed number payloads as typed-but-inactive rather than falling back to legacy `LIKE` predicates, because applying literal JSON text to numeric/date columns would be misleading. Unrecognized JSON without a supported filter `type` falls back to legacy text search.
- No docs/changelog update was needed for the 2026-06-11 loop because it tightens unreleased PR behavior and preserves the documented nested filter contract.

## Pitfalls - fixes
- A sub-agent's first option-loading fix prevented lookup-backed multi-select dropdowns from rendering choices. Sent it back through a review-fix loop and accepted the revised request-cache approach instead.
- `apply_patch` left bare LF in the edited partial and new task summary. Ran the repo normalizer and rechecked CRLF.
- Worker implementation initially added a Vue regression test without registering the test lookup model. The focused test failed, then the fixture was corrected and rerun successfully.
- Worker test attempts created an untracked `osafw-tests\TestResults` folder. Verified the absolute path was under the repo and removed only that generated folder.
- The first follow-up test hook was accidentally added to the Dynamic test controller instead of the Vue test controller; the focused compile failed and the hook was moved to the Vue subclass.
- A temporary artifact-hosted app was not suitable for browser smoke testing because templates are resolved outside the isolated DLL output. Avoid treating that empty-page result as an app regression.
- A reviewer caught the initial shared `FwModel` selected-id bypass as too broad because it could leak rows outside `baseWhere`; the fix was reworked into a default-deny hook plus explicit `Demos` parent lookup opt-in.
- Added an `IndexAction`-level Vue test after review noted that helper-only lookup tests did not exercise the `scope=list_rows,lookups` request path.
- The first 2026-06-11 fix made unrecognized JSON text fall back in SQL but still restored it as an inactive typed object in headers. The follow-up patch applies the same recognition rule during header state preparation.
- `apply_patch` introduced bare LF in edited files again; reran `Normalize-TextFiles.ps1` before final checks.

## Risks / follow-ups
- True open-time lazy option loading remains a follow-up; it needs a small endpoint plus client loading/error states for both server-rendered and Vue filters.
- Full suite was not run; focused build/tests covered the touched contracts.
- The Vue template regression test includes a small source-contract assertion because the legacy-text restore behavior lives in client template code that the .NET test suite does not execute.
- Final live-browser verification of the last Demos parent lookup opt-in was not rerun after that final patch; the VS-hosted app needs another rebuild/restart for that exact check. Automated tests now cover the corrected SQL contract and Vue request path.
- The existing Vue lookup payload is keyed by lookup model, not field plus lookup parameters. This fix preserves that contract; two fields using the same model but incompatible lookup parameters still share one option list as before.
- 2026-06-11 loop did not rerun live browser checks; automated tests cover the corrected server/client serialization contracts, but manual UI smoke remains the highest-value follow-up if the VS-hosted app is rebuilt.
- Full suite was not run; focused build/tests matched the requested verification scope.

## Heuristics (keep terse)
- Do not skip lookup-backed filter options unless the UI has a lazy path; otherwise the dropdown is empty.
- For base-controller helpers, prefer local per-call caches over extra instance fields unless the cached state must survive across calls.
- For lookup-id filters that use `0` as the no-selection sentinel, blank/not-blank should match user-visible emptiness, but do not generalize that rule to all numeric filters.

## Testing instructions
- Build: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_build\`
- Focused tests: `dotnet test osafw-tests\osafw-tests.csproj --filter FwDynamicControllerColumnFilterTests -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_tests_column\`
- Related tests: `dotnet test osafw-tests\osafw-tests.csproj --filter "ClassName=osafw.Tests.UtilsTests|ClassName=osafw.Tests.FwReportsTests|ClassName=osafw.Tests.UserOwnedPreferencesSecurityTests" -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_tests_related\`
- Lookup regression tests: `dotnet test osafw-tests\osafw-tests.csproj --filter FwModelLookupTests -p:OutDir=$PWD\artifacts\assistant_dynamic_column_filters_tests_lookup\`
- Whitespace: `git diff --check`
- CRLF/UTF-8: `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check <touched files>`; 2026-06-11 touched files were `FwDynamicController.ColumnFilters.cs`, `list-column-filter.html`, `fw.js`, and `FwDynamicControllerColumnFilterTests.cs`.

## Reflection
Sub-agent delegation helped isolate the four requested findings quickly, but the option-loading worker's first pass shows that performance fixes need immediate UI-contract review when there is no lazy data path. Future agents should explicitly distinguish "dedupe repeated eager work" from "make eager work lazy"; the latter needs endpoint and client-state design, not just server omission. The main-agent line-ending check was necessary after manual patches. No stable framework facts, shared heuristics, or ADRs were added beyond this task summary because the remaining lazy-loading design is still a follow-up, not a settled framework rule.

Follow-up reviewers were useful: they found two restore regressions missed in the previous pass, one in saved-filter shape detection and one in Vue legacy text hydration. The worker was useful for quick implementation but needed main-agent integration because its first test fixture was incomplete and generated a local test-results folder. Future agents should treat saved preference JSON shape changes as backward-compatibility work and add collision tests for plausible legacy keys. No stable framework facts, shared heuristics, ADRs, or AGENTS changes were added; this pass fixed PR-specific regressions and preserved the existing documented nested filter contract.

The manual-feedback loop showed that matching SQL semantics and rendered select semantics are separate checks for lookup-backed inline-edit columns. The final reviewer was valuable because it caught a security/ownership risk in a broad shared helper change before closeout; future agents should keep selected-id exception behavior model-specific unless they can prove `baseWhere` is only cosmetic. Temporary app verification with custom `OutDir` was not useful for Dynamic configs because template/config resolution followed the isolated output; prefer the VS-hosted app after rebuild for browser smoke checks. No shared heuristic/doc/ADR update was added because the selected-id bypass is a Demos-specific demo lookup behavior, not a settled framework-wide rule.

The 2026-06-11 review loop showed that active-state restoration must use the same typed-payload recognition rules as SQL application; otherwise a fallback search can apply correctly while the header UI looks inactive. One explorer sub-agent was useful for finding the parseable JSON/native payload gap, while the second explorer did not return in time and was replaced by main-agent review. No stable framework facts, shared heuristics, ADRs, or AGENTS changes were added because the fixes are PR-local contract tightening rather than new general workflow.
