## What changed
- Set the default `/Main` route access rule to member-only so it matches `MainController.access_level`.
- Added a config assertion that `/Main` is configured as `Users.ACL_MEMBER`.

## Scope reviewed
- Reviewed `MainController.access_level`, `FW._auth()`, `FW.callRoute()`, and `FW.controller()` route authorization behavior.
- Reviewed `osafw-app/appsettings.json` and the existing appsettings security test.

## Commands used / verification
- `dotnet test osafw-tests\osafw-tests.csproj --filter Appsettings_SentryDefaultsDoNotSendPiiOrRequestBodies` failed because IIS Express locked `osafw-app\bin\Debug\net10.0\osafw-app.dll`.
- `dotnet test osafw-tests\osafw-tests.csproj --filter Appsettings_SentryDefaultsDoNotSendPiiOrRequestBodies -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\` passed: 1/1.
- `dotnet test -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_fulltest\` ran but failed 2 unrelated/path-sensitive tests: `UploadFileSave_WritesUnderModuleFolder` due isolated `OutDir` path expectations, and `MainDashboard_MemberQueriesAreScopedToCurrentUser` due `Settings` reading DB without a configured connection.
- `curl.exe -k -s -o NUL -w "%{http_code} %{redirect_url}" https://localhost:44315/Main` returned `000`, so local browser smoke was not verified from the agent environment.
- Self-reviewed the final diff using `docs/agents/code_reviewer.md`; no issues found.

## Decisions - why
- Fixed the explicit `appSettings.access_levels` rule instead of changing core auth logic because framework comments and dispatch behavior intentionally let config override controller static access levels.
- Did not update `docs/CHANGELOG.md`; this is a default-config bug fix, not a breaking upgrade note.

## Pitfalls - fixes
- `/Main` was public because `appSettings.access_levels."/Main"` was `0`, causing `_auth()` to allow the route and skip controller-level access checks.

## Risks / follow-ups
- Manual logged-out smoke verification was not completed because the local HTTPS request returned `000`.
- Full-suite verification is not clean with isolated `OutDir`; see the two failures above.

## Heuristics (keep terse)
- Stable facts intentionally not added; the existing access-control heuristic already covers route rules and controller `access_level`.

## Testing instructions
- Focused regression check: `dotnet test osafw-tests\osafw-tests.csproj --filter Appsettings_SentryDefaultsDoNotSendPiiOrRequestBodies -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\`

## Reflection
- The auth behavior was quick to diagnose once config rules and controller static access were compared. The main slowdown was verification: the running IIS Express process locked normal build output, and isolated `OutDir` changes some path-sensitive test assumptions. Future runs should go straight to isolated `OutDir` for focused checks, but treat full-suite failures under isolated output as lower-signal unless the test is relevant to the change.
