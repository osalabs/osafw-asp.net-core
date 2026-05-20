## What changed
- Enabled SQLite-only runtime for the app by defining `isSQLite`, switching base/Development/Beta DB config to `Data Source=App_Data/db/osafw.sqlite`, and removing active SQL Server/MySQL sample override keys from `appsettings.json`.
- Added `/osafw-app/App_Data/db/` to `.gitignore` so local SQLite databases are not commit candidates.
- Fixed `/Dev/Configure` Initialize DB button by adding the session XSS token to the POST URL.
- Fixed Admin Direct DB `show tables` for SQLite by using provider-aware `db.tables()` instead of `DbConnection.GetSchema("Tables")`.
- Updated SQLite/default-provider docs in `docs/db.md`, `docs/agents/domain.md`, and `AGENTS.md`; synced `AGENTS.md` to `.github/copilot-instructions.md`.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/db.md`
- `docs/agents/domain.md`
- `AGENTS.md`
- `.github/copilot-instructions.md`
- `osafw-app/appsettings.json`
- `osafw-app/appsettings.Development.json`
- `osafw-app/osafw-app.csproj`
- `osafw-app/Program.cs`
- `osafw-app/App_Code/controllers/DevConfigure.cs`
- `osafw-app/App_Code/controllers/AdminDB.cs`
- `osafw-app/App_Data/template/dev/configure/index/main.html`
- `docs/agents/tasks/summary-2026-05-19-initialize-db.md`
- `docs/agents/code_reviewer.md`

## Commands used / verification
- Visual Studio MCP: startup project is `osafw-app\osafw-app.csproj`.
- Visual Studio MCP: built `osafw-app` successfully after enabling SQLite and after the Admin Direct DB fix.
- Visual Studio MCP: built `osafw-app` successfully again after converting Beta config and updating docs.
- Visual Studio MCP: launched the app without debugging at `https://localhost:44315/` after each build.
- Browser: `/Dev/Configure/` initially showed DB configured/connected/timezone OK and DB tables missing with `Initialize DB`.
- Browser: first `Initialize DB` click was rejected by XSS validation and redirected home; fixed the missing XSS token in the template.
- Browser: after fix, `Initialize DB` returned `Developer: Database initialized`; logs showed SQLite scripts from `App_Data/sql/sqlite` executed with 107 SQL statements.
- Browser: `/Dev/Configure/` after initialization showed DB configured, connected, timezone, and tables OK. Writable upload directories still showed FAIL in this local environment.
- Browser: logged in as seeded admin using the initializer-generated password.
- Browser smoke: loaded `/Main`, `/Admin/Reports`, `/Admin/Reports/sample`, `/Admin/Demos`, `/Admin/DemosDynamic`, `/Admin/DemosVue`, `/Admin/DemoDicts`, `/Admin/Spages`, `/Admin/Att`, `/Admin/Lookups`, `/Admin/Users`, `/Admin/Settings`, `/Admin/DB`, `/Admin/FwUpdates`, `/Dev/Manage`, and `/Dev/Configure`.
- Browser CRUD: standard Demo create, autosave update, view verification, and delete all worked.
- Browser CRUD: Dynamic Demo create, autosave update, view verification, and delete all worked.
- Browser CRUD: Vue Demo list/create/autosave update/view worked; direct `/Admin/DemosVue/{id}/delete` returned the existing `NotImplementedException`, so the test row was deleted through the standard Demo controller against the same table.
- Browser CRUD: Demo Dictionary create/delete worked.
- Browser update: Site Settings value update persisted and was visible on the list.
- Browser Direct DB: `show tables` initially failed on SQLite with missing schema collection; after the fix, it listed table row counts for `users`, `demos`, `fwupdates`, and other SQLite tables without error.
- Browser final check after reviewer fixes: `/Dev/Configure/` still reported DB configured/connected/timezone/tables OK, and Direct DB `show tables` still listed SQLite table counts without errors.
- `dotnet test osafw-tests\osafw-tests.csproj -p:DefineConstants=isSQLite` failed in sandbox because MSBuild could not write temp files.
- Escalated `dotnet test osafw-tests\osafw-tests.csproj -p:DefineConstants=isSQLite` failed because IIS Express locked the normal `bin\Debug\net10.0\osafw-app.dll`.
- Escalated `dotnet test osafw-tests\osafw-tests.csproj -p:DefineConstants=isSQLite -p:OutDir=artifacts\assistant_test\` ran 421 tests: 418 passed, 3 failed (`FwDynamicControllerTests.NextAction_*` null refs and `ParsePageTests.parse_string_dateTest` 12h/24h expectation mismatch).
- Escalated `dotnet test osafw-tests\osafw-tests.csproj -p:DefineConstants=isSQLite -p:OutDir=artifacts\assistant_test\ --filter "FullyQualifiedName~SQLite"` passed 7/7 SQLite-focused tests.
- Escalated final `dotnet test osafw-tests\osafw-tests.csproj -p:DefineConstants=isSQLite -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_sqlite_only\ --filter "FullyQualifiedName~SQLite"` passed 7/7 SQLite-focused tests.
- Removed generated `osafw-app/artifacts/` and `osafw-tests/artifacts/` output directories created by the relative `OutDir` test run.
- Code reviewer sub-agent pass 1 reported Beta override/doc drift findings; both were fixed.
- `git diff --check` passed.
- CRLF check: all edited files reported `LF_without_CR=0`.

## Decisions - why
- Kept the SQLite DB path under `App_Data/db/` because it matches the existing SQLite docs and the DB helper creates the directory when needed.
- Ignored the whole `App_Data/db/` directory because SQLite creates database and sidecar files that should not be committed.
- Fixed the Initialize DB button instead of bypassing XSS for `/Dev` because the framework pattern for `on-fw-submit` mutating actions is to include `XSS=<~SESSION[XSS]>`.
- Reused `db.tables()` in Admin Direct DB because the DB helper already contains provider-specific table enumeration for SQL Server, SQLite, MySQL, and OLE.
- Converted the Beta override to SQLite too because config overrides deep-merge, and a SQL Server connection string would inherit `type: SQLite`.

## Pitfalls - fixes
- `on-fw-submit` posts require an XSS token. Without it, `/Dev/Configure/(InitDB)` redirects home before executing SQL.
- `DbConnection.GetSchema("Tables")` is not available from `Microsoft.Data.Sqlite`; using `DB.tables()` avoids provider-specific schema collection assumptions.
- Relative `OutDir=artifacts\assistant_test\` is evaluated per project by MSBuild and created project-local artifacts; removed those outputs after the test run.
- IIS Express locks the normal app Debug DLL, so full test runs need either a stopped app or an isolated output directory.

## Risks / follow-ups
- Full suite has 3 failures under the SQLite build that appear unrelated to this provider/config change, but they should be triaged separately before using the whole suite as a release gate.
- `/Admin/DemosVue/{id}/delete` is not implemented in `FwVueController`; this is not SQLite-specific, but it is a functional gap if Vue delete is expected.
- `/Dev/Configure/` still reports writable upload directories as FAIL in this local environment; file upload write-path testing was limited by that environment issue.
- The local admin password was regenerated by Init DB; the previous local `~` password no longer applies to this SQLite test database.

## Heuristics (keep terse)
- No reusable heuristics added.

## Testing instructions
- Build app with SQLite enabled: `dotnet build osafw-app\osafw-app.csproj`.
- SQLite-focused tests: `dotnet test osafw-tests\osafw-tests.csproj -p:DefineConstants=isSQLite --filter "FullyQualifiedName~SQLite"`.
- Manual smoke: open `https://localhost:44315/Dev/Configure/`, initialize DB if needed, log in as seeded admin, then verify the menu routes and CRUD flows listed above.

## Reflection
- Visual Studio MCP was effective for build/restart of the running IIS Express app. Browser automation was effective for menu and CRUD coverage, especially after using DOM snapshots to target forms.
- The slowest parts were distinguishing SQLite-specific failures from existing framework gaps and handling autosave forms that require blur/focus movement before persistence.
- Future runs should use an absolute repo-root `OutDir` for test builds when IIS Express is running, or stop the app before broad `dotnet test`.
- No ADR was added because this task validated and aligned the branch's SQLite-only default rather than making a new architecture decision.
