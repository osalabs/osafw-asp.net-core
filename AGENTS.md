# Repository Scope and Core Rules

- This repository serves both as public framework source and as code copied into application repositories. Determine the repository role, branch policy, and app-specific contracts; do not infer them from a `master` branch name.
- Create and edit text as strict UTF-8 without BOM with CRLF line endings. ParsePage route literals such as `App_Data/template/**/url.html` stay one line with no trailing newline byte.
- Preserve unrelated and untracked work. Never put credentials, private paths, database backups, production data, machine preferences, or large logs in tracked files.
- Validate repository docs, prior summaries, generated output, and optional external references against current code and explicit developer decisions before relying on them.

# Start and Route

1. Inspect `git status`, the requested outcome, and the nearest implementation/contract before proposing a change.
2. Read ignored `docs/agents/local_instructions.md` when present. In a linked worktree where it is absent, resolve the absolute Git common directory and check the primary checkout (the common directory's parent) for the same ignored file. Never expose or commit it.
3. Determine whether this is framework development or copied-application development. Use the remote, repository docs, app customizations, and task context; ask only when ambiguity would materially change the result.
4. Use `docs/README.md` as the documentation router. Load only the routes needed:
   - non-trivial request validation, permissions, summaries, or staging: `docs/agents/workflow.md`;
   - stable architecture and terminology: `docs/agents/domain.md` and `docs/agents/glossary.md`;
   - build, test, provider, worktree, or cleanup decisions: `docs/agents/verification.md`;
   - meaningful-risk review: `docs/agents/review-routing.md`, then `docs/agents/code_reviewer.md` and only triggered overlays;
   - optional external/IDE/MCP capabilities: `docs/agents/mcp.md`;
   - historical recall: search `docs/agents/tasks/index.md` before opening targeted summaries.
5. For broad searches prefer `docs/agents/tools/Search-Repo.ps1`. Opt into ignored drafts, large files, vendor content, or task history only when directly relevant. For files over 1 MB, search headings/patterns and read bounded ranges rather than streaming the file.
6. Default to direct execution. `docs/agents/workflow.md` owns delegation eligibility, role selection, ownership, and fallback. Load `docs/prompts/orchestrator.md` only for work that qualifies under that workflow.

# Work Boundaries

- A request to diagnose, explain, audit, or review is read-only unless it also asks for changes. A change request authorizes scoped implementation and proportional verification, not commits, branch operations, pushes, PRs, releases, deployments, messages, or writes to shared/external systems.
- Short prompts are sufficient when the requested outcome is discoverable from current code, canonical docs, or an accessible linked issue. Separate the desired outcome from an inferred implementation and validate it against current behavior and downstream contracts before editing.
- Push back with evidence when a request contradicts current contracts, weakens security/data integrity, or silently breaks copied applications. Use a focused developer interview only when a high-level feature leaves material product, compatibility, data, or authorization choices unresolved.
- Keep the change as small as the outcome permits. Do not lead with broad cleanup, test-only production seams, speculative abstraction, or process ceremony.
- For a configured development database or any shared external resource, obtain explicit authority before mutating it. Prefer task-owned disposable resources and clean up only exact resources created by the task.

# Repository Contracts

- This is an ASP.NET Core source framework/template using custom `FW` routing and ParsePage templates, not Razor. Project files own current target frameworks, package references, and compile symbols; `docs/agents/domain.md` owns stable architecture.
- Controllers end in `Controller`; public actions end in `Action`. Template-rendering actions conventionally return `FwDict`, while an action may write the response directly. Keep controllers focused on request/response orchestration.
- Table models generally inherit `FwModel` and are obtained from the current `FW` instance with `fw.model<T>()` or `fw.model(name)`. Prefer model-owned business/data access where it clarifies the boundary; keep controller queries narrow and parameterized.
- Prefer `FwDynamicController` or `FwVueController` with `config.json` for standard CRUD and compose views under `osafw-app/App_Data/template`. For a new standard module follow `docs/feature_modules.md` and its approval-gated schema/scaffolder workflow.
- Nearby code and canonical topic docs own detailed conventions: `docs/naming.md`, `crud.md`, `db.md`, `templates.md`, `dynamic.md`, and `datetime.md`.
- Add XML docs for non-obvious framework intent, loose shapes, security/access expectations, side effects, exceptions, or null/empty behavior; do not restate signatures.

# Security and Data Integrity

- State-changing custom actions call `enforcePost()` before side effects; callers use POST with the current XSS token unless a documented framework exemption deliberately applies.
- Direct-id reads, writes, and deletes authorize the target row. User-owned records require owner-or-system predicates; dynamic children and attachments require authorization through the parent business object.
- Redirects satisfy the app-local URL policy unless an explicit Site Admin-managed external allowlist applies.
- Escape or sanitize stored/user/editor HTML and markdown before display. Raw markdown HTML, `noescape`, and Vue `v-html` require server-controlled or already-sanitized content.
- Authorize attachments before linking, serving, or issuing S3 redirects. Block or force-download active content and preserve safe image-decode limits.
- Keep dev/admin tooling, generated SQL/files, assistant tool calls, and schema writes behind appropriate environment gates, allowlists, resource authorization, and sensitive telemetry redaction.
- Parameterize SQL through `DB`. Never commit credentials, tokens, user secrets, production data, or sensitive diagnostics.

# Compatibility, Schema, and Documentation

- Framework mode must protect copied applications: public C# APIs, controller/action and route behavior, template/include/page-state shapes, configuration defaults and compile symbols, scaffolded/generated output, schema/provider behavior, storage paths, security defaults, and frontend/email contracts are downstream surfaces.
- Application mode must preserve intentional app customizations while importing or adapting framework behavior; do not overwrite a copied app merely because upstream differs.
- SQL Server is the production-primary provider and primary schema/update authority. SQLite is optional for embedded single-node apps and is preferred for disposable provider-neutral isolation. MySQL is optional and is not assumed to have schema parity. OLE/ODBC/MS Access paths are mainly compatibility/import surfaces. Follow `docs/db.md` and verify the exact affected provider.
- Keep provider fresh-install schemas and additive update paths aligned only for providers the change actually supports. Never use destructive fresh-schema scripts against an existing database.
- Prefer additive compatibility or a small shim when cheap. Security fixes may change behavior immediately. Otherwise document a breaking/behavioral change, its migration, and any case-specific deprecation treatment in `docs/CHANGELOG.md` under the change date.
- Update canonical docs whose contract changed. Prompts in `docs/prompts/` are optional workflow aids; task summaries are evidence logs, not reusable policy.

# Evidence, Verification, and Review

- Use `docs/agents/workflow.md` for task-summary requirements, exceptions, and index updates.
- Verify using `docs/agents/verification.md`: begin with the smallest check that can falsify the change, expand with risk, and state material checks not run. The default test build does not cover SQLite-only code; enable its compile symbol when that path changes.
- For runtime source, schema, templates, scripts, tests, runtime configuration, or risky workflow changes, route review through `docs/agents/review-routing.md`. `docs/agents/code_reviewer.md` produces the one final adjudicated verdict; specialist overlays add evidence but never independent final verdicts.
- The implementing agent owns final integration, diff inspection, verification, and safe cleanup even when optional delegation is available.
