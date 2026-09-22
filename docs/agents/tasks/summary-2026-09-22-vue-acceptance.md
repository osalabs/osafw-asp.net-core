# Vue acceptance walkthrough and row-action followup

## Outcome

Committed and pushed the previously reviewed feedback as `d71ae55e`. The requested browser acceptance walkthrough exposed action links overflowing the fixed-width controls column. The followup keeps those links inside their cell without imposing a larger fixed width on customized applications.

- Only tables using saved column widths receive `fw-list-fixed`; their action cells wrap. Automatic-layout tables retain the existing no-wrap behavior.
- Corrected row-action component prop bindings and omitted the disabled attribute for enabled links. An anchor with `disabled="false"` still matched the existing disabled-link CSS, preventing Delete and configured actions from receiving clicks.
- Kept read-only disabling, capability checks, configured actions, and prepend/append slots. Read-only custom actions also reject keyboard activation and expose their disabled state to assistive technology. No save/persistence or server authorization behavior changed.
- Bumped `SITE_VERSION` to `0.26.0922.2` for the stylesheet change. No breaking upgrade entry is needed.

## Verification

All **52 Vue browser cases passed, zero failures/skips**, with real Bootstrap and site CSS. Two added cases cover left/right controls columns, link bounds, configured actions, forwarded slots, Delete clicks, read-only pointer and Enter-key disabling, and unchanged no-width layout. The fixture uses a multiplication character for the otherwise stripped icon-only Delete link. An initial exact-label selector was corrected to allow the fixture's untranslated backtick delimiters. Isolated output compiled the app and tests without replacing Visual Studio's app binaries.

```powershell
$taskRoot = Join-Path (Get-Location).Path 'artifacts/assistant_vue297'
$env:PLAYWRIGHT_BROWSERS_PATH = "$taskRoot/browsers"
dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir="$taskRoot/build/default/" --filter 'FullyQualifiedName~VueInteractionBrowserTests' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=acceptance-reviewed.trx' --results-directory artifacts/assistant_vue297/test-results/acceptance
git diff --check
```

The user explicitly authorized local login, required additive updates, and disposable test data. Authenticated Chrome checks exercised filter visibility across reload, keyboard resizing and Ctrl+double-click fitting, named-view restoration, duplicate-email summary focus, child validation, failed Relations errors surviving a Main save, and successful child-ID reconciliation. The temporary parent record and named view were removed through the application, and the pre-test column layout was restored. Updates were already applied; none were run during this walkthrough.

Screenshots and controlled demo fixtures remain in ignored task artifacts. Controlled demos use current framework components with simulated responses; they do not establish backend authorization or database behavior. Visual Studio MCP was unreachable; the existing app was used without starting another IIS process. The running app retained its previously loaded asset version, so current-source browser fixtures provide the final CSS verification.

Fresh independent review used the ordinary high-effort reviewer and consumer-contract overlay for shared template compatibility. It identified keyboard activation of read-only custom links; the production guard and Enter-key negative control resolve that finding. The reviewer confirmed no remaining code findings. A separate worker supplied only ignored interactive demonstration fixtures; the primary agent performed the Chrome checks and captured the images.
