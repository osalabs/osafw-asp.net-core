# Developer tool and diagnostic security

## Outcome

- Local CLI remains the fresh-database bootstrap. Web initialization requires a tokened Site Admin POST; the shared initializer rejects non-development mode and all nonempty databases before running scripts.
- Removed destructive foreign-key reset from initialization. Existing databases use additive updates instead.
- SQL execution requires an active writable Site Admin account, a tokened POST, and current account password for each execution; removed source-embedded shared credentials and remembered unlock authority.
- Assistant DEBUG logs redact user-derived tool/query/result text unless PII diagnostics are explicitly enabled; developer log content is HTML escaped.
- No schema migration. Bootstrap/console behavior changes are recorded in deploy docs, README, and changelog. Assistant persistence remains separate from diagnostic logging.

## Verification

- Passed `pwsh -NoProfile -File scripts/test.ps1 -Filter 'FullyQualifiedName~DevCliTests|FullyQualifiedName~SecurityGroup1DevConfigureTests|FullyQualifiedName~DiagnosticSecurityTests|FullyQualifiedName~AssistantFeatureTests'`: clean build, 96 passed, zero skipped.
- Passed the full Fast profile: clean build, 1,013 passed, zero skipped. Default-feature app build passed with zero warnings/errors.
- Independent review found a table-link GET compatibility gap. Result links now prefill without executing; regression tests cover rendered links, real password verification, and base read-only enforcement. Independent reviewer rechecked the corrected consumer and tests with no blocking findings; final evidence and summary audit completed with no blocking findings. Browser and SQL Server integration remain unexecuted.
- Tests use task-owned SQLite files and temporary logs; no configured development database, live credentials, or external services are used.
