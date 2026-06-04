# Apply framework updates

The latest framework source is in `<FRAMEWORK_REPO_PATH>`.
This project is based on an older version of that framework and needs to be upgraded to the latest framework code.

Do a framework upgrade, not a blind overwrite:
1. Compare this project's current framework files against the framework repo.
2. Identify the best old upstream baseline commit/version for this project, preferably by file/hash comparison.
3. Review `docs/CHANGELOG.md` before merging framework files so breaking changes, new SQL updates, renamed public classes, and required app-side follow-up are explicit.
4. Use the upstream delta from that baseline to latest as guidance, but also do a broader latest-framework-vs-current-project comparison.
5. Direct-copy framework files only when the project copy still matches the old baseline and has no app-specific changes.
6. Hand-merge any framework files that have project-specific customizations. Preserve still-relevant app behavior and mark retained/custom reimplementation comments as `<APP_SPECIFIC_MARKER>`.
7. Add any new framework files, templates, scripts, SQL updates, package references, and config changes needed by the latest framework.
8. For schema changes, update both fresh-install schema files and additive update/migration scripts where applicable.
9. Preserve project business behavior and compatibility. Treat existing app-specific logic as authoritative unless the task explicitly changes product behavior.
10. Follow repo instructions, line-ending policy, coding style, and task-summary/review workflow if present.

After implementation:
- Build with Visual Studio MCP where available: solution/project info, build, build status, and Error List.
- If the app is running or can be safely started, apply hot reload/restart as needed and run focused smoke checks for affected flows.
- Check `<APP_LOG_PATH>` for new errors/warnings after build/runtime smoke.
- Run a review pass focused on lost app customizations, schema/update gaps, package/version issues, runtime behavior, and file/line-ending hygiene.
- Fix review findings and repeat until no material issues remain.
- Summarize the detected baseline, applied upstream range, relevant `docs/CHANGELOG.md` items handled, important preserved customizations, verification run, and any smoke checks not completed.

Placeholders:
- `<FRAMEWORK_REPO_PATH>`: path to latest framework repo, e.g. `C:\projects\osafw-asp.net-core`
- `<APP_SPECIFIC_MARKER>`: project marker for retained framework customizations, e.g. `APP SPECIFIC`, `CLIENT SPECIFIC`, or `<PROJECT_CODE> SPECIFIC`
- `<APP_LOG_PATH>`: app log path, e.g. `osafw-app\App_Data\logs\main.log`