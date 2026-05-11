<!-- AGENTS.md - Universal, Auto-Bootstrapping (v7) -->

# Applies to any changes
1. Always use Windows-style line endings.

# Process/Workflow for each user request
1. Create task summary file `docs/agents/tasks/summary-<YYYY-MM-DD>-<TASK-ID>.md` per template in
<task-summary-template>
## What changed
## Scope reviewed
## Commands used / verification
## Decisions - why
## Pitfalls - fixes
## Risks / follow-ups
## Heuristics (keep terse)
## Testing instructions
## Reflection
</task-summary-template>
   - For iterative user feedback within the same task session, keep updating the same summary file instead of creating a new one.

2. If `docs/agents/local_instructions.md` exists, read it before implementation. Treat it as machine-local guidance and do not commit its contents.

3. Implement the requested change first. Do not start test-only refactors or broad QA edits before the implementation pass is complete.

4. After the implementation pass, do verification work proportional to the change. You may delegate bounded test execution or targeted test updates to a separate agent, but keep the main agent responsible for final integration and verification.

5. If runtime-affecting code, schema, template, script, or configuration changed, run a review-fix loop before closing the task:
   - Spawn a code reviewer sub-agent and instruct it to follow `docs/agents/code_reviewer.md`.
   - Fix findings in the main agent workspace.
   - Repeat until the reviewer reports no issues or no improvement points worth another loop.
   - Documentation-only changes do not need this loop unless they alter runnable configuration or developer workflow in a risky way.

6. Keep `Testing instructions` in the task summary final-state and concise.
   - For code changes, list exact automated/manual checks run, the main affected flows, highest-risk follow-up checks not run, and any setup caveats.
   - For docs-only or internal instruction changes, explicitly say `N/A - docs/instructions only` or equivalent.

7. Continue updating the task summary file as needed. Prefer append-don't-overwrite updates so earlier notes stay available, except `Testing instructions`, which should always reflect the final state.

8. Post-process - after user's query is completely resolved, perform self-reflection, self-improvement, and optimization of the entire process/workflow, see steps in <post-process>
<post-process>
Goal: accumulate reusable project knowledge while staying concise.
- Review task summary file, make sure it's complete.
- Classify discoveries:
  - STABLE FACT - add to domain.md or glossary.md or relevant sections below in AGENTS.md.
  - HEURISTIC - add to heuristics.md, timestamp new heuristics and expire or revise anything older than 90 days.
  - ONE-OFF - keep in task summary file only.
- If a substantial business decision changed, add an ADR under `docs/agents/adr/`.
- AGENTS.md upkeep:
  - If patterns recur, add a new section here only if it reduces future token/step cost.
  - Do not change more than 20% of AGENTS.md content in one go unless the explicit task is to refresh agentic instructions.
- Keep all knowledge concise and informative.
- Replace "YYYY-MM-DD" with today's date.
- Don't store secrets or large logs.
</post-process>

Whenever AGENTS.md updated - make copy of it to `.github/copilot-instructions.md`.

Use this file as the default operating guide for work in this repo. Use `docs/README.md` as the fast entry point for the broader documentation set.

## Project Overview
- osafw-asp.net-core is an opinionated ASP.NET Core (.NET 10) web framework/template for building data-heavy admin and CRUD apps.
- Core concepts:
  - `FW` request pipeline with custom router and RESTful mapping.
  - `ParsePage` template engine for views (no Razor). Templates live in `osafw-app/App_Data/template`.
  - MVC-ish structure: controllers, models, templates; models use thin `DB` helper.
  - Dynamic and Vue controllers driven by JSON config to scaffold CRUD quickly.
  - Built-in features: logging, caching, file uploads, settings, activity logs, self-test, virtual controllers, reports, scheduled tasks.
- Runtime:
  - `Program.cs` uses minimal hosting, config via `FwConfig`, sessions via distributed SQL cache (`fwsessions`), data protection keys in `fwkeys`.
  - Optional MySQL support via `#define isMySQL` and matching SQL scripts.
- Database:
  - SQL Server by default; schema scripts under `osafw-app/App_Data/sql`. Key tables: `users`, `settings`, `spages`, `att*`, `activity_logs`, `fw*` (framework).

## Folder Structure
- `osafw-app/Program.cs` - app entrypoint and middleware registration.
- `osafw-app/App_Code/fw/` - core framework:
  - `FW`, `FwController`, `FwAdminController`, `FwVueController`, `FwDynamicController`, `FwVirtualController`
  - `DB`, `FwConfig`, `FwCache`, `FwLogger`, `FwExceptions`, `FwReports`, `FwCron(Service)`, `FwUpdates`
  - helpers: `Utils`, `FormUtils`, `DateUtils`, `ImageUtils`, `UploadUtils`, `SiteUtils`, `ConvUtils`
  - templating: `ParsePage` (+ options in `FW.parsePageInstance`)
- `osafw-app/App_Code/controllers/` - site/example controllers (Admin, Dev, My, auth, public).
- `osafw-app/App_Code/models/` - models per table and domain (Att*, Demos*, Roles*, Reports*, Dev*).
- `osafw-app/App_Data/template/` - templates, common includes, per-controller subfolders, dynamic configs.
- `osafw-app/App_Data/sql/` - SQL scripts: `fwdatabase.sql`, `lookups.sql`, optional `roles.sql`, `demo.sql`, `views.sql`, and `updates/*`. MySQL variants under `mysql/`.
- `docs/` - framework docs and ADRs.
- `osafw-tests/` - tests project.

## Coding Style
- C# on .NET 10, nullable enabled, namespace `osafw`.
- Use spaces for indentation. JavaScript uses 4-space indents; XML uses 2-space indents.
- Controllers: classes end with `Controller`, public action methods end with `Action`. Standard actions (constants in `FW`): `Index`, `Show`, `ShowForm`, `Save`, `SaveMulti`, `ShowDelete`, `Delete`. Overloads supported with `string` or `int` id; router resolves best match.
- Controllers return `FwDict` `ps` parsed by `FW.parser`. For JSON set `ps["_json"]=true` or assign data to `ps["_json"]`.
- Models: inherit `FwModel`. Obtain via `fw.model<T>()` or `fw.model("Name")`. Keep SQL in models, not controllers.
- Framework convention: `list*()` methods return an empty `FwList`/`DBList`, and `one*()` methods return an empty `FwDict`/`DBRow`; do not add nullable fallbacks unless the specific source can truly be null.
- Routing: `FW.getRoute()` implements RESTful mapping by HTTP method and URL. Prefixes (e.g., `/Admin`) are supported.
- Access control: static `access_level` on controller + `FwConfig.access_levels` rules. XSS token validated on mutating requests.
- Templates: prefer view composition in `osafw-app/App_Data/template`; override controller base dir with `controller.template_basedir` or `ps["_basedir_controller"]`.
- Keep ParsePage route literal templates such as `App_Data/template/**/url.html` on one line with no trailing newline byte.
- Utilities: use `FormUtils` for filtering/validation, `DateUtils` for user TZ formatting, `FwLogger` for logs, `FwCache` for memoization.
- For SQL queries or SQL fragments in code, prefer a single `$@"..."` string block over concatenated pieces so whitespace, quoting, and review are reliable.
- For new or updated C# methods, add XML docs explaining why the method exists and include detailed param/return info for non-primitive types; add inline comments for complex logic blocks.

## Sub-Agent Delegation
- The main agent owns task outcome, user communication, integration, and final verification. Use sub-agents to preserve focus when a bounded part of the work can run independently.
- At the start of non-trivial tasks, identify: critical-path work that must stay local, side work that can run in parallel, and high-risk or tightly coupled work that should not be delegated.
- Good delegation targets include targeted codebase research, independent docs/spec review, focused implementation in a disjoint area, post-implementation test execution, and code review via `docs/agents/code_reviewer.md`.
- Do not delegate vague ownership, broad "look around" tasks, or urgent blockers where the main agent cannot make progress until the answer returns.
- Give every sub-agent a narrow prompt with expected output, owned files/modules, and clear constraints. For code-editing workers, state that they are not alone in the codebase, must not revert others' changes, and must list changed paths in their final response.
- Prefer a small number of parallel sub-agents over many shallow ones. Inspect their evidence and changes before relying on them, then record material commands, findings, and decisions in the task summary.

## Agent Workspace
- Put disposable agent-created probes, temp scripts, and scratch outputs under `docs/agents/artifacts/`.
- Keep `docs/agents/artifacts/` gitignored; do not leave `tmp_*` scratch files at repo root or under `osafw-app/`.
- Use repo-root `/artifacts/` for build outputs and larger generated verification assets that do not belong under docs.
- Keep machine-specific agent guidance in `docs/agents/local_instructions.md`; check it before implementation when present, and keep that file out of Git.
- Put reusable agent/debug helpers under `docs/agents/tools/`.
- Do not store secrets, DB backups, or large logs in any agent workspace folder.

## Helpful Docs
- `docs/README.md` - documentation map and recommended reading order.
- `docs/templates.md` - templates and ParsePage guide.
- `docs/dynamic.md` - Dynamic/Vue controllers config.
- `docs/crud.md` - CRUD workflows.
- `docs/db.md` - DB helper overview and patterns.
- `docs/datetime.md` - per-user date/time and timezones.
- `docs/layout.md` - layout and page structure.
- `docs/dashboard.md` - dashboard panels.
- `docs/feature_modules.md` - module scaffolding.
- `docs/agents/code_reviewer.md` - review-fix loop instructions for code reviewer agents.
- `docs/agents/heuristics.md` - concise reusable working heuristics.
- `docs/agents/domain.md` - stable framework domain facts.
- `docs/agents/glossary.md` - framework vocabulary.
- `docs/adr/*` - architecture decisions (cache, db helper, ParsePage, datetime).
- `osafw-app/App_Data/template/dev/manage/docs/*` - in-app developer docs.
- SQL: `osafw-app/App_Data/sql/*.sql` (+ `mysql/*`).

## Documentation Sync
- `docs/templates.md` is the canonical templates and ParsePage doc for this repo.
- When changing shared ParsePage behavior, shared layout fragments, standard dynamic-controller screen structure, schema/update process, or agent workflow, review the related docs in the same task and note when no doc update was needed.
- When code changes alter agent workflow, review `AGENTS.md`, `docs/agents/code_reviewer.md`, and task-summary expectations together so they do not drift.

## Testing Guidance
- Prefer the smallest relevant verification that can falsify the change quickly: targeted build, focused test, then manual smoke for the affected flow.
- If no automated coverage exists or is practical, record concise manual verification steps and prerequisites in the task summary instead of inventing broad QA plans.
- For schema changes, verify both the additive update path (`App_Data/sql/updates`) and the from-scratch schema reference (`App_Data/sql/fwdatabase.sql`) were considered.

## Common Tasks
- Build solution: `dotnet build osafw-asp.net-core.sln`.
- Build app: `dotnet build osafw-app/osafw-app.csproj`.
- Build app to isolated output when normal `bin/Debug` is locked: `dotnet build osafw-app/osafw-app.csproj -p:OutDir=artifacts/assistant_build/`.
- Run app: `dotnet run --project osafw-app`.
- Watch app: `dotnet watch run --project osafw-app`.
- Test: `dotnet test`.
- Database setup (SQL Server): run `osafw-app/App_Data/sql/fwdatabase.sql`, then `lookups.sql`. Run `roles.sql` if roles required. Optionally `demo.sql` for sample data. Rebuild indexes as needed.
- Configure connection: `appsettings*.json` under `appSettings.db.main` (`type`, `connection_string`).
- Switch to MySQL: define `isMySQL` in `Program.cs`, set `db.main.type` to MySQL, use scripts from `osafw-app/App_Data/sql/mysql/`.
- Scheduled tasks: uncomment `builder.Services.AddHostedService<FwCronService>();` in `Program.cs`.
- Windows auth: enable Negotiate and use `/winlogin` path.
- Sessions/Data Protection: ensure `fwsessions` and `fwkeys` tables exist.

## MCP Tooling
- Prefer Visual Studio MCP for solution-aware work. Validate it with `solution_info` or `project_list`, then use `document_*`, `build_*`, `build_status`, `errors_list`, and debugger tools as needed.
- Prefer Playwright MCP for browser repros and UI verification. Re-run `browser_snapshot` after navigation or meaningful DOM changes; use `browser_evaluate`, console, or network tools when snapshots omit needed details.
- Check each MCP independently. Do not infer Visual Studio MCP health from generic MCP resource discovery or from Playwright health, and vice versa.
- If Playwright reports `EPERM` around `C:\Windows\System32\.playwright-mcp` but still returns a valid snapshot, URL, or title, treat it as usable and verify real page state before abandoning it.
- If a required MCP is missing, cannot connect, or returns a blocking runtime error, do one quick validation and at most one lightweight retry. If still blocked, stop and ask the user whether to use a non-MCP workaround or restart/fix that MCP first.
- Do not spend multiple turns looping on MCP recovery unless the user explicitly asks for fallback attempts. If the user explicitly asked to use MCP, prefer waiting for a working MCP path over silently switching to CLI.
- Machine-specific local app URLs, credentials, and browser notes belong in `docs/agents/local_instructions.md`.

## Command Palette
- `dotnet restore`; `dotnet build`; `dotnet run --project osafw-app`; `dotnet watch run --project osafw-app`; `dotnet test`.

## Heuristics
- See `docs/agents/heuristics.md`.
