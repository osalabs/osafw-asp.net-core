# Optional structured validation issues

## Outcome and scope

- Branch `codex/optional-validation-issues` starts from `codex/vue-interaction-contracts` at `bede800a`. This is a separate, additive proposal for public framework review.
- `FormErrors` / `error.details` remain the base errors. Existing codes win; warnings never become errors. Base and Dynamic controllers do not automatically convert legacy validation into issues.
- Moved the existing Vue collector into `FwController` and its field/tab/row metadata lookup into `FwDynamicController`. Standard Dynamic and Vue saves check collected errors before writing. Custom actions/plain controllers must call `validateCheckResult()` before persistence.
- `afterSave()` includes issues only when present. Only validation exceptions receive this additional response; authorization and unrelated exceptions keep their handling. Ordinary successful HTML saves reuse the existing warning flash across redirects.
- Dynamic field feedback is enabled by a form's `data-validation-issues` attribute. Standard initial HTML/autosave rendering supports escaped messages, single error feedback and non-blocking warnings. Custom AJAX/modal handlers require their own explicit helper call. No Dynamic summary, tab navigation, or save coordinator was added.
- DemosDynamic opts in. Title `validation-error` blocks saving; `validation-warning` saves with a warning. Existing validation remains unchanged for other values. Documentation retains the distinct read-only-on-edit contract.
- Updated the asset version. No schema, package, migration, or breaking-change changelog entry is needed.

## Verification

Run from the branch checkout, with installed Playwright Chromium available through `PLAYWRIGHT_BROWSERS_PATH` when not in the default cache. All output is ignored and task-owned:

```powershell
$taskOut = Join-Path (Get-Location).Path 'artifacts/optional-validation/build/default/'
dotnet build osafw-app/osafw-app.csproj -p:OutDir=$taskOut --verbosity quiet
dotnet test osafw-tests/osafw-tests.csproj -p:OutDir=$taskOut --filter 'FullyQualifiedName~OptionalValidation|FullyQualifiedName~VueInteractionBackendTests' --logger 'console;verbosity=minimal'
dotnet test osafw-tests/osafw-tests.csproj -p:OutDir=$taskOut --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=full.trx' --results-directory artifacts/optional-validation/results/full
```

- App build: passed, zero warnings/errors. Focused suite: 22 passed. Full default suite: 917 passed, zero failures/skips.
- Controller tests cover legacy-only response omission, retained error codes, blocking create/update paths, warning persistence, unrelated exception separation, sensitive-value exclusion, row metadata and HTML warning flash.
- Chromium uses production `fw.js` autosave handlers with controlled transport responses. Checks cover HTTP validation failure, success-envelope validation failure, opted-out legacy forms, escaped text, no duplicated feedback, exact row input names, independent forms and clearing on success.
- The final browser test additionally exercises a plain-text `error.details` value containing punctuation; its focused rerun passed (1 test).
- Updated stale Vue translation assertions: the current compact summary has danger/warning styling and no separate severity/value labels.
- No live database or IIS/Visual Studio changes were made. Live DemosDynamic acceptance and provider-specific builds were not run; persistence uses a memory model in focused tests. This change adds no provider-specific code.

## Review

- Public/source-copy compatibility triggered fresh independent `reviewer_high` review with the consumer-contract overlay, after focused deterministic checks. Initial review: no blocking findings. The subsequent active-summary/index audit found no supplemental issues or private-data leakage. Final adjudication: no blocking findings; review loop can stop.
