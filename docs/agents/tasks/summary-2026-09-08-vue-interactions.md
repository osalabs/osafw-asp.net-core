# Vue interaction contracts

## Objective / acceptance

Improve standard Vue lists and forms with saved column widths, actionable validation, remembered filter visibility, permission-aware actions, optional quick-edit context retention, and immutable-on-edit fields. Keep server authorization authoritative, retain existing save triggers, protect copied configuration, and provide additive schema updates for the supported saved-view providers.

## What changed

- Widths persist with default/named views. Browser controls support pointer dragging, keyboard bounds/steps and autofit, with listener cleanup and ordered persistence. Both sides normalize configured columns and values.
- Neutral validation issues accompany legacy errors. Summaries and field messages are escaped; links select tabs, reveal fieldsets and target repeated rows. Attempted values require explicit safe-field metadata.
- Initial and per-row capabilities drive common controls. Immutable-on-edit metadata is enforced by standard server save paths and reflected in main and repeated-row controls.
- Filter visibility uses scoped browser storage without changing filter values. Storage failures leave usable controls.
- Opt-in quick-edit refresh obtains current server list data while retaining the edit pane. Failed saves/deletions do not reload; refresh failure is distinguished from save failure. Concurrent identical saves are suppressed, changed forms are queued, and delayed callbacks cannot save a different form after navigation.

## Requirements / decisions

- This branch builds on the calculated-column branch; their separate PRs can be reviewed independently in dependency order.
- Width changes require the provider's additive user-view update before using the new model/controller against an existing database. No shared database was modified.
- Field metadata applies to standard controller writes. Applications requiring immutability across custom actions or direct model writes must enforce that broader rule themselves.
- Existing quick-edit change handlers remain the save triggers. Context retention adds no autosave event.
- Backend and frontend implementation used disjoint ownership. A separate browser-test worker strengthens actual Vue/Pinia interaction coverage; root owns integration and final review.

## Commands used / verification

Initial frontend checkpoint: `dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter 'FullyQualifiedName~VueInteractionBrowserTests' --logger 'console;verbosity=normal'` passed three tests with the matching task-owned Chromium cache selected using `PLAYWRIGHT_BROWSERS_PATH` and restored local libraries selected using `FW_BROWSER_ASSETS_ROOT`. The fixture runs offline and loads the actual common templates and Pinia store. Early fixture corrections added the missing form-control module dependency; a real template correction then made immutable presentation cover both existing control chains. The extended browser packet below supersedes this initial frontend checkpoint; later independent-review findings are recorded separately below.

## Testing instructions

Restore the project and frontend libraries. Install the matching Playwright Chromium executable or select an existing matching task-owned cache with `PLAYWRIGHT_BROWSERS_PATH`. `FW_BROWSER_ASSETS_ROOT` may point to a restored framework assets directory; absent that override, tests use the application's assets directory. Run the focused browser command above. Tests report unavailable prerequisites as inconclusive rather than installing browsers or reaching live services. Provider checks must use disposable databases and the exact compile symbol for SQLite.

## Risks / follow-ups

- No live SQL Server or MySQL schema update has been run.
- Browser tests use synthetic records and isolated response fixtures; they do not replace application-specific authorization and validation tests.
- Custom subtable components must provide the documented validation anchors and honor nested child definitions.
- Saved-width updates from separate browser tabs remain last-write-wins user preferences.

## Reflection

Using the real Vue component modules exposed a split conditional chain that a store-only check would have missed. Browser fixtures should fail on module-loading errors before interpreting missing controls as evidence of correct read-only behavior.

## Extended browser verification

A separate worker extended the real-component fixture and returned exclusive test-file ownership. Final command: `dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter FullyQualifiedName~osafw.Tests.VueInteractionBrowserTests` with the same matching Chromium and restored-asset environment overrides: 8 passed, none skipped. Scenarios cover width bounds/order/rollback/mode capture; mixed-selection permissions; errors/warnings and duplicate saves; tab/fieldset and repeated-row focus; new/existing immutable fields; filter storage failures and scope; quick-edit refresh of rows/count with pane focus/selection/scroll retention; refresh-failure warning; changed-draft follow-up; failed deletion; and delayed save cancellation across forms. Fixture module errors, Vue warnings and unhandled errors fail tests. Unrelated child components and autocomplete are fixture stubs, and the icon-only delete control uses a dispatched DOM click because the fixture removes ParsePage icons.

A transient backend write encoded physical line breaks as literal escapes. The backend owner verified the affected baseline had no matching legitimate literal sequences, repaired the files, and passed the recovery compile before the final browser run. Strict text checks remain required on the final combined diff.

## Final backend verification

Focused default tests: 52 passed. SQLite-symbol focused/provider tests: 13 passed. Structured error issues are checked after validation and before model persistence; a direct action regression confirms HTTP 400 and zero writes, while warning-only saves succeed. Compound immutable controls preserve initialization on create and skip post-save changes on update. Backend diff and UTF-8/CRLF checks passed.

After merging the reviewed calculated-column prerequisite, combined default and browser verification passed 59 tests with no failures or skips (VueInteractionBrowserTests, VueInteractionBackendTests, FwVueControllerTests, FwVirtualControllerTests, and FwDynamicControllerColumnFilterTests). The single merge conflict was a blank line between tests. All changed files passed strict UTF-8 without BOM, CRLF, generic-context and diff-whitespace checks.

## Independent review corrections

Independent extra-high review was selected for interacting authorization/immutability, save concurrency, provider schema and public frontend contracts, after substantive integration corrections. It used the consumer-contract and state-integrity overlays. The initial handoff withheld the active summary and implementation narrative; a separate summary/index audit followed.

The first independent review found two data-loss cases not covered by the earlier passing browser tests: a stale subtable response could overwrite queued child edits/additions, and switching from one saving form to another could discard the second requested save. It also found legacy REQUIRED/INVALID aggregate markers appearing as fake field errors. The corrections and regression evidence follow; earlier passing results are not evidence that these cases worked initially.

Exact backend filters used for the 52/13 results above (task-owned output directories are shown relative to the worktree):

```powershell
dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir=artifacts/vue-backend-final2/ --filter "FullyQualifiedName~VueInteractionBackendTests|FullyQualifiedName~FwVueControllerTests|FullyQualifiedName~FwVirtualControllerTests|FullyQualifiedName~FwDynamicControllerTests|FullyQualifiedName~UserOwnedPreferencesSecurityTests"
dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:DefineConstants=isSQLite -p:OutDir=artifacts/vue-backend-sqlite-final2/ --filter "FullyQualifiedName~VueInteractionBackendTests|FullyQualifiedName~SQLiteDBTests.SQLiteSchemaScripts_CreateFreshFrameworkDatabase|FullyQualifiedName~SQLiteDBTests.SQLiteUserViewWidthsUpdate_AddsColumnToLegacySchema"
```
The correction tracks requests, busy state and explicitly requested followups per captured form. It snapshots submitted data, merges child responses without overwriting newer edits/additions/removals, and applies assigned child/parent IDs before another request. Different forms can save independently; navigation alone does not save. A stale response reconciles its captured draft without calling an active-form application override. Existing one-argument reconciliation remains an authoritative refresh. Legacy aggregate validation markers are omitted from field issues and boolean required errors receive the correct message.

Four new browser regressions failed before this correction. Final correction-specific command: `dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter 'FullyQualifiedName~VueInteractionBrowserTests' --logger 'console;verbosity=normal'`, using the same offline asset/browser prerequisites above: 15 passed, zero failures or skips. The expanded cases include child edit/add/remove preservation, independent forms and busy controls, inactive-form ID reconciliation, captured API/queued save behavior, queued parent creation, autosave controls and legacy validation aggregates. Both changed implementation/test files passed strict UTF-8 without BOM, CRLF and diff-whitespace checks. Final independent re-review is pending.

Final integrator run after the correction: the combined filter `FullyQualifiedName~VueInteractionBrowserTests|FullyQualifiedName~VueInteractionBackendTests|FullyQualifiedName~FwVueControllerTests|FullyQualifiedName~FwVirtualControllerTests|FullyQualifiedName~FwDynamicControllerColumnFilterTests` passed 66 tests with zero failures or skips, including all 15 browser cases. Final checks of all 39 changed files against the prerequisite branch passed strict UTF-8/no-BOM/CRLF, generic-context and diff-whitespace checks.

A second independent pass found that a queued save retained the first request's tab. Because standard server compound-field processing uses that tab's definitions, switching tabs and requesting another save could leave the new tab's child changes unprocessed. This was not covered by the 66-test checkpoint; the followup correction must capture the tab for each explicitly requested save and add a delayed-response tab-switch regression.

On 2026-09-09, execution resumed after an interruption. Git status and the published PR inventory confirmed the existing checkpoint; no previous agent remained active and the tab correction had not been applied. Recovery preserved all existing changes and resumed only the outstanding tab-specific save fix.

The recovered correction captures tab context for each explicitly requested save, coalesces repeats per tab in first-request order, and reconciles only the subtables processed by that tab. Debounced saves retain their originating tabs; ordinary Vue click-event arguments remain supported. Six added browser cases failed before the correction. The final browser-only command shown above then passed 21 tests with zero failures/skips, including all 15 earlier cases. No backend/provider changes were made during this recovery. A fresh independent reviewer is checking the final correction after the interrupted review process.

Final recovered integration command:

```powershell
dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter 'FullyQualifiedName~VueInteractionBrowserTests|FullyQualifiedName~VueInteractionBackendTests|FullyQualifiedName~FwVueControllerTests|FullyQualifiedName~FwVirtualControllerTests|FullyQualifiedName~FwDynamicControllerColumnFilterTests' --logger 'console;verbosity=minimal'
```

With the same offline browser/assets overrides, this passed 72 tests, zero failures or skips, including all 21 browser cases.

The fresh review found a cross-tab failure-reporting gap outside the 72-test checkpoint: a successful queued save on one tab could overwrite a failed result from another tab. The correction must retain each failed tab until that same tab saves successfully and prevent success navigation while any such failure remains. This finding is recorded separately from earlier passing evidence.

The failed-tab correction retains errors per captured form and tab, including through queued and later explicit/autosave calls. Successful other-tab data can reconcile, but unresolved failures remain visible and block success navigation. Only a successful retry of the failed tab clears that failure. Three new structured-validation, legacy-validation and transport-failure browser cases failed before this correction, then passed. The final browser-only command above passed 24 tests with zero failures/skips; it also checks error tab/row focus, independent form success and restored navigation after retry.

After this correction, the exact combined integration command above passed 75 tests, zero failures/skips, including all 24 browser cases.

Final independent review and supplemental summary/index audit completed after the correction. Independent read-only checks also covered multiple outstanding tab failures, selective clearing and restored navigation. No blocking findings. Review loop can stop.

## Lean review follow-up (2026-09-09)

Synced with master after the four approved framework updates and with the simplified calculated-column prerequisite. The merge preserved both changelog/index additions. No further Vue runtime redesign was warranted by the independent lean review; existing save coordination and customization hooks remain.

The combined integration command above passed 75 tests again, including all 24 browser cases, with zero failures or skips and the same isolated browser/assets prerequisites. No live application database or external service was used.