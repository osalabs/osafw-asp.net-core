# Lean Vue validation feedback

Removed the redundant "Not saved · tab" row from the shared save-status template. Existing header save badges and failed-tab exclamation marks remain. Transport, authorization, unrelated server errors, and list-refresh failures retain their separate alerts. Save coordination and navigation guards are unchanged.

Validation-summary buttons now inherit the surrounding font and use baseline alignment without a button border. This aligns field links with "Please review these fields:" while keeping native button keyboard behavior and issue navigation. The optional summary remains enabled in DemosVue. `SITE_VERSION` is `0.26.0926.2` to invalidate cached CSS and scripts, and `docs/dynamic.md` describes the reduced feedback. No schema or breaking upgrade change is involved.

Subtable summary labels now use "Subtable Field: Message", honoring configured labels and omitting internal row identifiers. The original field key and row identifier remain intact for focus/navigation, including newly added rows.

## Verification and review

- `dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir=<absolute-repository-root>/artifacts/assistant_vue297/build/default/ --filter 'FullyQualifiedName~ValidationPresentationUses|FullyQualifiedName~FailedTabSaveSurvives' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=validation-alignment.trx' --results-directory artifacts/assistant_vue297/test-results/validation-alignment`: 6 passed, 0 failed. `PLAYWRIGHT_BROWSERS_PATH` used the task's restored browser directory.
- Additional label verification: the same command with filter 'FullyQualifiedName~SubtableIssuesFocus|FullyQualifiedName~ValidationPresentationUses|FullyQualifiedName~FailedTabSaveSurvives' passed the six existing cases; the added subtable focus assertion initially ran before its asynchronous focus completed. After waiting for that operation, the focused 'FullyQualifiedName~SubtableIssuesFocus' rerun passed 1/1 (retained in artifacts/assistant_vue297/test-results/subtable-labels/subtable-labels.trx). Both existing-row and new-row labels and focus targets are verified; production focus behavior is unchanged.
- Browser tests render production Vue templates with Bootstrap and site CSS. They measure label/link text alignment and exercise field focus, optional summaries, toast deduplication, absence of the redundant status row, failed-tab markers, validation versus unrelated failures, retry recovery, and blocked navigation while failures remain.
- `git diff --check`, appsettings JSON parsing, and strict UTF-8 without BOM/CRLF checks passed.
- Local second-pass review: no blocking findings. The change is limited to presentation and documentation; shared error and save-state behavior remains covered by the focused regressions. Active summary audited for accuracy and public-framework privacy. Review loop can stop.

No live development records were changed or local servers restarted. Live Chrome acceptance and the full application/provider suites were not rerun for this bounded template/CSS change.

Before the requested commit on September 27, all seven cases passed together using filter 'FullyQualifiedName~ValidationPresentationUses|FullyQualifiedName~FailedTabSaveSurvives|FullyQualifiedName~SubtableIssuesFocus' with task-owned output under artifacts/assistant_validation_cleanup/build/default/ and results under artifacts/assistant_validation_cleanup/results/commit-final. The commit includes only this UI cleanup; unrelated local files remain excluded.
