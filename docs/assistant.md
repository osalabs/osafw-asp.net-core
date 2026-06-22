# Assistant, LLM, And Knowledge Base

The assistant is an optional read-only RAG starter for framework apps. It provides threaded chat, queued source indexing, hybrid retrieval, citations, feedback, sharing, and an optional background worker.

## Configuration

The feature is disabled by default. Configure these rows in Site Settings under the `AI` category. Site Settings category tabs are generated from the `settings.icat` values currently present in the database.

- `ASSISTANT_ENABLED=true`
- `OPENAI_API_KEY`
- `ASSISTANT_MODEL=gpt-5-mini`
- `ASSISTANT_VECTOR_MODE=auto`
- `ASSISTANT_MEMORY_ENABLED=false`
- `ASSISTANT_MAX_FILES_PER_MESSAGE=5`
- `ASSISTANT_MAX_INDEXED_FILE_BYTES=5242880`
- `ASSISTANT_MAX_INDEX_CHARS=200000`
- `ASSISTANT_MAX_INDEX_CHUNKS=80`

Keep `ASSISTANT_WORKER_ENABLED=true` in `appSettings` when queued source indexing and queued chat runs should be processed by the web host. Chat submissions require this worker-enabled host, or an equivalent external processor using the same queue contracts, so disabled-worker setups show a setup warning instead of creating runs that stay queued forever. Missing tables or a missing OpenAI key must not block application startup. When the assistant is enabled but `OPENAI_API_KEY` is missing, the `/Main` Assistant block remains visible, and submissions return "Please contact administrator to configure AI Assistant." without creating assistant threads, messages, runs, sources, or chunks.

## Schema

Existing databases should apply:

- SQL Server: `osafw-app/App_Data/sql/updates/upd2026-06-12-assistant-rag.sql`
- MySQL: `osafw-app/App_Data/sql/mysql/updates/upd2026-06-12-assistant-rag.sql`
- SQLite: `osafw-app/App_Data/sql/sqlite/updates/upd2026-06-12-assistant-rag.sql`

New databases already include the same tables in the provider-specific `fwdatabase.sql` files.

Core tables:

- `kb_articles`: manager-maintained knowledge base records.
- `rag_sources`: one row per indexable source, including KB article bodies, KB attachments, published Spages, and assistant uploads. Source rows carry queue state, content hash, parser version, ACL snapshot, last indexed time, and last error.
- `rag_chunks`: chunk text, JSON embedding, optional denormalized source/entity fields, vector metadata, stable `source_id`/`chunk_id`, and citation metadata.
- `assistant_threads`, `assistant_messages`, `assistant_runs`, and `assistant_runs_events`: durable chat, run state, events, and persisted retrieval evidence.
- `assistant_feedback`: reviewable feedback data.
- `assistant_memories`: optional per-user compacted/sanitized memory summaries when enabled.

## Knowledge Base And Sources

Managers maintain articles at `/Admin/KBArticles`. Saving or reindexing an article queues `rag_sources` rows instead of calling the embedding provider during the request. The worker later parses, chunks, embeds, and writes `rag_chunks`.

Article Content is optional. New articles can be saved with title/access/status first; the edit screen then exposes a standard multi-file upload block. Uploaded files are attached to the KB article entity and reconciled with RAG sources whenever the article is saved, reindexed, or files are uploaded. Removed files also remove stale KB attachment sources and chunks.

When an article has blank Content and supported attachments are indexed, the worker parses the current attachments and asks the configured LLM for a concise Markdown summary. The write is conditional, so user-authored Content is never overwritten. If summary generation fails but parsing succeeds, the worker logs a warning, writes a deterministic Markdown fallback from filenames, headings, and snippets, and continues indexing the uploaded files. After Content is auto-filled, the article body source is queued so the generated summary is indexed too.

KB attachment summary prompts and fallback Markdown are ParsePage templates under `osafw-app/App_Data/template/assistant/prompts/`. Override `kb_summary_system.md`, `kb_summary_user.md`, and `kb_summary_fallback.md` to customize the generated Content wording without changing runtime code.

Indexed source types:

- KB article body.
- KB article attachments with supported text/HTML/DOCX-like parsers.
- Published Spages.
- Files uploaded to assistant messages.

Unsupported KB article files can still be uploaded and shown on the article, but they are not queued for RAG indexing until a matching parser is added.

Article access uses numeric `access_level`. Retrieval queries include `KBArticles.buildAccessWhere()`, so live KB retrieval does not return article chunks above the current user's access level. Shared thread links intentionally expose prepared materialized thread content to recipients.

Use `/Admin/RagChunks` to inspect source/chunk state, vector metadata, backend selection, and queue counts. Its main search box runs vector search against chunk content so administrators can test retrieval behavior directly; Entity and Backend remain normal list filters. The screen reports setup-needed if the schema is missing.

## Retrieval

Knowledge retrieval uses a hybrid merge:

- Dense vector similarity over embeddings.
- Keyword/simple `LIKE` scoring over chunk/source text.
- Source diversity limits so one source does not dominate the top results.

Search results include stable `source_id`, `chunk_id`, retrieval mode, and scores. Tool calls persist evidence in `assistant_runs_events`; final assistant citations are filtered against that evidence before the assistant message is saved. Retrieval traces are also written through the normal framework logger with query, mode, source IDs, chunk IDs, and scores.

Users/contacts are not indexed into `rag_chunks`. The assistant exposes a separate read-only contact search tool that uses simple `LIKE` queries against active `users` rows.

## Vector Backends

Embeddings are always stored as JSON text for portability. Fallback implementations:

- SQL Server: `OPENJSON`
- MySQL: `JSON_TABLE`
- SQLite: `json_each`

The default `ASSISTANT_VECTOR_MODE=auto` uses SQL Server native vector search only when the database exposes `TYPE_ID(N'vector')` and the optional `rag_chunks.embedding_vector` column exists. From-scratch `fwdatabase.sql` includes commented SQL Server 2025 DDL for developers to apply manually when their server supports native vectors.

The default embedding dimension is 1536 for `text-embedding-3-small`.

## Assistant Flow

`/Main` shows an AI Assistant dashboard block when `ASSISTANT_ENABLED=true`. The block posts the prompt to `/Assistant`, where the threaded chat continues. The default sidebar does not include a separate Assistant link.

`/Assistant` renders the chat shell. The UI starts with a centered composer, then switches to the thread view after a chat starts. It supports send, poll, history search in a modal, button-driven file upload, drag-and-drop files, feedback, share, and cited response display.

The run processor uses Microsoft Agent Framework packages and read-only tools:

- Knowledge base and Spages search.
- Current-thread attachment search.
- Simple users/contact search with `LIKE`.
- Clarification requests.
- Progress events.

The assistant must not execute generated SQL, mutate application records, redirect users based on model output, or invent navigation/actions. Future mutating workflows must go through explicit controller/model contracts, POST, XSS tokens, validation, access checks, confirmation, and audit logging.

## Memory

Memory remains optional and disabled by default. When enabled, completed runs compact recent conversation context with the configured LLM, then sanitize likely secrets, credentials, personal contact details, payment numbers, and IDs before storing `assistant_memories`.
