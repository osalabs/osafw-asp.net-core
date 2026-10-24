# Settings PR stack sync and configuration comments

Added a short format/purpose comment to each of the seven defaults in FwHooks.configure for PR #299. Comments distinguish dictionary membership from value semantics and explain routing, XSS/CSRF exemptions, signup, API origin, and timezone defaults.

Merged master 5371f1dc into codex/settings-1-config, then merged each updated parent into the next branch through codex/settings-4-db-secrets. All four PR bases remain unchanged. No history rewrite, deployment, configured database mutation, or primary-checkout source edit was needed.

## Merge resolution

- FW renderer: retained upstream request-local page-state/error copies and both parser overloads; Settings globals load in their shared renderPage entry; sensitive form errors still use formErrorsForLog.
- Renderer tests: registered an isolated Settings stub for the newly combined runtime dependency, retaining actual Settings default selection and renderer assertions.
- Package references: retained master's dependency upgrades and the existing Secrets Manager reference.
- Task index: retained both sets of entries. Historical summaries were not edited.

## Verification

All commands use dotnet test osafw-tests/osafw-tests.csproj with isolated absolute OutDir values under artifacts/assistant_settings_sync/tests/; logs/TRX remain ignored under artifacts/assistant_settings_sync/.

- PR #299, filter FullyQualifiedName~FwConfigTests: 7 passed.
- PR #300, SQLite, filter FwParserIsolationTests|ParsePageFileCacheTests|FwConfigTests|FwTests|AdminSettings (each term prefixed FullyQualifiedName~): 90 passed.
- Final stack with SDK 10.0.300 and TRACE;DEBUG;isSQLite: 165 passed, zero failed/skipped. Filter names: FwDbConnectionsTests, SettingsStoreTests, FwTests, FwEmailTests, AdminSettings, FwSettingsMigrationTests, FwSettingsProtectionTests, FwDatabaseInitTests, FwParserIsolationTests, ParsePageFileCacheTests, FwConfigTests, FwCacheTests, FwVueControllerTests, joined using FullyQualifiedName~ and |. Result: focused-sdk10.trx.
- Final optional build with SDK 10.0.300 and TRACE;DEBUG;isSQLite;isRoles;isSentry;isS3, filter FullyQualifiedName~AdminSettings|FullyQualifiedName~FwDbConnectionsTests: 47 passed. Existing Att.cs unreachable-code warning remains.
- The initial broad SQLite run used the host-selected preview SDK 11.0.100-rc.2.26504.105: 1003 passed, one failed, zero skipped (sync.trx). Filter excluded osafw.Tests.DBTests and OptionalValidationBrowserTests. The decimal assertion in FwCacheTests.getRequestValueTest also failed in the earlier PR #299 build, and both cache source/test files match master. The same test passes with SDK 10.0.300; no unrelated cache change was made. The entire broad suite was not repeated with SDK 10.
- The renderer manual-login fixture fails when compiled with optional Roles because it lacks a Users/RBAC fixture. Default/SQLite renderer coverage and separate optional Admin/RBAC tests passed. No production authorization change was made to accommodate that fixture.

The SDK selector is an ignored task-local global.json, not repository or machine configuration. Live SQL Server/MySQL/AWS/SMTP and downstream copied-app acceptance were not performed. Primary master retains its pre-existing unrelated untracked items.

A fresh independent reviewer used consumer-contract and security-boundary overlays for the bounded merge resolutions and comments; the initial verdict found no blocking issues. Final publication also checks branch ancestry, scoped diffs, and UTF-8/no-BOM/CRLF formatting.
