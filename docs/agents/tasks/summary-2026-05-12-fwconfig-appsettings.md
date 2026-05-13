## What changed
- Cleaned up `FwConfig.settingsForEnvironment()` so startup config reads direct children of `appSettings` into a flat settings dictionary.
- Replaced the nested `appSettings` workaround with direct use of the shared appSettings section loader.
- Removed the unnecessary `readSettingsSection()` wrapper and single-use `readAppSettings()` helper after feedback to keep the code concise.
- Added regression coverage for flat startup settings and environment overrides.

## Scope reviewed
- Reviewed `docs/agents/local_instructions.md`, `docs/README.md`, `docs/naming.md`, `docs/agents/code_reviewer.md`.
- Reviewed `osafw-app/App_Code/fw/FwConfig.cs`, `osafw-app/Program.cs`, `osafw-app/appsettings.json`, and `osafw-tests/App_Code/fw/FwConfigTests.cs`.
- Updated `docs/agents/domain.md` and `docs/agents/heuristics.md` for stable knowledge capture.

## Commands used / verification
- `dotnet test osafw-tests\osafw-tests.csproj --filter FwConfigTests --no-restore` failed because IIS Express locked `osafw-app\bin\Debug\net10.0\osafw-app.dll`.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FwConfigTests --no-restore -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\` passed: 5 tests, 0 failed.
- `dotnet build osafw-app\osafw-app.csproj --no-restore -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build\` passed: 0 warnings, 0 errors.
- `git -c safe.directory=C:/DOCS_PROJ/github/osafw-asp.net-core diff --check -- osafw-app/App_Code/fw/FwConfig.cs osafw-tests/App_Code/fw/FwConfigTests.cs docs/agents/tasks/summary-2026-05-12-fwconfig-appsettings.md docs/agents/domain.md docs/agents/heuristics.md` passed.
- Code reviewer sub-agent found no issues; review loop can stop.
- 2026-05-13 feedback pass: `dotnet test osafw-tests\osafw-tests.csproj --filter FwConfigTests --no-restore -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\` passed: 5 tests, 0 failed.
- 2026-05-13 feedback pass: parallel app build failed with `CS2012` because it contended with the test build on `osafw-app\obj\Debug\net10.0\osafw-app.dll`; reran sequentially.
- 2026-05-13 feedback pass: `dotnet build osafw-app\osafw-app.csproj --no-restore -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build\` passed: 0 warnings, 0 errors.
- 2026-05-13 feedback pass: final `git diff --check` passed for all touched files.
- 2026-05-13 reviewer sub-agent found no issues; review loop can stop.

## Decisions - why
- Used the same child-section loading shape for startup settings that request settings already use, because supported config files and docs expose `appSettings` as the root container, not as a runtime settings key.
- Did not add legacy support for `appSettings.appSettings`; the nesting was caused by the old helper call pattern rather than by a documented config shape.
- Kept `applyAppSettings()` because both base host settings and startup environment settings use it; removed single-use helpers.

## Pitfalls - fixes
- `readSettingsSection(cfg.GetSection("appSettings"), ...)` wraps values under `appSettings`; loading `GetChildren()` avoids creating that artificial key.
- Build and test commands for this solution should not run concurrently because they share project `obj` paths even when `OutDir` differs.

## Risks / follow-ups
- Normal `bin\Debug` output is locked by IIS Express on this machine; use isolated `OutDir` while that process is running.

## Heuristics (keep terse)
- Added flat `appSettings` runtime shape fact to `docs/agents/domain.md`.
- Added heuristic to avoid reintroducing an `appSettings` child key in runtime settings to `docs/agents/heuristics.md`.

## Testing instructions
- Run `dotnet test osafw-tests\osafw-tests.csproj --filter FwConfigTests --no-restore -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\`.

## Reflection
- Stable flat-settings fact captured in agent docs; no ADR needed because this preserves the documented config contract.
