## What changed
- Added an `/Main` dashboard Assistant pane gated by `ASSISTANT_ENABLED`; it posts the prompt to `/Assistant` so the conversation continues in the full assistant shell.
- Kept Assistant unavailable when `OPENAI_API_KEY` is missing: the dashboard block still renders, but assistant turns fail with the standard admin-configuration message before thread/message/run/source/chunk rows are created.
- Renamed live `DocChunks`/`doc_chunks` code, templates, tests, and docs to `RagChunks`/`rag_chunks`; added first-class `RagSources`/`rag_sources`.
- Reworked indexing into a queued source flow for KB articles, KB attachments, Spages, and assistant uploads. Saves/uploads queue rows only; the hosted worker processes one RAG source per loop before assistant runs.
- Added Spages indexing, simple read-only contact search using `LIKE` on `users`, hybrid vector+keyword retrieval, source diversity, retrieval trace logging, source/chunk IDs, persisted evidence events, and evidence-bound final citations.
- Added optional SQL Server native vector-column support guarded by `TYPE_ID(N'vector')`; full schema includes commented SQL Server 2025 vector DDL for manual use.
- Moved `RagChunks` and `RagSources` under `osafw-app/App_Code/models/AI/` with the existing `osafw` namespace.
- Kept memory enabled as optional, but changed storage to LLM compaction plus sanitizer redaction before upsert.
- Updated Assistant docs, dashboard docs, changelog, SQL Server/MySQL/SQLite full schemas and update scripts, focused tests, and admin/debug templates.

## Scope reviewed
- `docs/agents/local_instructions.md`, `docs/README.md`, `docs/assistant.md`, `docs/db.md`, `docs/crud.md`, `docs/templates.md`, `docs/dashboard.md`.
- Existing Assistant/KB implementation: assistant controllers, `DocChunks`/`RagChunks`, `DocumentEmbeddingService`, KB/Spages models, worker, run processor, templates, SQL scripts, and focused tests.
- Prior task context from `docs/agents/tasks/index.md`, `summary-2026-06-12-assistant-port.md`, and the local review draft summary.
- Review-loop focus: source queue lifecycle, no-key behavior, migration paths, retrieval evidence, memory compaction, dashboard form handoff, and route/template contracts.

## Commands used / verification
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_premerge_build\` - passed after review-loop fixes.
- `dotnet test osafw-tests\osafw-tests.csproj --filter AssistantFeatureTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_premerge_tests\` - passed, 16/16.
- `git diff --check` - passed.
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 ...` - normalized/checked touched files as UTF-8 without BOM and CRLF.
- Byte check on `osafw-app\App_Data\template\admin\ragchunks\url.html` - confirmed single line with no trailing newline byte.
- `rg -n "DocChunks|doc_chunks|AdminDocChunks|Admin/DocChunks|models[/\\]RagChunks|models[/\\]RagSources" osafw-app osafw-tests docs --glob "!docs/drafts/**" --glob "!docs/agents/tasks/**"` - no matches.
- `rg -n "^\s*(INSERT|UPDATE|ALTER|DROP|RENAME|SET|PREPARE|EXECUTE|DEALLOCATE|EXEC\b|MERGE\b|DELETE\b)" osafw-app\App_Data\sql\**\upd2026-06-12-assistant-rag.sql` equivalent targeted check - no migration/DML commands in assistant update scripts.
- Review loop: sub-agent reviewer found migration, queue-claim, and failed-reindex issues; after fixes, focused re-review reported no blocker/high/medium issues and allowed the loop to stop. Later feedback-only schema/model move was self-reviewed against `docs/agents/code_reviewer.md`; no issues found.

## Decisions - why
- Use a separate task summary from the architecture review because this task changes runtime behavior, schema, templates, docs, and tests.
- `rag_sources` owns lifecycle and queue state; `rag_chunks` keeps denormalized source/entity fields for faster retrieval and stable citations.
- Missing `OPENAI_API_KEY` is checked before assistant writes, and RAG queue helpers also require Assistant enabled plus OpenAI configured, to satisfy the no-data-creation requirement.
- Index replacements are staged in memory and swapped only after embeddings are built, so transient parser/provider failures preserve prior good chunks.
- Assistant update scripts are first-application schema scripts only; because these updates were never applied to live databases, they do not migrate or backfill old `doc_chunks` rows.
- Users/contacts stay out of RAG and are exposed only through a read-only `LIKE` search tool.
- RAG models live in `models/AI` to keep AI-related model code grouped together.

## Pitfalls - fixes
- Running app build and tests in parallel once caused an intermediate `osafw-app.dll` lock; reran verification serially with isolated `OutDir`.
- Initial non-SQL Server source claim used a raw list in an update predicate; fixed to use `db.opIN(...)` so the affected-row check is atomic.
- User feedback clarified that `doc_chunks` migration/backfill is unnecessary because the update scripts have not been applied yet; removed rename/alter/drop/backfill/settings DML from the assistant update scripts.
- Moved `RagChunks.cs` and `RagSources.cs` into `osafw-app\App_Code\models\AI\`; the namespace stayed `osafw`, so no call-site churn was needed.
- Initial replacement indexing deleted rows before embeddings were regenerated; changed to build chunks first, then delete/insert/mark indexed.

## Risks / follow-ups
- Provider-specific update scripts were reviewed statically but not executed against live SQL Server/MySQL/SQLite databases.
- The assistant update scripts now create only assistant/KB/RAG schema objects. Existing databases still need AI settings configured in `settings` separately, as noted in the changelog/docs.
- Full `dotnet test` was not run; focused Assistant tests and app build passed.
- Browser/UI smoke was not run against a live database-backed app in this task.

## Heuristics (keep terse)
- No stable framework facts, reusable heuristics, or ADRs were added. The implementation followed existing agent guidance; no `AGENTS.md` change was needed.

## Testing instructions
- Apply the matching provider update script, configure `ASSISTANT_ENABLED=true` and `OPENAI_API_KEY`, then run/observe the worker to process queued `rag_sources`.
- For local verification: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=$PWD\artifacts\assistant_premerge_build\` and `dotnet test osafw-tests\osafw-tests.csproj --filter AssistantFeatureTests -p:OutDir=$PWD\artifacts\assistant_premerge_tests\`.

## Reflection
- The task was slowed mostly by cross-provider migration details and a parallel build/test lock. Future Assistant/RAG changes should avoid parallelizing builds that share project intermediates.
- The sub-agent review was useful: it caught migration and worker-race issues that compile/tests would not falsify.
- For future schema-heavy assistant work, review legacy upgrade behavior before runtime code polish, and decide early whether each provider preserves or intentionally clears draft data.
