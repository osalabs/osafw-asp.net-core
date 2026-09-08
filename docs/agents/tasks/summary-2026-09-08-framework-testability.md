# Framework testability, review follow-up, and dependency refresh

## Objective / acceptance

Support FW/model tests with explicitly supplied named databases, isolated settings, and a strict fake or disposable SQLite file. Preserve the selected factory/ownership design, fresh-wrapper defaults, typed missing-row behavior, and PII policy. Make successful scope disposal repeatable, use one reversible logger setter, simplify representative existing fixtures, cover replacement-main ownership, and update both projects' direct and optional NuGet dependencies to current compatible stable releases.

## What changed

- The constructor DB factory receives the initial main name and every later requested name. Null results fail without configured fallback. FW binds context/logger/PII policy and owns each distinct returned wrapper by reference identity.
- FW disposes named/repeated wrappers and the current replacement main DB, attempts every resource after individual failures, and cleans up returned dependencies after constructor failure. Short comments explain the factory and ownership set.
- DB exposes small virtual operation, schema, and connection boundaries. Dictionary missing rows remain empty; typed missing rows remain null.
- FwConfig scopes isolate static consumers, host buckets, base settings, trust caches, reload, and reinitialization. Repeated successful disposal is a no-op; failed out-of-order disposal remains retryable after inner scopes finish.
- setLogger accepts null and returns the prior delegate for restoration. The separate swap helper was removed. Statement calls remain valid; Action method-group consumers must use a lambda or a matching Func.
- Added repeat-disposal, exception-preservation, and replacement-main ownership regressions. FwControllersModelTests now supplies DB dependencies at construction; FwUpdatesTests uses scoped in-memory settings instead of manual restoration and post-construction assignment.
- Updated direct and conditional NuGet references in both projects. Adapted optional Sentry logging to BreadcrumbLevel.Fatal and annotated the already-connected MySQL casts consistently with the other providers. The optional CSV import test now exercises synthetic file reads and early stopping. The cron cancellation test uses Assert.ThrowsAsync to accept both valid cancellation exception types and checks the original token. Its fake cancels after the first ProcessJobs call and the test asserts exactly one call, removing the wall-clock timer dependency.

## Contracts / compatibility

Ordinary getDB calls still create fresh wrappers; an explicit factory may reuse its own results within one FW. HTTP wrappers can still share a physical connection. Do not retain FW-owned wrappers beyond the FW lifetime or share factory results across FW lifetimes. Existing internal model registration is sufficient and unchanged.

Configuration remains ambient: use each FW inside its intended scope. Child work inherits mutable settings. Independently initialized scopes have separate settings and configuration caches. Environment variables, caller-owned provider mutations, application memory caches, DB schema caches, and SQL diagnostics remain outside this guarantee. Assembly-level test serialization remains.

The logger return-type change is an intentional source-compatibility change requested for this API. Canonical DB docs and the dated changelog show the Action-to-lambda migration; suppression/restoration does not change parameter redaction.

## Dependency verification

Queried stable versions from nuget.org for every reference, including conditional references, then confirmed no remaining direct updates with the NuGet CLI. There are 28 project references across 27 package IDs; 27 references were upgraded. Otp.NET 1.4.1 was already the newest stable release.

The all-feature evaluated graph includes 23 direct and 44 transitive packages in the app and 5 direct and 98 transitive packages in the test project. Normal, SQLite, and all-feature audits each reported no known direct/transitive vulnerabilities and no outdated direct packages. SQLite now resolves SQLitePCLRaw 2.1.12, clearing the prior 2.1.11 advisory.

Raw registry/version, evaluated-graph, vulnerability, and outdated reports are retained in ignored artifacts/assistant_testability/revision-packages. Audit commands use the same process-local DefineConstants as the corresponding restore; otherwise conditional references and project.assets.json do not agree.

## Commands used / verification

Baseline: the original feature commit was 8cd03d9c on codex/framework-testability, based on master d1c1490d. The worktree was clean before this follow-up.

- Before the disposal fix, three new regression tests failed with repeated-disposal/exception-masking failures. After the fix, the focused configuration/dependency/model/update/privacy set passed 36 tests.
- Full normal before the final test-only cleanup: `dotnet test osafw-tests/osafw-tests.csproj -p:NuGetAuditMode=all --filter 'FullyQualifiedName!~osafw.Tests.DBTests' --logger 'trx;LogFileName=normal-updated.trx' --results-directory artifacts/assistant_testability/revision-test-results/normal` — 713 passed.
- Full SQLite before the final test-only cleanup: `dotnet test osafw-tests/osafw-tests.csproj '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite' -p:NuGetAuditMode=all --filter 'FullyQualifiedName!~osafw.Tests.DBTests' --logger 'trx;LogFileName=sqlite-updated.trx' --results-directory artifacts/assistant_testability/revision-test-results/sqlite` — 724 passed.
- `dotnet build osafw-asp.net-core.sln --no-restore` and the same command with `'-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite'` — both passed with zero warnings/errors.
- All-feature restore/build and focused tests used `'-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite%3BisMySQL%3BisS3%3BisRoles%3BisWindowsAuth%3BisFwCronService%3BisSentry%3BisExcelDataReader'`. The final focused run passed 54 tests; its filter was `FullyQualifiedName~FwConfigScopeTests|FullyQualifiedName~FwDependencyTests|FullyQualifiedName~FwControllersModelTests|FullyQualifiedName~FwUpdatesTests|FullyQualifiedName~FwLoggerTests|FullyQualifiedName~SecurityGroup9ATests|FullyQualifiedName~SQLiteFwTests|FullyQualifiedName~SQLiteDBTests|FullyQualifiedName~FwCronTests|FullyQualifiedName~FwCronServiceTests|FullyQualifiedName~ImportSpreadsheet_UsesOptionalReaderOrReportsUnsupportedFeature`. The TRX is under revision-test-results/all-features-passed. A full all-feature recompilation exposes one existing CS0162 warning at unchanged Att.cs:713 when isS3 is enabled.
- `dotnet test osafw-tests/osafw-tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~FwDependencyTests' --collect 'XPlat Code Coverage' --results-directory artifacts/assistant_testability/revision-test-results/coverage` — 10 passed and Cobertura output produced, exercising the upgraded collector.
- For each graph, set process-local DefineConstants to the restored symbols and run `dotnet list osafw-asp.net-core.sln package --no-restore --include-transitive --vulnerable --format json` and `dotnet list osafw-asp.net-core.sln package --no-restore --outdated --format json` — no findings or query errors in the retained final reports.
- Strict UTF-8 without BOM/CRLF and git diff --check passed on the changed source, tests, project files, and canonical docs. Sandbox attempts that could not create MSBuild/NuGet temporary files were retried successfully outside the sandbox; no shared cache was cleared.

- Final test-only cleanup: `dotnet test osafw-tests/osafw-tests.csproj -p:NuGetAuditMode=all --filter 'FullyQualifiedName~FwCronServiceTests|FullyQualifiedName~FwCronTests' --logger 'trx;LogFileName=cron-normal.trx' --results-directory artifacts/assistant_testability/cron-followup/normal` — 4 passed. The same command with `'-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite'`, `cron-sqlite.trx`, and the cron-followup/sqlite results directory also passed 4 tests. Both rebuilt successfully without warnings. Full suites and all-feature checks above preceded this test-only cleanup and were not repeated; production code and package references are unchanged.

## Review

A fresh independent review with consumer-contract and state-integrity overlays inspected the bounded runtime/API/dependency diff and retained verification evidence. The mandatory supplemental summary/index audit found no factual or privacy issues. The implementing agent adjudicated no remaining findings: No blocking findings. Review loop can stop.

The subsequent localized cron-test cleanup received the eligible deliberate local second pass under the same review procedure, followed by a supplemental summary/index audit. The test exercises the real ExecuteAsync loop, cancels synchronously after its first fake processing call, verifies the same token, and disposes its owned service/token source. No production contract changed, so no additional changelog entry was needed. No blocking findings. Review loop can stop.

## Limits / cleanup

DBTests remains excluded because its setup connects to a configured SQL Server database and drops/creates a table. SQLite tests use uniquely named local files with pooling disabled and close connections before deleting only their own files and sidecars. No shared database tests or private application tests ran.

Optional integrations were compiled; live SQL Server/MySQL/OLE/ODBC behavior, S3, Sentry transport, Windows authentication, external AI calls, and browser execution were not exercised. SQLite does not prove other providers' SQL, locking, precision, mapping, or deployment behavior. No IIS, non-Windows, merge, or deployment validation is claimed.

Retained evidence is under ignored task artifacts. Removed only the three worktree-local images created by the existing upload tests and their empty parent directories after verifying each absolute target remained inside this worktree. Package/global caches are preserved.

## Reflection

The earlier independent review missed repeated disposal. The failing regression demonstrated both the visible scope exception and cleanup masking before the fix. Configuration isolation is useful to ordinary existing tests: explicit construction removes assignment/reset scaffolding without changing their behavioral assertions. Conditional package audits must use the same compile symbols as restore; a default evaluation of an all-feature graph fails rather than providing a valid audit.
