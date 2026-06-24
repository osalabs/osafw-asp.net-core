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
- Fixed the Admin Settings category tabs so they auto-build from `settings.icat` values: unfiltered settings are shown under `All`, empty category rows stay under `Site`, and AI rows show under the database-provided `AI` tab.
- Removed the default sidebar Assistant link; users enter Assistant through the `/Main` dashboard pane.
- Moved the `/Main` Assistant pane into the lower-right dashboard slot under `Events per day`, gave it `bi-stars`, linked the pane icon/title to `/Assistant`, and moved `Active Users` under `Last Events`.
- Reworked `/Assistant` into a centered composer-first start state, thread-focused content area, modal history, custom file upload button, and drag/drop file support while keeping the existing JSON endpoints.
- Follow-up feedback removed the non-breaking Assistant entry-point changelog note, added the `bi-stars` icon into the dashboard Assistant title, moved Settings tab URL/active/label rendering into ParsePage, vertically aligned the Assistant topbar actions with the title, and reduced Assistant CSS by moving layout/border/shadow work to Bootstrap utility classes.
- Rebuilt `/Admin/KBArticles` and `/Admin/RagChunks` on `FwDynamicController` with `config.json` field/list definitions, standard Dynamic page headers/filter/list/form shells, compact RAG status badges, and small custom partials only for KB/RAG-specific actions, badges, and filters.
- Latest feedback pass moved remaining KB/RAG admin readiness/count/detail queries out of controllers into `KBArticles`, `RagChunks`, and `RagSources`; removed controller-supplied page titles/base URLs where templates provide them; removed `AdminKBArticlesController.applyViewListConversions`; rendered KB access/status list labels through ParsePage custom columns; and made RAG admin list SQL model-provided instead of hardcoded in JSON.
- Added KB article multi-file uploads through the standard Dynamic attachment block. KB Content is no longer required; saved articles can upload multiple general files under `FwEntities.ICODE_KB`, queue/reconcile current supported attachment sources, and remove stale RAG attachment sources/chunks when files are removed.
- Added queued worker-side KB Content autofill for blank articles: supported attachments are parsed during RAG indexing, summarized through the configured LLM, written only while Content is still blank, and followed by a queued article-body source. Summary failures fall back to deterministic Markdown from filenames, sections, and snippets without blocking file indexing.
- Added the standard Bootstrap Markdown editor to the KB article Content field by marking the Dynamic textarea with `markdown autoresize` and including `common/markdown_editor` on the KB article showform page.
- Moved KB attachment summary system prompt, user prompt, and deterministic fallback Markdown into ParsePage templates under `osafw-app/App_Data/template/assistant/prompts/`; `DocumentEmbeddingService` now only shapes article/document data and renders those templates with `fw.parsePage`.

## Scope reviewed
- `docs/agents/local_instructions.md`, `docs/README.md`, `docs/assistant.md`, `docs/db.md`, `docs/crud.md`, `docs/templates.md`, `docs/dashboard.md`.
- Existing Assistant/KB implementation: assistant controllers, `DocChunks`/`RagChunks`, `DocumentEmbeddingService`, KB/Spages models, worker, run processor, templates, SQL scripts, and focused tests.
- Prior task context from `docs/agents/tasks/index.md`, `summary-2026-06-12-assistant-port.md`, and the local review draft summary.
- Review-loop focus: source queue lifecycle, no-key behavior, migration paths, retrieval evidence, memory compaction, dashboard form handoff, and route/template contracts.
- Feedback pass focus: data-driven Settings tabs, dashboard/sidebar placement, Assistant UI layout, and Dynamic-controller parity for the KB/RAG admin screens.
- Latest upload pass focus: KB Dynamic attachment rendering, `SaveAttFilesAction` entity binding, attachment cleanup, source reconciliation, worker-side attachment parsing/summary autofill, docs, and focused Assistant tests.

## Commands used / verification
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_premerge_build\` - passed after the Settings tab fix.
- `dotnet test osafw-tests\osafw-tests.csproj --filter AssistantFeatureTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_premerge_tests\` - passed after the Settings tab fix, 16/16.
- `git diff --check` - passed.
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 ...` - normalized/checked touched files as UTF-8 without BOM and CRLF.
- Byte check on `osafw-app\App_Data\template\admin\ragchunks\url.html` - confirmed single line with no trailing newline byte.
- `rg -n "DocChunks|doc_chunks|AdminDocChunks|Admin/DocChunks|models[/\\]RagChunks|models[/\\]RagSources" osafw-app osafw-tests docs --glob "!docs/drafts/**" --glob "!docs/agents/tasks/**"` - no matches.
- Targeted SQL check over provider update scripts confirmed no table/data migration commands remain; only create-table/index DDL plus idempotent `settings` seed inserts remain.
- Admin Settings tab self-review confirmed the old `f[icat]=` Site link was not filtering because empty categories were skipped; the controller now treats an explicit empty category as a filter.
- Browser smoke on the VS-hosted app at `https://localhost:44315/` verified no sidebar Assistant item, dashboard ordering/linking/icon behavior, Settings `All`/`Site`/`AI` tabs and filters, Assistant centered composer, hidden file inputs with visible file buttons, no-key unavailable message, and History modal opening. Follow-up smoke also verified the dashboard Assistant title icon, template-rendered Settings tabs, no Assistant rows on `Site`, no client script syntax errors, and Assistant title/actions center alignment.
- Visual Studio MCP confirmed the expected solution/startup project were loaded before browser verification; it also built the solution and launched the app without debugging after C# changes.
- Latest KB/RAG feedback pass: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_premerge_build\` passed after review fixes; `dotnet test osafw-tests\osafw-tests.csproj --filter AssistantFeatureTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_premerge_tests\` passed 16/16 after review fixes; `git diff --check` passed before task-summary closeout.
- Latest browser smoke at `https://localhost:44315/` after VS build/relaunch verified `/Admin/KBArticles` uses the standard Dynamic filter/table with Add New and RAG Chunks actions, `/Admin/RagChunks` uses the standard Dynamic filter/table with compact badges plus Entity/Backend filters and no Add New action, `/Admin/KBArticles/new` loads the standard edit shell without autosave, and browser console errors were empty. A second smoke after review fixes confirmed the same list/form signals.
- Latest controller/model cleanup was not browser-smoked after the final C# changes because the exposed VS MCP controls could not safely restart the user's no-debug IIS Express process. Visual Studio MCP confirmed the expected solution is loaded, and standalone build/tests passed against the final code.
- Concise verification evidence was captured in `docs/agents/artifacts/assistant-premerge-verification-2026-06-13.md`.
- Review loop: sub-agent reviewer found migration, queue-claim, and failed-reindex issues; after fixes, focused re-review reported no blocker/high/medium issues and allowed the loop to stop. Final feedback pass used an independent read-only reviewer against the changed Settings/dashboard/sidebar/Assistant UI/docs diff; no issues found and the review loop can stop.
- Latest KB/RAG Dynamic-screen review: sub-agent reviewer was spawned but did not finish after two bounded waits, so the main agent performed the `docs/agents/code_reviewer.md` review locally. Findings fixed: existing KB edit screens needed an explicit standard Save/Cancel row after autosave was removed, and both new Dynamic lists needed `row_click_url.html` partials for standard row navigation/edit links. Review loop can stop after the fixes and rerun checks.
- Latest cleanup review: sub-agent reviewer was spawned but timed out and was closed; the main agent performed the `docs/agents/code_reviewer.md` review locally. Finding fixed: assigning only `config["list_view"]` after `loadControllerConfig()` left the controller `list_view` field on the base table, which would break `/Admin/RagChunks` joined-column filters/listing. The fix assigns both `list_view` and `config["list_view"]` from `RagChunks.adminListViewSql()`. Review loop can stop after rerun build/tests.
- KB upload pass: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_premerge_build\` passed 0 warnings/0 errors; `dotnet test osafw-tests\osafw-tests.csproj --filter AssistantFeatureTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_premerge_tests\` passed 20/20; `git diff --check` passed; CRLF check via `Normalize-TextFiles.ps1 -Check` passed for all task-touched files.
- KB upload browser smoke on the VS-hosted app: created article `2` with blank Content, verified the new edit form shows save-first file upload, uploaded `sample-kb-one.txt` and `sample-kb-two.html` through the same multipart `SaveAttFiles` endpoint, reloaded `/Admin/KBArticles/2/edit`, and confirmed both files render in the standard attachment block. `/Admin/RagChunks` showed `Sources 4`, `Queued 4`, `Chunks 0`; the local app has `ASSISTANT_WORKER_ENABLED=false`, so worker-side chunking and Content autofill were not observable in that run.
- KB upload review loop: independent sub-agent reviewer followed `docs/agents/code_reviewer.md`, reviewed the changed controller/model/service/template/test/docs diff plus nearby dynamic/attachment context, found no issues, and allowed the loop to stop. Main-agent self-review also made a small summary-fence/fallback-title cleanup before rerunning build/tests.
- KB markdown editor follow-up: `dotnet test osafw-tests\osafw-tests.csproj --filter AssistantFeatureTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_premerge_tests\` passed 20/20; `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_premerge_build\` passed 0 warnings/0 errors; browser smoke on `/Admin/KBArticles/2/edit` confirmed the Content textarea has `markdown autoresize font-monospace`, one `.md-editor` wrapper is present, and the textarea is wrapped by the markdown editor. Main-agent review found no issues; the change is a small Dynamic/template follow-up and the review loop can stop.
- KB summary prompt-template follow-up: `dotnet test osafw-tests\osafw-tests.csproj --filter AssistantFeatureTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_premerge_tests\` passed 20/20; `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_premerge_build\` passed 0 warnings/0 errors. `rg` confirmed the summary prompt/fallback prose is no longer in `DocumentEmbeddingService`; it appears only in prompt templates and test assertions. Main-agent review found no issues; the refactor is small and directly verified, so the review loop can stop.

## Decisions - why
- Use a separate task summary from the architecture review because this task changes runtime behavior, schema, templates, docs, and tests.
- `rag_sources` owns lifecycle and queue state; `rag_chunks` keeps denormalized source/entity fields for faster retrieval and stable citations.
- Missing `OPENAI_API_KEY` is checked before assistant writes, and RAG queue helpers also require Assistant enabled plus OpenAI configured, to satisfy the no-data-creation requirement.
- Index replacements are staged in memory and swapped only after embeddings are built, so transient parser/provider failures preserve prior good chunks.
- Assistant update scripts are first-application schema plus settings seed scripts only; because these updates were never applied to live databases, they do not migrate or backfill old `doc_chunks` rows.
- Users/contacts stay out of RAG and are exposed only through a read-only `LIKE` search tool.
- RAG models live in `models/AI` to keep AI-related model code grouped together.
- KB article files use the existing save-first Dynamic `att_files_edit` UX instead of a custom one-step create/upload form, keeping uploads on saved rows with stable parent ids.
- KB attachments are stored under `FwEntities.ICODE_KB` rather than `kb_articles` so the attachment UI, source queue, indexing, and cleanup all speak the same entity contract.
- Content autofill lives in the queued indexing path so saves/uploads never make synchronous parsing, embedding, or LLM summary calls. The database write is conditional to protect user-authored Content.
- KB Content uses the existing markdown editor include instead of a custom field type so it matches Spages/Roles/Dynamic demo behavior and keeps preview sanitization defaults centralized in `common/markdown_editor`.
- KB summary wording is kept in ParsePage templates so projects can override prompt/fallback text without changing C#; C# still owns parser selection, truncation, and data shaping.

## Pitfalls - fixes
- Running app build and tests in parallel once caused an intermediate `osafw-app.dll` lock; reran verification serially with isolated `OutDir`.
- Initial non-SQL Server source claim used a raw list in an update predicate; fixed to use `db.opIN(...)` so the affected-row check is atomic.
- User feedback clarified that `doc_chunks` migration/backfill is unnecessary because the update scripts have not been applied yet; removed rename/alter/drop/backfill DML from the assistant update scripts and kept idempotent AI settings inserts.
- Moved `RagChunks.cs` and `RagSources.cs` into `osafw-app\App_Code\models\AI\`; the namespace stayed `osafw`, so no call-site churn was needed.
- Initial replacement indexing deleted rows before embeddings were regenerated; changed to build chunks first, then delete/insert/mark indexed.
- The Settings page previously used hardcoded tabs and the controller ignored explicit empty `icat`, so AI rows appeared on Site. Replaced the tabs with database-driven category rows, treated `f[icat]=` as an empty-category filter, and moved URL/label/active tab presentation into the ParsePage template.
- ParsePage treats backticks as translation markers, so JavaScript template literals inside inline templates render invalid client script. Replaced Assistant inline script template literals with string concatenation and kept backticks only in translated HTML text.
- Visual Studio MCP could not stop the user's no-debug run because it reported no active debugging session; the port was not listening after the failed launch/build, so the app was relaunched from VS after the compile fix.
- The Dynamic edit shell normally adds autosave for existing records, but KB saves queue indexing and the custom save path is optimized for full form posts; the KB edit template keeps the standard layout while intentionally omitting `data-autosave`, and the save path now preserves existing code/access/status on partial submissions.
- Dynamic list screens need an `index/row_click_url.html` partial when using the common list table; otherwise row clicks and the standard Edit link can render blank targets.
- `loadControllerConfig()` copies `config["list_view"]` into the controller `list_view` field during initialization; any runtime override after loading must update both values before `IndexAction()` uses list SQL.
- Dynamic attachment field preparation uses `model0.table_name` by default; KB overrides the prepared `att_files` data and upload URL/prefix/entity so existing saved KB files display from the `kb` entity.
- Dynamic `processSaveAttFiles()` also deletes by `model0.table_name`; KB save now reconciles posted `kb_files` against `FwEntities.ICODE_KB` before re-queueing the article.
- The in-app browser API did not expose file chooser upload, so the smoke used the browser-authenticated UI for save/display verification and a separate local curl session for the same multipart upload endpoint shape (`file1`, `XSS`, `item[att_category]`, `item[item_id]`).

## Risks / follow-ups
- Provider-specific update scripts were reviewed statically but not executed against live SQL Server/MySQL/SQLite databases.
- The assistant update scripts now create assistant/KB/RAG schema objects and seed AI setting rows; administrators still need to fill `OPENAI_API_KEY` and enable `ASSISTANT_ENABLED`.
- Full `dotnet test` was not run; focused Assistant tests and app build passed.
- Browser smoke used the local SQL Server-backed VS app only; provider-specific SQL scripts still need database execution in their target engines.
- `/Admin/RagChunks` remains a read-only inspection screen; it uses the standard Dynamic list table but keeps row actions to View-only and leaves destructive cleanup behind the existing explicit `DeleteEntity` POST action on the detail page.
- The KB upload pass browser-smoked request-side upload/display/source queueing only. Worker-side chunk creation and Content autofill need a local run with `ASSISTANT_WORKER_ENABLED=true` or a manually invoked `AssistantRunProcessor`; the current VS-hosted app had the worker disabled in `appsettings.json`.
- Supported KB attachment parsing remains limited to the formats currently handled by `DocumentEmbeddingService` parsers: text, HTML, and DOCX-like documents. Unsupported files upload normally but do not queue for RAG until a parser is added.
- The smoke created local dev article `2` and attachments `134`/`135` in the VS-hosted database; they can be removed manually if the local database should stay clean.

## Heuristics (keep terse)
- Added a 2026-06-12 heuristic to `docs/agents/heuristics.md`: avoid JavaScript template literals inside ParsePage templates because backticks are translation markers. No stable framework facts or ADRs were added; no `AGENTS.md` change was needed.

## Testing instructions
- Apply the matching provider update script, configure `ASSISTANT_ENABLED=true` and `OPENAI_API_KEY`, then run/observe the worker to process queued `rag_sources`.
- Verify settings categories at `/Admin/Settings/?dofilter=1`: `All` is unfiltered, `Site` uses `f[icat]=`, and `AI` uses `f[icat]=AI`.
- For KB uploads: save a KB article with blank Content, upload supported text/HTML/DOCX files on the edit screen, confirm files render, enable/run the assistant worker, then confirm RAG sources become chunks and Content is filled only if it is still blank.
- For local verification: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=$PWD\artifacts\assistant_premerge_build\` and `dotnet test osafw-tests\osafw-tests.csproj --filter AssistantFeatureTests -p:OutDir=$PWD\artifacts\assistant_premerge_tests\`.

## Reflection
- The task was slowed mostly by cross-provider migration details and a parallel build/test lock. Future Assistant/RAG changes should avoid parallelizing builds that share project intermediates.
- The sub-agent review was useful: it caught migration and worker-race issues that compile/tests would not falsify, and the final smaller reviewer pass was quick enough to use instead of a self-review fallback.
- Inline JavaScript in ParsePage templates should avoid JavaScript template literals because the parser consumes backtick-delimited text.
- For future schema-heavy assistant work, review legacy upgrade behavior before runtime code polish, and decide early whether each provider preserves or intentionally clears draft data.
- Browser upload verification is faster when a tool can set file inputs directly; when it cannot, use the app's exact multipart endpoint with a separate local HTTP session, then verify rendered UI in the browser. For worker-dependent smoke tests, check `ASSISTANT_WORKER_ENABLED` before waiting on asynchronous state.
