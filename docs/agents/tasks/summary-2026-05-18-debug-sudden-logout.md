## What changed
- Investigating sudden logout/session loss under Visual Studio/IIS Express.
- Patched flash consumption so requests no longer write an empty `_flash` session value on every page load.
- Removed the temporary auth-denied diagnostic logging after investigation.
- Added tests covering flash session consumption behavior.

## Scope reviewed
- Existing `main.log` request sequence around a sudden access failure.
- FW auth/session redirect paths and SQL-backed session configuration.
- FW flash/session handling during request construction.

## Commands used / verification
- Read local instructions and docs map.
- Inspected recent `App_Data/logs/main.log` entries.
- Reviewed `FW.dispatch`, `_auth`, controller access checks, session middleware setup, and `fwsessions` schema.
- Playwright login and repeated Admin/DemosDynamic fetch/navigation checks against the currently running app did not reproduce the logout before patching.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\` passed.
- First targeted test run collided with the parallel build on `obj`; reran serially.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~FwTests" -p:OutDir=artifacts\assistant_test_build\` passed 7/7.
- Visual Studio MCP built `osafw-app` with 0 failed projects and launched it without debugging.
- Playwright against restarted `https://localhost:44315/`: stayed logged in through `/Admin/DemosDynamic/1128/edit`, a Dynamic demo POST, `/My/Settings`, and a 40-request concurrent burst across admin screens; no auth/access error log entries appeared afterward.
- `dotnet test -p:OutDir=artifacts\assistant_test_build\` ran 400 tests: 396 passed, 4 failed in existing unrelated areas (`FormUtilsTests.AutocompleteParsingExtractsLeadingId`, two `FwDynamicControllerTests` prev/next tests, and `ParsePageTests.parse_string_dateTest`).
- Review loop completed with no issues found.
- Removed temporary auth-denied diagnostic logging per follow-up feedback.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~FwTests" -p:OutDir=artifacts\assistant_test_build\` passed 7/7 after logging cleanup.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\` initially hit an `obj` static-web-assets file lock while the app/test build was active; reran serially with `--no-restore` and it passed with 0 warnings/errors.
- Cleanup review loop completed with no issues found.

## Decisions - why
- Current evidence points to request session loss before controller access, not a timezone conversion path.
- The framework was modifying session on every request by clearing flash through `SessionDict("_flash", [])`. With SQL-backed ASP.NET Core sessions, unnecessary writes can allow parallel requests to overwrite newer login state with an older session blob.

## Pitfalls - fixes
- Avoided reading raw session identifiers from `fwsessions`; use aggregate checks only.
- Replaced always-write flash clearing with one-time key removal when `_flash` exists.

## Risks / follow-ups
- If logout recurs, inspect `Bad access - Not authorized (2)` log entries around the failing request and verify whether the session row/cookie is being lost outside the flash-write path.

## Heuristics (keep terse)
- New behavior: missing `_flash` must not create a session key; existing `_flash` is consumed once and removed.
- Added session-write heuristic to `docs/agents/heuristics.md`.

## Testing instructions
- Rebuild/restart the VS/IIS Express app, log in, browse several admin screens, save a simple form, and watch for unexpected `Bad access`/redirects.

## Reflection
- The observed logout matches session loss before controller access. The most plausible code issue was unnecessary per-request session writes from flash cleanup, which can race with SQL-backed session state. No environment-only cause was found during this pass.
