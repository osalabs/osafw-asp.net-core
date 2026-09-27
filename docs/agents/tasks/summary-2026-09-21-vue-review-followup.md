# Vue interaction review followup

## Outcome and boundaries

Reviewed PR #297 from the current diff and code before consulting the historical Vue interaction summary. Switched the primary checkout to `codex/vue-interaction-contracts` and merged `origin/master` as requested. The followup implementation is uncommitted; nothing was pushed. Unrelated work was preserved.

- Shared interactions now live in `wwwroot/assets/js/vue-interactions.js`, exported as a plain action object. Application store overrides remain last. Static resize, cell overflow, and read-only text styles live in `site.css`; table width defaults share one constant.
- Renamed the unreleased option to `is_quick_edit_keep_context`. Removed redundant pending-save state and an unused save-signature getter.
- Retained `FwDynamicController.is_new_model_save` with its purpose documented: it carries creation status into the existing virtual post-save hook after an ID is assigned, preserving override signatures.
- Acknowledged draft snapshots advance only for compound fields processed by the requested tab. Newer edits survive queued and later independent saves; new forms start with an empty acknowledgment, and object-key order cannot cause false dirty state. Child IDs and server normalization are reconciled without replacing newer edits.
- Failed-tab errors remain until that tab succeeds. Transport, authorization, and server failures use save alerts rather than manufactured field issues. Quick-edit refresh failure is a separate translated warning. Legacy `FormErrors`/`error.details` remain supported; structured validation stays in `FwVueController`.
- Failed queued width changes roll back to the last confirmed values for the captured view, while writes retain their originating API, mode, and token.

`is_edit_readonly` remains writable on creation and ignored on standard existing-row saves; it is not a promise of immutability for direct model writes or custom actions. Capability checks preserve read-only restrictions with or without RBAC. No base-controller validation unification, public hook signature change, or downstream business rule was introduced.

## Branch features and verification

| Feature | Evidence |
| --- | --- |
| Saved default/named view widths, reset, keyboard/pointer resize, auto-fit, bounds | Backend normalization/POST/ownership tests; rendered table and keyboard/pointer browser tests; four queued success/failure combinations |
| Structured errors/warnings alongside legacy validation, safe attempted values, tab/fieldset/child-row focus | Backend save/error boundary and translated-template tests; rendered browser positive/negative controls |
| Chinese built-in validation and summary messages | Actual translation file parsed by backend tests; browser checks preserve plain-text escaping |
| Controller/row permissions and read-only controls, including virtual controllers | Focused backend and browser tests, native RBAC-disabled Users permissions, separate RBAC-enabled build/tests |
| Read-only-on-edit parent and child fields | Standard create/update backend tests and rendered new/existing control tests |
| Remembered filter visibility scoped by app/controller/mode/related record | Browser state/storage checks, preserving filter values |
| Opt-in quick-edit context retention | Browser focus/selection, list refresh, unchanged triggers, refresh-failure checks |
| Concurrent/debounced saves and safe navigation | Independent forms/APIs, originating tabs, coalescing order, create destinations, unsaved parent/child changes, assigned IDs, row additions/removals, reverse tab acknowledgment, failed-tab retry tests |
| Failed delete retention | Single-row/bulk failure and successful deletion browser checks |
| Provider width schemas and updates | SQL Server/MySQL schema/update inspection, actual disposable SQLite fresh schema and legacy-column migration tests |

The final focused default run passed **104 tests, zero failures/skips** (48 browser cases and 56 backend cases). The SQLite run passed **17**, and the `isRoles` run passed **50**, each with zero failures/skips. These are overlapping configurations, not unique-test totals.

Commands ran from the repository root; absolute output paths were resolved under the following task-owned directories:

```powershell
$taskRoot = Join-Path (Get-Location).Path 'artifacts/assistant_vue297'
$env:PLAYWRIGHT_BROWSERS_PATH = "$taskRoot/browsers"
dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir="$taskRoot/build/default/" --filter 'FullyQualifiedName~VueInteractionBrowserTests|FullyQualifiedName~VueInteractionBackendTests|FullyQualifiedName~FwVueControllerTests|FullyQualifiedName~FwVirtualControllerTests|FullyQualifiedName~FwDynamicControllerTests|FullyQualifiedName~UserOwnedPreferencesSecurityTests' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=vue-interactions-complete.trx' --results-directory artifacts/assistant_vue297/test-results/default
dotnet test osafw-tests/osafw-tests.csproj -p:DefineConstants=isSQLite -p:OutDir="$taskRoot/build/sqlite/" --filter 'FullyQualifiedName~SQLiteDBTests.SQLiteSchemaScripts_CreateFreshFrameworkDatabase|FullyQualifiedName~SQLiteDBTests.SQLiteUserViewWidthsUpdate_AddsColumnToLegacySchema|FullyQualifiedName~VueInteractionBackendTests' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=sqlite.trx' --results-directory artifacts/assistant_vue297/test-results/sqlite
dotnet test osafw-tests/osafw-tests.csproj -p:DefineConstants=isRoles -p:OutDir="$taskRoot/build/roles/" --filter 'FullyQualifiedName~VueInteractionBackendTests|FullyQualifiedName~FwVueControllerTests|FullyQualifiedName~FwVirtualControllerTests|FullyQualifiedName~UserOwnedPreferencesSecurityTests' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=roles.trx' --results-directory artifacts/assistant_vue297/test-results/roles
dotnet build osafw-app/osafw-app.csproj --no-restore --verbosity minimal
```

The normal app build passed with zero warnings/errors. A matching Playwright Chromium was installed in the task-owned browser directory after an initial skipped run; the passing browser results above have no skips. Early test-fixture failures were corrected before the final run. Browser fixtures load repository Vue templates, JavaScript, and CSS, with isolated fake APIs; they are not live database end-to-end tests.

The local IIS Express app was rebuilt and restarted with explicit user approval for the command-line fallback. Its login page was verified in Chrome. Restart required process-scoped build-output launch overrides normally supplied by Visual Studio; project and IDE settings were not changed. Authenticated live testing and applying the two pending additive local updates remain awaiting login/database authorization. No configured database was mutated. SQL Server and MySQL migrations have not been executed by this followup; SQLite results do not establish those providers' runtime behavior.

## Review and upgrade

A bounded worker owned only the static interaction asset and CSS/template packet. The primary integrated save coordination, docs, and tests. A fresh independent reviewer used consumer-contract and state-integrity overlays, inspected the outcome before task summaries, and identified two create-form acknowledgment defects. Both received production-entry regression tests and fixes. Final independent re-review reproduced both fixes successfully and audited this summary without supplemental findings: no blocking findings; review loop can stop.

Canonical behavior is in `docs/dynamic.md`; the September 21 changelog entry covers the static asset/CSS copy requirement and the unreleased boolean option rename. Existing databases still need the provider's additive `upd2026-09-08-user-view-widths.sql`; do not run fresh schemas on an existing database. The historical September 8 summary was not rewritten.
