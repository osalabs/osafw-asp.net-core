## What changed
- Added a muted sidebar stamp above `Main Dashboard` for logged-in users.
- `GLOBAL[current_time]` is now UTC and continues to render through ParsePage so it converts to the user's timezone.
- The stamp uses the short version read once from the nearest `.git/FETCH_HEAD` during `FwConfig` base settings initialization.
- Removed the temporary extra timestamp global, runtime Git HEAD/ref parsing, and custom sidebar CSS.
- Feedback pass: uppercased private `FwConfig` constants, inlined short-hash extraction, and centered the sidebar stamp at 12px-equivalent size.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/templates.md`
- `docs/layout.md`
- `docs/design_system.html` sidebar/token references
- `docs/agents/code_reviewer.md`
- `docs/agents/tasks/index.md` search for related task history
- `osafw-app/App_Data/template/layout/sidebar.html`
- `osafw-app/App_Code/fw/FW.cs`
- `osafw-app/App_Code/fw/FwConfig.cs`
- `osafw-app/App_Data/template/layout/footer.html`
- `docs/datetime.md`
- `osafw-app/osafw-app.csproj`
- `osafw-app/appsettings.json`
- `scripts/deploy_sample_v2.bat` deploy-state references

## Commands used / verification
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Search-Repo.ps1 -Pattern "Main Dashboard" -Path osafw-app\App_Data\template osafw-app\App_Code osafw-app\wwwroot docs`
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Search-Repo.ps1 -Pattern "git|version|commit|Build" -Path osafw-app\App_Code osafw-app\App_Data\template osafw-app\Program.cs osafw-app\osafw-app.csproj`
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check osafw-app\App_Code\fw\FW.cs osafw-app\App_Data\template\layout\sidebar.html osafw-app\wwwroot\assets\css\site.css docs\agents\tasks\summary-2026-06-11-sidebar-version-stamp.md`
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=$PWD\artifacts\assistant_build\` - passed, 0 warnings, 0 errors.
- Initial browser smoke check at `https://localhost:44315/Main` verified the stamp appears before `Main Dashboard` and shows `6285b8d8`; after the feedback cleanup, in-app browser inspection was blocked by Browser Use URL policy, so final UI re-check was not repeated.
- `git log -1 --oneline` and `git rev-parse --short HEAD` verified the local short hash.
- `git diff --check -- ...` passed.
- Self-review using `docs/agents/code_reviewer.md`: no remaining findings after moving version resolution into `FwConfig`, removing custom CSS, and rebuilding.
- Feedback pass verification: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=$PWD\artifacts\assistant_build\` passed with 0 warnings/errors; CRLF and `git diff --check` passed.

## Decisions - why
- Use existing `GLOBAL[current_time]` for the sidebar timestamp and set it to `DateTime.UtcNow` to match the UTC-first datetime contract.
- Store `app_version_stamp` in `FwConfig` base settings so the version is read once for the process/config lifetime.
- Read the first commit hash from `.git/FETCH_HEAD`, with a parent-directory check so dev layout `repo/osafw-app` resolves the repo-root Git metadata.
- Use Bootstrap classes (`text-muted`, `text-center`, spacing, `lh-sm`) plus inline `.75rem` font size for a 12px-equivalent muted sidebar line without adding project CSS.

## Pitfalls - fixes
- Build caught that `FW.cs` still needed `System.Collections.Concurrent` for an existing controller cache after cleanup; restored the using.
- Local repo stores `.git` one level above `osafw-app`, so `FwConfig` checks parent directories for `.git/FETCH_HEAD`.
- Removed the single-use `shortGitCommit()` helper and kept the extraction local to the Fetch Head reader.
- `apply_patch` introduced LF line endings; normalized all touched files with `docs/agents/tools/Normalize-TextFiles.ps1`.

## Risks / follow-ups
- Git hash is cached for the running process/config lifetime; this matches deploy behavior where servers pull and restart rather than editing live code.
- No changelog entry added: this is an additive sidebar display, not a breaking framework/API/schema/config change.
- No docs update added: related layout/template/design docs were reviewed, and this small built-in stamp does not change documented extension patterns.

## Heuristics (keep terse)
- No stable facts, heuristics, glossary entries, or ADRs added.

## Testing instructions
- Build: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=$PWD\artifacts\assistant_build\`
- Manual UI smoke: open/login to the local app and confirm the sidebar shows a muted datetime/hash line above `Main Dashboard`.

## Reflection
- Reading the layout/template docs first was enough; old task summaries were not needed after the index search.
- Browser verification helped catch that the app `site_root` was below the repo root and that the local short hash length was eight characters; the feedback pass further simplified this by using `.git/FETCH_HEAD`.
- The normalization helper was necessary after patch edits; future runs touching this repo should expect to normalize immediately after `apply_patch`.
- Moving stable process-level data into `FwConfig` kept `FW` request setup cleaner than per-request helper/cache code.
