# Vue interaction review fixes

## Scope

- Serialize width, reset, density, and named-view writes through one store-local queue while retaining width rollback and captured request context. Resizes queued after reset/load use the refreshed widths; a later reset/load remains authoritative over earlier resizes.
- Keep Add New available when creation is allowed but the current row cannot be edited; retain existing navigation guards.
- Supply the translated save-failure fallback during actual initial page setup and update obsolete summary assertions.
- Share the demo and virtual-controller subtable implementation behind their existing template entry points, and reuse a reactive column-width calculation in the table component.
- Refresh the asset version. No schema or breaking consumer change is introduced by this stage.

## Verification

- With restored frontend assets and Playwright Chromium, `dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir=<worktree>/artifacts/assistant_vue_review/build/default/ --filter FullyQualifiedName~VueInteraction` passed 77 controller/browser cases.
- After correcting the named-view test request to its actual nested shape, the `FullyQualifiedName~UserViewMutationWaitsForPendingWidthsAndRemainsLast` filter passed all four cases.
- Independent review caught stale width state for reset/load followed by resize. After applying queued width deltas to the confirmed refreshed view, `FullyQualifiedName~ColumnWidth|FullyQualifiedName~ColumnResizeAfterViewMutation|FullyQualifiedName~WidthWritesAcrossViewRefresh|FullyQualifiedName~UserViewMutation|FullyQualifiedName~FilterVisibility` passed 13 cases, including successful/failed reverse-order saves and newer resizes across refresh.
- Follow-up review extended this to failed/skipped refreshes. Reset/load now return already-normalized widths without another query; the queue keeps confirmed widths by originating API and list mode. A load without a known width base fails a dependent resize rather than sending stale widths. The final `FullyQualifiedName~VueInteraction` run passed 89 cases, including refresh failure, context changes, and the saved-width response.
- Changed text passes strict UTF-8 without BOM/CRLF checks and `git diff --check`.
- UI/template and regression implementation was delegated with exclusive ownership while the primary implemented saved-view coordination and integration. Database, live server, and user browser state were not touched.
