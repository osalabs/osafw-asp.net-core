## What changed
- Made `/My/Settings` Time Zone `auto` a real empty preference and added explicit `UTC` under it.
- Added browser timezone capture on `/My/Settings` so saving `auto` applies the detected zone to the active session.
- Preserved browser-detected timezone through MFA login for users with `auto` timezone.
- Preserved browser-detected timezone through enforced MFA setup before first login.
- Aligned `users.timezone` defaults and update SQL so existing/default `UTC` rows migrate to empty `auto`; `UTC` is now explicit going forward.
- Added targeted tests for My Settings save behavior and login session behavior.
- Updated datetime and agent domain docs for the new user timezone contract.

## Scope reviewed
- `MySettingsController` save/session flow.
- Login browser timezone capture and MFA handoff.
- `Users.doLogin` timezone update behavior.
- `users.timezone` SQL defaults and migration update path.
- Timezone select template and settings form template.

## Commands used / verification
- `dotnet build osafw-app\osafw-app.csproj` failed because IIS Express locked `osafw-app.dll`.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\` passed with 0 warnings/errors after source/schema/test changes.
- `dotnet test` failed before tests because IIS Express locked `osafw-app.dll`.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~MySettingsControllerTests|FullyQualifiedName~UsersTimezoneTests" -p:OutDir=artifacts\assistant_test_build\` passed 5/5 targeted tests.
- `dotnet test -p:OutDir=artifacts\assistant_test_build\` ran 398 tests: 394 passed, 4 failed in existing unrelated areas (`FormUtilsTests.AutocompleteParsingExtractsLeadingId`, two `FwDynamicControllerTests` prev/next tests, and `ParsePageTests.parse_string_dateTest`).
- Playwright MCP against `https://localhost:44315/My/Settings/new`: verified `auto` option has empty value, `UTC` option is directly beneath it, and hidden browser timezone field is populated.
- Review loop completed with no remaining issues after two fix passes.

## Decisions - why
- `auto` now stores an empty `users.timezone` so it can be distinguished from explicit `UTC`.
- Explicit `UTC` is no longer overwritten by browser auto-detect on login.
- Browser timezone is stored only in session for `auto`, keeping the preference portable across machines.
- Existing `UTC` rows are migrated to empty `auto` because the old UI exposed `UTC` as the visible `auto` option; users who need fixed UTC can now choose the explicit `UTC` option.

## Pitfalls - fixes
- The old `UTC|auto` option made Auto and UTC the same database value.
- The settings form did not collect browser timezone, so selecting Auto could save an empty value and fall back to the app default.

## Risks / follow-ups
- Remember-me cookie login has no browser timezone submission path, so an auto user may use the default timezone until a login/settings page captures the browser timezone.
- MFA browser timezone handoff is covered by targeted controller tests but not by an end-to-end MFA browser flow.

## Heuristics (keep terse)
- Stable framework fact added to `docs/agents/domain.md`.

## Testing instructions
- Rebuild/restart the VS/IIS Express app to load the C# controller/model changes before manual save-path testing.
- Recheck `/My/Settings`: set Time Zone to `auto`, save, and verify the session uses the browser timezone; set it to `UTC`, save, log out/in, and verify login does not overwrite it with the browser timezone.

## Reflection
- Normal build/test output is blocked while IIS Express is running; isolated output verifies compilation without stopping the user's app.
