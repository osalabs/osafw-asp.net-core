## What changed
- Dev Tools controller config generation now removes copied demo tab-specific field blocks (`show_fields_*`, `showform_fields_*`) before writing generated layouts.
- Added a focused regression test that verifies a normal generated controller keeps only the Main tab and no copied demo tab field keys.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/dynamic.md` form tab section
- `docs/feature_modules.md`
- `osafw-app/App_Code/models/Dev/CodeGen.cs`
- Existing dynamic/Vue/virtual controller tests

## Commands used / verification
- `dotnet test osafw-tests\osafw-tests.csproj --filter DevCodeGenTests --no-restore -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\`
  - Passed: 1 test.
  - Caveat: normal `bin\Debug` output was locked by IIS Express, so isolated output was used; test run also emitted a transient apphost copy retry warning but passed.
- `dotnet build osafw-app\osafw-app.csproj --no-restore -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build\`
  - Passed with 0 warnings and 0 errors.
- Code reviewer sub-agent reviewed the diff and task summary: no issues found; review loop can stop.

## Decisions - why
- Clean stale copied tab-specific keys in `DevCodeGen.updateControllerConfig` because the generated controller config is rebuilt from entity metadata after copying the demo template.

## Pitfalls - fixes
- `DevCodeGen` is internal to the app assembly, so the focused test invokes it through reflection instead of widening production visibility.

## Risks / follow-ups
- No known follow-up. Broader Dev Tools browser scaffolding flow was not run because the focused code path is covered by the unit test.

## Heuristics (keep terse)
- None.

## Testing instructions
- Run `dotnet test osafw-tests\osafw-tests.csproj --filter DevCodeGenTests --no-restore -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\` when `osafw-app\bin\Debug` is locked by IIS Express.

## Reflection
- No stable framework facts, heuristics, glossary entries, ADRs, or AGENTS.md updates needed; this was a narrow generator cleanup matching existing dynamic config behavior.
