# Report and virtual-module security

Custom report page state and exports expose presentation fields and rendered filter options while keeping execution definitions private. Option projection remains necessary because model-backed lookups can bypass the SQL/JSON normalizer. Virtual routes/navigation require active status; management lookup stays status-neutral for regeneration, and reactivation invalidates metadata caches. Removed the unnecessary unreleased-fix CHANGELOG entry. No schema change.

Removed ReportsModulesSqlServerTests, added during acceptance verification. Retained FwReportsTests (JSON/state privacy and lookup compatibility), VirtualModuleSecurityTests, FwControllersModelTests and existing virtual-controller/code-generation coverage. The removed native probe repeated the same report and lifecycle assertions.

Verification:
- Before test cleanup, `pwsh -NoProfile -File scripts/test.ps1 -Profile Full -SqlServer localhost`: Default 967, Fast 1010, Browser 87 and SQL Server 36 passed, zero skipped. Those SQL counts include the subsequently removed acceptance probes.
- After cleanup, `pwsh -NoProfile -File scripts/test.ps1 -Profile SqlServer -SqlServer localhost`: the original 35 tests passed, zero skipped; build reported zero warnings/errors.
- `git diff --check`: passed. Provider/browser verification uses disposable resources; task-owned apps/databases and the temporary bootstrap credential were removed, with evidence kept in ignored artifacts.

The three PR-local summaries were consolidated into this single current record at the user's request; unrelated merged history is unchanged. Prior independent runtime/security reviews found no remaining blocking findings. Independent cleanup review and consolidated-summary audit completed with no blocking findings. Copied-application viewer overrides should use the documented public fields. Built-in report templates were checked against the allowlists.
