# Schema metadata and typed-row generation

## Objective / acceptance

Add provider-neutral column comments to full schema metadata, correct schema-qualified SQL Server lookup, make cold OLE metadata reads connect without changing warm-cache behavior, improve typed `Row` generation, and exclude the current framework persistence tables from application entity scaffolding. Bulk source regeneration and live/shared database mutation are excluded.

Acceptance requires focused offline tests through `DB.loadTableSchemaFull`, disposable SQLite, `DevEntityBuilder.isFwTableName`, and the established `DevCodeGen.createModel` entry path. Negative and preserved-positive controls cover same-named SQL Server schemas, a cold OLE cache miss, a disposed wrapper with a warm cache hit, a non-framework table, and existing identity/computed/offset-aware generated fields.

## What changed

- Full schema rows now use the neutral `comments` key across SQL Server, MySQL, SQLite, and OLE metadata paths. SQL Server joins columns to tables and system metadata by catalog, schema, table, and column, and accepts `schema.table` input without combining same-named tables.
- OLE schema discovery opens the provider connection only after a full-schema cache miss. Cached metadata remains available without reconnecting, including after wrapper disposal under the existing cache contract.
- Generated typed rows map signed `bigint` to `long`, unsigned `bigint` to `ulong`, decimal/numeric metadata to `decimal`, and retain `DateTimeOffset`. Database comments become escaped XML summaries; normalized, keyword, leading-digit, duplicate, and otherwise unsafe property names become valid unique C# identifiers with escaped `[DBName]` metadata where required.
- The framework table inventory now includes current core, reporting, RBAC, Knowledge Base, RAG, and Assistant persistence tables so they are not treated as application entities.

## Scope reviewed

Reviewed the full-schema provider branches and caches in `DB`, entity conversion and framework-table classification in `DevEntityBuilder`, typed-row generation in `DevCodeGen`, SQL Server/SQLite/MySQL fresh framework schemas, the documented scaffolding entry path, and existing metadata/code-generation tests.

## Requirements / decisions

- Kept `comments` additive and provider-neutral. Providers without native column comments return an empty value.
- Kept existing `desc` on OLE rows for compatibility while adding `comments`.
- Kept full-schema caches keyed and returned exactly as before; connection creation occurs only after a miss.
- Used the SQL Server fresh schema as the primary framework table inventory and included the framework RBAC tables from the shipped roles schema. Provider-specific legacy application tables were not added.
- Did not regenerate existing model source. Future explicit regeneration can consume this metadata foundation.

## Changed contracts

`DB.loadTableSchemaFull`/`tableSchemaFull` add `comments` to provider metadata rows and make SQL Server `schema.table` selection exact. Newly scaffolded model `Row` output has range-accurate signed and unsigned `bigint` types, more accurate decimal/numeric types, optional XML summaries, and safe property mappings. Existing generated source, schema, runtime configuration, routes, and database data are unchanged.

## Commands used / verification

- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~osafw.Tests.DevCodeGenTests" --verbosity minimal` — passed 18/18 after verifying the full unsigned-`bigint` range maps to nullable and non-nullable `ulong` generated properties.
- `dotnet test osafw-tests\osafw-tests.csproj '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite' --filter "FullyQualifiedName~osafw.Tests.SQLiteDBTests|FullyQualifiedName~osafw.Tests.DBOperationTests|FullyQualifiedName~osafw.Tests.DevCodeGenTests|FullyQualifiedName~osafw.Tests.DevEntityBuilderTests" --verbosity minimal` — passed 46/46.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~osafw.Tests.DBOperationTests|FullyQualifiedName~osafw.Tests.DevCodeGenTests|FullyQualifiedName~osafw.Tests.DevEntityBuilderTests" --verbosity minimal` — passed 39/39.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName!~osafw.Tests.DBTests" --verbosity minimal` — passed 717/717; the configured live SQL Server test class was excluded.
- `dotnet test osafw-tests\osafw-tests.csproj '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite' --filter "FullyQualifiedName!~osafw.Tests.DBTests" --verbosity minimal` — passed 728/728; the configured live SQL Server test class was excluded.
- `dotnet build osafw-app\osafw-app.csproj --no-restore --verbosity minimal` — passed with zero warnings and errors.
- `dotnet build osafw-app\osafw-app.csproj '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite' --verbosity minimal` — passed with zero warnings and errors.
- `docs\agents\tools\Normalize-TextFiles.ps1 -Check <touched paths>` and `git diff --check` — passed after final normalization.

## Testing instructions

Run the focused test command above from the repository root. It creates only disposable temporary SQLite and generated-model files and removes them during test cleanup.

## Risks / follow-ups

No live SQL Server, MySQL, or OLE catalog was available, so those provider queries are covered by deterministic SQL/parameter assertions and the Windows cold-connection boundary rather than provider integration. SQL decimal/numeric values beyond the .NET `decimal` range still require an application-specific representation. Bulk regeneration of existing typed rows remains separate work.

## Reflection

The existing public scaffolding entry and disposable provider tests made the generated-output and cache contracts observable without adding production test seams or touching a configured database. A reusable provider-metadata fixture would improve future exact SQL Server/MySQL/OLE integration evidence, but it should be introduced only with isolated provider resources rather than embedded deployment configuration.
