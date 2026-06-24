## What changed
- Simplified framework tests by replacing broad private reflection with tracked internal seams where the exercised logic is a durable framework contract.
- Added an assembly-level `InternalsVisibleTo("osafw-tests")` attribute in `osafw-app/Program.cs` so the test project can call internal framework seams without making them public app API.
- Centralized test `DefaultHttpContext` and fake session setup in `TestHelpers`, including direct model-cache seeding through `FW.registerModelForTesting()`.
- Refactored core, security, reports, cron, dev-codegen, dev-entity-builder, updates, config, and log-type tests to use direct calls or shared helpers instead of repeated reflection scaffolding.
- Removed two `ConvUtils` tests that only froze private helper details already covered through stronger public behavior.
- Added `docs/prompts/app_test_bootstrap_cleanup.md` and linked it from prompt docs and framework-upgrade guidance so downstream apps can drop inherited framework implementation tests while retaining smoke and app-specific coverage.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/agents/tasks/index.md`
- Targeted `docs/drafts/FPF-Spec.md` sections found by heading/search: current-practice grounding, parsimony, strict distinctions, regression/conformance harnesses, and plain technical rewriting.
- Current working tree status before edits: unrelated untracked `.jshintrc` and `docs/agents/tasks/summary-2026-06-23-uat-testing.md`.
- Test files under `osafw-tests/App_Code/fw` and `osafw-tests/App_Code/security` with reflection, duplicate context/session setup, or private-helper-only coverage.
- Nearby framework internals under `osafw-app/App_Code/fw`, `osafw-app/App_Code/controllers/AdminReports.cs`, `osafw-app/App_Code/models/Dev`, and `osafw-app/Program.cs`.
- Prompt docs under `docs/prompts`.

## Commands used / verification
- `Get-Content docs\agents\local_instructions.md | Select-Object -Index (0..220)`
- `Get-Content docs\README.md | Select-Object -Index (0..220)`
- `git -c core.quotepath=false status --short`
- `Get-Content docs\agents\tasks\index.md | Select-Object -Index (0..260)`
- `rg -n "SoTA definition|Two-part SoTA test|Ontological Parsimony|Strict Distinction|Conformance Checklist|Private method|private method|plain|apparatus|regression" docs\drafts\FPF-Spec.md`
- `rg -n "System\.Reflection|BindingFlags|GetField\(|GetMethod\(|GetNestedType\(|GetConstructors\(|\.Invoke\(" osafw-tests`
- `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_cleanup\` - passed, 691 tests.
- `dotnet test osafw-tests\osafw-tests.csproj -p:DefineConstants=isSQLite -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_cleanup_sqlite\` - passed, 698 tests; restore/build emitted existing `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory.
- `powershell -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 ... -Check` - passed for edited/created files.
- `git -c core.quotepath=false diff --check` - passed.

## Decisions - why
- Cleanup is conservative: remove or merge only tests that duplicate stronger behavior coverage, freeze temporary implementation shape, or can be simplified with a small framework-owned setup seam.
- Framework tests should remain in the framework repository; downstream app projects should start with app-specific tests plus a small smoke suite.
- Use plain project language in deliverables rather than terminology from the planning source unless a precise technical term is needed.
- Internal seams were preferred over public test-only APIs. The changes expose deterministic framework collaborators and reusable helper logic to the test assembly while keeping app-facing public contracts unchanged.
- Assistant feature tests still contain reflection. They cover a newer, separate area with active history; converting them would require a focused assistant-contract pass rather than a broad framework-test cleanup.
- No `docs/CHANGELOG.md` entry is needed because there is no public app API, route, schema, config, storage, template/include, security default, or frontend contract change.
- No stable domain/glossary/ADR update is needed; the only reusable project guidance added is the downstream app test cleanup prompt.

## Pitfalls - fixes
- `ISession.SetString` extension calls in refactored `FwTests` needed `using Microsoft.AspNetCore.Http;` restored.
- Direct constant assertions triggered MSTest analyzer warnings; tests now read constants through tiny helpers while still asserting stable values.
- Initial friend-assembly attribute was placed under ignored `osafw-app/Properties/`; review caught that it would not be tracked, so it was moved into tracked source and then consolidated into `osafw-app/Program.cs`.
- Edited files were normalized to CRLF with the repo helper.

## Risks / follow-ups
- Remaining reflection in `AssistantFeatureTests` should be handled only in a focused assistant-test pass, if that area is stable enough to define internal seams.
- Internal seams are intentionally small, but they still increase the internal surface visible to the test project; avoid expanding this pattern for one-off branch coverage.
- SQLite test build still reports the existing `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory.
- Review loop used `docs/agents/code_reviewer.md`; self-review found and fixed the ignored `AssemblyInfo.cs` issue. No further issues found.

## Heuristics (keep terse)
- Prefer deleting tests that only pin private helper shape when equivalent public behavior coverage exists.
- Prefer internal test seams over reflection for durable framework contracts, but do not create public test-only APIs.
- Keep inherited framework implementation tests out of downstream app repos unless the app intentionally forks that behavior.

## Testing instructions
- Default framework tests: `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_cleanup\`
- SQLite-constant framework tests: `dotnet test osafw-tests\osafw-tests.csproj -p:DefineConstants=isSQLite -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_cleanup_sqlite\`
- Text normalization check and `git diff --check` passed after edits.

## Reflection
- The main slowdown was identifying which reflection represented broad framework-test debt versus active assistant-specific test scaffolding. Future passes should scope reflection cleanup by subsystem and avoid mixing stable framework helpers with newer feature internals.
- The repo normalization helper is useful; run it after all doc/test edits instead of chasing Git CRLF warnings one file at a time.
- Self-review was enough for this bounded cleanup. The review instruction caught a real process issue: files under ignored `Properties/` are a poor place for required source attributes.
