# Security signin integration verification

## Outcome

- Completed the remaining repository browser and SQL Server checks for PR #304.
- Added SigninSqlServerTests against the fresh supported SQL Server schema. Native SQL remember tokens reject expired issuance, renew successfully, preserve unchanged-status profile edits, and reject disabled or MFA-enrolled accounts.
- Live isolated app: normal browser form login reached the dashboard and created one remember token. A valid-token GET login, wrong-token POST, and query-credential POST returned 403. No Windows domain/Negotiate hosting was exercised.
- No application-code failure was found; production behavior and schema are unchanged by this follow-up.

## Verification

- Passed `pwsh -NoProfile -File scripts/test.ps1 -Profile Browser`: clean build, 87 passed, zero failed/skipped.
- Passed `pwsh -NoProfile -File scripts/test.ps1 -Profile SqlServer -SqlServer localhost`: clean build, 36 passed, zero failed/skipped, including the new focused regression coverage.
- Initial default LocalDB attempts failed during fixture connection before database creation. Used the runner's explicit server override with the running local SQL Server 2025 instance; no machine-specific default was added to tracked configuration.
- Fixtures created and removed uniquely named databases. Live app probes, where used, had separate disposable databases, synthetic accounts, process configuration, and loopback listeners; their exact task processes/databases were stopped/removed afterward.
- Final test-project build with `EnableSqliteTests=true` passed with zero warnings/errors.
- Fresh independent test/evidence review and final summary audit completed with no blocking findings.
