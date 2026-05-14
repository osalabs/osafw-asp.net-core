## What changed
Implemented `offset` / `limit` support for `DB.array()` and `DB.array<T>()`, with consistent `offset, limit` parameter order.
- Added provider-aware paging SQL for SQL Server, MySQL/DB2 LIMIT syntax, and TOP-based client-trim fallback.
- Wired `FwModel.listByWhere()` and `FwModel<TRow>.listTByWhere()` through to the new `DB.array()` paging arguments.
- Updated DB/CRUD docs and added paging tests.

## Scope reviewed
- `docs/README.md`
- `docs/agents/local_instructions.md`
- `docs/agents/code_reviewer.md`
- `docs/drafts/FPF-Spec.md` TOC and relevant API/selection boundary sections during planning
- `osafw-app/App_Code/fw/DB.cs`
- `osafw-app/App_Code/fw/FwModel.cs`
- `osafw-app/App_Code/fw/FwModel.Generic.cs`
- `docs/db.md`
- `docs/crud.md`
- Existing DB/model tests under `osafw-tests/App_Code/fw/`

## Commands used / verification
- `dotnet test` - compiled, then failed on 4 unrelated existing tests: `AutocompleteParsingExtractsLeadingId`, `NextAction_ReturnsNextId`, `NextAction_WrapsAroundAndKeepsMode`, and `parse_string_dateTest`.
- `dotnet test --filter "FullyQualifiedName~DBOperationTests|FullyQualifiedName~DBTests"` - passed, 69 tests.
- `dotnet build osafw-app\osafw-app.csproj` - passed.
- `git diff --check` - passed.
- Code reviewer sub-agent loop: first pass found model parameter-order compatibility concern and LF line endings; second pass found no issues after CRLF fix and recorded the user-approved model order decision.

## Decisions - why
- Use `offset, limit` order consistently for DB/model paging helpers to match existing `selectRaw()` and SQL/MySQL paging convention.
- Append optional parameters to `DB.array()` because framework consumers recompile from source and no app-level `DB` subclasses are expected.
- Keep `arrayp()` unchanged because raw SQL paging stays caller-owned.
- Keep SQL Server limit-only as `TOP` to preserve existing SQL shape; use `OFFSET ... FETCH NEXT` only for offset pages.
- Kept `FwModel.listByWhere()` and `FwModel<TRow>.listTByWhere()` order as `offset, limit` after reviewer flagged positional compatibility, because the user explicitly requested consistent order and repo search found no existing call sites using those paging parameters.

## Pitfalls - fixes
- Existing `FwModel.listByWhere()` and `FwModel<TRow>.listTByWhere()` accepted paging parameters but ignored them; implementation will wire them through.
- SQL Server offset paging requires `ORDER BY`; validation will fail early for offset without order.
- TOP-based providers cannot express offset directly; implementation over-fetches `offset + limit` rows and trims locally, matching the previous `selectRaw()` fallback.
- First review found edited files had LF endings; fixed by re-saving changed files with CRLF before closing.

## Risks / follow-ups
- Full `dotnet test` still has unrelated existing failures outside this change.
- Highest-risk follow-up not run: real MySQL/OLE integration smoke tests; SQL generation is covered, but only SQL Server integration tests ran locally.

## Heuristics (keep terse)
- None added yet.

## Testing instructions
- For this change: `dotnet test --filter "FullyQualifiedName~DBOperationTests|FullyQualifiedName~DBTests"`.
- Build app: `dotnet build osafw-app\osafw-app.csproj`.
- Full suite currently has unrelated failures noted above.

## Reflection
- No stable domain facts, reusable heuristics, or ADRs were added; this is a small framework API completion rather than a new architectural decision.
