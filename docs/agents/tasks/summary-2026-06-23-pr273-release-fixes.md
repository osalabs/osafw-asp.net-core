## What changed
- Addressed PR #273 release-review fixes: narrowed Assistant contact search output, blocked readonly Site Settings saves, added Assistant message object access, removed duplicate Assistant run timeout sweeps, repaired the dashboard security test fixture, made an upload utility test isolated-output-safe, and normalized Assistant-related constant names to upper case.

## Scope reviewed
- `docs/agents/local_instructions.md`, `docs/README.md`, `docs/assistant.md`, `docs/crud.md`, `docs/db.md`, `docs/naming.md`.
- Targeted code: Assistant runtime/messages/threads/run processor/worker/navigation, Assistant document embedding, Admin Settings, Main dashboard fixture tests.

## Commands used / verification
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=$PWD\artifacts\pr273_release_fixes_build\` - passed, 0 warnings.
- `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=$PWD\artifacts\pr273_release_fixes_tests\ --filter "FullyQualifiedName~AssistantFeatureTests|FullyQualifiedName~AdminSettingsControllerTests|FullyQualifiedName~SecurityGroup9ATests|FullyQualifiedName~UploadUtilsTests.UploadFileSave_WritesUnderModuleFolder"` - passed, 68/68.
- `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=$PWD\artifacts\pr273_release_fixes_tests\ --filter "FullyQualifiedName~AssistantFeatureTests"` - passed, 43/43 after the constant rename.
- `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=$PWD\artifacts\pr273_release_fixes_fulltests\` - passed, 683/683.
- `dotnet list osafw-asp.net-core.sln package --vulnerable --include-transitive` - passed, no vulnerable packages reported.
- `rg -n -P "\bconst\b\s+[A-Za-z0-9_<>,?]+\s+[A-Z0-9_]*[a-z][A-Za-z0-9_]*\s*=" osafw-app\App_Code\models\AI osafw-app\App_Code\fw\DocumentEmbeddingService.cs osafw-app\App_Code\controllers\Assistant.cs osafw-app\App_Code\controllers\AdminRagChunks.cs osafw-app\App_Code\controllers\AdminKBArticles.cs` - no remaining mixed-case const identifiers.
- `git -c core.quotepath=false diff --check` - passed.
- `docs\agents\tools\Normalize-TextFiles.ps1 -Check ...` on edited files - passed, CRLF/no BOM.

## Decisions - why
- Kept contact search available as an internal read-only assistant tool per user policy, but limited returned fields to `name`, `email`, and `title` plus `source_type` metadata.
- Added `AssistantThreads.isOwnerAccess()` so `AssistantMessages.isAccess()` can reuse the existing owner/thread policy without duplicating owner SQL.
- Removed the second `failTimedOutActiveRuns()` call because the first sweep already covers queued and processing timed-out runs.
- Renamed Assistant-related `const` identifiers to `UPPER_CASE` per framework naming guidance and the follow-up request.

## Pitfalls - fixes
- `AdminSettingsController.SaveAction()` overrides the base admin save path, so it needed its own `checkReadOnly()`.
- `SecurityGroup9ATests` used a fake dashboard DB but did not register fake `Settings` after `/Main` started reading `ASSISTANT_ENABLED`.

## Risks / follow-ups
- No release-blocking follow-ups found in this pass.
- No changelog entry added: these are PR #273 pre-release fixes without schema, route, template include, public config, or documented end-user upgrade changes.

## Heuristics (keep terse)
- None added.

## Testing instructions
- Run `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=$PWD\artifacts\pr273_release_fixes_fulltests\`.

## Reflection
- The useful speedup was running focused filters before full suite; it exposed the Admin Settings fixture gap quickly.
- Self-review using `docs/agents/code_reviewer.md` was sufficient for this small, tightly scoped diff; no sub-agent was needed.
- Isolated `OutDir` remains valuable, but tests should not assert physical paths include `/upload` when the app base path can shift under isolated output.
- No stable facts, heuristics, ADRs, or shared agent instructions were added.
