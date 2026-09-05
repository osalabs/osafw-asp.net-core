# Computed fields in generated Virtual Controllers

## Objective / acceptance

- Detect SQL Server computed, SQLite generated, and MySQL generated columns through normalized schema metadata.
- Keep schema-generated computed fields visible but read-only in Dynamic/Vue/Virtual Controller forms and inline lists.
- Exclude computed fields from generated `save_fields` while leaving their source fields writable.
- Preserve explicit stored/file controller config as authoritative and refresh derived values only on the next normal load.

## What changed

- Added provider-normalized `is_computed` column metadata for SQL Server, SQLite, and MySQL.
- Made `DevCodeGen` generate computed form fields as `plaintext`, remove generated required/validation behavior, and omit them from `save_fields` without removing them from list/view metadata.
- Added focused metadata and generated-config regression coverage and documented the additive contract.

## Scope reviewed

- `DB.loadTableSchemaFull()` / `tableSchemaFull()` provider branches and schema caches.
- `DevCodeGen.updateControllerConfig()` field, list, layout, validation, and save-field generation.
- Virtual Controller config generation/merge order and existing Vue/Dynamic read-only rendering behavior.
- Canonical DB/Dynamic docs and provider-specific fresh schemas containing generated `users.iname` columns.

## Requirements / decisions

- Computed detection applies during schema-driven generation; no save-path or DB-write guard was added.
- The computed override runs after entity UI inference, while later explicit stored/file controller config can still override the generated result.
- Computed values remain visible in edit-list defaults/maps and use the existing `plaintext` behavior; no frontend changes or immediate post-save refresh were added.
- Access/OLE computed-column inference remains outside the supported scope.

## Changed contracts

- Additive: full schema metadata rows now include `is_computed` for SQL Server, SQLite, and MySQL.
- Behavioral bug fix: schema-generated computed fields are read-only and absent from generated `save_fields`.
- No database schema/update script, route, template/page-state shape, or new controller-config key was added.

## Commands used / verification

- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~DevCodeGenTests|FullyQualifiedName~DBOperationTests" -p:OutDir=artifacts\assistant_computed_fields\default\` passed 33 tests.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~SQLiteDBTests -p:DefineConstants=isSQLite -p:OutDir=artifacts\assistant_computed_fields\sqlite\` passed 7 tests; restore/build reported the existing `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName!~osafw.Tests.DBTests" -p:OutDir=artifacts\assistant_computed_fields\full-default\` passed 654 tests.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName!~osafw.Tests.DBTests" -p:DefineConstants=isSQLite -p:OutDir=artifacts\assistant_computed_fields\full-sqlite\` passed 662 tests; the same existing SQLite package advisory was reported.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_computed_fields\app-default\` passed with zero warnings/errors.
- `dotnet build osafw-app\osafw-app.csproj -p:DefineConstants=isMySQL -p:OutDir=artifacts\assistant_computed_fields\app-mysql\` passed with two existing nullable warnings in MySQL-only DB branches.
- SQL Server `DBTests` were excluded because their fixture drops/recreates a table in the configured `demo` database; no authority was given to mutate it.
- `docs/agents/tools/Normalize-TextFiles.ps1 -Check` passed for every changed text file, and `git diff --check` is clean.
- Removed the exact task-owned `osafw-app/artifacts/assistant_computed_fields` and `osafw-tests/artifacts/assistant_computed_fields` verification output directories.

## Review

No blocking findings. The final code-review pass included the consumer-contract and state-integrity overlays: provider metadata is additive, schema-generated behavior changes only inferred config, explicit config remains authoritative, and no shared database was mutated. Review loop can stop.

## Risks / follow-ups

- No live SQL Server/MySQL provider or browser Lookup Manager smoke test was run because no authorized disposable database was provided. Provider query-shape tests cover the native metadata expressions, and SQLite exercises stored/virtual generated columns against a disposable database.
- Existing explicit configs that mark computed fields writable can still produce the underlying provider error by design.
- No `docs/CHANGELOG.md` entry is required because this is an additive metadata field and generated-config bug fix with no app migration.

## Reflection

- Reusing schema metadata plus existing `plaintext` handling kept the runtime change small and avoided a new config flag, frontend type, or write-layer exception path.
