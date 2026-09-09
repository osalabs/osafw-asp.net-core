# Schema metadata and typed-row generation

## Objective / acceptance

Add provider-neutral column comments to full schema metadata, correct schema-qualified SQL Server lookup, make cold OLE metadata reads connect without changing warm-cache behavior, improve typed `Row` generation, and exclude the current framework persistence tables from the `/Dev/Manage` application-model picker. Bulk source regeneration and live/shared database mutation are excluded.

Acceptance requires focused offline tests through `DB.loadTableSchemaFull`, disposable SQLite, `DevManageController.IndexAction`, `DevEntityBuilder.isFwTableName`, and the established `DevCodeGen.createModel` entry path. Negative and preserved-positive controls cover schema-qualified SQL Server query construction for the same-named-table risk, a cold OLE cache miss, a disposed wrapper with a warm cache hit, an ordinary application table beside a framework table in the public picker output, and existing identity/computed/offset-aware generated fields.

## What changed

- Full schema rows now use the neutral `comments` key across SQL Server, MySQL, SQLite, and OLE metadata paths. The SQL Server query joins columns to tables and system metadata by catalog, schema, table, and column, and binds both parts of `schema.table` input; deterministic query/parameter assertions cover this same-named-table risk.
- OLE schema discovery opens the provider connection only after a full-schema cache miss. Cached metadata remains available without reconnecting, including after wrapper disposal under the existing cache contract.
- Generated typed rows map signed `bigint` to `long`, unsigned `bigint` to `ulong`, decimal/numeric metadata to `decimal`, and retain `DateTimeOffset`. Database comments become escaped XML summaries; normalized, keyword, leading-digit, duplicate, and otherwise unsafe property names become valid unique C# identifiers with escaped `[DBName]` metadata where required.
- The framework table inventory now includes current core, reporting, RBAC, Knowledge Base, RAG, and Assistant persistence tables. The `/Dev/Manage` model picker uses that inventory to offer application tables and views while omitting framework persistence tables.

## Scope reviewed

Reviewed the full-schema provider branches and caches in `DB`, entity conversion and framework-table classification in `DevEntityBuilder`, `/Dev/Manage` model-picker output, every `dbschema2entities` consumer, explicit browser/CLI model-generation paths, typed-row generation in `DevCodeGen`, SQL Server/SQLite/MySQL fresh framework schemas, and existing metadata/code-generation tests.

## Requirements / decisions

- Kept `comments` additive and provider-neutral. Providers without native column comments return an empty value.
- Kept existing `desc` on OLE rows for compatibility while adding `comments`.
- Kept full-schema caches keyed and returned exactly as before; connection creation occurs only after a miss.
- Used the SQL Server fresh schema as the primary framework table inventory and included the framework RBAC tables from the shipped roles schema. Provider-specific legacy application tables were not added.
- Applied framework-table exclusion at the model picker. Existing/external database analysis continues to inspect the full database, and explicit browser or CLI model requests can still regenerate a framework model `Row` when deliberately requested.
- Did not regenerate existing model source. Future explicit regeneration can consume this metadata foundation.

## Changed contracts

`DB.loadTableSchemaFull`/`tableSchemaFull` add `comments` to provider metadata rows and make SQL Server `schema.table` selection exact. Newly scaffolded model `Row` output chooses signed and unsigned `bigint` CLR types that preserve their database ranges, more accurate decimal/numeric types, optional XML summaries, and safe property mappings. `/Dev/Manage` no longer publishes framework persistence tables in its Create Model selection state. Existing generated source, schema, runtime configuration, routes, and database data are unchanged.

## Commands used / verification

- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~osafw.Tests.DevCodeGenTests" --verbosity minimal` — passed 18/18 at that stage; unsigned-`bigint` coverage verifies generated nullable/non-nullable `ulong` type selection, not provider materialization of the full numeric range.
- `dotnet test osafw-tests\osafw-tests.csproj '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite' --filter "FullyQualifiedName~osafw.Tests.SQLiteDBTests|FullyQualifiedName~osafw.Tests.DBOperationTests|FullyQualifiedName~osafw.Tests.DevCodeGenTests|FullyQualifiedName~osafw.Tests.DevEntityBuilderTests" --verbosity minimal` — passed 46/46.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~osafw.Tests.DBOperationTests|FullyQualifiedName~osafw.Tests.DevCodeGenTests|FullyQualifiedName~osafw.Tests.DevEntityBuilderTests" --verbosity minimal` — passed 39/39.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName!~osafw.Tests.DBTests" --verbosity minimal` — passed 717/717; the configured live SQL Server test class was excluded.
- `dotnet test osafw-tests\osafw-tests.csproj '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite' --filter "FullyQualifiedName!~osafw.Tests.DBTests" --verbosity minimal` — passed 728/728; the configured live SQL Server test class was excluded.
- `dotnet build osafw-app\osafw-app.csproj --no-restore --verbosity minimal` — passed with zero warnings and errors.
- `dotnet build osafw-app\osafw-app.csproj '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite' --verbosity minimal` — passed with zero warnings and errors.
- `dotnet test osafw-tests\osafw-tests.csproj '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite' --filter "FullyQualifiedName~osafw.Tests.SQLiteDBTests|FullyQualifiedName~osafw.Tests.DevEntityBuilderTests|FullyQualifiedName~osafw.Tests.DevCodeGenTests" --verbosity minimal` — passed 30/30 after adding the public `/Dev/Manage` picker assertion with an ordinary-table positive control and framework-table negative control.
- `dotnet test osafw-tests\osafw-tests.csproj --no-restore --filter "FullyQualifiedName~osafw.Tests.DBOperationTests|FullyQualifiedName~osafw.Tests.DevCodeGenTests|FullyQualifiedName~osafw.Tests.DevEntityBuilderTests" --verbosity minimal` — passed 41/41 after the picker correction and removal of the tautological BCL maximum-value assertion.
- `docs\agents\tools\Normalize-TextFiles.ps1 -Check <touched paths>` and `git diff --check` — passed after final normalization.

## Testing instructions

Run the focused test command above from the repository root. It creates only disposable temporary SQLite and generated-model files and removes them during test cleanup.

## Risks / follow-ups

No live SQL Server, MySQL, or OLE catalog was available, so schema-qualified SQL Server query construction and the other provider queries are covered by deterministic SQL/parameter assertions and the Windows cold-connection boundary rather than provider integration. Unsigned `bigint` coverage verifies metadata-to-CLR type choice and generated source; it does not materialize the full database range through a live provider. SQL decimal/numeric values beyond the .NET `decimal` range still require an application-specific representation. Database analyzers intentionally retain framework tables, and explicit model requests can still target them. The framework-table inventory must be updated when new framework persistence tables are added. Bulk regeneration of existing typed rows remains separate work.

## Reflection

The existing public scaffolding entry and disposable provider tests made the generated-output and cache contracts observable without adding production test seams or touching a configured database. A reusable provider-metadata fixture would improve future exact SQL Server/MySQL/OLE integration evidence, but it should be introduced only with isolated provider resources rather than embedded deployment configuration.

## Integration review follow-up

Independent review found that MySQL unsigned bigint was not discovered from the provider's COLUMN_TYPE. The final integration reads and normalizes that metadata, verifies the discovery-to-generator path, and uses DATABASE() inside the query so cold MySQL connections select their configured database. Final targeted command: `dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter 'FullyQualifiedName~DBOperationTests|FullyQualifiedName~DevCodeGenTests|FullyQualifiedName~DevEntityBuilderTests' --verbosity quiet`: 41 passed. Earlier broader and SQLite results above precede this final provider correction; no live MySQL query was run.
