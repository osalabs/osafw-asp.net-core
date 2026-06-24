## What changed
- Added ParsePage file-template recursion protection.
- Recursive file-template includes are allowed for tree rendering until a practical depth limit is reached.
- When the depth limit is exceeded, ParsePage logs `WARN` and returns an empty value for that deeper include.
- Added focused regression coverage for `title.html` containing `<~title>` with no `title` data.
- Added focused regression coverage for sitemap-style recursive tree templates.
- Updated `docs/templates.md` pitfall note to describe the runtime guard.

## Scope reviewed
- `osafw-app/App_Code/fw/ParsePage.cs`
- `osafw-tests/App_Code/fw/ParsePageTests.cs`
- `docs/templates.md`
- `docs/agents/domain.md`
- `docs/agents/heuristics.md`
- `docs/agents/local_instructions.md`

## Commands used / verification
- `rg -n "class ParsePage|ParsePage|parsePageInstance|<~" osafw-app\App_Code osafw-app\App_Data\template docs -g "*.vb" -g "*.html" -g "*.md"`
- `rg --files | rg "ParsePage|FwLogger|Logger|Tests|\.cs$|\.vb$"`
- `git -c safe.directory=C:/DOCS_PROJ/github/osafw-asp.net-core status --short`
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~ParsePageTests` - failed because IIS Express locked `osafw-app\bin\Debug\net10.0\osafw-app.dll`.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~ParsePageTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\` - passed, 38 tests.
- Reviewer loop found raw-path recursion-key risk, LF line endings, and summary placeholders; fixes applied.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~ParsePageTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\` - passed after review fixes, 39 tests.
- `git -c safe.directory=C:/DOCS_PROJ/github/osafw-asp.net-core diff --check -- ...` - passed after CRLF normalization.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~ParsePageTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\` - passed after CRLF normalization, 39 tests.
- Second reviewer pass reported no issues; review loop can stop.
- User feedback: `/sitemap` needs legitimate recursive templates; cycle detection blocked the tree. Reworked guard to depth limiting.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~ParsePageTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\` - passed after depth-limit change, 39 tests.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~ParsePageTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\` - passed after CRLF normalization, 39 tests.
- Feedback-change reviewer pass reported no issues; review loop can stop.
- User feedback: remove the configurable `MaxTemplateRecursionDepth` option and keep a fixed `MAX_TEMPLATE_RECURSION_DEPTH` crash-protection constant. Renamed `templateRecursionDepth` to `recursionDepth`.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~ParsePageTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\` - passed after fixed-constant change, 39 tests.
- Final simplification reviewer pass reported no issues; review loop can stop.

## Decisions - why
- Track file-template recursion as an integer depth per parse call chain instead of detecting cycles, because sitemap-style templates intentionally recurse.
- Keep recursion depth as a fixed parser constant, not an app option, because it is only process crash protection and not expected to vary by app.
- Use a simple integer comparison for performance; no per-include canonical path calculation or stack scan on the hot path.
- Guard only file-template loads (`page` empty and `tpl_name` set), so inline templates and repeat bodies reuse parser paths without false positives.
- Keep missing-template behavior unchanged: empty file/missing file still renders empty without warning.
- Post-process updated `domain.md` and `heuristics.md` for depth-limited recursive templates.

## Pitfalls - fixes
- Git needs `-c safe.directory=...` in this workspace because ownership differs for the current user.
- Normal build/test output was locked by IIS Express; reran tests with isolated `OutDir` under `artifacts/assistant_test`.
- Apply-patch writes can introduce LF; convert edited files to CRLF before closing.

## Risks / follow-ups
- Full `dotnet test` not run; focused ParsePage suite passed.
- Local IIS Express on `https://localhost:44315` was not restarted, so that running instance may still serve the old parser until rebuilt/restarted.

## Heuristics (keep terse)
- 2026-05-11: For ParsePage recursion protection, prefer a file-include depth limit over cycle detection so legitimate recursive tree templates can render.

## Testing instructions
- Automated: `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~ParsePageTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\` - passed, 39 tests.
- Main affected flow: ParsePage recursive file includes for tree templates such as sitemap and runaway recursion from missing data tags.
- Setup caveat: use isolated output if IIS Express or a local app process locks `osafw-app\bin\Debug`.
- Manual caveat: restart the local IIS Express/app process before checking `https://localhost:44315/sitemap` against this change.

## Reflection
- Review loop caught a real edge case before close; later user feedback replaced cycle detection with depth limiting for legitimate recursive templates.
- Final review found no remaining issues.
- User feedback clarified that cycle detection was too strict for ParsePage's tree-template contract; depth limiting better matches intended behavior and is cheaper.
- Final feedback-change review found no remaining issues.
- Final simplification review found no remaining issues.
