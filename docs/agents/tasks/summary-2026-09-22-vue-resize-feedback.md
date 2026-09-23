# Vue resize feedback and upgrade changelog cleanup

## Outcome

Committed the previously reviewed interaction changes as `7e20133f` before addressing this feedback. This followup remains uncommitted; nothing was pushed. Unrelated untracked work was preserved.

- Kept `CHANGELOG.md` focused on breaking changes and concrete upgrade actions. Removed the followup-commit narrative, unreleased renames, additive tooling/features, dependency-refresh notes, and duplicate compatibility text. Retained the required saved-view widths migration and actual API/default/security changes. Feature details remain in `docs/dynamic.md`.
- Kept table row/cell classes and data attributes. They provide styling and identity/auto-fit hooks; no measured performance problem justified removing them. No benchmark result is claimed. The new all-column fit scans rendered cells once and submits one widths update.
- Resize handles retain an eight-pixel hit area and show a one-pixel divider only on hover or keyboard focus. The scoped cursor rule overrides Bootstrap's button cursor.
- Ctrl+double-click fits visible data columns from the current page, including displayed input values, while retaining hidden-column widths. Ordinary double-click still fits one column. Pointer clicks without movement no longer cause redundant width saves.
- Added a batch width action using the existing ordered persistence/rollback path; retained `saveColumnWidth(field, width)` for existing callers and overrides. Width bounds and server authorization contracts are unchanged.
- Updated `appsettings.json` `SITE_VERSION` to `0.26.0922.1`, verified the existing CSS/module URLs use it, and added the new tooltip's Chinese translation.

## Verification

The browser fixture now loads the repository's real Bootstrap CSS alongside `site.css`. **50 browser cases passed, zero failures/skips**, covering hover/focus visibility, cursor specificity, grab area/line width, keyboard/pointer resize, ordinary and Ctrl+double-click fitting, one-request batch persistence, bounds, hidden widths, rollback, and existing Vue save/validation contracts. Loading Bootstrap exposed an older fixture clicking the mobile Save button at desktop width; that test now uses the appropriate responsive viewport.

From the repository root, with an absolute task-owned output path:

```powershell
$taskRoot = Join-Path (Get-Location).Path 'artifacts/assistant_vue297'
$env:PLAYWRIGHT_BROWSERS_PATH = "$taskRoot/browsers"
dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir="$taskRoot/build/default/" --filter 'FullyQualifiedName~VueInteractionBrowserTests' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=resize-feedback-final.trx' --results-directory artifacts/assistant_vue297/test-results/feedback
git diff --check
```

The test command also compiled the app/test projects. JSON parsing verified the new version; strict UTF-8 without BOM/CRLF and diff checks passed. Tests use isolated browser contexts and fake APIs; no database, Visual Studio process, or IIS app was changed or restarted during this feedback. Prior provider/backend results remain in the September 21 summary; these frontend changes did not require rerunning them. Live authenticated testing remains outside this evidence.

Independent consumer-contract/state-integrity review found no blocking findings. The supplemental summary audit found no factual, evidence, or privacy issues. Review loop can stop. Authenticated live IIS behavior remains unverified by this followup.
