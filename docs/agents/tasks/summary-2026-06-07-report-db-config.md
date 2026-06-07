## What changed
- Added `db_config` support to `FwReportsBase` so reports can use a named DB connection like `FwModel`.
- Updated `FwCustomReport` parameter option loading so SQL/table-backed lookup sources use the report runtime DB while metadata stays on the framework DB.
- Documented hardcoded-report and global custom-report `db_config` usage in `docs/reports.md`.
- Added focused report tests for default DB behavior, named report DB behavior, custom report metadata separation, and lookup SQL DB selection.

## Scope reviewed
- Read required local guidance and documentation entry points: `docs/agents/local_instructions.md`, `docs/README.md`, `docs/reports.md`, and `docs/db.md`.
- Reviewed report runtime and custom report flow: `FwReportsBase`, `FwCustomReport`, `FwReports`, `AdminReportsController`, sample report, report tests, and custom report schemas/templates.

## Commands used / verification
- `dotnet build osafw-app/osafw-app.csproj -p:OutDir=artifacts/assistant_build/` - passed, 0 warnings/errors.
- `dotnet test osafw-tests/osafw-tests.csproj -p:OutDir=artifacts/assistant_test/ --filter FullyQualifiedName~FwReportsTests` - passed, 33/33.
- `git diff --check` - passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Normalize-TextFiles.ps1 -Check ...` - touched files passed CRLF/UTF-8 checks.
- Local review using `docs/agents/code_reviewer.md` found one XML-doc cleanup for the new public optional DB parameter; fixed and reran build/tests.

## Decisions - why
- Do not persist `db_config` per custom report; custom reports can be globally forced to a named connection by setting inherited `db_config` in `FwCustomReport`.
- Keep custom report metadata on the framework DB; only report SQL and SQL/table-based parameter lookups should use the report runtime DB.
- No schema or custom report UI changes are needed because `db_config` is developer-controlled, not report-record data.

## Pitfalls - fixes
- The repo text normalizer is blocked by the default PowerShell execution policy when invoked directly; using `powershell -NoProfile -ExecutionPolicy Bypass -File ...` works.
- Custom report SQL lookup options originally used the metadata model DB; added an optional source DB parameter so report SQL/table lookups can follow the report runtime DB without moving metadata writes.

## Risks / follow-ups
- Full `dotnet test` was not run; focused report tests cover the changed report runtime behavior.
- No schema/UI change and no changelog entry needed because `db_config` is developer-controlled and additive.

## Heuristics (keep terse)
- No stable heuristics, domain facts, or ADRs added.

## Testing instructions
- Build app with isolated output: `dotnet build osafw-app/osafw-app.csproj -p:OutDir=artifacts/assistant_build/`.
- Run focused report regressions: `dotnet test osafw-tests/osafw-tests.csproj -p:OutDir=artifacts/assistant_test/ --filter FullyQualifiedName~FwReportsTests`.

## Reflection
The main ambiguity was whether custom reports needed per-row configuration; locking `db_config` as developer-controlled avoided unnecessary schema/UI churn. The useful check was tracing the custom-report parameter lookup path, because the report SQL already used inherited `db` but SQL-backed filter options did not. No sub-agent was needed for this small shared-runtime diff; the local reviewer pass was faster and caught the only cleanup.
