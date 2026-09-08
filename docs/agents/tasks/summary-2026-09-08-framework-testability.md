# Framework testability and database lifetime

## Objective / acceptance

Make FW/model tests readable with explicitly supplied named database dependencies, scoped settings, and either a strict fake or a disposable SQLite file. Preserve normal creation defaults, model caching, typed missing-row behavior, and logging privacy. Keep fixtures and synthetic entities in the MSTest project. Exclude configured/shared database integration and provider-parity claims.

## What changed

- Added a constructor-supplied DB factory that receives the initial main name and every later requested name. Null results fail; configured creation is only used when no factory is supplied.
- Centralized ownership of all returned DB wrappers with reference identity, context/logger/PII binding, constructor-failure cleanup, and best-effort disposal of all resources. Default calls still create fresh wrappers; an explicit factory may reuse its own results.
- Added virtual typed/dictionary read, delete, typed update, metadata, and connection boundaries alongside existing virtual DB operations.
- Kept the void logger setter and added nullable suppression plus a separate swap/restore helper.
- Added opt-in async-flow configuration scopes covering static consumers, host buckets, base settings, trust caches, reload, and reinitialization.
- Added synthetic model/fake and SQLite tests. Updated canonical DB/CRUD guidance and the dated lifetime upgrade note. Existing internal model registration requires no change.

## Scope reviewed / decisions

Reviewed FW construction, config/model lookup, logger binding and disposal; DB constructors, connection/transaction sharing, CRUD/mapping/schema paths and disposal; FwModel initialization and public CRUD/cache flows; static configuration consumers; current MSTest helpers and provider gates. Used direct implementation because these contracts are coupled.

Fresh-wrapper creation remains the default because wrapper sharing changes timeout and transaction state. HTTP wrappers still share physical connections for the same connection string. No new force-new API, wrapper cache, IDB abstraction, SQL emulator, service-container requirement, schema changes, or test-runner migration.

Configuration remains ambient. Each independent async flow must create and initialize its own scope; child work inherits mutable settings. Environment variables, mutable caller-owned configuration providers, application memory caches, DB schema caches, and SQL diagnostics remain outside this isolation guarantee. Existing assembly-level test serialization remains.

## Changed contracts

FW now owns every wrapper obtained from getDB, including offline/named/repeated results; the public main DB replacement remains owned. Do not retain these wrappers beyond FW disposal. Disposal continues through individual errors and then aggregates them; getDB after disposal throws. Factories transfer returned instances to FW and must not share them across FW lifetimes. Typed missing rows remain null, and dictionary missing rows remain empty. Normal constructor/getDB signatures and the void logger setter are preserved.

## Commands used / verification

- Initial worktree was clean and HEAD matched master at d1c1490dc5d3725c6433c2eb35808bb4d3d2c6ac. Created codex/framework-testability from master.
- Focused normal tests: 12 passed before the additional constructor-failure case; focused SQLite tests: 15 passed at that stage.
- Final normal suite: `dotnet test osafw-tests/osafw-tests.csproj --filter 'FullyQualifiedName!~osafw.Tests.DBTests' --logger 'trx;LogFileName=safe-normal.trx' --results-directory artifacts/assistant_testability/test-results/safe-normal` — 708 passed.
- Final SQLite suite: `dotnet test osafw-tests/osafw-tests.csproj '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite' --filter 'FullyQualifiedName!~osafw.Tests.DBTests' --logger 'trx;LogFileName=safe-sqlite.trx' --results-directory artifacts/assistant_testability/test-results/safe-sqlite` — 719 passed.
- `dotnet build osafw-asp.net-core.sln --no-restore` — passed, zero warnings/errors. An initial sandbox attempt could not create MSBuild temporary files; the authorized retry succeeded.
- `dotnet build osafw-asp.net-core.sln '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite'` — passed, zero errors, four NU1903 warnings for the existing SQLitePCLRaw.lib.e_sqlite3 2.1.11 dependency.
- Evaluated DefineConstants for both projects: TRACE;DEBUG. Preserved both while adding isSQLite.
- Strict UTF-8 without BOM/CRLF validation and git diff --check passed on implementation, tests, and canonical documentation.
- TRX evidence retained in ignored artifacts/assistant_testability/test-results.

Behavior evidence includes named models and model-cache reuse without reflection, rejecting unknown dependencies/operations, controlled model-update failure with cache preservation, successful-update cache invalidation, logger binding/privacy and suppression/restoration, base-constructor factory availability, disposal of distinct results including failure, configuration restoration after initialization failure, nested/parallel scopes and static trust/route consumers. SQLite exercises typed aliases/nulls/identity, dictionary and typed missing rows, CRUD, rollback, fresh wrappers, HTTP connection sharing, and offline connection cleanup.

## Review

Fresh independent reviewer routed with consumer-contract and state-integrity overlays because public/source-copy and state-lifetime contracts changed. Initial independent review found no blocking findings. The supplemental summary audit returned no candidates and independently confirmed retained TRX counts. Final integrator verdict: No blocking findings. Review loop can stop.

## Testing instructions / limitations

Run the two filtered test commands above. DBTests is deliberately excluded: its setup connects to a configured SQL Server database and drops/creates a table. No SQL Server/shared-resource integration was enabled. SQLite tests create uniquely named local files with pooling disabled and delete only their own files/sidecars after connections close.

SQLite does not establish SQL Server/MySQL/OLE/ODBC SQL, locking, precision, type mapping, or deployment correctness. No IIS/browser or non-Windows runtime verification was performed. The existing SQLite package advisory is recorded for a separate dependency update.

## Reflection

The useful boundary is an explicit constructor dependency plus an ambient settings scope; increasing virtual coverage alone cannot isolate static configuration or prevent schema paths from connecting. Existing upload tests left three worktree-local generated images; only those exact task-created images and their empty parent directories were removed at closeout. No reusable agent-policy change is needed for this feature.
