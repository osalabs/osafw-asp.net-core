## What changed
- Added SQLite as an optional provider: `DB.DBTYPE_SQLITE`, SQLite command/transaction/identity handling, identifier quoting, paging, `PRAGMA` foreign keys, schema/FK introspection, table/view enumeration, and provider expression helpers.
- Added `FwSqliteDistributedCache` for SQLite-backed sessions and startup registration gated by `isSQLite` plus `appSettings.db.main.type = "SQLite"`.
- Replaced SQL Server-only runtime SQL in dashboard, advanced search, login/date aggregation, attachment cleanup, static page publishing, activity counts, users display-name SQL, and key cleanup.
- Added SQLite schema/init scripts under `osafw-app/App_Data/sql/sqlite`, provider-specific update/view resolution, Dev Configure init routing, and Dev codegen SQLite DDL mapping.
- Moved provider SQL script root resolution from `DB` to `FwUpdates.sqlScriptRoot()` so script layout belongs to the framework update/init flow.
- Updated SQLite setup docs, datetime/provider notes, feature module update paths, README, agent domain facts, and heuristics.
- Added provider-neutral unit coverage and SQLite temp-database integration tests for CRUD, identity, parameter expansion, schema/FKs, full schema scripts, `fwkeys`, `fwsessions`, and numeric expression behavior.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- FPF TOC and relevant provider-boundary sections from `docs/drafts/FPF-Spec.md`
- Existing SQL Server/MySQL/OLE provider seams in `DB.cs`, startup/session wiring, SQL scripts, and provider-specific runtime SQL.
- SQL Server canonical schema scripts translated into a fresh SQLite script set under `App_Data/sql/sqlite`.

## Commands used / verification
- `dotnet build osafw-app/osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build\` - passed.
- `dotnet build osafw-app/osafw-app.csproj -p:DefineConstants=isSQLite -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build_sqlite\` - passed; one retry warning occurred when `obj\Debug\net10.0\apphost.exe` was briefly locked.
- `dotnet test osafw-tests/osafw-tests.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_build\ --filter "FullyQualifiedName~DBOperationTests|FullyQualifiedName~DBTests|FullyQualifiedName~FwKeysTests"` - passed, 72 tests; existing nullable warning in `ConvUtilsTests.cs`.
- `dotnet test osafw-tests/osafw-tests.csproj -p:DefineConstants=isSQLite -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_build_sqlite\ --filter "FullyQualifiedName~DBOperationTests|FullyQualifiedName~DBTests|FullyQualifiedName~FwKeysTests|FullyQualifiedName~SQLiteDBTests"` - passed, 77 tests; existing nullable warning in `ConvUtilsTests.cs`.
- `git diff --check` - passed.
- Normal `dotnet build osafw-app/osafw-app.csproj` and unisolated `dotnet test` were blocked by VS/IIS Express locking `osafw-app\bin\Debug\net10.0\osafw-app.dll`, so verification used repo-root artifact output paths.
- Browser smoke against the VS-hosted app at `https://localhost:44315/` after restart: root/login page loaded, `/Dev/Configure` reported DB configured/connected/timezone/tables OK, local dev login reached `/Main`, `/Admin/Users` loaded, Users search for `demo` filtered to one row, `/Admin/Att` loaded, and browser console warnings/errors were empty.
- Code review sub-agent found issues; fixes applied for SQLite cache enablement docs/compile behavior, generated-column schema introspection, SQLite numeric search parity including malformed numeric text, task summary state, and diff hygiene.
- Final code review pass reported no issues found.
- Focused follow-up review after moving script-root resolution to `FwUpdates.sqlScriptRoot()` found one codegen update-path issue; fixed generated update scripts to write under the provider-specific `updates` folder. Follow-up default and SQLite app builds passed; `git diff --check` passed.

## Decisions - why
- SQLite production target is durable single-node production with app DB, sessions, and data-protection keys in SQLite.
- SQLite framework updates will use provider-specific update folders going forward rather than replaying SQL Server historical updates.
- Attachment public codes are now generated in the model before insert so SQLite does not depend on unsupported non-constant random defaults.
- `FwSqliteDistributedCache` now compiles whenever the SQLite package is present; runtime usage is still gated by `isSQLite` and config. This avoids project-reference/test define propagation gaps.
- SQLite schema introspection uses `PRAGMA table_xinfo` so generated columns like `users.iname` remain visible to framework schema helpers.

## Pitfalls - fixes
- SQLite DDL cannot use SQL Server inline indexes or identity syntax; scripts and Dev codegen use `INTEGER PRIMARY KEY AUTOINCREMENT` and separate `CREATE INDEX` statements.
- `Microsoft.Data.Sqlite` does not implement `GetSchema("Tables")`; SQLite table/view lists query `sqlite_schema`.
- SQLite `CAST('abc' AS REAL)` returns `0`; `sqlNumberExpr` now returns `NULL` for non-numeric text before casting.
- Relative `OutDir` values under web projects caused recursive artifact copying; use absolute repo-root artifact paths.

## Risks / follow-ups
- Live app smoke was run against the current VS-served configuration. Automated temp-database integration covers SQLite behavior; a final full browser smoke with SQLite still requires VS running with `isSQLite` and `db.main.type = "SQLite"`.
- SQLite is single-node production only; multi-node deployments should use SQL Server/MySQL or external distributed cache.

## Heuristics (keep terse)
- Added reusable provider/update-path and provider-neutral SQL helper heuristics to `docs/agents/heuristics.md`.

## Testing instructions
- For SQL Server/default compile path, run the default build and targeted DB tests above.
- For SQLite, build/test with `-p:DefineConstants=isSQLite`, set `db.main.type` to `SQLite`, use `Data Source=App_Data/db/osafw.sqlite;Mode=ReadWriteCreate;Foreign Keys=True;Default Timeout=30;Pooling=True;`, then initialize from `App_Data/sql/sqlite`.
- If VS/IIS Express is running, use absolute `OutDir` under repo-root `artifacts\` or stop the app before building to normal `bin\Debug`.

## Reflection
- Stable provider facts were added to `docs/agents/domain.md`; no ADR was added because this is an implementation-level optional-provider decision already bounded by the request and docs.
- `AGENTS.md` was not changed, so no `.github/copilot-instructions.md` sync was required.
