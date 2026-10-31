# Settings simplification after PR review

## Outcome and contracts

PR #299 remains unchanged. PR #300 consolidates Settings persistence, encryption and transfer primitives into the existing Settings model; Program dispatches through DevCli, which owns guarded empty-development-database initialization. Permanent migration classes, commands, browser actions, plaintext migration routines, and schema-upgrade bypasses are removed. Existing applications must complete an app-specific offline schema/value migration before deploying this runtime.

Settings.read(code), typed readers and readSecret inherit FwConfig defaults when a row is absent or inherited; explicit empty values remain explicit. Two-argument reader overrides still participate. FwConfig owns operational defaults and presentation-config filtering; application defaults remain in FwHooks. Partial nested deployment configuration preserves unspecified defaults. Credential classification uses input metadata only, including custom codes.

Controller-level Site Admin access replaces redundant AdminSendEmail action guards. AdminSettings retains numeric row floors and credential restrictions; explicit POST/XSS checks remain because explicit action routes can reach Save through GET. No configured development database was changed.

Added a short offline migration map and a new-application guide, registered in instruction pack 1.13.3. Explicit app preparation requests trigger scoped intake/configuration; repository heuristics alone do not authorize database changes or cleanup.

## Verification and review

Used SDK 10.0.300 selected by a task-owned ignored global.json. From that selector directory, with $taskRoot pointing at the worktree:

```powershell
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --filter 'FullyQualifiedName!~osafw.Tests.DBTests&FullyQualifiedName!~BrowserTests&FullyQualifiedName!~ReportRenderTests&FullyQualifiedName!~Pdf' --verbosity minimal '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite' "-p:OutDir=$taskRoot/artifacts/assistant_settings_lean/tests/foundation/" --logger 'trx;LogFileName=lean-foundation-final.trx' --results-directory "$taskRoot/artifacts/assistant_settings_lean/test-results"
pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1
git diff --check HEAD
```

- Foundation regression: passed 899, failed 0, skipped 0. Configured-DB, browser, report-rendering and PDF tests were excluded; database checks use disposable SQLite. Includes actual DevCli fresh-database/rerun refusal, protected storage, custom credential metadata, config defaults/explicit empties, authorization, redirects, MFA and loopback SMTP coverage.
- Instruction validation passed after staging both new guides, including inventory, links and text encoding.
- Initial failures came from Settings test doubles that lacked FW initialization or a mocked row lookup; fixtures now follow initialized-model behavior.
- Bounded implementation delegation supplied DevCli/bootstrap changes and docs; the primary integrated model/config/controller changes and owns stack integration.
- Fresh independent reviewer_astra_high review used security-boundary and agent-workflow overlays for credential/authorization judgment and copied-app guidance. Initial review found partial nested mail config discarded default port/TLS values; fixed centrally and covered by both omitted and explicit override cases. The independent finding recheck and active-summary audit found no blocking findings.

## Admin transfer integration

PR #301 retains grouped SMTP/AWS editing and Site Admin Export/Import while removing the migration action/button. Restored the Site Admin helper needed by these actions on the level-90 controller. Transfer documentation now has one server-move procedure. The protection-prefix test uses the consolidated Settings API.

```powershell
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --filter 'FullyQualifiedName~AdminSettingsControllerTests|FullyQualifiedName~SettingsStoreTests|FullyQualifiedName~SettingsProtectionTests|FullyQualifiedName~FwEmailTests' --verbosity minimal '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite%3BisRoles%3BisSentry%3BisS3' "-p:OutDir=$taskRoot/artifacts/assistant_settings_lean/tests/admin/" --logger 'trx;LogFileName=lean-admin-focused.trx' --results-directory "$taskRoot/artifacts/assistant_settings_lean/test-results"
```

Passed 60, failed 0, skipped 0. An initial broader optional-symbol selection also included SecurityGroup9BTests: 75 passed, one failed because its AdminUsers fixture attempts an unmocked UsersRoles database operation with isRoles enabled. That class passed the default-symbol foundation run. No production test seam or unrelated fixture change was added; the recorded final optional run targets the Settings/admin/mail boundaries. Optional coverage does not claim the whole security suite passes with isRoles.

## Final stack verification

PR #302 now uses Settings.configureProtection for durable keys, preserving the existing shared FwDbConnections resolver used by startup and named runtime databases. PR #301's bounded integration review found no blocking findings and its summary audit passed. PR #302's bounded review used security-boundary and state-integrity overlays; its final evidence/summary audit found no blocking findings.

```powershell
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --filter 'FullyQualifiedName!~osafw.Tests.DBTests&FullyQualifiedName!~BrowserTests&FullyQualifiedName!~ReportRenderTests&FullyQualifiedName!~Pdf' --verbosity minimal '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite' "-p:OutDir=$taskRoot/artifacts/assistant_settings_lean/tests/final/" --logger 'trx;LogFileName=lean-final.trx' --results-directory "$taskRoot/artifacts/assistant_settings_lean/test-results"
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --filter 'FullyQualifiedName~AdminSettingsControllerTests|FullyQualifiedName~SettingsStoreTests|FullyQualifiedName~SettingsProtectionTests|FullyQualifiedName~FwEmailTests|FullyQualifiedName~FwDbConnectionsTests|FullyQualifiedName~FwConfigTests|FullyQualifiedName~DevCliTests' --verbosity minimal '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite%3BisRoles%3BisSentry%3BisS3' "-p:OutDir=$taskRoot/artifacts/assistant_settings_lean/tests/optional/" --logger 'trx;LogFileName=lean-optional-final.trx' --results-directory "$taskRoot/artifacts/assistant_settings_lean/test-results"
dotnet build "$taskRoot/osafw-app/osafw-app.csproj" --verbosity minimal "-p:OutDir=$taskRoot/artifacts/assistant_settings_lean/build/default/"
```

- Final broad SQLite regression: 915 passed, 0 failed, 0 skipped within the stated filter.
- Final affected optional-symbol tests: 97 passed, 0 failed, 0 skipped. One pre-existing S3 unreachable-code warning in Att.cs was also present in the archived baseline build.
- Default application build: 0 warnings, 0 errors.
- Reproduced the optional AdminUsers fixture failure against an untouched git archive of pre-refactor PR #301 commit 50c1b442. The exact single-test filter was FullyQualifiedName~SecurityGroup9BTests.AdminUsers_LowerAdminCanSaveLowerUserBelowOwnAccess with the same SQLite/Roles/Sentry/S3 symbols: 1 failed with the same unknown-DB-type stack at UsersRoles.updateJunctionByMainId. Evidence is retained in ignored lean-baseline-role-fixture.trx/log. This proves the failure predates the refactor; it is outside the Settings changes.
- Main checkout and approved PR #299 remain unchanged. The existing four-PR order is retained; no PR is merged.

## Verification limits

Live SQL Server/MySQL execution, external SMTP/AWS and copied-app deployment acceptance are not established by disposable SQLite tests. No live application or configured database migration was performed.
