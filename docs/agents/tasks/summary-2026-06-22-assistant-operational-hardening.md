## What changed
- Implemented Assistant/RAG queue operational hardening for PR #273 branch `assistant`.
- Added RAG source retry metadata, bounded backoff retry eligibility, and admin requeue from `/Admin/RagChunks`.
- Changed Assistant chat runs from stale requeue to timeout failure with explicit retry UI/action.
- Added worker fairness so assistant runs are checked after at most three RAG source iterations.
- Extended `/Admin/RagChunks` diagnostics with failed/stuck sources, queued/processing/failed assistant runs, and recent retrieval evidence.
- Updated SQL Server, MySQL, and SQLite fresh schemas plus the existing Assistant update scripts only.
- Updated `docs/assistant.md` with timeout, retry, diagnostics, and RAG backoff behavior.
- Feedback pass: kept pure boolean predicates as `is*` names, kept side-effecting boolean-returning methods as action verbs such as `queue*`, `requeue*`, and `Process*`, and removed unreleased ALTER/index-rebuild migration logic from the existing Assistant update scripts.

## Scope reviewed
- Read `docs/agents/local_instructions.md`, `docs/README.md`, `docs/assistant.md`, `docs/agents/tasks/index.md`, and the requested recent Assistant task summaries.
- Reviewed Assistant/RAG models, worker, controller, templates, provider schemas/update scripts, and focused Assistant tests.
- Local config uses SQL Server database `demo`; pre-existing unrelated worktree changes are `osafw-app/appsettings.json` and `.jshintrc`.

## Commands used / verification
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~AssistantFeatureTests|FullyQualifiedName~AssistantRuntimeStatusTests" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_operational_hardening_tests\` - passed, 36/36.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_operational_hardening_build\` - passed, 0 warnings/errors.
- Re-ran the same focused test/build commands after the naming and SQL update-script feedback changes; both passed.
- Applied local SQL Server dev DB update to `(local)` / `demo`: added `rag_sources.index_attempt_no`, `rag_sources.next_retry_at`, recreated `IX_rag_sources_queue`, inserted `ASSISTANT_RUN_TIMEOUT_SECONDS=120`.
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check <task-owned files>` - passed; direct script invocation without bypass was blocked by local PowerShell execution policy.
- `git -c core.fsmonitor=false diff --check` - passed.
- Review loop using `docs/agents/code_reviewer.md` - issues found and fixed, then no remaining issues worth another loop.

## Decisions - why
- Fold schema changes into the existing Assistant update scripts because this branch is not deployed anywhere beyond the user's local dev database.
- Keep the existing Assistant update scripts create-only after feedback because this update has not shipped; local dev DB was handled directly instead of keeping ALTER migration logic in the shared scripts.
- Use default assistant run timeout of 120 seconds and RAG retry policy of 5 attempts with exponential backoff.
- Retry preserves existing assistant messages and queues a new run for the latest user message; this keeps history auditable and avoids hidden deletion/overwrite behavior.
- No `docs/CHANGELOG.md` entry was added because the work remains inside the existing undeployed Assistant upgrade path and does not add a separate public/breaking upgrade note.

## Pitfalls - fixes
- Fixed a timeout recovery race found during review: timed-out run failure now rechecks the timeout predicate in the `UPDATE`, so another worker cannot freshly claim an old queued run and then have it incorrectly failed by a stale selector.
- Initially fixed SQLite update-script parity with additive ALTERs, then removed those ALTERs after feedback because the Assistant update is unreleased and the script should only create the final table shape.
- Normalized files after patches to keep project-required CRLF line endings.

## Risks / follow-ups
- The new tests are focused mostly on static contracts and deterministic helper behavior rather than full live multi-worker database execution.
- Existing local/dev databases that already ran an older Assistant update script need direct local adjustment rather than shared ALTER logic; the SQL Server `demo` database was updated directly in this task.
- No browser UI pass was run; template behavior is covered by markup/static contract checks and build/test verification.

## Heuristics (keep terse)
- No stable reusable heuristics or ADRs were added.

## Testing instructions
- Re-run the focused test command and isolated app build listed above.
- Re-run `Normalize-TextFiles.ps1 -Check` for the task-owned files after any patch, then `git diff --check`.

## Reflection
- The biggest slowdown was provider-script policy drift: unreleased branch update scripts should stay create-only, with local dev DBs patched directly when needed.
- The review loop was useful; it caught a real multi-worker timeout race and the SQLite migration gap.
- Future Assistant queue work should include provider update-script checks in the first implementation pass, not only final review.
