## What changed
- Kept `DB.splitMultiSQL()` as the simple semicolon/`GO` splitter for interactive/simple script use.
- Added `DB.splitMultiSQLBatches()` for SQL Server scripts; it splits only on line-only `GO` separators and keeps semicolon-terminated statements in the same batch.
- Added `DB.splitMultiSQLForSqlServer()` for SQL Server execution; it preserves batches containing variables/control flow or module DDL bodies and still splits simple semicolon-delimited batches for compatibility.
- `execMultipleSQL()` now uses the SQL Server-specific splitter for SQL Server and keeps the simple splitter for non-SQL Server paths.
- Simplified the SQL Server splitter after feedback: inlined shallow helper methods and compacted the line-state scanner while keeping the same behavior.
- Added focused tests for the existing simple split behavior, simple SQL Server semicolon compatibility, the `upd2026-05-18-timezone.sql` style `IF/BEGIN` update, module DDL bodies, comments/strings containing `GO`, single-statement `IF/ELSE` branches, and SQL Server `DECLARE` variable batch scope.
- Added `docs/db.md` guidance and updated `views.sql` comments for `execMultipleSQL()` script boundaries.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/db.md`
- `osafw-app/App_Code/fw/DB.cs`
- `osafw-app/App_Data/sql/views.sql`
- `osafw-app/App_Data/sql/updates/upd2026-05-18-timezone.sql`
- `osafw-tests/App_Code/fw/DBTests.cs`
- `osafw-tests/App_Code/fw/DBOperationTests.cs`

## Commands used / verification
- Code reviewer pass 1 found that top-level semicolon splitting still broke SQL Server variable batches; fixed by adding `splitMultiSQLBatches()` and using it from SQL Server `execMultipleSQL()`.
- Code reviewer pass 2 found no issues before final simplification.
- Code reviewer pass 3 found SQL Server semicolon compatibility risk; fixed by adding `splitMultiSQLForSqlServer()` so simple batches still split on semicolon-newline while scoped batches stay intact.
- Code reviewer pass 4 found module DDL body preservation and CRLF issues; fixed by preserving SQL Server module batches only when the batch starts with module DDL, and by normalizing CRLF before closeout.
- Code reviewer pass 5 found no issues; review loop can stop.
- Feedback pass simplified `updateSqlBatchState()` and inlined `shouldPreserveSqlServerBatch()`, `startsWithSqlServerModuleDdl()`, `isGoBatchSeparator()`, and `addSplitSqlStatement()`.
- Code reviewer pass 6 found no issues after the simplification feedback; review loop can stop.
- `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=bin\assistant_tests\net10.0\ --filter "Name~MultiSQL"` - passed 8 tests; existing nullable warning in `ConvUtilsTests.cs` remained.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=bin\assistant_build\net10.0\` - passed with 0 warnings/errors.
- `git diff --check -- osafw-app\App_Code\fw\DB.cs osafw-tests\App_Code\fw\DBTests.cs docs\db.md osafw-app\App_Data\sql\views.sql docs\agents\tasks\summary-2026-05-19-execmultiple-sql.md` - passed.
- CRLF check passed for edited files.

## Decisions - why
- Kept the helper local and dependency-free instead of introducing a full SQL parser; update scripts only need practical batch splitting, not semantic SQL validation.
- Continued supporting the simple semicolon splitter for AdminDB/simple non-SQL Server scripts. For SQL Server `execMultipleSQL()`, use `GO` as the explicit batch boundary and preserve scoped or module-definition batches; simple batches still get legacy semicolon-newline splitting.
- Kept a compact state scanner instead of replacing it completely with regex because quote/comment state can cross line boundaries; regex is used for line-only `GO` and batch-preservation checks.
- Documented `GO` as the explicit batch boundary for SQL Server statements that must be first in a batch.

## Pitfalls - fixes
- The old regex split at any `;` followed by a newline, which breaks `IF/BEGIN` updates and SQL Server variables. SQL Server execution now sends scoped `GO`-delimited batches intact.
- The batch splitter still scans comments, strings, bracket identifiers, and double-quoted identifiers so a line containing `GO` inside those constructs is not treated as a separator.
- A pure GO-only split would regress simple semicolon-delimited SQL Server scripts such as `views.sql`; added the SQL Server-specific compatibility layer instead.
- Preserving every batch containing module DDL would regress `DROP VIEW; CREATE VIEW` compatibility; module DDL preservation now applies when the batch starts with the module definition.

## Risks / follow-ups
- This remains a practical script splitter, not a full SQL parser. Unusual T-SQL constructs should use explicit `GO` separators.
- No live database update application was run for this change; verification focused on splitter behavior, SQL Server batch splitting, and compile/test coverage.

## Heuristics (keep terse)
- None yet.

## Testing instructions
- Run `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=bin\assistant_tests\net10.0\ --filter "Name~MultiSQL"`.
- Run `dotnet build osafw-app\osafw-app.csproj -p:OutDir=bin\assistant_build\net10.0\` when IIS Express locks the normal Debug output.

## Reflection
- Added docs for script splitting behavior in `docs/db.md`; no domain/glossary/ADR updates needed.
