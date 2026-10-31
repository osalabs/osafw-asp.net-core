# PR 300 cleanup and whole-change review

## Outcome and scope

Follow-up review of PR #300 as a whole against its PR #299 parent, plus scoped simplifications requested after the previous feedback pass. Work uses the existing stack worktree and leaves primary master/unrelated files alone. Database-backed sessions/keys, database-free Home/DevConfigure, strict durable credential writes, simple development InitDB, and ordinary local log_pii/DEBUG diagnostics remain the accepted contracts.

- Replaced AdminSettings action-name allowlisting with ordinary virtual overrides that deny inherited mutation/search/navigation actions and generic export. Custom action RBAC mappings are set in init; ordinary base authorization remains active.
- Removed the redundant display-mask branch, duplicate display calculation, thin metadata/filter wrappers, and duplicate input normalization. Saving still calls the public overridable Validate hook; model writes independently enforce metadata constraints.
- Removed braces around simple single-statement blocks in changed PR code, retaining braces for multiple statements, required syntax, or scope. Recorded the preferred style in docs/naming.md.
- Removed redundant default-URL metadata UPDATE statements from all three unapplied provider update scripts. INSERT definitions already contain local_url metadata. Necessary conversion of existing rows' basis/access/mask is retained.
- Removed an unused request-item marker and FW argument from Settings.markSensitiveRequest; Sentry scope tagging remains. Updated actual callers and migration/API documentation for this unreleased helper.
- API authentication now returns early for an already signed-in session, avoiding an unnecessary API_KEY lookup/decryption. Anonymous API-key and origin checks remain. S3 reads its credential-source default through FwConfig/FwHooks. README no longer claims that database tables are required before opening bootstrap pages.
- Fixed an observed SMTP test-fixture startup race by beginning its asynchronous accept directly instead of scheduling it through Task.Run. Denied requests can dispose the listener immediately without racing an unstarted accept task.

Reviewed the complete PR runtime/configuration/schema/template changes and the affected tests/documentation; retained public Settings read aliases and the existing cache/protection boundaries rather than adding a replacement abstraction. No configured database, external mail service, or AWS resource was modified.

## Verification

Windows, SDK 10.0.300. All output and disposable database evidence are task-owned/ignored.

- Focused SQLite/Roles checks passed 95/95, covering inherited-action denial through the base virtual API, normal editor values and masking, the public validation hook, RBAC, storage/protection, fresh/additive SQLite schema, and DevCli.
- The full default suite passed 1,043/1,043 after correcting the SMTP fixture startup race observed in the initial run.
- The final focused SQLite/Roles/Sentry/S3 run passed 114/114. Its first run exposed four API unit tests using the real RBAC model without a configured database; a local Users stub now isolates the authorization tests. A subsequent default-build API check passed 4/4 after that fixture-only change.
- The #301 merge passed 72/72 focused SQLite/Roles Settings checks, including grouped editing and transfer authorization.
- The combined #302 stack passed 138/138 focused tests with SQLite, Roles, Sentry and S3 enabled, including key-repository and database-secret resolver tests. The normal application build passed with zero warnings/errors. The optional S3 build retains the existing unreachable-code warning in Att.cs.
- Repository instruction validation, diff whitespace checks, and strict UTF-8 without BOM/CRLF checks passed.
- No live SQL Server/MySQL initialization, IIS deployment, live AWS or external SMTP acceptance is claimed. Existing SQLite coverage verifies the actual revised update script, preserving old rows and creating new URL definitions with their metadata.

```powershell
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --verbosity minimal "-p:OutDir=$taskRoot/artifacts/assistant_settings_cleanup/tests/default/" --logger 'trx;LogFileName=default-final.trx' --results-directory "$taskRoot/artifacts/assistant_settings_cleanup/test-results"
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --filter 'FullyQualifiedName~AdminSettingsControllerTests|FullyQualifiedName~AdminSettingsRbacTests|FullyQualifiedName~SettingsStoreTests|FullyQualifiedName~SettingsProtectionTests|FullyQualifiedName~SQLiteDBTests|FullyQualifiedName~DevCliTests|FullyQualifiedName~FwEmailTests|FullyQualifiedName~FwApiControllerTests' --verbosity minimal '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite%3BisRoles%3BisSentry%3BisS3' "-p:OutDir=$taskRoot/artifacts/assistant_settings_cleanup/tests/optional/" --logger 'trx;LogFileName=optional-final.trx' --results-directory "$taskRoot/artifacts/assistant_settings_cleanup/test-results"
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --filter 'FullyQualifiedName~FwApiControllerTests' --verbosity minimal "-p:OutDir=$taskRoot/artifacts/assistant_settings_cleanup/tests/default/" --logger 'trx;LogFileName=default-api-final.trx' --results-directory "$taskRoot/artifacts/assistant_settings_cleanup/test-results"
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --filter 'FullyQualifiedName~AdminSettings|FullyQualifiedName~SettingsStoreTests|FullyQualifiedName~SettingsProtectionTests' --verbosity minimal '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite%3BisRoles' "-p:OutDir=$taskRoot/artifacts/assistant_settings_cleanup/tests/transfer/" --logger 'trx;LogFileName=transfer.trx' --results-directory "$taskRoot/artifacts/assistant_settings_cleanup/test-results"
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --filter 'FullyQualifiedName~AdminSettings|FullyQualifiedName~SettingsStoreTests|FullyQualifiedName~SettingsProtectionTests|FullyQualifiedName~SQLiteDBTests|FullyQualifiedName~DevCliTests|FullyQualifiedName~FwEmailTests|FullyQualifiedName~FwApiControllerTests|FullyQualifiedName~FwDbConnectionsTests|FullyQualifiedName~FwKeys' --verbosity minimal '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite%3BisRoles%3BisSentry%3BisS3' "-p:OutDir=$taskRoot/artifacts/assistant_settings_cleanup/tests/stack/" --logger 'trx;LogFileName=stack.trx' --results-directory "$taskRoot/artifacts/assistant_settings_cleanup/test-results"
dotnet build "$taskRoot/osafw-app/osafw-app.csproj" --verbosity minimal "-p:OutDir=$taskRoot/artifacts/assistant_settings_cleanup/build/stack/"
pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1
git diff --cached --check
```

## Independent review

A fresh reviewer_high independently reviewed the complete PR with security-boundary and state-integrity criteria. Its initial verdict found no blocking runtime/SQL issue; the stale README statement and duplicate S3 default observations were addressed. Final code follow-up found no additional issues. Active-summary audit passed with no supplemental findings; the reviewer checked the recorded final test logs. Bounded review of both stacked merge resolutions also found no blocking findings: grouped actions and permission mappings remain in #301, and #302 still resolves the database definition before constructing its key repository. Final audit of the added stack evidence passed with no supplemental findings; the review loop is complete.
