## What changed
- Moved the Assistant memory compaction user prompt into `osafw-app/App_Data/template/assistant/prompts/memory_compaction_user.md`.
- Moved synthetic user-message framing for prompt text, clarification answers, and file-upload context into `osafw-app/App_Data/template/assistant/prompts/user_message.md`.
- Updated focused Assistant tests and docs so these prompt override points are explicit.
- Updated the task index for this implementation summary.

## Scope reviewed
- `AssistantRunProcessor` memory compaction prompt construction.
- `AssistantAppService` user message construction.
- Existing Assistant prompt template conventions.

## Commands used / verification
- `rg -n 'Existing memory|Conversation excerpts|Return one concise durable user memory summary only|Clarification answers|Files were uploaded with this message' osafw-app\App_Code osafw-app\App_Data\template\assistant\prompts osafw-tests\App_Code\fw\AssistantFeatureTests.cs`
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~AssistantFeatureTests" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_prompt_template_tests\`
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_prompt_template_build\`
- `powershell -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 ... -Check`
- `git diff --check`

## Decisions - why
- Kept P3 tool `[Description]` strings in C# per user direction.
- Used one `user_message.md` template for the whole submitted-message envelope so apps have a single override point.

## Pitfalls - fixes
- `apply_patch` created LF endings; normalized all touched files to UTF-8 without BOM and CRLF.

## Risks / follow-ups
- No docs/CHANGELOG.md entry added because this only moves unreleased Assistant prompt text into override templates and does not change a released app contract.
- P3 tool `[Description]` strings remain hardcoded by explicit user direction.

## Heuristics (keep terse)
- None added.

## Testing instructions
- Focused Assistant tests passed: 40 passed, 0 failed.
- App build passed: 0 warnings, 0 errors.
- Formatting checks passed.

## Reflection
The review was slowed mostly by separating prompt prose from normal C# status/log/metadata strings. Future passes should classify assistant strings by runtime role first: prompt/user-message shaping belongs in `App_Data/template/assistant/prompts`, while tool schema metadata may remain code-owned unless the extension contract changes. Direct self-review was enough for this small, verified diff; a sub-agent would have added overhead.
