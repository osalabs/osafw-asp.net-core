# Settings refactor and PR feedback

## Outcome and accepted contracts

Consolidated the four October 31 Settings summaries into this single record at the developer's request. PR #299 remains unchanged. PR #300 owns the Settings foundation and the follow-up simplification; #301 adds grouped administration and server transfer; #302 adds cached external database-secret resolution. Work stays in the existing stack worktree. Primary master and unrelated files are untouched. No configured development database or external service was modified.

- Deployment/bootstrap values stay in appsettings; shared defaults stay in FwConfig, application policy/defaults in FwHooks.configure, and feature defaults at their consumers. SMTP delivery, diagnostics and the grouped editor use port 587/TLS enabled; S3 and its editor use sdk mode by default.
- Settings owns persistence, credential protection, metadata validation, transfer and the application cache. There is no separate settings store/protection class, defaults registry, runtime migration command, schema-upgrade guard or legacy compatibility subsystem. Existing applications need the offline migration map before deployment.
- Non-credential read(code) defaults to fw.config(code), including dotted keys. Explicit empty and inheritance remain distinct. Credential classification uses input metadata, including custom names. Missing/inherited credentials are unconfigured and never inherit config/caller secrets. Credential controls use Keep/Replace/Clear; fresh/additive credential INSERTs use basis=1.
- Database-backed sessions and DPAPI-protected keys remain. Optional session-key persistence allows database-free bootstrap; credential writes use a separate strict provider against the same durable database repository. Unavailable durable storage cannot silently produce stored credentials protected only by transient keys. No file key store or distributed cache is introduced.
- AdminSettings uses numeric row access floors plus RBAC; credentials and transfer require Site Admin. Ordinary inherited-action overrides deny unsupported admin mutations/export. It reuses Users.isSiteAdmin and preserves the public Validate extension point. Import is transactional, re-encrypts on the destination, and has no concurrent-editor conflict machinery or AWS-specific pair validator.
- Production logging omits submitted values by default; ordinary log_pii/DEBUG development diagnostics remain available. Settings audit data stays value-free and Sentry sensitive-request filtering remains. Local debug logging can expose entered values.
- SQL uses model table identifiers, parameters for runtime constants, and readable indented clauses. Unapplied update files retain necessary old-row metadata conversion and new INSERTs, without redundant repairs to rows they just insert. Canonical readability instructions preserve unbraced single-statement conditionals and blank lines between logical blocks.
- New-application preparation is explicit and scoped; repository heuristics do not authorize database mutation or cleanup. Copied-app migration guidance is offline, application-specific and value-free.

## Globals, initialization and cache

FW.G now deep-clones config() and removes only db and override, which can contain database credentials. No presentation-key whitelist or integration-secret-name filter remains in FW/FwConfig. Custom non-secret configuration stays available to templates and nested dictionaries remain request-local. Credentials belong only in Settings; old credential keys must be removed from every config provider during migration.

Settings.applyGlobals() owns the small display override list and its once-per-request flag. Web and email rendering call it; FW contains neither the list nor a settings load flag. The model copies site name/contact/UI values from the application cache and preserves session theme/mode preferences. Date/time, timezone and language remain available without the database. Failed optional display reads retain configured defaults; operational readers still report storage errors.

The cache stores one ciphertext snapshot per database identity with no timed expiration. First read loads the rows once, then other readers and requests reuse them. Supported writes/imports invalidate the snapshot and dependent menu data; the next read refreshes it. Explicit cache clear, eviction or application restart can also reload it. Direct SQL edits require invalidation or restart. FwCache.setValue(..., expire_seconds: 0) now means no timed expiry; its ordinary one-hour default remains unchanged.

Database initialization lives in the existing models/Dev/DevCli.cs. Both CLI and web entry points use it; CLI no longer constructs a controller. DevConfigure retains its IS_DEV/POST/session-token guards and protected virtual initDatabase delegation hook. The CLI retains its empty-database guard. Script order includes settings.sql after schema creation; the initializer clears schema and Settings caches even if a later script fails. No new initialization class or session/CSRF fallback system was added.

## Consumer coverage

The earlier scan covered all template extensions (HTML, selectors, Markdown and email text), GLOBAL references in attributes/text, global tag attributes, code-side G access and Vue global_keys. Its 33 original spellings covered shared presentation and request state plus MFA/sender feature values. Public S3 bucket/region remain available for CSP, and timezone/PAGE_LAYOUT variants remain available to renderer consumers. Redirects use Settings at their runtime consumers. Developer documentation reads MFA/sender values into page state. The login support link's case and missing tag terminator were corrected; lang uses its actual lowercase key.

The previous full-PR review found no need to rebuild #301/#302. #301 received the matching SMTP/AWS-mode fallbacks; #302 retained its process-cached shared connection resolver. This follow-up is being propagated through the same stack without redesigning those features.

## Verification

The final optional regression set passed 207/207, including the initializer cache fix and signup fixture correction. The full default suite passed 1,048/1,048 after those changes. Instruction validation, staged diff checks, and strict UTF-8/CRLF checks passed. Task output is ignored under artifacts/assistant_settings_cache. Database-backed tests use disposable resources. The preceding feedback passes established database-free HTTP Home/DevConfigure rendering, empty SQLite web InitDB, strict credential protection and the full Settings/admin/template paths; this follow-up rechecks affected behavior.

Current regressions cover custom config cloning with database-section exclusion, template/email/CSP consumers, session preference precedence, and separate FW instances sharing one Settings snapshot across seven simulated days. Each of update, batch and import invalidates it and the next read reloads once. A timed control entry expires, proving the simulated clock advances. A partial-schema bootstrap test warms old Settings values, runs the actual shared initializer, and verifies that reads/globals see the seeded defaults afterward. The test fixture sets session preferences before FW construction; batch/import validation queries are counted separately from the subsequent snapshot reload.

Final commands run from the existing ignored SDK-10 selector directory, with taskRoot identifying the active checkout:

```powershell
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --filter 'FullyQualifiedName~SettingsStoreTests|FullyQualifiedName~SettingsProtectionTests|FullyQualifiedName~FwCacheTests|FullyQualifiedName~FwTests|FullyQualifiedName~FwConfigTests|FullyQualifiedName~DevCliTests|FullyQualifiedName~SecurityGroup1DevConfigureTests|FullyQualifiedName~AdminSettings|FullyQualifiedName~FwEmailTests|FullyQualifiedName~FwApiControllerTests|FullyQualifiedName~SQLiteDBTests|FullyQualifiedName~FwKeys|FullyQualifiedName~SecurityDynamicObjectLinkTests' --verbosity minimal '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite%3BisRoles%3BisSentry%3BisS3' "-p:OutDir=$taskRoot/artifacts/assistant_settings_cache/tests/optional/" --logger 'trx;LogFileName=optional-review-final.trx' --results-directory "$taskRoot/artifacts/assistant_settings_cache/test-results"
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --verbosity minimal "-p:OutDir=$taskRoot/artifacts/assistant_settings_cache/tests/default/" --logger 'trx;LogFileName=default-reviewed.trx' --results-directory "$taskRoot/artifacts/assistant_settings_cache/test-results"
pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1
git diff --cached --check
```

An actual temporary HTTP host returned 200 for Home and /Dev/Configure with SQL Server deliberately unreachable at a task-only loopback target; Configure included Initialize DB. The probe ran the updated default output, disabled workers/auto-updates, retained HTML under the ignored http directory, and stopped only its exact process. No InitDB POST or configured-database writes occurred in that probe.

The first full default run passed 1,047/1,048; the single failure was an uninitialized injected Settings model in the signup fixture. The fixture now follows production model initialization, and its full class passed 13/13 before the full rerun. A warm-cache test initially replayed the entire SQLite fresh schema, which is not fully rerunnable (fwreports already exists); the final fixture starts with only the old Settings table and tests supported partial-schema bootstrap without changing provider scripts.

One intermediate rerun accidentally selected the installed preview SDK and exposed its decimal-cast difference in the existing FwCache request-serialization test. Final verification uses SDK 10.0.300. The optional S3 build still reports the previously observed Att.cs unreachable-code warning. Successful SQL Server/MySQL initialization or credential persistence, live IIS, copied-app deployment and real SMTP/AWS remain unverified; SQLite does not establish those contracts.

## Review

Earlier iterations had bounded implementation help for admin, CLI and docs work, followed by fresh independent reviews and summary audits. The final prior review found and resolved the SMTP diagnostic fallback gap. Current implementation is direct. A fresh reviewer_high used consumer-contract and state-integrity overlays. Its initial code review found one Medium issue: initialization did not invalidate an already loaded Settings snapshot. The initializer now clears it in finally, including partial failure; the warm-cache regression covers the actual initializer. Finding recheck and consolidated-summary audit passed with no further findings. Stack propagation follows this foundation checkpoint.
