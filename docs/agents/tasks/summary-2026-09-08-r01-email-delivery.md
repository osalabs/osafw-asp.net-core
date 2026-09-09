# R01 email delivery result and test recipient

## Objective / acceptance

Make `FW.sendEmail()` report failures for missing final recipients and unusable SMTP host/port configuration, clear stale delivery errors, and keep per-send SMTP overrides isolated from shared configuration. Add an administrator-editable, clearable `test_email` Site Setting with deterministic Site Setting/configuration/current-user precedence while preserving test-mode To redirection and CC/BCC suppression. Keep all verification local and avoid external databases and mail services.

## What changed

- Email sends now clear `last_error_send_email` at the start, reject an empty final recipient, validate SMTP host and port, and send with a deep copy of shared mail configuration before applying overrides.
- Test-recipient resolution trims the database Site Setting first, falls back to trimmed application configuration, and resolves an empty value or `current_user` to the logged-in session email. Database read failures remain failures instead of being silently treated as missing rows.
- The admin settings flow permits a blank value specifically for `test_email`, while other text settings retain required-value validation.
- Fresh schemas and idempotent provider updates seed the editable blank setting with an admin hint for `current_user`.

## Scope reviewed

Reviewed the public email helper and its admin/report/user callers, test-mode recipient display, application mail defaults, Site Settings model/controller/templates, provider schema and update discovery paths, existing email precedence history, database bootstrap diagnostics, and the framework test-isolation helpers.

## Requirements / decisions

- A recipient is considered present only after test-mode routing and address parsing. A missing logged-in email therefore fails when the selected sink is blank or `current_user`.
- SMTP requires a non-empty host and a port from 1 through 65535. Username/password remain optional under the existing transport behavior.
- A database outage is not equivalent to a missing `settings` row. The normal exception path makes the send fail and records the error instead of hiding an operational failure behind the application-config fallback.
- The established test-mode body diagnostic and original CC/BCC delivery protections remain intact.

## Changed contracts

- `FW.sendEmail()` no longer returns true when no final recipient or usable SMTP host/port exists.
- `FW.resolveTestEmailRecipient()` now reads `settings.test_email` before `appSettings.test_email`; `current_user` explicitly selects the logged-in user's email.
- `settings.test_email` is a seeded, editable and clearable public configuration key for SQL Server, MySQL and SQLite installations.

## Commands used / verification

- `dotnet test osafw-tests\osafw-tests.csproj --no-restore --filter "FullyQualifiedName~FwEmailTests|FullyQualifiedName~FwTests.ResolveTestEmailRecipient|FullyQualifiedName~AdminSettingsControllerTests" -p:OutDir=...\artifacts\assistant_email_tests\focused\ --logger "console;verbosity=minimal"` - passed 28 tests.
- `dotnet test osafw-tests\osafw-tests.csproj -p:DefineConstants=isSQLite --filter "FullyQualifiedName~SQLiteDBTests.SQLiteSchemaScripts_CreateFreshFrameworkDatabase|FullyQualifiedName~SQLiteDBTests.TestEmailUpdate_IsIdempotentAndPreservesExistingValue" -p:OutDir=...\artifacts\assistant_email_tests\sqlite\ --logger "console;verbosity=minimal"` - passed 2 tests using disposable SQLite files.
- `dotnet test osafw-tests\osafw-tests.csproj --no-restore --filter "FullyQualifiedName!~osafw.Tests.DBTests" -p:OutDir=...\artifacts\assistant_email_tests\full-default\ --logger "console;verbosity=minimal"` - passed 724 tests before the final added missing-test-recipient case; the final focused run covers that case.
- `git diff --check` and the touched-file UTF-8/CRLF scan - passed after final normalization.

The focused SMTP tests use a task-owned in-process loopback stub. They cover invalid input without transport, controlled transport failure, successful delivery, stale-error clearing, and test-mode envelope/header controls without contacting an external mail service.

## Testing instructions

Run the default focused command above. Enable `isSQLite` and run the two named SQLite tests to replay the fresh schema and apply the update twice against disposable databases.

## Risks / follow-ups

SQL Server and MySQL fresh-schema/update text was inspected but not executed because no task-owned instances were authorized. Actual TLS/authentication behavior remains dependent on deployment SMTP servers and was not exercised. The primary integrator owns independent consumer-contract/state-integrity review and final branch integration.

## Reflection

The main contract edge was distinguishing a missing Site Setting from an unavailable settings database. A useful packet-level instruction for future settings-backed runtime work would explicitly state whether operational database failures may fall back to application configuration; absent that authority, preserving the failure is safer and observable.
