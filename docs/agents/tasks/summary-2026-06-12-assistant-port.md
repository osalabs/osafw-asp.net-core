## What changed
- Ported the optional Assistant/LLM/Knowledge Base feature as a generic read-only RAG assistant.
- Replaced the old SQL-generation assistant screen with threaded chat, polling, history, share links, feedback, citations, and file upload display.
- Added assistant persistence models, run processing, read-only Agent Framework tools, OpenAI chat/embedding wrappers, document parsers, SQL-backed chunks, KB article admin screens, and document embedding admin screens.
- Added SQL Server, MySQL, and SQLite schema/update scripts plus docs and focused tests.

## Scope reviewed
- Startup and local instructions: `AGENTS.md`, `docs/agents/local_instructions.md`, and `docs/README.md`.
- Implementation source of truth: `docs/drafts/assistant_porting_plan_validated.md`; supplementary context from `docs/drafts/assistant_porting_plan.md`.
- Related docs: `docs/db.md`, `docs/crud.md`, `docs/templates.md`, `docs/layout.md`, `docs/design_system.html`, and `docs/naming.md`.
- Existing source reviewed around assistant templates/controller, AI model folder, SQL schemas, package references, layout sidebar, attachment helpers, dynamic admin patterns, and test helpers.
- Old task summaries were not opened beyond the index because this was a new implementation session.

## Commands used / verification
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=$PWD\artifacts\assistant_build\` - passed, 0 warnings, 0 errors.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~AssistantFeatureTests|FullyQualifiedName~SecurityQuickFixTests"` - passed, 41/41.
- `dotnet test osafw-tests\osafw-tests.csproj` - ran full suite, 633/634 passed; `UploadFileSave_WritesUnderModuleFolder` failed on an existing upload-path expectation unrelated to the assistant port.
- Browser smoke via the in-app browser for `https://localhost:44315/Assistant` was attempted and blocked by `ERR_CONNECTION_REFUSED`; `Test-NetConnection localhost:44315` also returned `TcpTestSucceeded: False`.
- SQL Server native vector smoke was attempted with `sqlcmd` and `System.Data.SqlClient`; both were blocked by local SQL connection/encryption/SSPI errors.
- Review loop used `docs/agents/code_reviewer.md`; reviewer findings were fixed and the focused test subset was rerun.
- `docs/agents/tools/Normalize-TextFiles.ps1 -Check` over edited files - passed.
- `git diff --check` - passed.
- Byte-level checks confirmed assistant/admin route literal `url.html` files have no trailing newline byte.

## Decisions - why
- The feature remains disabled by default and the optional worker is separately guarded by `ASSISTANT_WORKER_ENABLED` so startup is safe without schema/API keys.
- OpenAI is the v1 provider because the package guidance and current Agent Framework path centered on OpenAI Responses and embeddings.
- Embeddings use portable JSON vector storage with SQL JSON cosine fallback; SQL Server native vector mode is selected only when runtime detection succeeds.
- Generic model lookup was removed from the assistant tool catalog after review because a model-selected lookup surface would be too broad for a read-only RAG assistant.
- Offline `FW` instances now have an in-memory session backing store so worker runs preserve effective user/access context for KB filtering.
- Assistant indexing has configurable count, byte, parsed-character, and chunk caps to limit request time and embedding-provider cost.

## Pitfalls - fixes
- Initial Agent Framework package update required `OpenAI` 2.10.0 to avoid a package downgrade.
- Agent Framework/OpenAI APIs had current-signature drift; the LLM wrapper and run processor now use `GetResponsesClient()` and current `AsAIAgent` options.
- Read/search paths originally used entity get-or-create helpers; those were changed to non-mutating lookups so RAG reads do not create metadata rows.
- Reviewer found background access-level loss, generic lookup overexposure, upload indexing caps, route-literal formatting, and untracked runtime artifacts; code fixes were applied for the first three, formatting is handled in the closeout pass, SQLite runtime DB artifacts are now ignored, and the unrelated pre-existing `.jshintrc` remains unstaged.
- Local app and database smoke checks were blocked by machine state rather than code failures.

## Risks / follow-ups
- Manual UI/theme smoke remains required once the local Visual Studio app is actually listening on `https://localhost:44315/`.
- SQL Server 2025 native vector mode still needs a live database smoke after local connection/SSPI issues are corrected; JSON fallback is covered by tests.
- Full suite has one unrelated upload utility failure that should be triaged separately.
- Production OpenAI key, assistant schema migration, and optional worker enabling are deployment/configuration steps.

## Heuristics (keep terse)
- No stable framework facts, heuristics, or ADRs were added. The review finding about tool allowlists was fixed in code rather than promoted to shared guidance.

## Testing instructions
- Apply the provider-specific assistant migration script before enabling the feature on an existing database.
- Set `ASSISTANT_ENABLED=true` and configure `OPENAI_KEY` or `OPENAI_API_KEY`.
- Optionally set `ASSISTANT_WORKER_ENABLED=true` for background processing and adjust assistant upload/indexing caps as needed.
- Run `dotnet build osafw-app\osafw-app.csproj`.
- Run `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~AssistantFeatureTests|FullyQualifiedName~SecurityQuickFixTests"`.
- With the app running, smoke `/Assistant`, `/Admin/KBArticles`, and `/Admin/DocumentEmbeddings` in light and dark/theme modes.

## Reflection
- The most expensive part was package/API drift plus broad schema/template parity across three providers; future ports should validate package compatibility with a build before deep implementation.
- The reviewer sub-agent was effective: it caught real RAG access-control, tool-surface, and indexing-cost issues before closeout.
- Browser and SQL MCP/tooling could not complete local smoke checks because the app/database were unreachable; failing fast with concrete connection evidence was more useful than retrying.
- Better conventions around assistant tool allowlists and upload/indexing caps would reduce review churn in future LLM feature work, but this task-specific implementation is enough for now.
