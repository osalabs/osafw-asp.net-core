## What changed
- Reviewed DB generic single-row nullability through the FPF lens: dictionary rows and typed DTO rows are separate bounded contexts with different honest absence signals.
- Changed typed `DB.row<T>` / `DB.rowp<T>` to return `T?` and `null` when no record is found.
- Kept non-generic row methods returning empty `DBRow` and documented the split contract.

## Scope reviewed
- `docs/drafts/FPF-Spec.md` for bounded-context, boundary, and evidence/framing discipline.
- `DB`, typed model wrappers, demo typed-row usage, DB tests, CRUD/DB docs, and agent/reviewer guidance.

## Commands used / verification
- `dotnet build osafw-app\osafw-app.csproj` - normal output failed because IIS Express locked `osafw-app\bin\Debug\net10.0\osafw-app.dll`.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build\` - passed, 0 warnings/errors.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~DBTests --no-restore -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_build\` - passed, 47 tests. Build emitted existing `CS8602` in `osafw-tests\App_Code\fw\ConvUtilsTests.cs`.
- Checked CRLF line endings for touched files.
- Code reviewer sub-agent reviewed the final task diff and reported no issues; review loop can stop.
- After user review feedback, simplified typed model cache guards to use `!isRowEmpty(row)` directly and added `NotNullWhen(false)` so nullable flow analysis understands the virtual hook. Re-ran the isolated app build and focused DB tests with the same results.
- Follow-up code reviewer sub-agent reviewed the cache-guard simplification and reported no issues; review loop can stop.

## Decisions - why
- Non-generic dictionary rows keep empty-row semantics because callers can inspect `Count` and avoid null checks.
- Typed single-row reads use `null` because a default DTO can look like a valid record populated with default property values.
- `*OrFail` remains the required-record path and throws instead of returning null.

## Pitfalls - fixes
- `FwModel<TRow>.oneTOrFail` depended on `isRowEmpty`, but DB never returned null for typed misses; the DB layer now preserves the missing-row signal.
- Avoid stacking `row != null` with `isRowEmpty(row)` because the hook already accepts null. Keep `isRowEmpty` for protected virtual compatibility and annotate it for flow analysis.

## Risks / follow-ups
- This is a public framework API change for typed `DB.row<T>` / `rowp<T>` callers; compile-time nullable warnings should point callers to handle misses explicitly.
- Highest-risk follow-up not run: full solution test suite. The focused DB tests exercise the changed behavior, but the full suite was left out to keep scope proportional.

## Heuristics (keep terse)
- Dictionary row absence: empty row. Typed row absence: null.

## Testing instructions
- Build to isolated repo-root output if IIS Express keeps normal `bin\Debug` locked:
  `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build\`
- Focused regression:
  `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~DBTests --no-restore -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_build\`

## Reflection
- Stable framework fact added to `docs/agents/domain.md` and working heuristic added to `docs/agents/heuristics.md`.
