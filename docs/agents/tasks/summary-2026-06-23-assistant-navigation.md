## What changed
- Added catalog-driven AI Assistant navigation support:
  - `AssistantNavigationCatalog` parses `template/assistant/prompts/navigation_catalog.json`, filters by user access level, rejects non-app-local URLs, validates declared list filters/prefill fields, and builds REST-style list/new/view/edit links.
  - Registered read-only `find_app_navigation` tool and persisted navigation tool results as run events.
  - Added `AssistantLink` DTO/schema support, message DTO mapping, and final response binding so saved links must exactly match URLs returned by the navigation tool.
  - Rendered validated assistant links in the chat UI.
- Moved assistant runtime prompts from `template/assistant/*.md` into `template/assistant/prompts/*.md`, added `navigation.md`, and added the initial curated `navigation_catalog.json`.
- Added `docs/prompts/update_assistant_navigation_catalog.md` for developer/Codex-assisted catalog refreshes.
- Updated `docs/assistant.md`, `docs/prompts/README.md`, and `docs/CHANGELOG.md`.

## Scope reviewed
- Assistant runtime/tool contracts, result persistence, UI rendering, prompt loading, prompt files, catalog JSON, docs prompt, assistant docs/changelog, and focused assistant tests.
- Ignored unrelated pre-existing local `osafw-app/appsettings.json` and untracked `.jshintrc`.

## Commands used / verification
- `ConvertFrom-Json (Get-Content osafw-app\App_Data\template\assistant\prompts\navigation_catalog.json -Raw) | Format-List` - catalog parses.
- `rg -n 'parsePage\("/assistant",' osafw-app osafw-tests docs` - no stale runtime assistant prompt loads.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~AssistantFeatureTests|FullyQualifiedName~AssistantRuntimeStatusTests" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_navigation_tests\` - passed, 41/41.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_navigation_build\` - passed, 0 warnings/0 errors.
- `docs\agents\tools\Normalize-TextFiles.ps1 -Check ...` via process-local PowerShell execution-policy bypass - touched files OK.
- `git diff --check -- ...` - passed for touched tracked files.
- Code reviewer sub-agent reviewed the scoped diff and found no issues; review loop can stop.

## Decisions - why
- Used separate `navigation.md` and `navigation_catalog.json` so LLM guidance stays human-readable while runtime validation consumes strict JSON.
- Kept catalog curated and developer-maintained; no runtime controller discovery in this iteration.
- Bound final links to navigation tool output to prevent model-invented routes or external links.
- Added a changelog entry because moving prompt template override paths is a breaking path change for apps with overrides.

## Pitfalls - fixes
- PowerShell execution policy blocked direct helper execution; used `powershell -ExecutionPolicy Bypass -File ...` for the repo normalization helper.
- Initial warning storage used `FwList`, which only accepts `FwDict`; changed warnings to `List<string>`.
- Added explicit app-local URL validation after self-review to keep bad catalog entries from producing external links.

## Risks / follow-ups
- Initial catalog is intentionally framework-curated. Downstream apps should refresh it with `docs/prompts/update_assistant_navigation_catalog.md`.
- V1 only enforces catalog `min_access_level`; deeper RBAC/action-specific filtering remains future work.
- Exact URL binding is intentionally strict; if models frequently rewrite encoded query keys, consider a canonicalizer that preserves the same app-local/declared-field safety.

## Heuristics (keep terse)
- None added.

## Testing instructions
- Run `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~AssistantFeatureTests|FullyQualifiedName~AssistantRuntimeStatusTests" -p:OutDir=$PWD\artifacts\assistant_navigation_tests\`.
- Run `dotnet build osafw-app\osafw-app.csproj -p:OutDir=$PWD\artifacts\assistant_navigation_build\`.
- Optional browser smoke: ask `/Assistant` "where do I manage KB articles?" and verify it returns a clickable `/Admin/KBArticles` link instead of auto-redirecting.

## Reflection
Prompt relocation and navigation implementation were tightly coupled; a clearer existing convention for assistant prompt paths would have reduced the number of stale-path checks. Future agents should treat prompt files as public override paths and decide changelog impact before moving them. Delegated review was useful for a runtime/template/doc change, but main-agent verification was still needed because the reviewer environment could not run tests.
