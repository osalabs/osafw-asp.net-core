# Assistant, LLM, And Knowledge Base

The assistant is an optional read-only RAG feature for framework apps. It provides threaded chat, knowledge base retrieval, document attachment search, citations, feedback, sharing, and an optional background worker.

## Configuration

The feature is disabled by default. Configure these rows in Site Settings under the `AI` category:

- `ASSISTANT_ENABLED=true`
- `OPENAI_API_KEY`
- `ASSISTANT_MODEL=gpt-5-mini`
- `ASSISTANT_VECTOR_MODE=auto`
- `ASSISTANT_MEMORY_ENABLED=false`
- `ASSISTANT_MAX_FILES_PER_MESSAGE=5`
- `ASSISTANT_MAX_INDEXED_FILE_BYTES=5242880`
- `ASSISTANT_MAX_INDEX_CHARS=200000`
- `ASSISTANT_MAX_INDEX_CHUNKS=80`

Keep `ASSISTANT_WORKER_ENABLED=true` in `appSettings` when queued runs should be processed by the web host. The embedding model is a code constant in `LLM.MODEL_TEXT_EMBEDDING_3_SMALL`.

With `ASSISTANT_ENABLED=false`, the UI reports that the assistant is unavailable. With `ASSISTANT_WORKER_ENABLED=false`, the hosted worker is not registered and queued runs remain queued until another process calls the run processor. Missing tables or a missing OpenAI key must not block application startup.

## Schema

Existing databases should apply:

- SQL Server: `osafw-app/App_Data/sql/updates/upd2026-06-12-assistant-rag.sql`
- MySQL: `osafw-app/App_Data/sql/mysql/updates/upd2026-06-12-assistant-rag.sql`
- SQLite: `osafw-app/App_Data/sql/sqlite/updates/upd2026-06-12-assistant-rag.sql`

New databases already include the same tables in the provider-specific `fwdatabase.sql` files.

Core tables:

- `kb_articles`: manager-maintained knowledge base records.
- `doc_chunks`: chunk text, JSON vector, norm, dimension, embedding model, selected backend, and citation metadata.
- `assistant_threads`, `assistant_messages`, `assistant_runs`, and `assistant_runs_events`: durable chat and run state.
- `assistant_feedback`: reviewable feedback data.
- `assistant_memories`: optional per-user memory summaries when enabled.

## Knowledge Base

Managers can maintain articles at `/Admin/KBArticles`. Saving an active article calls `KBArticles.reindexKBArticle()`, which uses `DocumentEmbeddingService.IndexKBArticleAsync()` to chunk markdown text and store embeddings in `doc_chunks`.

Article access uses numeric `access_level`. Retrieval queries include `KBArticles.buildAccessWhere()`, so users do not receive chunks from articles above their access level.

## Document Attachments

Assistant messages can include supported uploads. Text, markdown, HTML, RTF text fallback, and DOCX files are parsed and indexed against the assistant message entity. Unsupported file types remain attached but are not indexed. The assistant enforces per-message file-count, indexed-file byte, parsed-character, and chunk-count caps before calling the embedding provider.

Use `/Admin/DocChunks` to inspect indexed chunks, vector metadata, and backend selection. The screen reports setup-needed if the schema is missing.

## Vector Backends

Embeddings are stored as JSON text for portability. The default `ASSISTANT_VECTOR_MODE=auto` detects SQL Server native `vector` support at runtime. When native support is not available, or a native query fails, retrieval falls back to SQL JSON cosine scoring.

Fallback implementations:

- SQL Server: `OPENJSON`
- MySQL: `JSON_TABLE`
- SQLite: `json_each`

The vector distance metric is cosine. The default embedding dimension is 1536 for `text-embedding-3-small`.

## Assistant Flow

`/Assistant` renders the chat shell. The UI supports send, poll, history search, upload, feedback, share, and cited response display.

The run processor uses Microsoft Agent Framework packages and read-only tools:

- Knowledge base search.
- Current-thread attachment search.
- Clarification requests.
- Progress events.

The assistant must not execute generated SQL, mutate application records, or redirect users based on model output.
