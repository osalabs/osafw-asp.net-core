<!-- AGENTS.md - Universal, Auto-Bootstrapping (v7.1) -->

# Non-Negotiables

- Use Windows-style line endings for every created or edited file.
- Read `docs/agents/local_instructions.md` before implementation when it exists. Treat it as machine-local guidance and do not commit its contents.
- Use `docs/README.md` as the fast entry point for the broader documentation set.
- When `AGENTS.md` changes, copy it byte-for-byte to `.github/copilot-instructions.md` before closing the task.

# Task Workflow

1. Scope and record the task.
   - Create or update `docs/agents/tasks/summary-<YYYY-MM-DD>-<TASK-ID>.md` unless the user explicitly asked for no file changes or a read-only review. Use one short kebab-case task id per task session, and keep updating the same summary for iterative feedback in that session.
   - If no task summary is created because the request is read-only, include the useful summary in the final response instead.
   - For non-trivial tasks, identify the critical path, safe parallel side work, and tightly coupled work that should stay local.

2. Implement the requested change first.
   - Do not start test-only refactors or broad QA edits before the implementation pass is complete.
   - Keep edits scoped to the requested behavior and nearby contracts.

3. Verify proportionally.
   - Prefer the smallest automated or manual check that can falsify the change quickly.
   - Keep the main agent responsible for final integration and verification, even when delegating bounded checks.

4. Run a review-fix loop when risk justifies it.
   - For runtime-affecting source code, schema, templates, scripts, tests, configuration, or risky developer/agent workflow changes, review the final diff using `docs/agents/code_reviewer.md`.
   - Prefer a code reviewer sub-agent. If sub-agents are unavailable, perform the same review yourself and record that fallback in the task summary.
   - Fix findings in the main workspace and repeat until there are no issues or no improvement points worth another loop.
   - Documentation-only changes do not need this loop unless they alter runnable configuration or risky developer/agent workflow.

5. Keep the task summary current.
   - Append useful notes as the task evolves, except `Testing instructions`, which should always reflect the final state.
   - For code changes, list exact automated/manual checks run, affected flows, highest-risk follow-up checks not run, and setup caveats.
   - For docs-only or internal instruction changes, set `Testing instructions` to `N/A - docs/instructions only` or an equivalent concise statement.

6. Close out knowledge capture.
   - Review the task summary and make it complete.
   - Record whether stable facts, heuristics, or ADRs were added or intentionally not added.
   - Add stable framework facts to `docs/agents/domain.md` or `docs/agents/glossary.md`.
   - Add reusable working heuristics to `docs/agents/heuristics.md`; timestamp new heuristics and expire or revise touched heuristics older than 90 days.
   - Add substantial business or architecture decisions under `docs/adr/`.
   - Update `AGENTS.md` only when a recurring pattern would reduce future effort or drift.
   - Do not store secrets, DB backups, large logs, or machine-local notes in shared docs.

## Task Summary Template

```md
## What changed
## Scope reviewed
## Commands used / verification
## Decisions - why
## Pitfalls - fixes
## Risks / follow-ups
## Heuristics (keep terse)
## Testing instructions
## Reflection
```

## Project Overview

- `osafw-asp.net-core` is an opinionated ASP.NET Core (.NET 10) web framework/template for data-heavy admin and CRUD apps.
- Core concepts:
  - `FW` request pipeline with custom router and RESTful mapping.
  - `ParsePage` template engine for views; templates live in `osafw-app/App_Data/template`.
  - MVC-style controllers, models, and templates; models use the thin `DB` helper.
  - Dynamic and Vue controllers driven by JSON config to scaffold CRUD quickly.
  - Built-in logging, caching, file uploads, settings, activity logs, self-test, virtual controllers, reports, and scheduled tasks.
- Runtime:
  - `Program.cs` uses minimal hosting, config via `FwConfig`, sessions via distributed SQL cache (`fwsessions`), and data protection keys in `fwkeys`.
  - Optional MySQL support uses `#define isMySQL` and matching SQL scripts.
- Database:
  - SQL Server by default. Schema scripts live under `osafw-app/App_Data/sql`.
  - Key tables include `users`, `settings`, `spages`, `att*`, `activity_logs`, and framework `fw*` tables.

## Key Paths

- `osafw-app/Program.cs` - app entrypoint and middleware registration.
- `osafw-app/App_Code/fw/` - core framework: `FW`, controller bases, `DB`, config/cache/logging, reports, cron, updates, utilities, and `ParsePage`.
- `osafw-app/App_Code/controllers/` - site/example controllers.
- `osafw-app/App_Code/models/` - table and domain models.
- `osafw-app/App_Data/template/` - templates, common includes, controller folders, and dynamic configs.
- `osafw-app/App_Data/sql/` - SQL Server scripts plus optional MySQL variants under `mysql/`.
- `docs/` - framework docs, ADRs, and agent docs.
- `osafw-tests/` - test project.

## Coding Style

- C# uses .NET 10, nullable enabled, namespace `osafw`, and spaces for indentation.
- JavaScript uses 4-space indents. XML uses 2-space indents.
- Controllers end with `Controller`; public action methods end with `Action`.
- Standard action names are constants in `FW`: `Index`, `Show`, `ShowForm`, `Save`, `SaveMulti`, `ShowDelete`, `Delete`.
- Controllers return `FwDict` `ps` parsed by `FW.parser`. For JSON, set `ps["_json"]=true` or assign data to `ps["_json"]`.
- Models inherit `FwModel`. Obtain them with `fw.model<T>()` or `fw.model("Name")`. Keep SQL in models, not controllers.
- Use `docs/naming.md` for framework naming conventions. Prefer result-shape and side-effect prefixes such as `list*`, `one*`, `count*`, `add*`, `update*`, and `save*` over generic `Get*`/`Set*` names.
- `list*()` methods return empty `FwList`/`DBList`; dictionary-backed `one*()` methods return empty `FwDict`/`DBRow`; typed single-row methods (`DB.row<T>`, `DB.rowp<T>`, `oneT*`) return `null` when no record is found unless using `*OrFail`.
- `FW.getRoute()` implements RESTful routing by HTTP method and URL. Prefixes such as `/Admin` are supported.
- Access control uses static `access_level` on controllers plus `FwConfig.access_levels` rules. XSS tokens are validated on mutating requests.
- Prefer view composition in `osafw-app/App_Data/template`; override a controller base directory with `controller.template_basedir` or `ps["_basedir_controller"]`.
- Keep ParsePage route literal templates such as `App_Data/template/**/url.html` on one line with no trailing newline byte.
- Use `FormUtils` for filtering/validation, `DateUtils` for user timezone formatting, `FwLogger` for logs, and `FwCache` for memoization.
- For SQL queries or SQL fragments in code, prefer one `$@"..."` string block over concatenated pieces.
- For new or updated C# methods, add XML docs explaining why the method exists and include detailed param/return info for non-primitive types. Add inline comments only for complex logic blocks.

## Sub-Agent Delegation

- The main agent owns the task outcome, user communication, integration, and final verification.
- Delegate only bounded work with clear expected output, owned files/modules, and constraints.
- Good delegation targets include targeted codebase research, independent docs/spec review, focused implementation in disjoint files, post-implementation test execution, and code review.
- Do not delegate vague repo sweeps, broad ownership, or blockers where the main agent cannot progress until the answer returns.
- For code-editing workers, state that they are not alone in the codebase, must not revert others' changes, and must list changed paths in their final response.
- Prefer a small number of useful parallel sub-agents over many shallow ones. Inspect their evidence and changes before relying on them, then record material findings and commands in the task summary.

## Agent Workspace

- Put disposable probes, temp scripts, and scratch outputs under `docs/agents/artifacts/`.
- Keep `docs/agents/artifacts/` gitignored; do not leave `tmp_*` scratch files at repo root or under `osafw-app/`.
- Use repo-root `artifacts/` for build outputs and larger generated verification assets that do not belong under docs.
- Keep machine-specific guidance in `docs/agents/local_instructions.md`; check it before implementation and keep it out of Git.
- Put reusable agent/debug helpers under `docs/agents/tools/`.
- Do not store secrets, DB backups, or large logs in any agent workspace folder.

## Documentation Entry Points

- Use `docs/README.md` for the full documentation map and recommended reading order.
- High-frequency implementation docs:
  - `docs/templates.md` - ParsePage templates and parser rules.
  - `docs/dynamic.md` - Dynamic/Vue controller config.
  - `docs/crud.md` and `docs/db.md` - CRUD and data-access patterns.
  - `docs/datetime.md` - per-user date/time and timezone behavior.
  - `docs/layout.md`, `docs/dashboard.md`, and `docs/feature_modules.md` - shared UI and module scaffolding.
- Agent docs:
  - `docs/agents/code_reviewer.md` - review loop instructions.
  - `docs/agents/mcp.md` - MCP usage and troubleshooting notes.
  - `docs/agents/heuristics.md`, `docs/agents/domain.md`, and `docs/agents/glossary.md` - reusable project knowledge.

## Documentation Sync

- Agent instruction sync set: `AGENTS.md`, `.github/copilot-instructions.md`, `docs/agents/code_reviewer.md`, `docs/README.md`, and task-summary expectations.
- `docs/templates.md` is the canonical templates and ParsePage doc.
- When changing shared ParsePage behavior, shared layout fragments, standard dynamic-controller screen structure, schema/update process, public framework behavior, or agent workflow, review the related docs in the same task and note when no doc update was needed.
- When schema changes are present, consider both the additive update path under `osafw-app/App_Data/sql/updates/` and the from-scratch schema reference in `osafw-app/App_Data/sql/fwdatabase.sql`.

## Testing Guidance

- Prefer targeted build/test/manual checks before broad suites.
- If no automated coverage exists or is practical, record concise manual verification steps and prerequisites in the task summary.
- Build app: `dotnet build osafw-app/osafw-app.csproj`.
- Build app to isolated output when normal `bin/Debug` is locked: `dotnet build osafw-app/osafw-app.csproj -p:OutDir=artifacts/assistant_build/`.
- Build solution: `dotnet build osafw-asp.net-core.sln`.
- Test: `dotnet test`.

## Common Commands

- Restore: `dotnet restore`.
- Run app: `dotnet run --project osafw-app`.
- Watch app: `dotnet watch run --project osafw-app`.
- Database setup for SQL Server: run `osafw-app/App_Data/sql/fwdatabase.sql`, then `lookups.sql`; add `roles.sql` and `demo.sql` when needed.
- Configure connection strings in `appsettings*.json` under `appSettings.db.main` (`type`, `connection_string`).
- Switch to MySQL: define `isMySQL` in `Program.cs`, set `db.main.type` to MySQL, and use scripts from `osafw-app/App_Data/sql/mysql/`.
- Scheduled tasks: uncomment `builder.Services.AddHostedService<FwCronService>();` in `Program.cs`.
- Windows auth: enable Negotiate and use `/winlogin`.
- Sessions/Data Protection: ensure `fwsessions` and `fwkeys` tables exist.

## MCP Tooling

- Prefer Visual Studio MCP for solution-aware .NET work when available; validate it independently before relying on it.
- Prefer Playwright MCP for browser repros and UI verification; rerun snapshots after navigation or meaningful DOM changes.
- If a required MCP is missing or blocked, do one quick validation and at most one lightweight retry. Then ask the user whether to fix MCP or use a non-MCP fallback.
- Keep machine-specific URLs, credentials, and browser notes in `docs/agents/local_instructions.md`.
- See `docs/agents/mcp.md` for detailed MCP troubleshooting guidance.
