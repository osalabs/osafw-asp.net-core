## What changed
- Added `appSettings.is_fwupdates_auto_apply` with default `true`; `FwUpdates.checkApplyIfDev()` now skips the Home-page update redirect when the flag is disabled.
- Updated generated virtual/dynamic controller layout placement so wide content stays in the primary column, compact fields on larger forms balance across content columns, and metadata remains in the right-side column.
- Applied follow-up feedback so generated layouts render only two columns; legacy/explicit middle buckets collapse into the right-side column.
- Applied Lookup Manager follow-up feedback so major lookup fields `iname` and `icode` stay in the left/primary column while minor `prio` stays right-side.
- Applied Permissions edit follow-up feedback so right-side support fields render below `id` but above bottom metadata, and compact balancing keeps the right column visually lighter than the main column.
- Added focused tests for the FwUpdates flag and DevCodeGen layout behavior.
- Documented the generated layout heuristic and the FwUpdates dev auto-apply flag.
## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/templates.md`
- `docs/layout.md`
- `docs/dynamic.md`
- `osafw-app/App_Code/controllers/Home.cs`
- `osafw-app/App_Code/fw/FwUpdates.cs`
- `osafw-app/App_Code/fw/FwVirtualController.cs`
- `osafw-app/App_Code/fw/FwVueController.cs`
- `osafw-app/App_Code/models/Dev/CodeGen.cs`
- `osafw-app/App_Data/template/common/virtual/*`
- `osafw-app/App_Data/template/common/vue/*`
- `osafw-app/App_Data/template/admin/fwupdates/config.json`
## Commands used / verification
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~DevCodeGenTests` failed because IIS Express locked `osafw-app\bin\Debug\net10.0\osafw-app.dll`.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~DevCodeGenTests -p:OutDir=artifacts\assistant_test\` passed: 5 tests.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~FwUpdatesTests -p:OutDir=artifacts\assistant_test\` passed: 3 tests.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\` passed with 0 warnings/errors.
- After reviewer fixes, reran `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~FwUpdatesTests -p:OutDir=artifacts\assistant_test\`; passed 3 tests.
- After reviewer fixes, reran `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~DevCodeGenTests -p:OutDir=artifacts\assistant_test\`; passed 5 tests.
- After reviewer fixes, reran `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\`; passed with 0 warnings/errors.
- Removed temporary `osafw-app\artifacts` and `osafw-tests\artifacts` verification outputs after the checks.
- Reviewer sub-agent first pass found two issues; both were fixed. Second review pass found no issues and said the review loop can stop.
- Follow-up feedback reported the generated `/Admin/FwUpdates` screen produced three columns with only `Applied Time` in the middle; changed the layout generator to render only primary and right-side columns.
- After the two-column follow-up, reran `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~DevCodeGenTests -p:OutDir=artifacts\assistant_test\`; passed 7 tests.
- After the two-column follow-up, reran `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~FwUpdatesTests -p:OutDir=artifacts\assistant_test\`; passed 3 tests.
- After the two-column follow-up, reran `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\`; passed with 0 warnings/errors.
- Removed temporary `osafw-app\artifacts` and `osafw-tests\artifacts` verification outputs after the follow-up checks.
- Follow-up reviewer found missing method-boundary coverage for `checkApplyIfDev()`; added a spy-model regression test.
- After adding the `checkApplyIfDev()` regression test, reran `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~FwUpdatesTests -p:OutDir=artifacts\assistant_test\`; passed 4 tests.
- After adding the regression test, reran `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~DevCodeGenTests -p:OutDir=artifacts\assistant_test\`; passed 7 tests.
- After adding the regression test, reran `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\`; passed with 0 warnings/errors.
- Removed temporary `osafw-app\artifacts` and `osafw-tests\artifacts` verification outputs after the final checks.
- Second reviewer pass found no issues; noted the duplicate `is_dynamic_index_edit` key in already-dirty `fwupdates/config.json` is outside this task and does not need to keep the review loop open.
- Follow-up feedback reported `iname` and `icode` were still being balanced into the right column on Lookup Manager virtual controllers; pinned those major fields to the primary column and `prio` to the right-side column.
- After Lookup Manager follow-up, reran `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~DevCodeGenTests -p:OutDir=artifacts\assistant_test\`; passed 9 tests.
- After Lookup Manager follow-up, reran `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\`; passed with 0 warnings/errors.
- Removed temporary `osafw-app\artifacts` and `osafw-tests\artifacts` verification outputs after retrying a transient locked test output file.
- Lookup Manager follow-up reviewer pass found no issues; review loop can stop.
- Follow-up feedback reported `Resource` on `/Admin/Permissions/3/edit` appeared at the bottom of the right column and made the right column visually taller; changed right-column ordering and compact-field balancing.
- After Permissions follow-up, first `DevCodeGenTests` run found an outdated assertion for right-column ID ordering; updated the expected order.
- After Permissions follow-up, reran `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~DevCodeGenTests -p:OutDir=artifacts\assistant_test\`; passed 11 tests.
- After Permissions follow-up, reran `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\`; passed with 0 warnings/errors.
- Removed temporary `osafw-app\artifacts` and `osafw-tests\artifacts` verification outputs after the Permissions follow-up checks.
- Permissions follow-up reviewer pass found no issues; review loop can stop.
## Decisions - why
- Add a developer config flag in `FwUpdates.checkApplyIfDev()` so Home visits can skip the dev auto-update redirect before scanning/applying updates.
- Improve the generated virtual/dynamic controller layout heuristic in `DevCodeGen` instead of special-casing `/Admin/FwUpdates`.
- Preserve current behavior by defaulting `is_fwupdates_auto_apply` to `true`; developers can set it to `false` locally.
- Keep explicit `UI: formcol=left|mid|right` as the highest-priority layout override.
- Treat `formcol=mid` as a right-side placement because generated forms now have a strict two-column contract.
- Treat `iname` and `icode` as major lookup fields that should not participate in compact-field balancing; treat `prio` as a right-side minor ordering field.
- Keep right-side bottom metadata (`prio`, `status`, `add_time`, `upd_time`) below support fields, and place compact fields right only when the right column remains visually lighter after the placement.
## Pitfalls - fixes
- Git status requires `safe.directory`; used command-local `-c safe.directory=...` rather than changing global machine config.
- Normal `dotnet test` output was locked by IIS Express; used isolated `OutDir` for build/test verification.
- Reviewer found disabling auto-apply also skipped update discovery; fixed `checkApplyIfDev()` so dev Home still loads update files and only the redirect is gated.
- Reviewer found docs overstated `class` / `class_contents` as placement overrides; clarified that only `UI: formcol=left|mid|right` moves generated columns.
- Follow-up fixed the generated layout still being able to render three columns by clamping added fields and final layout buckets to the primary/right-side pair.
- Follow-up fixed lookup major fields being balanced right by adding a semantic placement rule before compact-field balancing.
- Follow-up fixed right-column support fields sinking below lifecycle metadata by adding render-time ordering for generated secondary columns.
- Reviewer noted duplicate JSON in the already-dirty `App_Data/template/admin/fwupdates/config.json`; left it untouched because that file has unrelated pre-existing edits outside this task.
- Cleanup of isolated test output initially hit a transient file lock on `System.Diagnostics.PerformanceCounter.dll`; retry succeeded after a short delay.
## Risks / follow-ups
- Local IIS Express was not restarted, so a browser pointed at the existing app process may still show old layout behavior until rebuild/restart.
- Existing unrelated dirty files remain outside this task, including `App_Data/template/admin/fwupdates/config.json` and local untracked files.
## Heuristics (keep terse)
- N/A - documented generated layout behavior in `docs/dynamic.md`; no reusable agent heuristic added.
## Testing instructions
- For this task, run the focused tests with isolated output if IIS Express is running: `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~DevCodeGenTests -p:OutDir=artifacts\assistant_test\` and `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~FwUpdatesTests -p:OutDir=artifacts\assistant_test\`.
- Build with isolated output when local app binaries are locked: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\`.
## Reflection
- Stable framework behavior was documented in `docs/dynamic.md` and `docs/feature_modules.md`; no glossary/domain/ADR entries were needed.
- No AGENTS.md changes were needed.
