## What changed
- Removed redundant `.Cast` calls where the receiver is now strongly typed (`FwDict.Keys`, `FwList`, `StrList`, and parser logger `string[]`).
- Fixed two unsafe direct casts found during the sweep:
  - `FwList(ICollection)` now delegates through the legacy `IEnumerable` filtering constructor instead of casting to `IEnumerable<FwDict>`.
  - `DB.limit` TOP fallback now wraps `List<DBRow>` slices in a new `DBList` instead of casting `List<DBRow>` to `DBList`.
- Updated `GlobalUsings.cs` comments to reflect that `FwList` is `List<FwDict>`, not `List<object?>`.
- Removed UTF-8 BOM markers from detected tracked repo files and the non-ignored untracked `osafw-app/osafw-app.csproj.user`; no repo text files need the BOM.
- Added `.editorconfig` `charset = utf-8` for common text-file extensions so editors preserve UTF-8 without BOM going forward.

## Scope reviewed
- `FwCollections.cs`, `GlobalUsings.cs`, all `.Cast` call sites in `osafw-app` and `osafw-tests`, and nearby direct/as-cast patterns.

## Commands used / verification
- `rg -n "\.Cast" -g "*.cs" osafw-app osafw-tests`
- `rg -n "\bas\s+(FwDict|FwList|DBRow|DBList|List<FwDict>|IList|IDictionary)" -g "*.cs" osafw-app osafw-tests`
- `rg -n "\((FwDict|FwList|DBRow|DBList|IList|IDictionary|IEnumerable)\)" -g "*.cs" osafw-app osafw-tests`
- BOM scan/removal:
  - `git -c safe.directory=C:/DOCS_PROJ/github/osafw-asp.net-core ls-files`
  - `git -c safe.directory=C:/DOCS_PROJ/github/osafw-asp.net-core ls-files --others --exclude-standard`
  - Removed only the initial `EF BB BF` bytes from detected files.
- Verified `TrackedBOM=0` and `UntrackedNonIgnoredBOM=0`.
- Verified no touched tracked file has LF-only line endings.
- Verified `.editorconfig` has no BOM and no LF-only line endings after adding `charset = utf-8`.
- `dotnet build osafw-app\osafw-app.csproj` failed because IIS Express locked `osafw-app\bin\Debug\net10.0\osafw-app.dll`.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\` passed with 0 warnings/errors.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_build\` passed with 0 warnings/errors after BOM cleanup.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~FwCollectionsTests|FullyQualifiedName~FormUtilsTests|FullyQualifiedName~UtilsTests" -p:OutDir=artifacts\assistant_test_build\` failed on existing `FromUtilsTests.AutocompleteParsingExtractsLeadingId`.
- Exact reruns passed:
  - `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName=osafw.Tests.FromUtilsTests.FilterTest" -p:OutDir=artifacts\assistant_test_build\`
  - `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~osafw.Tests.FwCollectionsTests" -p:OutDir=artifacts\assistant_test_build\`
  - `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName=osafw.Tests.UtilsTests.PrepareRowsHeaders_AddsMissingHeadersAndCols" -p:OutDir=artifacts\assistant_test_build\`
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName=osafw.Tests.FromUtilsTests.FilterTest|FullyQualifiedName~osafw.Tests.FwCollectionsTests|FullyQualifiedName=osafw.Tests.UtilsTests.PrepareRowsHeaders_AddsMissingHeadersAndCols" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test_build\` passed 5 tests after BOM cleanup.
- Verified edited files use CRLF line endings.
- Code reviewer sub-agent final scoped pass: no issues found; review loop can stop.
- BOM cleanup reviewer pass: no issues found; `git diff --check` clean; review loop can stop.

## Decisions - why
- Keep `.Cast` calls that are still needed for non-generic `IDictionary`/`IList` API boundaries.
- Use typed keys directly on `FwDict` because it inherits `Dictionary<string, object?>`.
- Use `toStr()` for `FwDict.Values` in list-map sorting because values are still object-typed even when keys are strongly typed.

## Pitfalls - fixes
- `apply_patch` introduced mixed LF on touched hunks; normalized edited files back to CRLF and restored original UTF-8 BOMs where present.
- Later BOM cleanup intentionally removed those BOMs; two files that were LF-only after byte cleanup were normalized to CRLF.
- Normal app build is blocked by a running IIS Express process; used documented isolated build output.
- Broad focused test filter also ran an unrelated autocomplete parser test that currently fails; reran exact touched tests separately.
- Reviewer noted `osafw-app/App_Data/template/admin/fwupdates/config.json` has unrelated pre-existing edits/duplicate JSON key; this file was already modified before this task and was not changed here.

## Risks / follow-ups
- Two `.Cast` calls intentionally remain:
  - `FW.sendEmail` attachment filenames use `IDictionary.Keys`.
  - `FormUtils.col2comma_str` accepts a non-generic `IList`.
- Existing failing test: `osafw.Tests.FromUtilsTests.AutocompleteParsingExtractsLeadingId` expects `123` for `"123 - Test Value"` but currently gets `0`.
- Existing unrelated modified file: `osafw-app/App_Data/template/admin/fwupdates/config.json`.
- Ignored `.vs` IDE/cache files were not modified; they are outside the repository file set.

## Heuristics (keep terse)
- No reusable heuristic added; this was a local typed-collection cleanup.

## Testing instructions
- Use the isolated build command if IIS Express is running: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\`.
- For touched tests, run the exact filters listed above. The broader `FromUtilsTests` class currently has an unrelated autocomplete failure.
- For BOM verification, scan tracked plus non-ignored untracked files; ignored `.vs` caches are intentionally excluded.

## Reflection
- No stable domain facts, glossary terms, ADRs, or agent heuristics were added. No `AGENTS.md` update needed.
