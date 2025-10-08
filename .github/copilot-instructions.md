<!-- Mirror of AGENTS.md for Copilot -->

# Process/Workflow for each user query
1. Create task summary file `docs/agents/tasks/summary-<YYYY-MM-DD>-<TASK-ID>.md` per template in
<task-summary-template>
## What changed
## Commands that worked (build/test/run)
## Pitfalls - fixes
## Decisions - why
## Heuristics (keep terse)
</task-summary-template>

2. Work on user's query/task, update task summary file as needed

3. Post-process - after user's query is completely resolved, perform self-reflection, self-improvement, and optimization of the entire process/workflow, see steps in <post-process>
<post-process>
- review task summary file, make sure it's complete
- classify discoveries:
  - STABLE FACT - add to domain.md or glossary.md or AGENTS.md relevant section
  - HEURISTIC - add to heuristics.md, timestamp new heuristics and expire or revise anything older than 90 days.
  - ONE-OFF - keep in task summary file only
- if a substantial business decision changed, add an ADR under `docs/agents/adr/`.
- keep all knowledge concise and informative
- replace "YYYY-MM-DD" with today's date.
- don't store secrets or large logs.
</post-process>

## Project Overview
- osafw-asp.net-core is an opinionated ASP.NET Core (.NET 8) web framework/template for building data-heavy admin and CRUD apps.
- Core concepts:
  - `FW` request pipeline with custom router and RESTful mapping.
  - `ParsePage` template engine for views (no Razor). Templates live in `App_Data/template`.
  - MVC-ish structure: controllers, models, templates; models use thin `DB` helper.
  - Dynamic and Vue controllers driven by JSON config to scaffold CRUD quickly.
  - Built-in features: logging, caching, file uploads, settings, activity logs, self-test, virtual controllers, scheduled tasks.
- Runtime:
  - `Program.cs` uses minimal hosting, config via `FwConfig`, sessions via distributed SQL cache (`fwsessions`), data protection keys in `fwkeys`.
  - Optional MySQL support via `#define isMySQL` and matching SQL scripts.
- Database:
  - SQL Server by default; schema scripts under `App_Data/sql`. Key tables: `users`, `settings`, `spages`, `att*`, `activity_logs`, `fw*` (framework).

## Folder Structure
- `osafw-app/Program.cs` - app entrypoint and middleware registration.
- `osafw-app/App_Code/fw/` - core framework and helpers.
- `osafw-app/App_Code/controllers/` - site controllers.
- `osafw-app/App_Code/models/` - data models.
- `osafw-app/App_Data/template/` - templates and dynamic configs.
- `osafw-app/App_Data/sql/` - SQL scripts (MSSQL + MySQL).
- `osafw-app/docs/` - framework docs and ADRs.
- `osafw-tests/` - tests project.

## Coding Style
- C# 12, .NET 8, namespace `osafw`.
- Controllers: actions end with `Action`; return `Hashtable` for ParsePage.
- Models: inherit `FwModel`; use `fw.model<T>()`.
- Routing: RESTful via `FW.getRoute()` with prefixes.
- Access: static `access_level` + `FwConfig.access_levels`.

## Helpful Docs
- `docs/parsepage.md`, `docs/dynamic.md`, `docs/db.md`, `docs/datetime.md`, `docs/adr/*`.

## Common Tasks
- Build: `dotnet build`. Run: `dotnet run -p osafw-app`. Test: `dotnet test`.
- DB: run `App_Data/sql/fwdatabase.sql`, `lookups.sql`, optional `roles.sql`, `demo.sql`.
- Configure `appsettings*.json` for DB.
- MySQL: define `isMySQL`, use `App_Data/sql/mysql/*`.
