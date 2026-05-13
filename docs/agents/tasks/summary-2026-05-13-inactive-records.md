## What changed
- Added selected-aware lookup option handling so model-backed dropdowns return active rows by default plus the same-field selected inactive row on edit.
- Kept stale inactive form submissions allowed by design; no `ACTIVE_LOOKUP` validation or template error text remains.
- Added Vue edit payload support for field-specific lookup options so inactive exceptions do not leak across fields using the same lookup model.
- Restored `Users.listSelectOptions` as a real override that builds display names from first and last name while using the selected inactive exception rule.
- Simplified the framework implementation by removing `lookup_include_inactive`, `show_all`, and `lookup_statuses` support from the shared inactive lookup path.
- Added `FwModel.LOOKUP_INACTIVE_SUFFIX` for the inactive option label.
- Simplified selected inactive lookup loading to one query: active rows, or active rows plus selected ids/name values through a single `OR` predicate.
- Inlined the single-use lookup helper methods into `FwModel.listSelectOptions` and removed the `listLookupOptions` wrapper.
- Kept `Users.listSelectOptions` as a concise full-name override by delegating inactive-selected query behavior back to `FwModel`.
- Forwarded the new optional lookup arguments through the `Demos` parent lookup branch.
- Simplified lookup option result shape so `listSelectOptions` always returns a `status` alias, using `0 AS status` for models without status support.
- Reviewed framework selected-id callers; current framework usage needs scalar values and enumerable values, while comma-separated multi values are split before calling `listSelectOptions`.
- Documented the active-row default and selected inactive exception in `docs/dynamic.md`.
## Scope reviewed
- Reviewed downstream BIG-1370 inactive member design note.
- Reviewed framework lookup APIs in `FwModel`, `Users`, dynamic classic forms, Vue forms, and select rendering.
- Reviewed `listWithChecked`, junction model option helpers, autocomplete option lookup, and Vue lookup payload shape.
## Commands used / verification
- `dotnet build osafw-app\osafw-app.csproj` failed because the running IIS Express worker locked `osafw-app\bin\Debug\net10.0\osafw-app.dll`.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build\` passed after simplification.
- After the final review fix, one rerun hit a transient default `obj` lock, and a custom `BaseIntermediateOutputPath` rerun was not usable because SDK-generated assembly files were included twice; retrying the standard isolated-output build passed.
- `dotnet test osafw-tests\osafw-tests.csproj` failed on the same locked app DLL during project-reference build.
- `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_build\ --filter "FullyQualifiedName~FwModelLookupTests"` passed after simplification: 6 tests.
- After the second simplification feedback, the same build passed, the focused lookup tests passed again after one retry for the transient `obj` lock, and `git diff --check` passed.
- After preserving spaces in name-valued selected lookups, focused lookup tests passed again and the app build passed after one transient `obj` lock retry.
- After the single-query lookup rewrite, `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build\` passed, focused `FwModelLookupTests` passed: 7 tests, and `git diff --check` passed.
- After the Users override simplification review fix, the app build passed, focused `FwModelLookupTests` passed: 8 tests, and `git diff --check` passed.
- After the Demos parent lookup pass-through review fix, the app build passed, focused `FwModelLookupTests` passed: 9 tests, and `git diff --check` passed.
- After normalizing lookup option status projection, the app build passed, focused `FwModelLookupTests` passed: 10 tests, and `git diff --check` passed.
- `git diff --check` passed.
- Full suite with isolated output ran after first pass and had unrelated existing failures in `FormUtilsTests.AutocompleteParsingExtractsLeadingId`, `FwDynamicControllerTests.NextAction_*`, and `ParsePageTests.parse_string_dateTest`.
## Decisions - why
- Implement as framework lookup option behavior with selected-value exceptions, not as project-specific member field patches.
- Keep the framework default simple; projects that intentionally need broader status coverage should override the model lookup method.
- Keep `Users.listSelectOptions` explicit for first-name/last-name display, but let the base helper append the selected inactive row.
- Keep global Vue lookups active-only and send `lookups_by_field` on edit screens for selected inactive exceptions.
- Do not validate submitted lookup ids for active status; stale forms can submit an inactive id because the important behavior is keeping inactive options out of normal selection.
## Pitfalls - fixes
- Normal build/test output was locked by IIS Express; verified with repo-root `artifacts` output instead.
- Review found `listSelectOptionsName` did not preserve inactive selected names; added a name-valued selected exception path and test coverage.
- Review found junction checked-state could break for lookup models with custom `field_id`; changed comparisons to use normalized option `id` and added regression coverage.
- Final review flagged ambiguous object/string handling in junction selected ids; converted junction ids to strings before storing and comparing them.
- User feedback identified the validation path as unnecessary complexity; removed dynamic validation helpers, tests, and template/store error strings.
- User feedback identified the status option plumbing and long `Users.listSelectOptions` override as unnecessary complexity; simplified the base model helpers and the users override.
- Name-valued lookup selections must not split on spaces; selected-id parsing now treats strings as scalar values and collections as lists.
- Second simplification review pass found no remaining issues.
- User feedback identified the second query in selected inactive handling as avoidable; changed base lookup loading and `Users.listSelectOptions` to use a single query when selected values exist.
- User feedback identified the remaining lookup helper methods and wrapper method as unnecessary indirection; moved the logic into `listSelectOptions` while keeping specialized behavior as optional parameters.
- Review found `Users.listSelectOptions` still duplicated base inactive-selected SQL; added an optional trusted label SQL expression to the base method so Users only supplies its full-name expression.
- Review found `Demos` parent lookup mode dropped the new optional pass-through arguments; forwarded them and added regression coverage.
- User feedback noted the conditional status projection was unnecessary; replaced it with a consistent `status` alias for all lookup option rows.
- One-query review pass found no remaining issues.
- Status-alias review pass found no remaining issues.
- Final review pass found no remaining issues; review loop can stop.
## Risks / follow-ups
- Vue lookups are currently cached by lookup model, so edit-specific selected inactive options need field-specific lookup payloads.
- Downstream projects with custom Staff models should override active rules such as `Inactive = 0 AND status = 0` and can use the same selected-id contract.
- Full test suite still has pre-existing unrelated failures; focused inactive lookup tests should be used for this change.
## Heuristics (keep terse)
- Stable framework fact added to `docs/agents/domain.md`.
- No ADR added; this is framework behavior refinement, not a substantial architecture decision.
## Testing instructions
- Build with isolated output if IIS Express locks normal output: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build\`.
- Run focused regression checks: `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_build\ --filter "FullyQualifiedName~FwModelLookupTests"`.
## Reflection
- The rule belongs in framework lookup option plumbing; model-specific active predicates still belong in each app/model override.
