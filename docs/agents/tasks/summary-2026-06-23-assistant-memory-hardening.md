## What changed
- Simplified Assistant memory to one sanitized per-user summary.
- Removed `terminology_json` and `preferences_json` from `AssistantMemories`, the run processor memory contract, and unreleased SQL Server/MySQL/SQLite fresh/update schema scripts.
- Changed memory compaction output schema to summary-only and prompt wording to request one durable memory summary.
- Hardened memory sanitization for API keys, bearer tokens, connection-string passwords, emails, phones, payment-like numbers, IDs, and long opaque tokens.
- Added summary storage guardrails: 2,000-character cap and skip for blank/redaction-only summaries.
- Applied the requested direct local SQL Server dev DB patch against `(local)` / `demo`; both removed columns now verify as `NULL`.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- Assistant memory model, run processor, prompts, schemas, docs, and focused tests.
- Existing unrelated worktree item ignored: untracked `.jshintrc`.

## Commands used / verification
- `rg -n "terminology_json|preferences_json|memory_terminology|memory_preferences|draft\.terminology|draft\.preferences" ...` - no runtime/schema/doc hits after implementation.
- `sqlcmd -S "(local)" -d demo -E -b -Q "<drop/verify assistant_memories columns>"` - succeeded; `COL_LENGTH` verification returned `NULL` for both removed columns.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_memory_build\` - passed, 0 warnings/0 errors.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~AssistantFeatureTests|FullyQualifiedName~AssistantRuntimeStatusTests" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_memory_tests\` - passed, 42/42 after correcting the cap test input.
- Code reviewer sub-agent reviewed the diff and flagged missing migration/drop steps for existing installs. Not applied because this directly conflicts with the user-approved plan: Assistant schema is unreleased, no new/shared migration should be added, and only the local dev DB should be patched directly.

## Decisions - why
- Kept memory opt-in and UI-less per user request.
- Edited the existing unreleased Assistant schema/update scripts instead of adding a migration because the feature has not shipped.
- Kept `assistant_memories.summary` as the only active memory payload so future prompt injection is simple and auditable.
- Used a direct local DB patch only for the current SQL Server `demo` database, not as a shared repo migration.
- Kept the unreleased update scripts create-only/final-shape only; local or draft databases that already ran an older Assistant script must be adjusted directly rather than encoded as shipped upgrade logic.

## Pitfalls - fixes
- The first length-cap test used a repeated long token, which was correctly redacted as an opaque token before capping; changed it to natural prose.
- `IsStorableMemorySummary` removes redacted placeholders and secret labels before deciding whether content remains meaningful.

## Risks / follow-ups
- Memory compaction remains best-effort and depends on the configured LLM succeeding after a completed run.
- No browser/manual memory smoke was run yet; focused tests cover the summary-only contract and sanitization.
- Existing local/draft databases that previously ran the old Assistant script need direct one-off cleanup; the local SQL Server `demo` database was cleaned in this task.

## Heuristics (keep terse)
- None added.

## Testing instructions
- Re-run the focused test and app build commands above.
- Verify local SQL Server columns with `COL_LENGTH('dbo.assistant_memories','terminology_json')` and `COL_LENGTH('dbo.assistant_memories','preferences_json')`; both should be `NULL`.

## Reflection
The main slowdown was making a deterministic cap test that did not overlap with opaque-token redaction. Future memory tests should use natural prose for length caps and separate token-redaction fixtures from truncation fixtures.
