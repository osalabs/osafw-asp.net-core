## What changed
- Centralized optional compile-time feature constants in `osafw-app/osafw-app.csproj`.
- Renamed `ExcelDataReader` compile symbol to `isExcelDataReader`.
- Renamed `is_S3` compile symbol/template flag to `isS3`.
- Removed stale file-local `#define` enablement comments from related source files.
- Wrapped `builder.WebHost.UseSentry()` in `#if isSentry` so Sentry startup is enabled from the project constant.

## Scope reviewed
- Read `docs/agents/local_instructions.md`, `docs/README.md`, and `docs/naming.md`.
- Reviewed conditional compilation symbols across `osafw-app`, `osafw-tests`, docs, and templates with `rg`.
- Reviewed targeted sections in `Program.cs`, `DB.cs`, `Utils.cs`, `S3.cs`, `Users.cs`, `FwLogger.cs`, `DevManage.cs`, SQL schema comments, README, and `docs/db.md`.
- Revisited `Program.cs`, `osafw-app.csproj`, and `FwLogger.cs` for Sentry startup enablement.

## Commands used / verification
- `rg -n "is_S3|#if ExcelDataReader|#define ExcelDataReader|#define isMySQL|#define isSQLite|#define isRoles|#define isSentry|Program.cs and DB.cs|enable in Utils" osafw-app osafw-tests README.md AGENTS.md .github docs --glob "!docs/drafts/**" --glob "!docs/agents/tasks/**"` - no stale enablement/symbol matches.
- `rg -n "#if\s+([A-Za-z_][A-Za-z0-9_]*)|#elif\s+([A-Za-z_][A-Za-z0-9_]*)|DefineConstants" osafw-app osafw-tests --glob "!**/obj/**" --glob "!**/bin/**"` - confirmed active custom symbols are `isSQLite`, `isMySQL`, `isS3`, `isRoles`, `isSentry`, and `isExcelDataReader`.
- `Get-FileHash AGENTS.md, .github\copilot-instructions.md` - hashes matched after copying `AGENTS.md` byte-for-byte.
- `git diff --check` - passed.
- CRLF byte scan over touched files - no bare LF bytes found.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build_compile_symbols\` - passed, 0 warnings.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~ImportSpreadsheetNotSupportedWithoutPackage" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_compile_symbols\` - passed, 1 test; existing nullable warning in `ConvUtilsTests.cs`.
- `dotnet build osafw-app\osafw-app.csproj -p:DefineConstants=isSQLite%3BisS3%3BisRoles -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build_compile_symbols_optional\` - passed; existing `Att.cs(553)` unreachable-code warning remains when `isS3` is enabled.
- `rg -n "UseSentry|uncomment.*Sentry|Program.cs UseSentry|enable Sentry middleware" osafw-app docs README.md AGENTS.md .github --glob "!docs/drafts/**" --glob "!docs/agents/tasks/**"` - confirmed the only active `UseSentry` occurrence is the guarded call in `Program.cs`.
- Re-ran `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build_compile_symbols\` after the Sentry guard change - passed, 0 warnings.

## Decisions - why
- Kept existing `is*` naming for project-level feature constants and renamed only inconsistent symbols requested by feedback.
- Used commented `PropertyGroup` blocks in one project-file section so each optional feature can be enabled independently.
- Kept the `UseSentry()` call in source behind `#if isSentry` so enabling Sentry does not require editing `Program.cs`.

## Pitfalls - fixes
- `apply_patch` produced LF endings; normalized touched files back to CRLF to satisfy repo instructions.
- Optional `isS3` build surfaced nullable initialization for the guarded S3 client field; initialized it with null-forgiving assignment because `initClient()` owns runtime setup.
- Initial `OutDir=artifacts\...` verification wrote under project directories; removed those generated outputs and reran checks with absolute repo-root artifact paths.

## Risks / follow-ups
- Optional package-backed constants (`isMySQL`, `isSentry`, `isExcelDataReader`) still require uncommenting matching package references before building with those symbols.

## Heuristics (keep terse)
- Stable fact recorded in `docs/agents/domain.md`: optional compile-time framework features are enabled from `osafw-app/osafw-app.csproj`.

## Testing instructions
- Default app compile: `dotnet build osafw-app\osafw-app.csproj`.
- Spreadsheet fallback test: `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~ImportSpreadsheetNotSupportedWithoutPackage"`.
- Optional package-free symbol compile: `dotnet build osafw-app\osafw-app.csproj -p:DefineConstants=isSQLite%3BisS3%3BisRoles`.
- For `isMySQL`, `isSentry`, or `isExcelDataReader`, uncomment the matching package reference in `osafw-app/osafw-app.csproj` before building with the symbol.

## Reflection
- Stable framework fact was added to `docs/agents/domain.md`; no ADR needed because this is an enablement/naming cleanup, not a new architecture decision.
- First review loop found generated project-local artifacts and pending summary fields; both were fixed. Final reviewer pass found no remaining issues.
- Follow-up review of the `#if isSentry` `UseSentry()` startup change found no issues.
