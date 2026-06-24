## What changed
- Fixed `Utils.urlescape()` so URL-encoded output is not lowercased after encoding.
- Added regression coverage for mixed-case URL path/query values so case-sensitive route and token data remains intact.

## Scope reviewed
- `osafw-app/App_Code/fw/Utils.cs`
- `osafw-tests/App_Code/fw/UtilsTests.cs`
- `docs/agents/heuristics.md`
- Code reviewer sub-agent reviewed the final diff and found no issues.

## Commands used / verification
- `dotnet test osafw-tests\osafw-tests.csproj --filter urlescape` - failed because IIS Express locked `osafw-app\bin\Debug\net10.0\osafw-app.dll`.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~urlescape -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\` - passed, 2 tests.
- `git diff --check -- osafw-app\App_Code\fw\Utils.cs osafw-tests\App_Code\fw\UtilsTests.cs docs\agents\heuristics.md docs\agents\tasks\summary-2026-05-15-urlescape-case-preservation.md` - passed.

## Decisions - why
- Removed broad `ToLowerInvariant()` instead of normalizing percent-escape hex digits because URL path/query data can be case-sensitive.
- Used a decode round-trip assertion for the regression test so the test guards semantic URL preservation without depending on percent-hex letter casing.

## Pitfalls - fixes
- Existing tests only covered lowercase safe characters and lowercase percent escapes, so they accepted broad lowercasing.

## Risks / follow-ups
- Existing callers that compared encoded strings byte-for-byte with lowercased percent escapes should be checked if such comparisons exist; URL semantics are now preserved.
- Full test suite was not run; targeted URL escaping tests compiled the app and test project through an isolated output directory.

## Heuristics (keep terse)
- Do not normalize whole encoded URLs; only normalize explicitly bounded encoding syntax when required.
- Added matching reusable heuristic to `docs/agents/heuristics.md`.

## Testing instructions
- Targeted regression: `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~urlescape -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\`

## Reflection
- Added a reusable heuristic. No domain fact or ADR needed for this narrow framework bug fix.
