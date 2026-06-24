## What changed
- Created `docs/drafts/assistant_review.md` with a critical architecture review of PR #273's Knowledge Base and Assistant implementation.
- Updated the review after user feedback: shared thread links are treated as intentional user-controlled publication, and the before-merge scope was changed to the requested dashboard, RAG, queue, Spages, contact search, hybrid retrieval, evidence, native vector, and memory work.
- Updated the review again after the pre-merge implementation session completed. The review now treats those implementation items as done in the working tree and focuses on remaining verification, operational hardening, and next-iteration architecture.

## Scope reviewed
- Startup and local instructions: `AGENTS.md`, `docs/agents/local_instructions.md`, and `docs/README.md`.
- Relevant prior task history: `docs/agents/tasks/index.md` and `docs/agents/tasks/summary-2026-06-12-assistant-port.md`.
- Pre-merge implementation summary: `docs/agents/tasks/summary-2026-06-12-assistant-premerge.md`.
- Assistant docs and implementation: `docs/assistant.md`, assistant controllers, KB/RAG models, AI models, parsers, SQL migrations, prompts, templates, and focused tests.
- Framework convention docs: `docs/naming.md` and `docs/dynamic.md`.
- Follow-up code samples for the second review pass: `RagSources`, `RagChunks`, `DocumentEmbeddingService`, `AssistantRunWorkerService`, `AssistantAppService`, and dashboard Assistant templates.
- PR metadata from GitHub PR #273.
- External calibration for SQL Server 2025 vectors, OpenAI retrieval/agents, and recent RAG retrieval benchmarks.

## Commands used / verification
- `git status --short --branch`
- `git diff --stat origin/master...HEAD`
- `git diff --name-only origin/master...HEAD`
- `git rev-parse HEAD`
- Targeted `Get-Content` and `rg` reads over Assistant/KB source, templates, docs, migrations, and tests.
- `Get-Content docs\agents\tasks\summary-2026-06-12-assistant-premerge.md`
- `Get-Content docs\drafts\assistant_review.md`
- `Get-Content docs\agents\tasks\summary-2026-06-12-assistant-review.md`
- `rg -n "class RagSources|markFailed|claimNextPending|ProcessNextQueuedSourceAsync|BindSourcesToRunEvidence|TYPE_ID\(N'vector'\)|type_assistant" osafw-app docs osafw-tests`
- Follow-up targeted `rg` scans over `docs/drafts/assistant_review.md` for stale sharing/memory/naming recommendations after user feedback.
- GitHub PR metadata lookup for `osalabs/osafw-asp.net-core#273`.
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check docs\drafts\assistant_review.md docs\agents\tasks\summary-2026-06-12-assistant-review.md docs\agents\tasks\index.md` - passed after normalization in the first pass.
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check docs\drafts\assistant_review.md docs\agents\tasks\summary-2026-06-12-assistant-review.md` - passed after the follow-up edit.
- `git diff --check` - passed after the follow-up edit.
- `git check-ignore -v docs\drafts\assistant_review.md` - confirmed `docs/drafts/` is ignored by repo policy.

## Decisions - why
- Treated the current branch plus working tree as authoritative because local `HEAD` remains the PR head SHA while the pre-merge implementation is present as uncommitted changes.
- Kept this as a documentation/review task only; no runtime implementation changes were made.
- Replaced stale release-blocking implementation recommendations with a verification checklist and next-work roadmap.
- Did not recommend adding mutating assistant tools in this PR; the current read-only boundary is the right safety point until action contracts and approvals exist.
- Accepted the user's sharing model: a shared thread intentionally exposes prepared materialized content to recipients without recipient-side KB re-filtering.
- Kept assistant memory in scope and treated the implemented compaction/summarization and sanitization as the right first release shape.

## Pitfalls - fixes
- The review needed current external calibration for vector/RAG/agent architecture; official Microsoft/OpenAI docs plus recent benchmark papers were checked during the first pass.
- GitHub web browsing did not return useful page content, so the GitHub connector was used for PR metadata and local files were used for deep review.
- `docs/drafts/assistant_review.md` is under an ignored drafts directory by design, so it exists locally but will not appear in normal `git status`.
- The follow-up review needed to distinguish committed `HEAD` from the local pre-merge working tree so the document did not imply a new commit SHA.

## Risks / follow-ups
- This review did not run builds or tests because no runtime files were changed.
- The most important immediate follow-up is pre-merge verification: provider update-script execution, broader tests, browser smoke, no-key no-write behavior, and a live queued source/run/evidence smoke.
- The most important code follow-ups are failed-source requeue/backoff, worker fairness, retrieval/citation evaluation fixtures, PDF/OCR, and app extension interfaces.

## Heuristics (keep terse)
- No stable framework facts, heuristics, or ADRs were added; this was a branch-specific architecture review.

## Testing instructions
- N/A - docs/review only.

## Reflection
- The prior task summary was useful and avoided rereading unrelated implementation history.
- The pre-merge implementation summary made the second review pass efficient; future implementation sessions should keep summaries this concrete when handing work back for review.
- The feature spans schema, retrieval, assistant orchestration, UI, security, and future workflow design; reviewing it by subsystem was faster than reading every changed file linearly.
- Future Assistant reviews should start with source/chunk lifecycle, sharing/authorization, retrieval quality, and tool/action boundaries before UI polish.
