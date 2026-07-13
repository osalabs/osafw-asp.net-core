<!-- AGENTS.md - osafw-asp.net-core -->

# Authority and Non-Negotiables

- This file is the sole repository development-agent workflow authority. `.github/copilot-instructions.md` is its byte-for-byte generated mirror; never maintain separate policy there.
- Create and edit text as strict UTF-8 without BOM, using CRLF line endings. ParsePage route-literal fragments such as `App_Data/template/**/url.html` remain one line with no trailing newline byte.
- Before implementation, read `docs/agents/local_instructions.md` when it exists. It is machine-local, may contain sensitive setup details, and must not be committed or exposed.
- Preserve unrelated worktree changes. Do not store secrets, database backups, machine-specific notes, or large logs in tracked files.
- Use `docs/README.md` only to route into documentation relevant to the task; do not load the full documentation or task history by default.

# Task Workflow

1. Scope the smallest useful change.
   - Inspect `git status` and the nearby contract before editing.
   - For non-trivial work, identify the critical path, independent side work, and tightly coupled integration that should remain local.
   - Search `docs/agents/tasks/index.md` before opening historical summaries. Treat it as a routing aid: fall back to targeted filename/content search when needed.
   - For files over 1 MB or known large drafts/logs/generated outputs, start with `rg` headings or targeted ranges; do not stream the whole file. Record the sections used when a task summary is required.
   - For broad repository searches, prefer `docs/agents/tools/Search-Repo.ps1`; include ignored drafts or task history only when directly relevant.

2. Record work proportionally.
   - Create or update `docs/agents/tasks/summary-<YYYY-MM-DD>-<task-id>.md` when an active prompt requires it, or when the task is non-trivial, iterative, runtime/schema/config/test/script-affecting, or changes shared agent workflow.
   - Skip summary-file churn for read-only work, small investigations, and trivial text edits. Put the useful evidence in the final response instead.
   - Keep one summary for iterative feedback in the same task session. When a summary is created, add or update its concise entry in `docs/agents/tasks/index.md` before closeout.
   - Use the relevant headings from: `What changed`, `Scope reviewed`, `Commands used / verification`, `Decisions - why`, `Pitfalls - fixes`, `Risks / follow-ups`, `Heuristics (keep terse)`, `Testing instructions`, and `Reflection`. Omit optional empty sections; `Testing instructions` must reflect the final state.

3. Implement the requested behavior first.
   - Keep changes scoped to the requested behavior and nearby contracts.
   - Do not lead with test-only refactors, broad cleanup, or speculative QA changes.

4. Verify in proportion to risk.
   - Run the smallest automated or manual check that can falsify the change, then expand only when risk or a failure justifies it.
   - The main agent owns final integration, diff inspection, and verification even when bounded work is delegated.

5. Review when the change can carry meaningful risk.
   - For runtime source, schema, templates, scripts, tests, configuration, or risky shared workflow changes, review the final diff using `docs/agents/code_reviewer.md`.
   - Use an independent reviewer when that capability is available and useful; otherwise perform and disclose a deliberate local review.
   - Continue the review/fix loop only for Blocker, High, or Medium findings. Low observations are non-blocking and may be handled while context is loaded.

6. Close out evidence and reusable knowledge.
   - Summaries should list final checks, affected flows, material checks not run, setup caveats, and the reason a changelog entry was or was not needed.
   - `Reflection` is for future process improvement, not a task recap: note friction, avoidable work, delegation/tool value, and any instruction change worth considering.
   - Update `docs/agents/domain.md`, `glossary.md`, `heuristics.md`, or `docs/adr/` only for verified, reusable knowledge that belongs there. Record when no stable fact, heuristic, or ADR was added.
   - Put disposable probes and small private evidence under ignored `docs/agents/artifacts/`; use ignored repo-root `artifacts/` for build outputs and larger generated verification assets.

# Repository Contracts

- This is an ASP.NET Core framework/template with custom `FW` routing and ParsePage views rather than Razor. Exact target framework, packages, and optional compile symbols are owned by the project files; stable architecture is in `docs/agents/domain.md`.
- Controllers end in `Controller`; public actions end in `Action`. Template-rendering actions conventionally return `FwDict`, while actions may also handle the response directly. Keep controllers focused on request/response orchestration.
- Persistence/table models generally inherit `FwModel` and are obtained through the current `FW` instance (`fw.model<T>()` or `fw.model(name)`). Prefer model-owned business/data access when it keeps the boundary clearer; keep any controller-owned query narrow and parameterized.
- Prefer `FwDynamicController` or `FwVueController` plus `config.json` for standard CRUD before adding bespoke UI. Compose views under `osafw-app/App_Data/template`.
- Follow nearby style and the canonical topic docs: `docs/naming.md`, `crud.md`, `db.md`, `templates.md`, `dynamic.md`, and `datetime.md`. Those docs own detailed naming, result-shape, provider, parser, and date/time contracts.
- Add concise XML docs only for non-obvious framework intent, loose shapes, security/access expectations, side effects, exceptions, or null/empty behavior. Do not restate signatures or add comments for obvious code.
- Avoid one-use wrappers and test-only production entry points unless they clarify a real contract, reduce meaningful duplication, or isolate genuinely complex logic.

# Security and Data Integrity

- Custom actions that mutate state must call `enforcePost()` before side effects; valid callers must use POST and include the current XSS token. Preserve documented framework exemptions only deliberately.
- Direct-id reads, writes, and deletes require authorization at the target-row boundary. User-owned preference records need owner-or-system predicates; dynamic child and attachment operations need parent-object authorization.
- Redirects must satisfy the app-local URL policy unless an explicit Site Admin-managed external allowlist applies.
- Escape or sanitize stored/user/editor HTML and markdown before display. Raw markdown HTML, `noescape`, and Vue `v-html` require server-controlled or already-sanitized content.
- Authorize attachments against the parent business object before linking, serving, or issuing S3 redirects. Block or force download for active content and preserve safe image-decode limits.
- Keep dev/admin tooling, generated SQL, assistant tool calls, and generated file/schema writes behind appropriate environment/exposure gates, explicit allowlists, normal resource checks, and sensitive request/session/telemetry redaction.
- Parameterize SQL through the `DB` helper. Never commit credentials, tokens, production data, or sensitive diagnostic output.

# Performance Guardrails

- In request-wide, repeated, or likely hot paths, avoid repeated DB, remote, file, config, template, or metadata work inside loops. Batch, preload, project, filter, aggregate, cache, or page data while preserving authorization, ordering, staleness assumptions, and empty-result behavior.
- Avoid unbounded materialization, large per-request allocations/string building, sync-over-async or blocking I/O, and expensive per-request clients/resources. Prefer a small data-shape or existing-cache fix; ask for measurement before an invasive optimization.

# Delegation and Optional Tooling

- Delegate only when the runtime supports it and the subtask is bounded, independent, and likely to improve speed or confidence. Provide expected output, file ownership, and constraints; do not idle the critical path on vague delegated research.
- In a shared worktree, tell editing workers they are not alone, must not revert others' changes, and must report changed paths. The main agent retains shared-contract integration and final verification. A bounded independent review may remain a closeout gate.
- Choose workers by available capability and latency, not by model name. Do not encode product-specific model selection in repository guidance.
- Optional IDE, MCP, browser, and connector tools are capability-conditional. Follow `docs/agents/mcp.md`: validate a relevant capability briefly, use it when it materially helps, and use a safe local fallback unless the user explicitly required that tool or the task depends on its existing state.

# Documentation, Schema, and Compatibility

- `docs/README.md` owns navigation and document ownership. Update only the canonical docs whose contracts changed; note in the summary when a relevant doc needed no change.
- For schema work, follow the provider-specific fresh-install and additive-update rules in `docs/db.md`. Do not assume provider parity; verify the affected runtime path and keep required fresh schemas, update scripts, tests, and docs aligned.
- Record end-user-app breaking upgrade changes in `docs/CHANGELOG.md` under the change date. This includes public API/signature, route/template/include, schema/update, config/compile-symbol, storage key/URL, security/default, and frontend asset/class/plugin contract changes.
- Prompts under `docs/prompts/` are optional workflow aids subordinate to this file. Task summaries are current-task evidence, not reusable policy.
- When `AGENTS.md` changes, copy it byte-for-byte to `.github/copilot-instructions.md` before final validation.

# Verification Guidance

- Prefer focused build/test/manual checks before broad suites. Common baselines are `dotnet build osafw-app/osafw-app.csproj`, `dotnet build osafw-asp.net-core.sln`, and `dotnet test`; choose only what can falsify the touched behavior.
- Prefer behavior-level checks through public framework, controller/action, model, route, template, or UI boundaries. Use lower-level tests for shared, complex, security-sensitive, deterministic, or otherwise hard-to-reach logic; do not reshape production code solely for private branch coverage.
- Provider-specific work must enable and test the matching compile symbol. The default test run excludes `#if isSQLite` coverage; use a focused `-p:DefineConstants=isSQLite` test variant when SQLite behavior changes.
- If normal output may be locked by Visual Studio or IIS Express, use an absolute repo-root `OutDir` under `artifacts/assistant_*` instead of retrying `bin/Debug`. Do not set `BaseIntermediateOutputPath` for `osafw-app`; its compile glob can pick up generated intermediates and create duplicate assembly attributes.
- When automation is unavailable or disproportionate, record concise manual verification steps and prerequisites.
