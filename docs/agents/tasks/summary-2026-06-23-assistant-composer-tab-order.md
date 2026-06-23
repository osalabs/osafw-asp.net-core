## What changed
- Reordered both Assistant composer action rows so `Send` is the first tab stop after the textarea and `Files` remains available after Send.
- Restored the visual layout so Files remains on the left and Send remains at the right edge.

## Scope reviewed
- `/Assistant` landing composer and thread footer composer in `assistant/index/main.html`.

## Commands used / verification
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~AssistantFeatureTests" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_composer_tab_tests\`
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_composer_tab_build\`
- `powershell -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 ... -Check`
- `git diff --check`

## Decisions - why
- Kept DOM order as prompt, Send, Files for native keyboard behavior.
- Used Bootstrap flex ordering for the Files block so the visual order stays Files-left and Send-right without JavaScript or positive `tabindex`.
- Kept Files reachable by mouse, drag/drop, and the next tab stop after Send.

## Pitfalls - fixes
- `apply_patch` produced LF endings; normalized touched files to UTF-8 without BOM and CRLF.

## Risks / follow-ups
- No `docs/CHANGELOG.md` entry added because this only changes unreleased Assistant UI control order.
- Browser manual tabbing was not run; static DOM-order coverage plus focused tests/build passed.

## Heuristics (keep terse)
- None added.

## Testing instructions
- Focused Assistant tests passed: 41 passed, 0 failed.
- App build passed: 0 warnings, 0 errors.
- Formatting checks passed.

## Reflection
Changing DOM order plus flex visual ordering was simpler and less brittle than intercepting Tab in JavaScript. Future UI keyboard-order fixes should prefer native DOM order first, use layout classes to preserve visuals when needed, and only use scripting when the desired interaction cannot be represented in markup. Direct self-review was sufficient for this small template-only runtime change.
