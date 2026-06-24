## What changed
- Reviewed the uncommitted framework changes under `osafw-app/App_Code/fw` related to date-only rendering and per-user timezone conversion.
- Updated `docs/datetime.md` to document the date-only rule across DB read/write, `FW.formatUserDateTime`, ParsePage, and dynamic form saves.
- Updated `docs/parsepage.md` and `docs/dynamic.md` so template and field-type docs distinguish calendar dates from real datetimes.
- Updated the datetime ADR and top-level changelog to reflect the new documented behavior.

## Commands that worked (build/test/run)
- `git -c safe.directory=C:/DOCS_PROJ/github/osafw-asp.net-core status --short`
- `git -c safe.directory=C:/DOCS_PROJ/github/osafw-asp.net-core diff -- osafw-app/App_Code/fw`
- `rg -n "date-only|timezone|date_popup|datetime_popup|formatUserDateTime" docs osafw-app/App_Code/fw osafw-tests`
- `$env:DOTNET_CLI_HOME='C:\\DOCS_PROJ\\github\\osafw-asp.net-core\\.dotnet'; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; dotnet vstest osafw-tests\\bin\\Debug\\net10.0\\osafw-tests.dll --TestCaseFilter:"ClassName=osafw.Tests.ParsePageTests|ClassName=osafw.Tests.FwTests|ClassName=osafw.Tests.DateUtilsTests"`
- `$env:DOTNET_CLI_HOME='C:\\DOCS_PROJ\\github\\osafw-asp.net-core\\.dotnet'; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; dotnet build osafw-app\\osafw-app.csproj --no-restore -p:LibraryRestore=False -p:OutDir=bin\\Debug\\net10.0\\_assistant_build\\`

## Pitfalls - fixes
- Repo inspection needed `git -c safe.directory=...` because the worktree owner differs from the current Windows user.
- The requested `docs/datetime.md` lives under `docs/datetime.md`, so related framework docs were updated there instead of the top-level `docs/` folder.
- `dotnet vstest` needed `DOTNET_CLI_HOME` redirected into the workspace plus first-run disable flags because the sandboxed default home was not writable.
- The regular app build was blocked first by a locked `osafw-app.dll` from IIS Express, then by offline LibMan restores; building with `-p:LibraryRestore=False` fixed the latter.
- Visual Studio MCP was still not visible in this session after restart (`list_mcp_resources` and `list_mcp_resource_templates` both returned empty), so build/test/restart had to use local commands.
- The freshly built app runs in the foreground, but background relaunch from this shell session is unreliable because Windows launcher paths in this environment collide on `Path`/`PATH`.

## Decisions - why
- Kept the review focused on the `fw` changes the user called out and only updated documentation that describes the same storage/rendering pipeline.
- Documented the distinction between SQL `date` and real `datetime` values explicitly because the implementation now depends on that semantic split across DB, FW, ParsePage, and Dynamic controllers.
- Did not change framework code because the existing diff already matches the intended behavior and test coverage for ParsePage/user formatting is present.
- Used the focused datetime/ParsePage/FW test slice instead of a full test run because that directly covers the reviewed behavior and was already available as a compiled test assembly.

## Heuristics (keep terse)
- When documenting datetime behavior in osafw, always state separately how SQL `date` values behave versus SQL `datetime` values.
- For dynamic form docs, call out save semantics for `date_popup`/`date_combo` versus `datetime_popup`; the field type carries timezone meaning.
