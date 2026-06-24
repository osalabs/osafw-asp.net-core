# Framework Upgrade Prompt

The latest framework source is in `<FRAMEWORK_REPO_PATH>`.
This project is based on an older version of that framework and needs to be upgraded to the latest framework code.

Do a framework upgrade, not a blind overwrite.

## Inputs

- `<FRAMEWORK_REPO_PATH>`: path to latest framework repo, for example `C:\projects\osafw-asp.net-core`
- `<APP_SPECIFIC_MARKER>`: marker for retained framework customizations, for example `APP SPECIFIC`, `CLIENT SPECIFIC`, or `<PROJECT_CODE> SPECIFIC`
- `<APP_LOG_PATH>`: app log path, for example `osafw-app\App_Data\logs\main.log`

## Work

1. Follow the target repo instructions, line-ending policy, coding style, task-summary rules, and review workflow.
2. Compare this project's current framework files against the framework repo.
3. Identify the best old upstream baseline commit/version for this project, preferably by file/hash comparison.
4. Review `docs/CHANGELOG.md` before merging framework files so breaking changes, new SQL updates, renamed public classes, and required app-side follow-up are explicit.
5. Use the upstream delta from that baseline to latest as guidance, but also do a broader latest-framework-vs-current-project comparison.
6. Direct-copy framework files only when the project copy still matches the old baseline and has no app-specific changes.
7. Hand-merge framework files that have project-specific customizations. Preserve still-relevant app behavior and mark retained/custom reimplementation comments as `<APP_SPECIFIC_MARKER>`.
8. Add any new framework files, templates, scripts, SQL updates, package references, and config changes needed by the latest framework.
9. For schema changes, update both fresh-install schema files and additive update/migration scripts where applicable.
10. Preserve project business behavior and compatibility. Treat existing app-specific logic as authoritative unless the task explicitly changes product behavior.
11. If the app repo still carries inherited framework implementation tests, run `app_test_bootstrap_cleanup.md` after framework files are merged so the app keeps app-specific tests and a small smoke suite instead of the full upstream framework suite.

## Verification

- Build with Visual Studio MCP where available: solution/project info, build, build status, and Error List.
- If Visual Studio MCP is unavailable or unnecessary, run the repo's focused `dotnet build` or `dotnet test` command.
- If the app is running or can be safely started, apply hot reload/restart as needed and run focused smoke checks for affected flows.
- Check `<APP_LOG_PATH>` for new errors/warnings after build/runtime smoke when the log path exists.
- Run a review pass focused on lost app customizations, schema/update gaps, package/version issues, runtime behavior, and file/line-ending hygiene.
- Fix review findings and repeat until no material issues remain.

## Closeout

Summarize the detected baseline, applied upstream range, relevant `docs/CHANGELOG.md` items handled, important preserved customizations, verification run, and smoke checks not completed.
