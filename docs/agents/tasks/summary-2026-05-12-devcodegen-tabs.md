## What changed
- Dev Tools controller config generation now removes copied demo tab-specific field blocks (`show_fields_*`, `showform_fields_*`) before writing generated layouts.
- Added a focused regression test that verifies a normal generated controller keeps only the Main tab and no copied demo tab field keys.
- Generated lookup-controller update SQL now wraps new `fwcontrollers` inserts in `IF NOT EXISTS` by `icode` so repeat application does not fail on duplicate rows.
- EntityBuilder now defaults `FieldName FK(Table.Field)` to `INT` when no explicit type is present.
- `config.json` now supports `is_readonly: true`; `loadControllerConfig` sets the controller readonly flag, and standard mutating controller actions check that flag.
- Documented `is_readonly` in dynamic controller docs and FK default typing in the EntityBuilder README.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/dynamic.md` form tab section
- `docs/feature_modules.md`
- `osafw-app/App_Data/template/dev/manage/entitybuilder/README.md`
- `osafw-app/App_Code/models/Dev/CodeGen.cs`
- `osafw-app/App_Code/models/Dev/EntityBuilder.cs`
- `osafw-app/App_Code/fw/FwController.cs`
- Standard admin/dynamic/Vue save/delete readonly checks
- Existing dynamic/Vue/virtual controller tests

## Commands used / verification
- `dotnet test osafw-tests\osafw-tests.csproj --filter DevCodeGenTests --no-restore -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\`
  - Passed: 1 test.
  - Caveat: normal `bin\Debug` output was locked by IIS Express, so isolated output was used; test run also emitted a transient apphost copy retry warning but passed.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~DevCodeGenTests|FullyQualifiedName~DevEntityBuilderTests|FullyQualifiedName~FwAdminControllerTests" --no-restore -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\`
  - Passed: 7 tests.
  - Existing warning observed: `ConvUtilsTests.cs(87,33)` nullable dereference warning.
- `dotnet build osafw-app\osafw-app.csproj --no-restore -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build\`
  - Passed with 0 warnings and 0 errors.
- Code reviewer sub-agent reviewed the diff and task summary: no issues found; review loop can stop.
- Second code reviewer sub-agent reviewed the extended Dev Tools generation/read-only diff: no issues found; review loop can stop.

## Decisions - why
- Clean stale copied tab-specific keys in `DevCodeGen.updateControllerConfig` because the generated controller config is rebuilt from entity metadata after copying the demo template.
- Route standard mutation guards through `FwController.checkReadOnly()` so both user-level readonly and config-level readonly use the same enforcement point.
- Keep lookup SQL idempotence in the generated update script instead of swallowing DB errors, so the script remains explicit and repeatable.

## Pitfalls - fixes
- `DevCodeGen` is internal to the app assembly, so the focused test invokes it through reflection instead of widening production visibility.
- `DevEntityBuilder.ParseField` is private, so the FK default regression uses reflection for a narrow parser check.
- The normal app output was locked by IIS Express, so verification used isolated `artifacts/assistant_*` output directories.

## Risks / follow-ups
- No known follow-up. Broader Dev Tools browser scaffolding flow was not run because the touched generation/runtime paths are covered by focused unit tests.

## Heuristics (keep terse)
- None.

## Testing instructions
- Run `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~DevCodeGenTests|FullyQualifiedName~DevEntityBuilderTests|FullyQualifiedName~FwAdminControllerTests" --no-restore -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\` when `osafw-app\bin\Debug` is locked by IIS Express.

## Reflection
- Stable docs updated in `docs/dynamic.md` and the EntityBuilder README for new config/parser behavior.
- No reusable heuristics, glossary entries, ADRs, or AGENTS.md updates needed.
