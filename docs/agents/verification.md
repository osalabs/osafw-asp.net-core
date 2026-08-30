# Verification and Work Isolation

Use the smallest check that can falsify the touched behavior, then add checks in proportion to compatibility, security, persistence, deployment, and downstream risk. Record exact commands/results and material omissions in the active summary or final response.

## Resource ownership and concurrency

| Resource | Isolation / ownership rule | Cleanup rule |
| --- | --- | --- |
| Source files | A worktree isolates branches, not simultaneous edits inside one worktree. Give each concurrent editor explicit file ownership; keep shared-contract integration with one agent. | Revert nothing you did not create. Remove only task-owned disposable files. |
| Git worktrees | Use a separate worktree for a long independent branch, risky experiment, or parallel implementation whose source/build state must differ. Skip it for short sequential work or when a shared database/external dependency remains the real bottleneck. | Confirm the worktree path, branch, status, and ownership before removal. Do not remove a dirty or user-owned worktree. |
| `bin` / `obj` / build output | Each worktree has its own ordinary output, but Visual Studio/IIS can lock it. Use a unique absolute `OutDir` under `artifacts/assistant_<task>/` when needed. Never set `BaseIntermediateOutputPath` for `osafw-app`; its compile glob can include generated intermediates. | Delete only the exact ignored task output after relevant processes stop, or retain it and report the path. |
| NuGet/global caches | Normal concurrent restore against the shared read-mostly cache is acceptable. Do not clear/repair shared caches while other work may use them. Use a task-specific `NUGET_PACKAGES` path only when cache isolation is the test. | Remove only the task-specific cache. Never clean the global cache as routine closeout. |
| Test results | Give each run a unique ignored results directory under `artifacts/assistant_<task>/test-results/`. Do not introduce a shared tracked results folder. | Retain when useful and report it, or delete only that run's directory. |
| Kestrel/IIS ports and processes | Allocate a unique port/app identity per concurrent run. IIS/IIS Express and Visual Studio may share locks or configuration; do not assume process ownership from a name alone. | Record process IDs you start and stop only those exact processes. Never kill all `dotnet`, IIS, or browser processes. |
| Visual Studio state | Treat solution state, IIS Express config, debugger sessions, and file locks as user-owned. Prefer command-line checks with isolated output. | Do not close the IDE or modify its user settings unless explicitly requested. |
| SQL Server | This is the production-primary behavior/schema source. For isolated integration work, use a uniquely named disposable database only with explicit permission to create it; shared configured development databases require explicit approval before mutation. | Drop only an exact task-created database after verifying server, database name, and active connections. Never run fresh-schema/drop scripts against an existing database. |
| SQLite | Prefer a unique database file per worktree/task for disposable provider-neutral tests or optional embedded-app checks. It does not substitute for SQL Server-specific SQL, locking, types, or deployment behavior. | Close connections, then delete only the exact task-owned file and sidecars (`-wal`, `-shm`) after resolving their paths. |
| MySQL / OLE / ODBC / Access | MySQL is optional and parity is not assumed. Use a unique schema for targeted MySQL checks. Treat Access/OLE/ODBC sources as read/import-oriented unless the task proves a write contract. | Remove only task-created schemas/files with explicit authority; never modify an original import file for a read-path test. |
| Schema updates/migrations | Fresh schemas are destructive initialization inputs; updates are additive and sequential. Isolate provider/database and test the exact discovery/order path. | Preserve scripts as evidence; clean only the disposable database they were applied to. |
| `appsettings`, environment variables, user secrets | Tracked configuration is a value-free contract. Local values belong in ignored local files, environment variables, or user-secrets. User-secrets can be shared by all worktrees for one project ID, so prefer per-process environment overrides for concurrent runs. | Restore only values changed by the task; never print secrets. |
| Uploads/logs/browser state | Use worktree-local ignored upload/log paths and a task-specific browser context/profile when possible. A signed-in personal browser session is not disposable test state. | Delete only task-created uploads/logs/profile data; do not clear user browser state. |
| Queues/background services | Disable when irrelevant or give workers/queues a task-specific namespace. Prevent two instances from consuming the same real queue or running the same cron work. | Stop only task-started workers and reconcile task-created messages/jobs. |
| Containers | Use unique project/container/network/volume names and host ports. Containers do not make external databases/accounts isolated automatically. | Verify labels/names and remove only task-created resources. |
| External accounts/services | Prefer a sandbox tenant/account and unique object IDs. External writes, messages, deployment, billing, or destructive cleanup require request authority. | Delete/revert only exact task-created objects when authorized; otherwise report them for human cleanup. |

Windows with IIS is the primary real-application development/deployment path. Linux/container support is optional and must not be claimed from a successful Windows-only check. Conversely, optional-platform work must not weaken Windows/IIS behavior or DPAPI/security defaults.

## Verification entrypoints by change type

| Change surface | Minimum falsifying evidence | Add when risk warrants |
| --- | --- | --- |
| Agent/docs-only | Validate links/routes, strict UTF-8/CRLF, summary index entry, and representative prompt routing. Check commands/examples against real paths. | Integrating review plus the `agent-workflow` overlay for shared workflow changes; follow the capability-conditional execution in `review-routing.md`. |
| C# implementation | Focused compile/test at the nearest public controller/action/model/helper boundary. | `dotnet build osafw-app/osafw-app.csproj`, affected test class, then solution/full tests for shared code. |
| Public/source-copy contract | Search call sites and overrides; test old and new behavior or a compatibility shim; inspect generated/template/config consumers. | Clean-output solution build, copied-app upgrade thought experiment/diff, migration note, and `docs/CHANGELOG.md`. This repository distributes source and produces no framework NuGet package, so package-compatibility checks do not apply. |
| Route/template/frontend/email | Render or exercise the affected public route and verify page-state/JSON/HTML/include selectors and important empty/error cases. | Browser/manual cross-flow check, asset build/load behavior, downstream template override search, and compatibility note. |
| Scaffold/generated output | Run the built-in CLI against an approved disposable table/database and compare generated model/controller/config/templates with canonical examples. | Repeat for an affected provider and verify regeneration does not overwrite intentional app customizations. |
| SQL/schema/provider | Compile the provider path, test fresh schema only on a new disposable database, then apply additive updates sequentially to a suitable prior schema. Verify runtime update discovery/order. | SQL Server integration for production behavior; focused SQLite `-p:DefineConstants=isSQLite`; MySQL only when explicitly in scope. Verify transactions, concurrency, null/default/type/date behavior. |
| Authentication/security/privacy | Exercise positive and negative authorization at the actual row/render/serve boundary; verify POST/XSS-token, redirect, sanitization, attachment, redaction, and environment gates as triggered. | Threat-focused review, production-mode response check, IIS header/config check, and provider-backed tests. Never rely only on a happy-path unit test. |
| Project/package/config symbols | Restore/build with the exact symbol/config combination and inspect conditional references/output. | Clean-output solution build plus startup/smoke check with value-free config contract. |
| Deployment/IIS/script | Use script check/dry-run modes first and inspect resolved paths/branch/environment. | Publish to a task-owned target, validate IIS configuration/header/runtime paths, and document rollback. Deploy only when explicitly requested. |
| Documentation/example/release | Verify every edited path, command, API, config key, sample shape, and link against the current tree. | Run sample commands, perform copied-app upgrade walkthrough, and check dated changelog/migration coverage. This repository has no GitHub Actions gate; report only checks that exist. |

## Common commands

Choose only commands that can falsify the touched behavior:

```powershell
dotnet build osafw-app/osafw-app.csproj
dotnet build osafw-asp.net-core.sln
dotnet test osafw-tests/osafw-tests.csproj
dotnet test osafw-tests/osafw-tests.csproj -p:DefineConstants=isSQLite
```

If ordinary output is locked, use an absolute task-owned output path, for example:

```powershell
$taskOut = Join-Path (Resolve-Path .).Path 'artifacts\assistant_<task>\build\'
dotnet build osafw-app/osafw-app.csproj -p:OutDir=$taskOut
```

Do not convert automation gaps into invented evidence. When a check is unavailable or disproportionate, record the prerequisite and a concise manual test that would close the risk.
