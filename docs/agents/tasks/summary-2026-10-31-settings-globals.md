# Settings ownership and explicit template globals

## Outcome

Implemented the accepted separation on PR #300: shared/bootstrap defaults remain in FwConfig, application policy in FwHooks, integration credentials resolve only from Settings, and SMTP/AWS-mode fallbacks live at their consumers. FwConfig has no credential defaults, credential-name filter, or templateSettings method. FW initializes only explicitly selected presentation globals; no database lookup is needed to choose that set.

Credential classification remains generic input metadata. Missing or inherited credentials mean unconfigured, including through generic readers for credential rows; configuration/caller defaults are ignored. Explicit credentials decrypt at the consumer. Database and decryption errors still propagate. Credential forms offer Keep/Replace/Clear, inherited credentials display Not configured, and new credential INSERTs use basis=1 in all three provider scripts. No follow-up repair UPDATE or runtime migration was added.

The existing process-local ciphertext cache, strict durable credential protection, DB-backed sessions/keys, and database-free defaults remain. Email template parsing now loads the same optional display overrides as web rendering. No configured development database or external service was modified.

## Consumer inventory and initial-goal review

The scan covered every file under App_Data/template (including Markdown, selector and email files), GLOBAL[...] in attributes and text, the global tag attribute, code-side G accesses, and Vue global_keys. Before changes it found 33 distinct template spellings:

- Shared presentation: SITE_NAME, SITE_VERSION, PROJECT_CODE, ASSETS_URL, app_version_stamp, ROOT_URL, ROOT_DOMAIN, hostname, IS_DEV, is_test, IS_SIGNUP, date_format, time_format, LANG, feedback_email, support_email, SUPPORT_EMAIL, is_list_btn_left, ui_mode, ui_theme, AWSRegion, S3Bucket.
- Request state: _flash, controller, controller.action, current_time, err_ctr, err_msg, menu_items, ok_msg, request_url.
- Feature values moved to DevManage.DocsAction page state: is_mfa_enforced and mail_from. They are now read from Settings when that documentation is requested.

Corrected the case of lang/support_email references and a missing template-tag terminator in the login support link. Public S3 bucket/region remain available for CSP. The renderer/code also needs timezone and the nine PAGE_LAYOUT variants, which remain global. Runtime code reads redirect Settings directly, so their unused template copies were removed. Arbitrary custom config keys now need explicit safe publication through FwHooks.initRequest or controller page state; canonical template/Settings docs and the upgrade mapping record this source-copy contract.

The complete PR was reviewed against the original ownership/admin/performance/bootstrap goals and later explicit decisions. The existing model owns encryption, cached reads, validation and row metadata; Site Admin credential floors supplement RBAC; deployment/bootstrap and app policy retain their proper homes. No distributed cache, file key store, legacy migration runtime or new defaults registry is introduced. No whole-PR architectural rewrite is indicated.

One independent Medium finding identified the SMTP diagnostic form as another consumer of port/TLS defaults. Its real Settings reader path now uses the same 587/TLS-enabled fallbacks as mail delivery; a rendered GET test covers both absent and inherited rows. The broader consumer scan confirmed the grouped editor in PR #301 needs matching SMTP/AWS-mode fallbacks. PR #302 requires propagation and resolver verification, not redesign; the announced PR #301 follow-up now uses matching defaults and passed 95/95 integration checks, including rendered inherited and explicit values. PR #302 also merged without conflicts and retains its existing resolver behavior.

## Verification

Windows, SDK 10.0.300. Task output is ignored under artifacts/assistant_settings_globals. All database-backed test resources are disposable.

- Focused SQLite/Roles/Sentry/S3 checks passed 151/151, including actual layout/email/CSP/login/editor rendering, Settings credential resolution, schema and development routes.
- Full default suite passed 1,046/1,046 before the isolated diagnostic-form follow-up. The subsequent optional-feature review-fix selection passed 108/108, including the two new production-reader diagnostic-form cases. The default-build follow-up passed all 17 email tests.
- An actual temporary Kestrel host returned HTTP 200 for / and /Dev/Configure with SQL Server deliberately unreachable at a task-only loopback target. The Configure response included Initialize DB. Workers and automatic updates were disabled; only the task-owned host was stopped. No InitDB POST or configured database writes were performed. HTML/log evidence remains in the ignored http subdirectory.
- Final combined-stack checks passed 201/201 with SQLite/Roles/Sentry/S3, including DB resolver, key repository and DevCli coverage. The normal application build passed with zero warnings/errors. Instruction validation, diff checks and strict UTF-8/CRLF checks passed.
- The optional S3 build retains the existing unreachable-code warning in Att.cs. Successful SQL Server/MySQL persistence, IIS deployment, copied-app acceptance and live SMTP/AWS remain unverified.

Commands ran from the existing ignored SDK-10 selection directory with taskRoot set to the active checkout:

```powershell
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --filter 'FullyQualifiedName~FwTests|FullyQualifiedName~FwConfigTests|FullyQualifiedName~AdminSettings|FullyQualifiedName~SettingsStoreTests|FullyQualifiedName~SettingsProtectionTests|FullyQualifiedName~FwEmailTests|FullyQualifiedName~FwApiControllerTests|FullyQualifiedName~SecurityGroup1DevConfigureTests|FullyQualifiedName~SQLiteDBTests' --verbosity minimal '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite%3BisRoles%3BisSentry%3BisS3' "-p:OutDir=$taskRoot/artifacts/assistant_settings_globals/tests/optional/" --logger 'trx;LogFileName=focused-final.trx' --results-directory "$taskRoot/artifacts/assistant_settings_globals/test-results"
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --verbosity minimal "-p:OutDir=$taskRoot/artifacts/assistant_settings_globals/tests/default/" --logger 'trx;LogFileName=default.trx' --results-directory "$taskRoot/artifacts/assistant_settings_globals/test-results"
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --filter 'FullyQualifiedName~FwEmailTests|FullyQualifiedName~FwTests|FullyQualifiedName~AdminSettings|FullyQualifiedName~SettingsStoreTests' --verbosity minimal '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite%3BisRoles%3BisSentry%3BisS3' "-p:OutDir=$taskRoot/artifacts/assistant_settings_globals/tests/optional/" --logger 'trx;LogFileName=review-fix.trx' --results-directory "$taskRoot/artifacts/assistant_settings_globals/test-results"
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --filter 'FullyQualifiedName~FwEmailTests' --verbosity minimal "-p:OutDir=$taskRoot/artifacts/assistant_settings_globals/tests/default/" --logger 'trx;LogFileName=default-review-fix.trx' --results-directory "$taskRoot/artifacts/assistant_settings_globals/test-results"
pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1
git diff --check
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --filter 'FullyQualifiedName~AdminSettings|FullyQualifiedName~SettingsStoreTests|FullyQualifiedName~SettingsProtectionTests|FullyQualifiedName~FwEmailTests' --verbosity minimal '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite%3BisRoles%3BisSentry%3BisS3' "-p:OutDir=$taskRoot/artifacts/assistant_settings_globals/tests/transfer/" --logger 'trx;LogFileName=transfer.trx' --results-directory "$taskRoot/artifacts/assistant_settings_globals/test-results"
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --filter 'FullyQualifiedName~FwTests|FullyQualifiedName~FwConfigTests|FullyQualifiedName~AdminSettings|FullyQualifiedName~SettingsStoreTests|FullyQualifiedName~SettingsProtectionTests|FullyQualifiedName~FwEmailTests|FullyQualifiedName~FwApiControllerTests|FullyQualifiedName~SecurityGroup1DevConfigureTests|FullyQualifiedName~SQLiteDBTests|FullyQualifiedName~FwDbConnectionsTests|FullyQualifiedName~FwKeys|FullyQualifiedName~DevCliTests' --verbosity minimal '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite%3BisRoles%3BisSentry%3BisS3' "-p:OutDir=$taskRoot/artifacts/assistant_settings_globals/tests/stack/" --logger 'trx;LogFileName=stack.trx' --results-directory "$taskRoot/artifacts/assistant_settings_globals/test-results"
dotnet build "$taskRoot/osafw-app/osafw-app.csproj" --verbosity minimal "-p:OutDir=$taskRoot/artifacts/assistant_settings_globals/build/stack/"
```

## Independent review

A fresh reviewer_high, with no conversation history, reviewed the whole PR against its immediate parent and the working diff under security-boundary and consumer-contract criteria. Initial review found the SMTP diagnostic default gap described above and no other concrete defect. Follow-up adjudication resolved that finding; final verdict and active-summary audit passed with no blocking or supplemental findings. Both later branches merged without conflicts. Bounded independent reviews of #301 and #302 found no blocking findings; the grouped defaults preserve explicit overrides and database bootstrap/strict key protection still share the cached resolver. Final audit of the added stack evidence passed with no supplemental findings. The review loop is complete.
