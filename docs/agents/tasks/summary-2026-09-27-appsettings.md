# Appsettings and Site Settings refactor

Implemented the accepted design: configuration ownership and early hooks; encrypted, cached Settings persistence; Admin UI/schema/migration and transfer; optional cached AWS Secrets Manager resolution for named DBs. The developer subsequently authorized committing, pushing, and opening four stacked PRs. Publication is prepared in an isolated worktree; the original checkout and unrelated work remain preserved. No deployment or configured database mutation was performed.

## Contracts and migration

- Bootstrap/deployment values remain in JSON. FwHooks owns application policy and DB-independent locale defaults. Operational readers use Settings.
- Settings basis distinguishes inheritance from explicit empty values. Row access floors supplement existing RBAC; credentials require Site Admin. Generic secret projections and inherited exports are blocked.
- Windows DPAPI wraps the durable Data Protection key ring. Key-store errors fail closed, writes use the correct predicate, and old keys are retained. Branding is separate from the stable protection identity.
- Full Site Admin JSON Export includes plaintext credentials. Import preserves destination metadata and re-encrypts values atomically. A planned host move requires an old-host export, database backup, deliberate destination key initialization, local sign-in, and Import.
- Additive provider schema updates precede explicit legacy JSON/plaintext credential migration. Fresh installation uses provider settings.sql. A guarded development-only database-init command initializes an empty database before web key/session startup.
- Secrets Manager is optional; full connection-string secrets are fetched once per process/descriptor. Restart to refresh. Inline named connection strings remain supported.

Canonical contracts and upgrade steps are in docs/settings.md, docs/deploy.md, docs/db.md, and the 2026-09-27 CHANGELOG entry.

## Verification and limits

Broad regression commands used `dotnet test osafw-tests/osafw-tests.csproj --filter 'FullyQualifiedName!~osafw.Tests.DBTests&FullyQualifiedName!~OptionalValidationBrowserTests' --verbosity minimal`, with isolated absolute OutDir values under `artifacts/assistant_appsettings/tests/` and TRX files under `artifacts/assistant_appsettings/test-results/`:

- Default compile: 840 passed, 84 skipped, zero failed (`default-final.trx`).
- `-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite`: 870 passed, 84 skipped, zero failed (`sqlite-final.trx`).
- Focused Admin Settings with SQLite + Roles: 40/40 passed, including real role grants/denials and Reveal through the dispatcher/parser.
- Focused Admin Settings with Sentry + S3 + Roles: 33/33 passed. The existing S3-enabled Att.cs unreachable-code warning remains.
- Actual Windows DPAPI key persistence/recreated-provider decryption, different-provider export/import re-encryption, named-secret client coalescing/retry, real empty-database initialization/rerun refusal, and legacy migration are covered by these checks.

The broad runs preceded the final reviewed boundary fixes. Final combined post-review SQLite checks passed 93/93, zero skipped/failed (`post-review.trx`), including SMTP authorization and loopback delivery, Reveal dispatch/headers, nested-config secret projection, and real mid-batch rollback. The dedicated SMTP suite passed 15/15 (`email-final/email.trx`). Configured SQL Server DBTests are excluded because they mutate an external configured database. The excluded OptionalValidationBrowserTests and 84 skipped browser/PDF cases require a matching Playwright browser installation. SQLite fixtures own and remove their temporary files. AWS checks use fake clients; no live API credentials are used.

The final combined command was:

```powershell
dotnet test osafw-tests/osafw-tests.csproj --filter 'FullyQualifiedName~SettingsStoreTests|FullyQualifiedName~FwTests|FullyQualifiedName~FwEmailTests|FullyQualifiedName~AdminSettings|FullyQualifiedName~FwSettingsMigrationTests|FullyQualifiedName~FwSettingsProtectionTests|FullyQualifiedName~FwDatabaseInitTests' --verbosity minimal '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite' -p:OutDir=<checkout>/artifacts/assistant_appsettings/tests/final/ --logger 'trx;LogFileName=post-review.trx' --results-directory artifacts/assistant_appsettings/test-results
```

Use the checkout's absolute path in place of `<checkout>`. The Admin Settings variant filter is `FullyQualifiedName~AdminSettings`; the additional symbol sets are `TRACE%3BDEBUG%3BisSQLite%3BisRoles` and `TRACE%3BDEBUG%3BisSentry%3BisS3%3BisRoles`. Final `git diff --check` and strict UTF-8/no-BOM/CRLF checks passed for 88 task files.
Actual SQL Server/MySQL server execution, a full IIS host move, live SMTP/OAuth/AWS delivery, and downstream copied-application acceptance remain unverified.

## Work distribution and review

Three bounded implementation packets had exclusive ownership: configuration/runtime consumers; named DB resolution and maintenance helpers; schema/Admin Settings. The primary owns persistence/key integration, final verification and adjudication. Shared obj directories require serialized builds; isolated OutDir avoids the user-owned IIS Express process.

A fresh history-isolated reviewer used the security-boundary and state-integrity overlays for interacting authorization, encryption/key lifecycle and migration/restore contracts. Initial review required changes: restrict the SMTP diagnostic tool's credential boundary, make SQL Server's new-column backfill batch-safe, and retain Reveal's no-store policy through rendering. The SMTP tool is now Site Admin-only with POST/XSS/read-only enforcement and no password reflection; the SQL Server backfill uses EXEC inside the first-upgrade conditional; Reveal retains no-store through the parser. Real-dispatch coverage also corrected Reveal's `_json` response shape. The reviewer confirmed all initial findings resolved in code with no new blocking candidates. Post-fix checks above passed; the final independent adjudication is recorded in the task conversation. The active summary was supplied for the required consistency/privacy audit.

Unrelated untracked work was preserved.

## PR stack preparation

The publication branches are `codex/settings-1-config`, `codex/settings-2-foundation`, `codex/settings-3-admin-transfer`, and `codex/settings-4-db-secrets`, based successively on master and the preceding branch. Review and merge in that order. The first PR isolates the shared defaults/hooks. The second deliberately keeps schema, encryption, existing credential readers/writers, safe individual editing, operational migration, and bootstrap together. The third adds grouped admin controls and full JSON transfer. The fourth adds optional named-DB Secrets Manager resolution. This avoids unsafe intermediate credential readers or premature operational-JSON removal.

Additional checks at the isolated intermediate tips:

- PR 1: FwConfigTests, 7 passed.
- PR 2: SQLite + Roles focused Settings/runtime/email/migration/key checks, 97 passed. Broad SQLite regression, excluding the same configured-DB and unavailable-browser tests as above: 863 passed, 84 skipped, zero failed.
- PR 3: AdminSettings and SettingsStoreTests with SQLite + Roles, 46 passed.
- PR 4 / final stack: named-DB resolver, DevConfigure, Settings/runtime/email/migration/key and Admin checks with SQLite + Roles, 125 passed, zero skipped or failed.

These use the same `dotnet test` invocation pattern and four-level isolated OutDir paths above. The final stack is checked against the reviewed working files; only this publication record and removal of extra blank lines at nine SQL/template file endings differ from the prior reviewed output. The original broad and post-review evidence still applies to the combined implementation; intermediate-tip checks validate the dependency split.

A separate history-isolated reviewer checked the intermediate PR boundaries using the consumer-contract and security-boundary overlays and audited this publication record. No blocking findings remained. Until PR 3 adds the grouped editor, a fresh static AWS pair requires the migration command or trusted writeBatch API in PR 2. All 88 final task files match the preserved original checkout and pass strict UTF-8/no-BOM/CRLF checks; the full stack passes git diff --check.
