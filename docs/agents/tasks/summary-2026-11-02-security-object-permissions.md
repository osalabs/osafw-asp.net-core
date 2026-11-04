# Security object permissions

Attachment administration checks existing parents in the framework-dispatched controller access hook and proposed bindings after normalization. Standard Att.checkAccess overloads share parent policy; inactive rows remain manageable by authorized administrators while public serving requires active rows. Removed checkAdminAccess, per-action wrappers, and redundant empty-row fallback. Read-only account-administration guards and owner/system autocomplete predicates remain; bound attachment names are excluded from quick search. No schema change.

Removed ObjectPermissionsSqlServerTests, added during acceptance verification. Retained AdminAttControllerTests (existing/proposed parents, bulk access, inactive administration), PreferenceAutocompleteTests (visibility before limits), SecurityGroup9BTests and existing ownership/attachment tests. These protect the behavior without a second provider copy.

Verification:
- Before test cleanup, `pwsh -NoProfile -File scripts/test.ps1 -Profile Full -SqlServer localhost`: Default 969, Fast 1013, Browser 87 and SQL Server 36 passed, zero skipped. Those SQL counts include the subsequently removed acceptance probes.
- After cleanup, `pwsh -NoProfile -File scripts/test.ps1 -Profile SqlServer -SqlServer localhost`: the original 35 tests passed, zero skipped; build reported zero warnings/errors.
- Final focused AdminAttControllerTests run: 3 passed, including the proposed-parent assertion.
- `git diff --check`: passed. Provider/browser verification uses disposable resources; task-owned apps/databases and the temporary bootstrap credential were removed, with evidence kept in ignored artifacts.

The three PR-local summaries were consolidated into this single current record at the user's request; unrelated merged history is unchanged. Prior independent runtime/security reviews found no remaining blocking findings. Independent cleanup review and consolidated-summary audit completed with no blocking findings. Copied-application parent overrides remain application-specific acceptance checks.
