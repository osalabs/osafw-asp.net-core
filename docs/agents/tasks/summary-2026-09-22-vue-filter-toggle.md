# Compact Vue filter toggle

Replaced the list header's text button with a small bump above the filter panel's upper-right edge. The expanded state has a raised-tab outline; the collapsed state is fully rounded. Both states keep the same 44-by-12-pixel bounds and position. The absolutely positioned button adds no vertical layout space: the open form retains its original spacing, and the collapsed wrapper has zero height. The chevron, translated title, accessible label, and `aria-expanded` follow visibility; keyboard focus has a visible outline.

`list-filters` now keeps its toggle mounted and hides only the form with `v-show`, retaining entered values. The common `screens` template no longer hides the entire component. Existing storage keys and visibility actions are unchanged. The canonical documentation explains the toggle and tells custom screen templates to leave `list-filters` mounted. `SITE_VERSION` is now `0.26.0922.6` for CSS cache invalidation. No database changes or breaking upgrade changelog entry are needed for this unreleased PR refinement.

The preceding read-only Code demo changes were committed and pushed as `b13bbff9` before this work. The user requested commit/push after the final space-saving refinement.

## Verification and review

- `dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir=<absolute-repository-root>/artifacts/assistant_vue297/build/default/ --filter 'FullyQualifiedName~FilterToggleKeeps|FullyQualifiedName~SaveFailuresWarningsDuplicateCallsPermissionsAndVisibility' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=filter-bump.trx' --results-directory artifacts/assistant_vue297/test-results/filter-bump`: 2 passed, 0 failed. `PLAYWRIGHT_BROWSERS_PATH` used the task's restored browser directory. The tests check zero added space above the open form, zero collapsed panel height, complete space recovery for the table, stable toggle geometry, retained filter input, tooltip/expanded state, keyboard reopening, and existing visibility/storage behavior.
- Live Chrome on the Vue demo: expanded and collapsed screenshots inspected; both button bounds were identical. Hiding the form left the toggle visible, a reload retained the collapsed state, and Enter reopened the form. The original expanded preference was restored. No records were changed.
- The initial visual check's temporary server was stopped. The final no-space check used the already running app and left that server untouched. Task-created browser tabs were closed after restoring expanded visibility.
- `git diff --check`, appsettings JSON parsing, and strict UTF-8 without BOM/CRLF checks passed.
- Local second-pass review with consumer-contract considerations found no blocking findings. The change is a localized visual/template adjustment with focused behavior checks. Current template consumers use the common screen component; the custom-screen mounting requirement is documented. Review loop can stop.

The full application/provider suites were not rerun. Live visual acceptance covered the current desktop dark theme; light theme uses the same Bootstrap/framework color variables but was not separately captured.
