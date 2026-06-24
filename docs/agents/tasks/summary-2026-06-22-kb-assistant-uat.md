## What changed
Performed Chrome-based UAT for `/Main`, `/Admin/KBArticles`, `/Admin/RagChunks`, and `/Assistant`, then implemented the requested follow-up fixes.

- Changed `/Admin/RagChunks` free-text search to run vector retrieval against chunk content instead of Dynamic `LIKE`, with Entity and Backend filters moved to the standard second filter row.
- Added an ID-only vector query path for that admin search so it does not hydrate Assistant citation/source metadata just to build the ranked chunk-id filter.
- Woke the assistant worker whenever `rag_sources` rows are queued or requeued so the first newly saved article starts indexing promptly.
- Updated Assistant UI: hidden duplicate history previews, source links open in a new tab, assistant bubbles are full width, assistant responses have a Copy Markdown button, and assistant datetime shares the feedback action row.
- Fixed KB list Enter-to-search behavior and duplicate hidden feedback modal IDs found during UAT.
- Restored compact DB parameter logging while preserving the `log_pii` privacy gate.
- Updated `docs/assistant.md` for the RAG Chunks vector-search behavior.

## Scope reviewed
- Read `docs/agents/local_instructions.md`, `docs/README.md`, `docs/assistant.md`, `docs/templates.md`, `docs/dynamic.md`, and `docs/agents/code_reviewer.md`.
- Reviewed KB/Assistant/RAG implementation references: `AdminKBArticles`, `AdminRagChunks`, `Assistant`, `RagChunks`, `RagSources`, `AssistantAppService`, `DB`, assistant templates/CSS/JS, RAG chunk templates, and focused tests.
- Used Chrome against `https://localhost:44315/Main`, `/Admin/KBArticles`, `/Admin/RagChunks`, and `/Assistant`.
- Reviewed `osafw-app/App_Data/logs/main.log` targeted lines for RAG/Assistant indexing and DB logging evidence.
- Ignored unrelated pre-existing worktree items: `osafw-app/appsettings.json`, `.jshintrc`, and `docs/agents/tasks/summary-2026-06-17-assistant-chat-ui.md`.

## Commands used / verification
- Visual Studio MCP: confirmed the solution, started `build_project` for `osafw-app/osafw-app.csproj`, and relaunched the VS-hosted app with `debugger_launch_without_debugging`; `build_status` was unavailable, so CLI build/test supplied the final pass/fail result.
- `dotnet test --filter "Assistant|DbLogging" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_uat_test\` - passed, 26 tests.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_uat_build\` - passed, 0 warnings/errors.
- `git -c core.fsmonitor=false diff --check -- ...` on scoped changed files - passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Normalize-TextFiles.ps1 -Check ...` on touched files - passed.
- Chrome UAT: created and kept KB article `UAT Queue Wake Test 20260622161626` with code `uat-queue-wake-20260622161626` and phrase `SILVER VECTOR 20260622161626`; verified it indexed immediately after save, produced chunks, appeared through RAG Chunks vector search, and was cited by Assistant.
- Chrome UAT: verified `/Assistant` title/placeholder/chips/send button, one user bubble after submit, run progressed past queued to completed, assistant source links use `_blank`, assistant bubble full width, Copy button copied response markdown, and history no longer repeated title text.
- Chrome UAT: verified KB article duplicate IDs were gone and Enter in the KB list search field submits the filter.
- Code review loop: delegated reviewer found two medium performance concerns. Fixed the eager metadata hydration by adding the ID-only vector query path. The remaining sync-request concern is noted as a framework constraint because `FW.callController` synchronously invokes actions and casts directly to `FwDict`; changing that would be a broader framework dispatch contract change outside this UAT fix. Re-review found no remaining issues and the review loop can stop.

## Decisions - why
- RAG Chunks uses vector-only search for non-empty free text, with no LIKE fallback, because this admin screen is intended to test retrieval behavior directly.
- Vector search returns ranked chunk IDs first, then the Dynamic list uses `id in (...)` plus a `case` order expression so existing list rendering, filters, pagination, and status behavior stay intact.
- Admin RAG search uses an ID-only vector query because the list only needs chunk ids; the full Assistant retrieval path still hydrates citation/source metadata.
- `RagSources.queueSource` wakes the same assistant queue signal used by Assistant runs because the hosted worker checks sources first but sleeps on that shared signal.
- DB logs unwrap `DBParamValue` only in the logging path, leaving execution parameter normalization unchanged.
- No `docs/CHANGELOG.md` entry was added because these are bug fixes and admin/Assistant UI improvements, not breaking public framework or schema upgrade changes.

## Pitfalls - fixes
- Initial UAT was blocked by OpenAI quota/provider errors; post-credit retest verified RAG indexing and Assistant completion.
- The app was running under VS/IIS Express, so builds/tests used isolated `OutDir` paths to avoid locked normal output.
- Visual Studio MCP build polling was unavailable in this session; final build verification used `dotnet build`.
- Chrome runtime did not expose reliable native file upload primitives, so attachment-backed retrieval remains a separate manual follow-up.
- The delegated reviewer did not return during the first wait, so self-review continued in parallel. When the reviewer later returned, the concrete hydration finding was fixed and the sync-action finding was assessed against the existing framework dispatch contract.

## Risks / follow-ups
- True shared-thread readonly behavior was not fully verified because share testing was done in the owner session; use a second authenticated user/session for that check.
- Actual native file picker/drag-drop upload and attachment-backed retrieval were not verified in this Chrome runtime.
- RAG Chunks vector search depends on the OpenAI embedding provider and current vector backend availability; if provider setup fails, the screen now reports vector-search failure instead of falling back to LIKE.
- `/Admin/RagChunks` vector search still blocks the synchronous request while obtaining the query embedding because controller actions are synchronous in the current framework; this is scoped to siteadmin testing and would need a broader async action-dispatch change to remove fully.
- List-value DB parameters log after expansion, so list inputs appear as the executed parameter names such as `@ids_0`, `@ids_1`.

## Heuristics (keep terse)
No stable heuristics, ADRs, or glossary/domain facts were added. Provider/quota behavior and Chrome upload limitations were task/environment-specific.

## Testing instructions
Run focused checks after future Assistant/RAG changes:

- `dotnet test --filter "Assistant|DbLogging" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_uat_test\`
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_uat_build\`
- In Chrome on `https://localhost:44315/`, save a unique KB article phrase, confirm `/Admin/RagChunks` indexes it and vector-searches it, then ask `/Assistant` for the exact phrase and confirm one user bubble, a completed assistant response, `_blank` citations, and Copy Markdown.
- Remaining targeted checks: second-user readonly share behavior and attachment-backed retrieval with a browser/runtime that supports file picker or multipart upload.

## Reflection
The biggest slowdown was separating real app behavior from provider setup, VS-hosted process state, and background-worker cadence. Future Assistant UAT should use a unique KB phrase, watch `/Admin/RagChunks` plus `main.log`, and test queue wakeups immediately after save. Visual Studio MCP helped relaunch the app, but CLI `OutDir` builds/tests were more reliable for final verification in this session. Chrome was effective for the UI and chat flow, but not for native upload or cross-user share coverage. The delegated reviewer was useful, but the first wait timed out; continuing local review in parallel kept the task moving while still catching the later performance feedback.
