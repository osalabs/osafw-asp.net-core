<!-- AGENTS.md - Universal, Auto-Bootstrapping (v6) -->

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

Whenever AGENTS.md updated - make copy of it to top level /.github/copilot-instructions.md

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
- `osafw-app/App_Code/fw/` - core framework:
  - `FW`, `FwController`, `FwAdminController`, `FwVueController`, `FwDynamicController`, `FwVirtualController`
  - `DB`, `FwConfig`, `FwCache`, `FwLogger`, `FwExceptions`, `FwReports`, `FwCron(Service)`, `FwUpdates`
  - helpers: `Utils`, `FormUtils`, `DateUtils`, `ImageUtils`, `UploadUtils`, `SiteUtils`, `ConvUtils`
  - templating: `ParsePage` (+ options in `FW.parsePageInstance`)
- `osafw-app/App_Code/controllers/` - site controllers (Admin, Dev, My, auth, public).
- `osafw-app/App_Code/models/` - models per table and domain (Att*, Demos*, Roles*, Reports*, Dev*).
- `osafw-app/App_Data/template/` - templates, common includes, per-controller subfolders, dynamic configs.
- `osafw-app/App_Data/sql/` - SQL scripts: `fwdatabase.sql`, `lookups.sql`, optional `roles.sql`, `demo.sql`, and `updates/*`. MySQL variants under `mysql/`.
- `osafw-app/docs/` - framework docs and ADRs.
- `osafw-tests/` - tests project.

## Coding Style
- C# 12, .NET 8, namespace `osafw`.
- Controllers: classes end with `Controller`, public action methods end with `Action`. Standard actions (constants in `FW`): `Index`, `Show`, `ShowForm`, `Save`, `SaveMulti`, `ShowDelete`, `Delete`. Overloads supported with `string` or `int` id; router resolves best match.
- Controllers return `Hashtable` `ps` parsed by `FW.parser`. For JSON set `ps["_json"]=true` or assign data to `ps["_json"]`.
- Models: inherit `FwModel`. Obtain via `fw.model<T>()` or `fw.model("Name")`. Keep SQL in models, not controllers.
- Routing: `FW.getRoute()` implements RESTful mapping by HTTP method and URL. Prefixes (e.g., `/Admin`) are supported.
- Access control: static `access_level` on controller + `FwConfig.access_levels` rules. XSS token validated on mutating requests.
- Templates: prefer view composition in `App_Data/template`; override controller base dir with `controller.template_basedir` or `ps["_basedir_controller"]`.
- Utilities: use `FormUtils` for filtering/validation, `DateUtils` for user TZ formatting, `FwLogger` for logs, `FwCache` for memoization.

## Helpful Docs
- `docs/parsepage.md` - template engine.
- `docs/dynamic.md` - Dynamic/Vue controllers config.
- `docs/db.md` - DB helper overview and patterns.
- `docs/datetime.md` - per-user date/time and timezones.
- `docs/adr/*` - architecture decisions (cache, db helper, parsepage, datetime).
- `App_Data/template/dev/manage/docs/*` - in-app developer docs.
- SQL: `App_Data/sql/*.sql` (+ `mysql/*`).

## Common Tasks
- Build: `dotnet build`.
- Run: `dotnet run -p osafw-app` (or `dotnet watch run -p osafw-app`).
- Test: `dotnet test`.
- Database setup (SQL Server): run `App_Data/sql/fwdatabase.sql`, then `lookups.sql`. Run `roles.sql` if roles required. Optionally `demo.sql` for sample data. Rebuild indexes as needed.
- Configure connection: `appsettings*.json` under `appSettings.db.main` (`type`, `connection_string`).
- Switch to MySQL: define `isMySQL` in `Program.cs`, set `db.main.type` to MySQL, use scripts from `App_Data/sql/mysql/`.
- Scheduled tasks: uncomment `builder.Services.AddHostedService<FwCronService>();` in `Program.cs`.
- Windows auth: enable Negotiate and use `/winlogin` path.
- Sessions/Data Protection: ensure `fwsessions` and `fwkeys` tables exist.

## Command Palette
- dotnet restore; dotnet build; dotnet run -p osafw-app; dotnet watch run -p osafw-app; dotnet test.

## Heuristics
- See `docs/agents/heuristics.md`.