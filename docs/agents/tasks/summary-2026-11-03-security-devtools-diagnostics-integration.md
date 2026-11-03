# Security devtools-diagnostics integration verification

## Outcome

- Completed the remaining repository browser and SQL Server checks for PR #306.
- Added DevtoolsSqlServerTests against the fresh supported SQL Server schema. SQL Server bootstrap creates a fresh schema and verifies its generated account password, then refuses reinitialization. Web initialization refuses anonymous and populated-database requests; SQL console checks the real current password and read-only policy.
- Live isolated app: wrong SQL-console password returned 403; current-password form POST returned SELECT result 42. Table selection only prefilled SQL with zero queries and an empty password field. Site Admin web initialization of the populated database returned 400 without a password response.
- No application-code failure was found; production behavior and schema are unchanged by this follow-up.

## Verification

- Passed `pwsh -NoProfile -File scripts/test.ps1 -Profile Browser`: clean build, 87 passed, zero failed/skipped.
- Passed `pwsh -NoProfile -File scripts/test.ps1 -Profile SqlServer -SqlServer localhost`: clean build, 37 passed, zero failed/skipped, including the new focused regression coverage.
- Initial default LocalDB attempts failed during fixture connection before database creation. Used the runner's explicit server override with the running local SQL Server 2025 instance; no machine-specific default was added to tracked configuration.
- Fixtures created and removed uniquely named databases. Live app probes, where used, had separate disposable databases, synthetic accounts, process configuration, and loopback listeners; their exact task processes/databases were stopped/removed afterward.
- Final test-project build with `EnableSqliteTests=true` passed with zero warnings/errors.
- Fresh independent test/evidence review and final summary audit completed with no blocking findings.
