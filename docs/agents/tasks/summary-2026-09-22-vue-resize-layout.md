# Vue first-resize layout stability

Fixed the first resize after Reset to Defaults so unrelated columns retain their rendered widths, and kept row actions on one line without a resize handle.

## Decisions and scope

- Capture the actual header widths before pointer, keyboard, or auto-fit resizing. Keep those measurements local to the table; persist only explicitly resized or auto-fitted data columns.
- Replace the 160px fallback with measured widths, including narrow checkbox and selection columns. Saved partial width maps use natural widths for unconfigured columns on load.
- Preserve standard and custom action slots. Grow the action column if newly rendered actions require more room; account for collapsed borders without rounding fractional widths upward.
- Reset local measurements when changing the view, density, or edit mode. Remove the fixed-layout wrapping override; retain the original fallback-width export as a compatibility shim for customized templates.
- Observe table-body changes to accommodate actions revealed by child components or custom slots. Fixed-layout pointer moves do not rescan action cells, and the observer disconnects on unmount.
- Document the behavior in `docs/dynamic.md`; bump `SITE_VERSION` to `0.26.0922.3`. No breaking-change changelog entry is needed for this bug fix. No server, schema, authorization, or save-coordination contracts changed.

## Verification

Browser tests use real list-cell templates and Bootstrap for first-resize coverage in both read-only and editable lists, with actions on either side. They cover drag, keyboard resize, single-column auto-fit, reset, saved-width remount, one-line action bounds, actions revealed after fixed layout, and zero additional action measurements across repeated pointer moves. Existing action-slot and link behavior remains covered.

With `PLAYWRIGHT_BROWSERS_PATH` pointing to the task-owned browser installation, isolated build output avoids replacing the running application's binaries:

```powershell
dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir=<absolute-repository>/artifacts/assistant_vue297/build/default/ --filter 'FullyQualifiedName~VueInteractionBrowserTests' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=resize-layout.trx' --results-directory artifacts/assistant_vue297/test-results/resize-layout
dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir=<absolute-repository>/artifacts/assistant_vue297/build/default/ --filter 'FullyQualifiedName~FirstColumnResize|FullyQualifiedName~FullTableLoadsSavedWidths|FullyQualifiedName~FixedWidthRowActions' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=resize-layout-final.trx' --results-directory artifacts/assistant_vue297/test-results/resize-layout
dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir=<absolute-repository>/artifacts/assistant_vue297/build/default/ --filter 'FullyQualifiedName~FixedWidthRowActions' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=resize-action-growth.trx' --results-directory artifacts/assistant_vue297/test-results/resize-layout
```

- Initial full browser suite: 56 passed, zero failed/skipped. After the collapsed-border correction: all 7 focused layout/action cases passed, zero failed/skipped.
- Full rerun after review fixes used the same full-suite command with `LogFileName=resize-layout-reviewed.trx`: 54 passed; the 2 new action-growth assertions timed out because the added label fit within existing space. With a longer label that actually requires growth, those 2 cases passed (`resize-action-growth.trx`). No production changes occurred between these runs; all 56 cases have passing evidence for the final implementation, with no skips.
- Live Chrome: Reset to Defaults, then drag Email by 40px. Email grew by 39.75px (integer persisted width); every other column had a 0px width delta, and Email's left edge remained unchanged. All action links stayed on one line.
- Preserved and restored the original column preferences using a task-created disposable saved view, then deleted that view. Existing business records and named views were untouched. The existing app was used without restarting it or the stopped screenshot server.
- Backend/provider suites were not rerun for this frontend-only change.

## Review

Fresh independent `reviewer_high` review used consumer-contract and performance-scale overlays because the public table template and repeated DOM measurements are affected. The initial verdict identified two Medium findings: action measurements on every component update missed some child-only changes and ran during drag; removing the committed width export could break customized templates. The implementation now observes body changes and skips fixed-layout drag measurements, with regression assertions, and retains the export. Summary audit also clarified the disposable-view cleanup statement. Final independent recheck and summary audit found no blocking findings. The observer can still scan actions on unrelated body changes (Low); narrowing it is optional, and drag updates are excluded. Review loop can stop.

- Learning signal: CSS table cells with collapsed borders contribute half each adjacent border to their measured width; adding full borders and rounding introduced a 2px first-resize shift. A live DOM comparison exposed this after fixture tests passed; preserve fractional widths and check real-cell geometry when changing table layout.
