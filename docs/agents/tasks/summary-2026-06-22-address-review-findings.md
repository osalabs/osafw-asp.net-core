## What changed
- Addressed PR review findings 1-4 for assistant citations, assistant thread share-code update-script parity, stale RAG source recovery, and KB attachment byte-limit enforcement.

## Scope reviewed
- Assistant result citation binding and evidence events.
- RAG source queueing, worker recovery, attachment parsing, and assistant upload limits.
- MySQL and SQLite `upd2026-06-12-assistant-rag.sql` update scripts.
- Existing assistant feature tests and source-level schema/worker contract tests.

## Commands used / verification
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~AssistantFeatureTests" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_tests\` - passed, 31/31.
- `git -c core.quotepath=false diff --check` - passed.
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check ...` on edited files - passed, all CRLF/no BOM.
- Independent reviewer sub-agent inspected the final diff and reported one upgrade-in-place concern for MySQL/SQLite `assistant_threads`; disposition below.

## Decisions - why
- Final assistant citation metadata is now overwritten from retrieval evidence so model-supplied URLs cannot survive evidence binding.
- RAG source recovery marks stale `processing` rows as `stale`, preserving the existing pending/stale claim flow.
- KB attachment size checks are enforced before queueing and before parsing to match the documented setting and avoid oversized parser work.
- MySQL/SQLite update scripts were edited directly because the branch is unreleased and the user asked not to add extra upgrade handling for those scripts.

## Pitfalls - fixes
- Initial patch context matched a similarly named helper in another model; re-read the exact service range and applied the citation binding change there.
- First focused test run failed due a source-contract assertion that expected the earlier `db.update` recovery shape; adjusted it after switching recovery to raw SQL with null-safe stale checks.
- Reviewer asked for existing-install ALTER/duplicate-resolution SQL for MySQL/SQLite uniqueness. Not applied because the user explicitly scoped finding 2 to updating the unreleased `upd2026-06-12` files, with no released upgrade path to preserve.

## Risks / follow-ups
- No remaining required follow-up for findings 1-4.
- Did not run the full solution test suite; focused assistant tests cover the changed contracts.
- No changelog entry needed: changes fix unreleased assistant implementation/update scripts and do not introduce a new end-user breaking upgrade beyond the existing assistant branch work.

## Heuristics (keep terse)
- Stable facts/heuristics/ADRs not added; this task fixed reviewed implementation defects without adding reusable project knowledge.

## Testing instructions
- Run `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~AssistantFeatureTests" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_tests\`.

## Reflection
- The independent review was useful for forcing an explicit disposition on release-state assumptions, but the user had already narrowed schema work to unreleased update files. Future agents should record that scope early in the task summary before reviewer handoff to reduce false-positive upgrade-path feedback.
- The line-ending helper was effective; keep using it immediately after `apply_patch` on this repo.
