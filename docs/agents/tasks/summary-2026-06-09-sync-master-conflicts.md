## What changed
- Resolved master merge conflicts in `docs/dynamic.md`, `osafw-app/App_Code/fw/FwController.cs`, and `osafw-app/App_Code/fw/FwDynamicController.cs`.
- Combined the list-column-filter branch's saved `{ f, search }` filter payload restore with master's `UserFilters.oneAvail()` authorization and stale saved-filter metadata clearing.
- Preserved both dynamic controller hooks: typed list column filter opt-in and `SaveAttFiles` permission mapping.
- Kept both dynamic config doc entries for `list_column_filters` and `view_list_custom_trusted`.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/agents/tasks/summary-2026-06-01-column-filters-phase1.md`
- Conflict hunks in `docs/dynamic.md`, `FwController.cs`, and `FwDynamicController.cs`
- Nearby column-filter helpers, saved-filter model behavior, and focused tests under `osafw-tests`
- No >1 MB files required whole-file reads.

## Commands used / verification
- `rg -n "<<<<<<<|=======|>>>>>>>" docs\dynamic.md osafw-app\App_Code\fw\FwController.cs osafw-app\App_Code\fw\FwDynamicController.cs` - no matches.
- `git ls-files -u` - no unmerged entries after staging resolved files.
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check ...` - edited files are UTF-8 without BOM and CRLF.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_merge_build\` - passed, 0 warnings/errors.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FwControllerColumnFilterTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_merge_tests_column\` - passed, 6 tests.
- `dotnet test osafw-tests\osafw-tests.csproj --filter UserOwnedPreferencesSecurityTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_merge_tests_prefs\` - passed, 25 tests.
- `git diff --cached --check` - passed.
- `git grep -n -E "^(<<<<<<<|=======|>>>>>>>)"` - no tracked conflict markers.
- Self-review using `docs/agents/code_reviewer.md` because the manual resolution was small and focused tests covered the affected contracts; no issues found.

## Decisions - why
- Used `oneAvail()` before decoding saved filter JSON so direct saved-filter ids keep the owner-or-system access check from master.
- Restored the new saved-filter shape by loading `f` into standard filters and `search` into `_filtersearch_...`; legacy saved-filter JSON still loads directly into `f`.
- Kept `clearUserFilter()` on unauthorized, stale, and system-filter editable metadata paths so users cannot keep edit/delete metadata for filters they do not own.
- Added both dynamic controller overrides because they affect independent contracts.

## Pitfalls - fixes
- Conflict resolution could have dropped either typed search restore or the saved-filter security hardening; merged those paths explicitly.
- Conflict resolution could have replaced one dynamic controller override with the other; retained both methods.

## Risks / follow-ups
- Only focused tests were run, not the full suite.
- No changelog entry needed; this resolved merge conflicts while preserving the public behavior already introduced by the branch and master.

## Heuristics (keep terse)
- When saved filter JSON shape changes and security hardening also touches load paths, merge authorization first, then decode compatible payload shapes.

## Testing instructions
- Build: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_merge_build\`
- Focused tests: `dotnet test osafw-tests\osafw-tests.csproj --filter FwControllerColumnFilterTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_merge_tests_column\`
- Focused tests: `dotnet test osafw-tests\osafw-tests.csproj --filter UserOwnedPreferencesSecurityTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_merge_tests_prefs\`

## Reflection
Initial conflict inspection was fast because the merge state was limited to three files and the prior column-filter task summary named the key contracts. Future agents should compare both index-level conflict hunks and the related tests before choosing a side on saved preference merges; those files often carry both UX state and authorization semantics. `AUTO_MERGE` was useful for reviewing only the manual resolution instead of the whole master merge. No stable facts, heuristics, or ADRs were added beyond the task-summary note; the merge-specific heuristic is left here rather than promoted.
