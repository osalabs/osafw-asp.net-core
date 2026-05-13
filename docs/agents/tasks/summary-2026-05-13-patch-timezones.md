## What changed

- Reimplemented PR #255 as framework-level timezone handling in `DB.cs`.
- Added shared `_utc` suffix detection, `datetimeoffset` detection, read normalization, and write normalization helpers.
- Helper-built insert/update/where parameters now carry field metadata from `prepareParams` instead of relying on generated parameter names.
- `_utc` `datetime`/`datetime2` values skip DB timezone conversion; SQL `date` stays date-only; normal datetime keeps DB timezone conversion.
- SQL Server `datetimeoffset` is treated as an instant: untyped output is UTC-compatible, typed DTOs may use `DateTimeOffset`.
- Raw `DateTimeOffset` parameters pass through without generic DB timezone conversion; raw `_utc` parameter names still skip conversion.
- `DB.NOW` is field-aware for helper-built SQL: UTC for `_utc`, offset-aware for SQL Server `datetimeoffset`, DB-local for ordinary datetime fields.
- SQL Server timezone autodetection now tries `CURRENT_TIMEZONE_ID()` in an isolated probe and falls back to offset detection.
- Dev Configure now reports the resolved DB timezone through DB helper behavior.
- Code generation and entity-builder paths now support SQL Server `datetimeoffset`.
- Updated datetime/DB/dynamic docs, the timezone ADR, and stable framework domain notes.

## Scope reviewed

- Reviewed PR #255 diff and Copilot comments.
- Reviewed `docs/drafts/FPF-Spec.md` for temporal/work-plan distinction relevant to timestamp semantics.
- Reviewed current timezone pipeline in `DB.cs`, `FwModel.cs`, `FW.cs`, `FwController.cs`, dynamic codegen/entity-builder paths, and existing datetime docs/ADR.
- Reviewed SQL Server-backed DB tests and current local `demo` DB behavior.

## Commands used / verification

- `Get-Content docs\agents\local_instructions.md`
- `Get-Content docs\README.md`
- `Get-Content docs\drafts\FPF-Spec.md`
- `curl.exe -L https://github.com/osalabs/osafw-asp.net-core/pull/255.diff`
- `curl.exe -L https://api.github.com/repos/osalabs/osafw-asp.net-core/pulls/255/comments`
- `curl.exe -L https://api.github.com/repos/osalabs/osafw-asp.net-core/pulls/255/reviews`
- `curl.exe -L https://api.github.com/repos/osalabs/osafw-asp.net-core/pulls/255/files`
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts/assistant_build/` - passed.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~DBReadValueTests" --artifacts-path artifacts\assistant_test_dbread` - passed, 6 tests.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~FwTests" --artifacts-path artifacts\assistant_test_fwtests` - passed, 5 tests.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~DBTests" --artifacts-path artifacts\assistant_test_dbtests` - passed, 49 tests.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts/assistant_build_fix/` - passed after review fixes.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~DBTests" --artifacts-path artifacts\assistant_test_dbtests_fix` - passed, 51 tests.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~DBReadValueTests|FullyQualifiedName~FwTests|FullyQualifiedName~DevCodeGenTests" --artifacts-path artifacts\assistant_test_targeted_fix` - passed, 23 tests.
- `dotnet test --artifacts-path artifacts\assistant_test_full_fix` - failed with 4 unrelated existing failures: `AutocompleteParsingExtractsLeadingId`, `NextAction_ReturnsNextId`, `NextAction_WrapsAroundAndKeepsMode`, and culture-sensitive `parse_string_dateTest`.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts/assistant_build_mysql/ -p:DefineConstants=isMySQL` - blocked before branch verification because MySQL package references are currently commented out.
- `git -c safe.directory=C:/DOCS_PROJ/github/osafw-asp.net-core diff --check` - no whitespace errors.

## Decisions - why

- Kept existing `fwcron` schema unchanged; support `_utc` for new/custom fields only per user choice.
- Kept `DateTime` as the default framework instant type and added targeted `DateTimeOffset` support where SQL Server columns actually carry offsets.
- Normalized helper metadata at `prepareParams` so updates with generated suffixes still use the original field name and schema type.
- Preserved raw SQL as a simple fallback: `_utc` parameter names opt out of DB timezone conversion, and `DateTimeOffset` parameters are passed through.
- Used `SYSUTCDATETIME()`/`TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')` for helper-built `_utc` `DB.NOW` paths so stored values match read semantics.

## Pitfalls - fixes

- PR #255 used generated parameter names as a proxy for field names; fixed by wrapping helper-built parameter values with field metadata.
- PR #255 SQL Server 2022 timezone probe would suppress older-server fallback; fixed by isolating `CURRENT_TIMEZONE_ID()` in its own try/catch.
- Reviewer found `DB.NOW` would store DB-local current time into `_utc` fields; fixed with field-aware SQL and SQL Server regression coverage.
- Reviewer found raw `DateTimeOffset` parameters were still converted through DB timezone; fixed with raw pass-through and regression coverage.
- Reviewer found entity-builder `datetimeoffset` subtype generated `DateTime`; fixed codegen type selection and added a regression test.
- Normal `dotnet build`/`dotnet test` wrote to locked `bin\Debug` output because IIS Express had `osafw-app.dll` open; used `OutDir`/`--artifacts-path` instead.

## Risks / follow-ups

- Full `dotnet test` still has unrelated failures listed above.
- SQL Server-backed tests depend on local `(local)` `demo` availability.
- MySQL runtime semantics are not expanded in this pass; the optional compile probe cannot run until MySQL package references are enabled.
- Raw SQL still cannot infer target column names. Callers must use `_utc` parameter names or pass `DateTimeOffset` intentionally.

## Heuristics (keep terse)

- Added a stable framework fact to `docs/agents/domain.md`.
- No reusable working heuristic was added to `docs/agents/heuristics.md`.
- No new ADR was needed beyond amending the existing timezone ADR.

## Testing instructions

- Build with isolated output when IIS Express has normal `bin\Debug` locked: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts/assistant_build/`.
- Run focused timezone checks: `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~DBTests|FullyQualifiedName~DBReadValueTests|FullyQualifiedName~FwTests|FullyQualifiedName~DevCodeGenTests" --artifacts-path artifacts\assistant_test_targeted`.
- Full suite currently runs but fails on unrelated pre-existing tests listed in `Commands used / verification`.

## Reflection

- Review loop completed with one fix pass; second reviewer pass reported no issues.
- Documentation was updated for public framework behavior, raw SQL conventions, `datetimeoffset`, and `DB.NOW` semantics.
