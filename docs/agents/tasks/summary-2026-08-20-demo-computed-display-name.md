# Demos computed display-name example

## Objective / acceptance

- Add a meaningful computed-column example to the existing Demos kitchen-sink entity without creating another demo table.
- Keep `icode` and `iname` editable while deriving `display_name` as `CODE — Title` in the database.
- Show the computed value as read-only in classic, Dynamic, and Vue demo forms/lists and keep it out of every save contract.
- Support SQL Server, SQLite, and MySQL fresh schemas, with an additive update path only for the persistent SQL Server demo database.

## What changed

- Added writable `demos.icode` and computed/generated `demos.display_name` columns to the three provider demo schemas.
- Added a SQL Server update that backfills existing demo codes as `DEMO-{id}` before adding the computed column.
- Added `icode` and `display_name` to the typed Demos row and exposed the example across classic, Dynamic, and Vue lists/forms.
- Kept `display_name` absent from static and configured save fields; Vue inline editing uses the existing `plaintext` read-only behavior.
- Added explicit-config regression checks plus SQLite fresh-schema integration coverage.

## Requirements / decisions

- Reused the existing `demos` table because it intentionally serves as the framework kitchen-sink example.
- Preserved editable `iname` for compatibility and used the project-style `icode — iname` pattern rather than converting `iname` itself into a computed column.
- Used SQL Server `PERSISTED`, MySQL `STORED`, and SQLite `STORED` generated columns.
- MySQL and SQLite demo databases are created from scratch, so their fresh schemas define the example directly and no provider update scripts are needed.
- Explicit Dynamic/Vue configs mirror schema-generated behavior because stored/file config remains authoritative.
- Computed values use the existing next-reload refresh behavior; no immediate inline-list recomputation was added.
- The foundational computed-field support was committed separately as `e1a559d2`; this example is the follow-up demo change.

## Changed contracts

- Additive demo schema columns: `icode` and `display_name`.
- Existing SQL Server demo rows receive an `icode` value of `DEMO-{id}` when its additive update is applied; MySQL and SQLite receive the columns only through fresh schema creation.
- Demo list/form metadata gains Code and Display name fields; existing routes and editable `iname` behavior remain unchanged.

## Verification

- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~DemoComputedFieldTests" -p:OutDir=artifacts\assistant_demo_computed\focused-default\` passed 3 tests.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~SQLiteDBTests|FullyQualifiedName~DemoComputedFieldTests" -p:DefineConstants=isSQLite -p:OutDir=artifacts\assistant_demo_computed\focused-sqlite\` passed 10 tests; restore/build reported the existing `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName!~osafw.Tests.DBTests" -p:OutDir=artifacts\assistant_demo_computed\full-default\` passed 657 tests.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName!~osafw.Tests.DBTests" -p:DefineConstants=isSQLite -p:OutDir=artifacts\assistant_demo_computed\full-sqlite\` passed 665 tests; restore/build reported the existing `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_demo_computed\app-default\` passed with zero warnings/errors.
- `dotnet build osafw-app\osafw-app.csproj -p:DefineConstants=isMySQL -p:OutDir=artifacts\assistant_demo_computed\app-mysql\` passed with two existing nullable warnings in MySQL-only DB branches.
- Both explicit demo configs parse as JSON. SQLite integration executes the fresh schema, verifies computed metadata/value, edits the source title, and observes the recomputed value.
- `docs/agents/tools/Normalize-TextFiles.ps1 -Check` passed for every changed text file, and `git diff --check` is clean.
- SQL Server `DBTests` remain excluded because their fixture mutates the configured `demo` database; no authority was given to mutate it.
- Removed the exact task-owned `osafw-app/artifacts/assistant_demo_computed` and `osafw-tests/artifacts/assistant_demo_computed` verification outputs.

## Pitfalls / fixes

- The first SQLite schema edit matched the earlier `demo_dicts.iname` declaration instead of `demos.iname`; the fresh-schema integration test caught the mismatch, and the generated fields were moved to `demos` before final verification.

## Review

No blocking findings. The final code-review pass included consumer-contract and state-integrity checks: `iname` remains editable, every save contract omits `display_name`, the SQL Server update is additive, and no shared database was mutated. Review loop can stop.

## Risks / follow-ups

- Live SQL Server update, MySQL fresh-schema, and browser smoke checks require an explicitly authorized disposable database; do not apply fresh demo schemas to an existing database.
- Existing saved user-list/view preferences may hide the newly added list columns until reset or customized.
- No breaking-change changelog entry is needed because the schema and demo UI additions are additive.
