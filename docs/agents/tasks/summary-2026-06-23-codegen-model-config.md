## What changed
- Generated models now set `field_icode = ""` when the entity/table fields do not include `icode`.
- `ConfigJsonConverter` no longer writes the same ordered key twice, and the duplicated `model` entry was removed from the controller key order list.
- Removed the existing duplicate `is_dynamic_index_edit` key from `osafw-app/App_Data/template/admin/fwupdates/config.json`.
- Added focused regressions for no-`icode` model generation and duplicate `model` serialization.
## Scope reviewed
- `docs/README.md`
- `docs/dynamic.md` config key overview
- `docs/agents/tasks/index.md`
- `docs/agents/tasks/summary-2026-05-12-devcodegen-tabs.md`
- `docs/agents/code_reviewer.md`
- `osafw-app/App_Code/models/Dev/CodeGen.cs`
- `osafw-app/App_Code/models/Dev/ConfigJsonConverter.cs`
- `osafw-app/App_Code/models/Dev/EntityBuilder.cs`
- `osafw-app/App_Code/fw/FwModel.cs`
- All `osafw-app/App_Data/template/**/config.json` files
- `osafw-tests/App_Code/fw/DevCodeGenTests.cs`
- `osafw-tests/App_Code/fw/DevEntityBuilderTests.cs`
## Commands used / verification
- `rg -n "CodeGen|config\.json|model|icode" docs\agents\tasks\index.md docs\dynamic.md docs\crud.md docs\db.md docs\naming.md`
- `rg --files -g config.json`
- Python duplicate-key scan over `osafw-app/App_Data/template/**/config.json`: no duplicate keys found after cleanup.
- Python CRLF scan over all edited files: all edited files use CRLF after normalization.
- `git -c core.safecrlf=false diff --check`
- `dotnet restore osafw-tests\osafw-tests.csproj`
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~DevCodeGenTests --no-restore -p:OutDir=C:\DOCS_PROJ\github\_core_master\artifacts\assistant_test_codegen\`
  - Passed: 13 tests.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~DevEntityBuilderTests --no-restore -p:OutDir=C:\DOCS_PROJ\github\_core_master\artifacts\assistant_test_entitybuilder\`
  - Passed: 2 tests.
- `dotnet build osafw-app\osafw-app.csproj --no-restore -p:OutDir=C:\DOCS_PROJ\github\_core_master\artifacts\assistant_build\`
  - Passed with 0 warnings and 0 errors after restore.
## Decisions - why
- Added the `field_icode` reset inside `DevCodeGen.createModel` near the existing generated `field_*` resets so generated models reflect actual table schema.
- Fixed the serializer at the writer level with an already-written guard so future duplicate entries in an ordered-key list cannot duplicate JSON properties.
- Removed the duplicated `model` ordering entry because one ordered key is enough once writer de-duplication is enforced.
- No docs/changelog update needed: this is a bug fix to generated code/config serialization and does not change a documented public config contract or introduce a breaking end-user-app upgrade.
## Pitfalls - fixes
- Initial `dotnet build --no-restore` failed because `osafw-app/obj/project.assets.json` was missing. Ran `dotnet restore osafw-tests\osafw-tests.csproj`, which restored both app and test projects, then reran tests/build.
- `apply_patch` introduced LF-only lines in touched files; normalized only edited files back to CRLF and rechecked.
- Windows PowerShell lacked `System.Text.Json.JsonDocument`, so the duplicate-key scan used the bundled Python runtime with `json.object_pairs_hook`.
## Risks / follow-ups
- Broader Dev Tools browser scaffolding was not run; focused model generation, JSON serialization, config scan, tests, and app build covered the changed paths.
## Heuristics (keep terse)
- None added.
## Testing instructions
- Run the focused checks above after `dotnet restore osafw-tests\osafw-tests.csproj`, using isolated `OutDir` paths if normal build output is locked.
## Reflection
- What slowed this task: the read-only sandbox required explicit write/test escalations, and missing restore assets made the first `--no-restore` build/test attempts misleading until restore was run.
- Future agents should check `ConfigJsonConverter` ordered-key lists for duplicate names when JSON output duplicates keys; the bug can be in serialization order metadata, not the mutable `FwDict`.
- The self-review checklist was sufficient for this small verified diff; a sub-agent was not necessary.
- No stable facts, reusable heuristics, glossary entries, ADRs, AGENTS.md updates, or changelog entries were added.
