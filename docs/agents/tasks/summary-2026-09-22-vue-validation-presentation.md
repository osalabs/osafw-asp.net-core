# Vue validation presentation

Implemented the approved presentation changes: one failed-save toast, one field-feedback renderer, a compact optional linked summary, and subtable tooltips that do not increase row heights. The shared summary defaults off; DemosVue explicitly enables it for demonstration.

## Contracts and scope

- `uioptions.edit.is_validation_summary` controls the summary. Error summaries use danger styling; warning-only summaries use warning styling. Inline field-labelled buttons wrap naturally and retain tab/fieldset reveal and focus behavior. `issueLabel(issue)` supports application label customization.
- Legacy `error.details` and structured issues share field feedback instead of rendering duplicates. Attempted values remain visible only when the server included them under the existing opt-in policy; all text remains escaped.
- Identical failures on a form/tab suppress repeated toasts until that tab succeeds. The existing retained-failure map supplies failed-tab indicators and an unsaved hint; successful saves elsewhere do not clear them or allow navigation.
- Ordinary field validation has no generic top danger alert. Non-field/server/authorization/transport failures remain separate persistent save alerts. Existing save queues, draft merging, assigned child IDs, and server validation/response contracts are unchanged.
- Demo subtable cells use one positioned tooltip on hover or focus, unique message IDs, and input `aria-describedby`/`aria-invalid`. This preserves the user's accepted Bootstrap accessibility limitations. Custom scrollable tables should still verify clipping and overlap.
- Updated canonical documentation, Chinese translations, and `SITE_VERSION` to `0.26.0922.4`. No schema/provider changes or backend validation unification. No breaking-change changelog entry is needed for the approved presentation refinement.

## Verification

Commands use a restored task-owned Chromium installation via `PLAYWRIGHT_BROWSERS_PATH` and isolated build output to avoid locking the running application's binaries:

```powershell
dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir=<absolute-repository>/artifacts/assistant_vue297/build/default/ --filter 'FullyQualifiedName~ValidationPresentation|FullyQualifiedName~ValidationReveals|FullyQualifiedName~SubtableIssues' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=validation-presentation.trx' --results-directory artifacts/assistant_vue297/test-results/validation-presentation
dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir=<absolute-repository>/artifacts/assistant_vue297/build/default/ --filter 'FullyQualifiedName~VueInteractionBrowserTests' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=validation-presentation-all.trx' --results-directory artifacts/assistant_vue297/test-results/validation-presentation
dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir=<absolute-repository>/artifacts/assistant_vue297/build/default/ --filter 'FullyQualifiedName~LocalizedValidationMessages|FullyQualifiedName~FailedTabSaveSurvives' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=validation-presentation-updated-assertions.trx' --results-directory artifacts/assistant_vue297/test-results/validation-presentation
```

- Focused checks: 3 passed. Full suite: 51 passed and 6 failed because old assertions expected a vertical list, an always-enabled summary, a global validation alert, or the previous failed-tab accessible name. After updating those assertions for the approved presentation, all 6 passed. Production files were unchanged between the full suite and the six-case rerun; all 57 cases have passing evidence with no skips.
- Coverage includes legacy/structured deduplication, summary default/opt-in, field labels and focus, warnings, localized hostile text escaping, toast suppression/reset, subtable hover/focus visibility, unchanged row heights, matching message IDs, retained failures across tabs and independent forms, and navigation protection.
- Live Chrome: submitted an incomplete new draft, then an invalid email draft. Confirmed the demo summary is enabled, renders as a compact danger block, and has only one feedback message per invalid field with no generic top alert. The draft remained on the new-record route; the temporary tab was closed without creating a record. No existing record or saved view was changed, and no server was restarted.
- Backend/provider suites were not rerun for this frontend-only change.

## Review

Fresh independent `reviewer_high` review is routed through consumer-contract and state-integrity overlays because shared Vue templates and retained save-failure presentation are affected. Initial review and the subsequent summary audit found no blocking or supplemental findings. Accepted residual limits are copied-application presentation overrides and Bootstrap tooltip accessibility/clipping. Review loop can stop.
