## What changed
- Fixed Vue direct delete routes by letting `FwVueController.ShowDeleteAction()` render the standard Dynamic delete confirmation instead of throwing.
- Added `/Admin/Att` / Manage Uploads to the Assistant navigation catalog and focused catalog test coverage.
- Prevented the shared feedback modal from inheriting the current edit record title.
- Added title/accessible-name hints to shared icon-only list, Vue, attachment, date-picker, report date-filter, demo date, and shell/sidebar controls.
- Removed stale `/Sys/Backup` templates.
- Fixed `/Dev/Manage` "DB Initalizer" copy and gave `/Dev/SelfTest` a browser title through a minimal raw HTML wrapper.

## Scope reviewed
- docs/drafts/uat_testing2026-06-23.md
- docs/README.md
- docs/agents/local_instructions.md (machine-local, not committed)
- docs/agents/tasks/index.md
- docs/dynamic.md, docs/templates.md, docs/assistant.md, docs/layout.md
- FwVueController/FwDynamicController delete actions
- Assistant navigation catalog and tests
- Shared feedback, list, Vue, report, date, layout, and developer templates

## Commands used / verification
- `ConvertFrom-Json (Get-Content osafw-app\App_Data\template\assistant\prompts\navigation_catalog.json -Raw) | Format-List` - catalog parses.
- `rg -n 'Sys/Backup|sys/backup|Site Backup|Perform Backup|latest_backup' osafw-app\App_Data\template osafw-app\App_Code docs\README.md docs\assistant.md docs\dynamic.md docs\templates.md` - no active backup template/doc references found.
- `docs/agents/tools/Normalize-TextFiles.ps1 -Check ...` - all touched files are UTF-8 without BOM and CRLF.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=$PWD\artifacts\assistant_build\` - passed, 0 warnings/errors.
- `dotnet test osafw-tests\osafw-tests.csproj --filter AssistantNavigationCatalog_IsValidJsonAndIncludesFrameworkScreens` - passed, 1 test.
- Chrome retest against `https://localhost:44315/` after starting the app with `dotnet run --project osafw-app --launch-profile osafw_asp_net_core --urls https://localhost:44315`.
- Chrome verified `/Admin/DemosVue/1128/delete` renders `Delete Record` with standard confirmation and no server error.
- Chrome verified Assistant response to "Where do I manage uploads?" returns Manage Uploads `/Admin/Att`.
- Chrome verified feedback modal summary is blank on `/Admin/Demos/1128/edit` while the record title remains populated.
- Chrome verified Dynamic list search/filter/customize/density title hints, report date-filter title hints, `/Dev/Manage` copy, `/Dev/SelfTest` title, and `/Sys/Backup` Page Not Found without Site Backup content.

## Decisions - why
- Reused `FwDynamicController.ShowDeleteAction()` for Vue delete confirmation to match the existing standard screen and preserve read-only/return-context behavior.
- Added the Uploads catalog entry with manager access (`80`) to match `AdminAttController.access_level` and the sidebar visibility.
- Kept feedback modal fields empty at render time because feedback is a new support item, not part of the current record edit form.
- Removed only orphaned backup templates because no active backup controller/functionality exists in the framework.
- No `docs/CHANGELOG.md` entry: changes are fixes/removal of dead template residue, not a new breaking public contract or schema/config change.
- No stable domain/glossary/heuristic additions; findings were task-specific.

## Pitfalls - fixes
- PowerShell execution policy blocked direct helper execution; reran repo helper with `powershell.exe -ExecutionPolicy Bypass -File`.
- Chrome initially hit `ERR_CONNECTION_REFUSED`; started the local app explicitly on the prior UAT URL.
- Chrome was finalized before a late shell-title tweak; did not reopen Chrome after finalization. The shell tweak is static template markup covered by static review/line-ending checks.
- A reviewer sub-agent was spawned but did not complete within the wait window; main agent performed the checklist review fallback.

## Risks / follow-ups
- Full `dotnet test` was not run; focused test plus app build and Chrome UAT checks were used.
- Assistant live result depends on local Assistant configuration/worker behavior; this run completed and returned `/Admin/Att`.
- Upload readiness issues from UAT-002 and broader health-check failures from UAT-006 were outside this requested fix set.

## Heuristics (keep terse)
- No reusable heuristics added.

## Testing instructions
- Build: `dotnet build osafw-app\osafw-app.csproj`.
- Focused test: `dotnet test osafw-tests\osafw-tests.csproj --filter AssistantNavigationCatalog_IsValidJsonAndIncludesFrameworkScreens`.
- Browser smoke: start app on `https://localhost:44315`, sign in locally, then verify `/Admin/DemosVue/{id}/delete`, `/Assistant`, `/Admin/Demos/{id}/edit`, `/Admin/DemosDynamic`, `/Admin/Reports/sample`, `/Dev/Manage`, `/Dev/SelfTest`, and `/Sys/Backup`.

## Reflection
The slowest part was Chrome retesting because the expected IIS Express URL was not running and had to be started manually under Kestrel. Future UAT fix passes should check app availability before beginning browser setup, and should avoid finalizing Chrome until after all template-only follow-up tweaks are complete. The sub-agent review did not finish in time; for similarly small, already browser-verified diffs, a local checklist review is likely more efficient unless the change has security or schema impact.
