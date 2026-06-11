## What changed
- Moved typed list column filters out of `FwController` and into `FwDynamicController.ColumnFilters.cs`; `FwVueController` continues to inherit the behavior through `FwDynamicController`.
- Removed `FwController.ColumnFilters.cs` and the separate `FwListColumnFilter` engine shape. `FwController` now keeps only legacy list search/header behavior plus the generic `appendListSearchAdvancedField()` helper.
- Kept list column filters opt-in through `list_column_filters.enabled`; Dynamic infers simple visible filters from form definitions/schema and requires explicit config for complex aliases.
- Added `DB.schemaField()` and `FormUtils.normalizeSelectOptions()` to shrink Dynamic filter code.
- Changed `Utils.jsonDecode()` to return `null` on malformed JSON and added `Utils.jsonDecodeOrThrow()` for strict callers; updated controller config, reports, and LLM JSON parsing to use the strict helper.
- Removed arbitrary server-side filter template rendering. Server custom UI now uses static controller-local `index/list_filter_custom.html` with `template: "custom"`.
- Reworked column-filter tests from a plain `FwController` fixture to Dynamic/Vue behavior tests, and added JSON helper tests.
- Updated `docs/dynamic.md`, `docs/templates.md`, and `docs/db.md`.

## Scope reviewed
- `AGENTS.md`
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/agents/code_reviewer.md`
- `docs/drafts/FPF-Spec.md` searched by heading only for planning discipline.
- `docs/agents/tasks/index.md`
- Existing task summary `docs/agents/tasks/summary-2026-06-10-column-filter-engine.md`
- `osafw-app/App_Code/fw/FwController.cs`
- `osafw-app/App_Code/fw/FwDynamicController.cs`
- `osafw-app/App_Code/fw/FwController.ColumnFilters.cs`
- `osafw-app/App_Code/fw/FwListColumnFilter.cs`
- `osafw-app/App_Code/fw/DB.cs`
- `osafw-app/App_Code/fw/FormUtils.cs`
- `osafw-app/App_Code/fw/Utils.cs`
- `osafw-app/App_Code/fw/FwReports.cs`
- `osafw-app/App_Code/models/AI/LLM.cs`
- `osafw-app/App_Data/template/common/list/filters/*.html`
- `osafw-app/App_Data/template/common/vue/list-column-filter.html`
- `osafw-app/App_Data/template/common/vue/list-table-header.html`
- `osafw-app/App_Data/template/admin/demosdynamic/config.json`
- `osafw-app/App_Data/template/admin/demosvue/config.json`
- `osafw-tests/App_Code/fw/FwControllerColumnFilterTests.cs`
- `osafw-tests/App_Code/fw/UtilsTests.cs`

## Commands used / verification
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_dynamic_column_filters_build\` - passed, 0 warnings/errors.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FwDynamicControllerColumnFilterTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_dynamic_column_filters_tests_column\` - passed, 18 tests.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "ClassName=osafw.Tests.UtilsTests|ClassName=osafw.Tests.FwReportsTests|ClassName=osafw.Tests.UserOwnedPreferencesSecurityTests" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_dynamic_column_filters_tests_related\` - passed, 131 tests.
- `git diff --check` - passed.
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check ...` - all touched text files are UTF-8 without BOM and CRLF.
- Code review loop: spawned a reviewer sub-agent using `docs/agents/code_reviewer.md`, but it did not return after two waits and was closed. Performed the same review locally against the reviewer checklist; no issue worth another loop was found.

## Decisions - why
- Moved ownership to Dynamic because typed filters rely on Dynamic/Vue config, field metadata, list headers, and generated schemas; base `FwController` should remain usable for simple controllers without this overhead.
- Deleted the adapter and engine rather than keeping wrappers because the previous split moved complexity without enough reduction.
- Kept `appendListSearchAdvancedField()` in base as a generic helper, not a column-filter hook, so Dynamic can reuse the legacy parser without duplicating it.
- Kept `applyListColumnFilter(FwDict def, FwDict rawValue)` only on Dynamic as the custom predicate hook.
- Kept Dynamic/Vue flat header compatibility keys (`filter_type`, `filter_component`, `filter_options`, `autocomplete_url`) while server templates use nested `filter[...]`.
- Did not add dynamic ParsePage include-by-value support. Static `template: "custom"` plus controller-local `index/list_filter_custom.html` satisfies the current customization need with less framework surface.
- Did not add a changelog entry because this is still an unreleased feature refactor; related docs were updated.

## Pitfalls - fixes
- The first build exposed a malformed `try` block while splitting `jsonDecodeOrThrow()`; fixed by moving the old strict body directly into the new method.
- Initial related test filter used `FullyQualifiedName~UtilsTests`, which also matched `UploadUtilsTests`; reran with exact `ClassName=` filters.
- `Assert.ThrowsException` was not available in this MSTest version; replaced with an explicit try/catch for `JsonException`.
- PowerShell blocked the normalization helper under the default execution policy; reran with process-local `-ExecutionPolicy Bypass`.
- The reviewer sub-agent stalled; closed it and recorded the local review fallback.

## Risks / follow-ups
- Full `dotnet test` was not run.
- Dynamic inference is intentionally strict. Fields in expressions, dotted aliases, and subquery-only calculated fields need explicit `list_column_filters.fields` entries.
- Existing untracked/unrelated files were left alone: `.jshintrc`, `docs/agents/tasks/summary-2026-06-09-deploy-script.md`, `osafw-app/App_Data/db/`, `osafw-app/wwwroot/offline.htm`, and `scripts/deploy_sample_v2.bat`.
- Stable facts, shared heuristics, and ADRs were not added; this is a scoped refactor of an unreleased feature.

## Heuristics (keep terse)
- Keep feature-specific controller state on the narrowest controller base that owns the required metadata.
- For unreleased framework features, remove provisional seams when a stricter data contract makes production code smaller.
- Prefer static ParsePage override conventions before adding dynamic include features.

## Testing instructions
- Build: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_dynamic_column_filters_build\`
- Column-filter tests: `dotnet test osafw-tests\osafw-tests.csproj --filter FwDynamicControllerColumnFilterTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_dynamic_column_filters_tests_column\`
- Related tests: `dotnet test osafw-tests\osafw-tests.csproj --filter "ClassName=osafw.Tests.UtilsTests|ClassName=osafw.Tests.FwReportsTests|ClassName=osafw.Tests.UserOwnedPreferencesSecurityTests" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_dynamic_column_filters_tests_related\`

## Reflection
- The main slowdown was correcting the earlier extraction after it proved to be mostly a move, not a simplification. Future agents should check responsibility boundaries and net implementation size before treating a refactor as complete. The multi-agent reviewer was attempted but not useful this time because it stalled; for similar scoped refactors, one short wait is enough before falling back to a local review loop. The most useful tooling was the isolated build/test output directories and the line-ending helper. No agent instruction update is recommended.
