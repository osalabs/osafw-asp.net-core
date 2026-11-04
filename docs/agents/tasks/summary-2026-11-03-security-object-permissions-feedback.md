# Security object permissions feedback

Addressed PR #303 review feedback against pre-edit 30925f78. Consolidated attachment authorization into the automatically dispatched AdminAttController.checkAccess hook and the standard Att.checkAccess overloads. Existing parents and bulk selections are checked before actions; normalized proposed bindings are checked before save. Public id-based serving still requires active rows, while loaded-row checks allow authorized inactive administration. Removed checkAdminAccess and the repeated modelOne/delete/restore/bulk wrappers; one(id) supplies the existing empty-row contract without a conditional fallback.

Reviewed the complete PR runtime diff, including read-only account administration and owner/system autocomplete predicates. The remaining guards protect distinct boundaries. No new schema or changelog entry.

Verification:
- `pwsh -NoProfile -File scripts/test.ps1 -Profile Full -SqlServer localhost`: Default 969, Fast 1013, Browser 87, SQL Server 36 passed; zero failed/skipped, builds without warnings/errors.
- Final `-Profile Fast -Filter 'FullyQualifiedName~AdminAttControllerTests'`: 3 passed, including existing/proposed parent denial, bulk dispatch, inactive administration, and metadata-save compatibility. This rerun includes the final proposed-parent assertion.
- `git diff --check`: passed. SQL Server checks use task-owned disposable databases, never application connections.

Fresh reviewer_high security review returned no blocking findings; supplemental summary audit completed with no additional findings. Copied-app parent authorization overrides remain application-specific acceptance checks. Historical summaries retain the earlier implementation history.

Closeout: all task-owned app process trees and the disposable live database were removed; the temporary bootstrap credential was deleted. Browser evidence remains in ignored artifacts.
