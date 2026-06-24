## What changed
- Updated `/Assistant` progress rendering so `.assistant-progress` shows a small Bootstrap spinner while the assistant is busy or the active run is queued/processing.
- Fixed follow-up submits dropping prior visible messages by merging partial thread updates from `SaveAction` into the existing client-side thread state instead of replacing it.
- Added viewport auto-scroll to the Assistant progress line or latest message whenever the thread is rendered or a run starts, with scroll margins so the sticky composer does not cover the target.
- Strengthened scrolling after user feedback: the progress/latest-message target is centered and adjusted above the sticky composer after layout, and active runs keep a visible `Queued...` or `Processing...` label next to the spinner.
- Fixed navigation for "I want to change my password" by making `/My/Password` a personal openable catalog destination, adding prompt guidance to prefer `/My/...` routes for "my/own" requests, and adding action fallback so singleton forms are not dropped when the model asks for `edit`.
- Added regression assertions in `AssistantFeatureTests` for retry/follow-up merge behavior, spinner/scroll rendering, and the personal password navigation route.

## Scope reviewed
- `osafw-app/App_Data/template/assistant/index/main.html`
- `osafw-app/App_Data/template/assistant/index/head.css`
- `osafw-app/App_Code/models/AI/AssistantNavigationCatalog.cs`
- `osafw-app/App_Data/template/assistant/prompts/navigation.md`
- `osafw-app/App_Data/template/assistant/prompts/navigation_catalog.json`
- `osafw-tests/App_Code/fw/AssistantFeatureTests.cs`
- Related controller/service response shape in `AssistantController.SaveAction()` and `AssistantAppService.CreateOrContinueTurnAsync()`.

## Commands used / verification
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~AssistantFeatureTests|FullyQualifiedName~AssistantRuntimeStatusTests" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_ui_followup_tests\` - passed, 41/41.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_ui_followup_build\` - passed, 0 warnings/0 errors.
- `ConvertFrom-Json (Get-Content osafw-app\App_Data\template\assistant\prompts\navigation_catalog.json -Raw) | Format-List` - catalog parses.
- `Normalize-TextFiles.ps1 -Check` and `git diff --check` passed for touched files.
- Main-agent self-review of final template/CSS/navigation/test diff found no further issues; review loop stopped because the diff is a focused UI/navigation bug fix with targeted tests.
- Chrome plugin smoke was attempted but blocked because the installed Chrome plugin is missing required `scripts/browser-client.mjs`.
- Playwright smoke was attempted, but local npm is too old for `npm exec`, PowerShell blocks `npx.ps1`, and `npx.cmd` did not resolve the Playwright CLI binary.

## Decisions - why
- Kept the server response shape unchanged; `CreateOrContinueTurnAsync()` intentionally returns only the new user message for fast submit response, and the client now merges that partial state.
- Spinner rendering uses DOM node creation instead of string HTML so progress text remains escaped.
- `/My/Password`, `/My/Settings`, and `/My/MFA` now support `list` as a catalog action because their sidebar URLs are the user-facing destinations, even though their controllers redirect to `/new` internally.
- No changelog entry: this is a bug fix to unreleased/new Assistant UI behavior, not a public upgrade contract change.

## Pitfalls - fixes
- Error-path progress was initially rendered while busy state was still true; `setBusy(false)` now repaints the existing progress text when no run is active so failed submits do not leave a spinner behind.
- `.assistant-message-list` scroll position alone did not move the browser viewport on long threads; `scrollThreadToEnd()` now scrolls the page to the progress line or latest message after layout.
- `scrollIntoView({ block: 'end' })` still left the target behind the sticky composer in the real page. The scroll helper now centers the target, repeats after layout, and nudges it above the footer/composer if necessary.
- `change` inferred as `edit`, which dropped `/My/Password` because it is a singleton form. The catalog now exposes personal routes as list/open destinations and the lookup can fall back from unsupported edit/view actions to list/new.

## Risks / follow-ups
- Manual browser smoke against `https://localhost:44315/Assistant?thread_id=9` still needs working Chrome/Playwright tooling or manual verification in the already-open browser.

## Heuristics (keep terse)
- None added.

## Testing instructions
- Run the focused assistant test command above.
- Browser smoke: open an existing Assistant thread, send a follow-up, and verify older messages remain visible, the page scrolls to the progress line/latest message above the sticky composer, and the progress line shows a spinner plus `Queued...` or `Processing...` until completion.
- Ask "I want to change my password" and verify the answer includes a Change Password link to `/My/Password`, not only Admin Users.

## Reflection
The client history bug was easier to diagnose by reading the response shape than by starting with browser automation, but the scroll bug needed the user's screenshot because CSS-only scroll margins looked plausible in code review. Future agents should inspect JSON response contracts first for SPA-like state loss, then prefer a real browser check for sticky-footer viewport behavior when tooling is available. The Chrome/Playwright failures were external to the repo; record them as smoke-test gaps rather than spending time on plugin repair during a small UI fix.
