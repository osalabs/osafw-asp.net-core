# Settings bootstrap and readability review

## Outcome and decisions

Follow-up fixes for PR #300, to be propagated into #301/#302. Database-backed sessions and DPAPI-wrapped keys stay in the database. Home and `/Dev/Configure` render before the configured database is reachable or initialized. No file key store, distributed cache, session fallback subsystem, runtime migration machinery, or additional InitDB token system was added.

- Optional session-cookie key persistence preserves bootstrap behavior. Persisted Settings credentials use their own strict cached provider against the same database key store, never the optional HTTP provider. Missing durable storage fails before a credential can be saved.
- Presentation globals contain an explicit non-secret list; unavailable overrides leave FwConfig/FwHooks defaults. Date/time, language and timezone require no Settings lookup. Operational Settings reads continue reporting storage errors. Database definitions still need valid provider/connection configuration at host startup.
- The developer explicitly retained the simple local development InitDB flow. Existing IS_DEV/POST guards and available session-token validation remain; unavailable sessions do not require new bootstrap CSRF infrastructure.
- AdminSettings reuses Users.isSiteAdmin, inlines the single-use cache-header helper, and documents its action allowlist against inherited generic mutations/export paths. Model/row authorization and RBAC remain additive.
- Settings validation uses control metadata, including local_url for app-local redirects. AWS-specific grouping/complete-pair validation and affected-row import concurrency guards were removed. Imports retain transactions and rollback.
- Restored ordinary log_pii/DEBUG diagnostics and full development error dumps as requested. Production defaults omit submitted values; Settings audit records stay value-free and the Sentry sensitive-request filter remains. Explicit local debug settings can expose entered values.
- SQL now uses model table identifiers, parameters for runtime constants, and indented clauses/predicates. Canonical SQL/readability instructions and the instruction-pack version record the requested style.

No configured development database was changed. Primary master and unrelated untracked files were left alone. The task uses the existing stack worktree.

## Verification

SDK 10.0.300, Windows. Commands run from a task-owned SDK selection directory; taskRoot identifies the existing worktree and all outputs are ignored.

```powershell
dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --verbosity minimal "-p:OutDir=$taskRoot/artifacts/assistant_settings_bootstrap/tests/default/" --logger 'trx;LogFileName=default.trx' --results-directory "$taskRoot/artifacts/assistant_settings_bootstrap/test-results"

dotnet test "$taskRoot/osafw-tests/osafw-tests.csproj" --filter 'FullyQualifiedName~AdminSettingsControllerTests|FullyQualifiedName~AdminSettingsRbacTests|FullyQualifiedName~SettingsStoreTests|FullyQualifiedName~SettingsProtectionTests|FullyQualifiedName~FwKeys|FullyQualifiedName~SecurityGroup1DevConfigureTests|FullyQualifiedName~SecurityGroup9ATests|FullyQualifiedName~DevCliTests' --verbosity minimal '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite%3BisRoles' "-p:OutDir=$taskRoot/artifacts/assistant_settings_bootstrap/tests/bootstrap/" --logger 'trx;LogFileName=bootstrap-final.trx' --results-directory "$taskRoot/artifacts/assistant_settings_bootstrap/test-results"

python artifacts/assistant_settings_sync/bootstrap-http.py
```

- Default suite: 1,041 passed, 0 failed. Focused SQLite/Roles suite: 124 passed, 0 failed. Covers generic credential edits/import rollback, row/list authorization, metadata validation, separate strict credential protection after optional cookie-key creation, durable DPAPI provider recreation, and production versus development error responses.
- Actual task-owned Kestrel hosts: unreachable SQL Server test connection returned HTTP 200 and rendered Home and `/Dev/Configure`; an empty disposable SQLite database rendered both pages, initialized through InitDB, received Settings seed rows, and then reported initialized schema. Only the exact task-created host processes were stopped. The first HTTP probe omitted the template path needed for isolated OutDir output and returned an empty page; corrected fixture paths produced the passing observations above.
- Earlier focused cleanup run passed 81 tests. A row-filter fixture originally invoked RBAC without a role schema; direct IndexAction filtering plus the dedicated RBAC suite cover the separate boundaries without a production test seam.
- Not exercised: successful SQL Server schema initialization or credential writes against SQL Server, live IIS, MySQL runtime, Linux DPAPI (unsupported), or real AWS. The SQL Server observation proves unavailable-storage bootstrap, not successful SQL Server persistence. SQLite/provider tests do not substitute for those deployment checks.

## Review and remaining integration

Bounded implementation delegation owned AdminSettings/controller tests; the primary integrated model validation, storage/bootstrap behavior, docs and verification. Fresh reviewer_astra_high was selected for the consequential optional-cookie/strict-credential security and state distinction. Initial independent review found only stale documentation claims; these were corrected. Final independent review and summary audit found no blocking findings. Instruction validation passed. PR #301 integration also passed independent review. Its grouped editor permits either AWS credential to be replaced while a blank counterpart remains unchanged; the shared noStore helper now has two callers. Focused AdminSettings/RBAC/storage/protection tests passed 71/71 (same SQLite/Roles test command narrowed to those four classes, OutDir `artifacts/assistant_settings_bootstrap/tests/transfer/`, result `transfer-final.trx`). The initial merged list fixture lacked the grouped editor definitions; adding those fixture rows restored the intended authorization/count coverage. Final stack propagation/push remain pending.
