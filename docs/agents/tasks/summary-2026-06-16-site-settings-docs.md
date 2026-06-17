## What changed
- Added `docs/settings.md` with developer documentation for the Site Settings table, model API, admin controller behavior, input types, seeding, and known pitfalls.
- Added `settings.md` to `docs/README.md` framework docs and reading guidance.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/agents/tasks/index.md` keyword search for related history
- Draft file keyword search for relevant settings guidance
- `osafw-app/App_Data/sql/fwdatabase.sql`
- `osafw-app/App_Data/sql/mysql/fwdatabase.sql`
- `osafw-app/App_Data/sql/sqlite/fwdatabase.sql`
- `osafw-app/App_Code/models/Settings.cs`
- `osafw-app/App_Code/controllers/AdminSettings.cs`
- `osafw-app/App_Code/fw/FwAdminController.cs`
- `osafw-app/App_Code/fw/FwController.cs`
- `osafw-app/App_Code/fw/FwModel.cs`
- `osafw-app/App_Data/template/admin/settings/**`
- `docs/crud.md`, `docs/db.md`, `docs/assistant.md`

## Commands used / verification
- `Test-Path docs\agents\local_instructions.md`
- `Test-Path docs\agents\tasks\index.md`
- `Get-Content docs\README.md | Select-Object -Index (0..220)`
- `rg --files`
- `rg -n "settings|AdminSettings|Site Settings|site settings" docs\agents\tasks\index.md`
- `rg -n "settings|AdminSettings|Site Settings|site settings|app_name|site_name" [draft file] osafw-app\App_Data\sql osafw-app\App_Code\models\Settings.cs osafw-app\App_Code\controllers\AdminSettings.cs osafw-app\App_Data\template\admin\settings`
- `rg -n "select_options_|allowed_values|setPS\(|setListPS\(|FormUtils\.select_options|select_options\(" osafw-app\App_Code\fw osafw-app\App_Code\controllers osafw-app\App_Code\models`
- `powershell.exe -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 docs\settings.md docs\README.md docs\agents\tasks\summary-2026-06-16-site-settings-docs.md`
- `powershell.exe -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 docs\settings.md docs\README.md docs\agents\tasks\summary-2026-06-16-site-settings-docs.md -Check`
- `git -c core.safecrlf=false diff --check -- docs/settings.md docs/README.md docs/agents/tasks/summary-2026-06-16-site-settings-docs.md`

## Decisions - why
- Documented option-based admin inputs as incomplete because the select options are not populated and checkbox/radio partials are TODO placeholders.
- Recommended explicit seeded rows for admin-facing settings because `Settings.write()` auto-creates minimal rows without labels/categories.
- Updated the docs map because `settings.md` is a new reusable framework doc entry point.
- No changelog entry needed: documentation-only change with no app behavior, public API, route, schema, config, or frontend contract change.

## Pitfalls - fixes
- `is_user_edit` looks like an edit-control flag in the schema, but current `AdminSettingsController` does not enforce it; documented it as metadata rather than access control.
- The admin list shows `ivalue`, so the docs call out secret visibility to admins.

## Risks / follow-ups
- No runtime checks were run because this is documentation-only.
- Future implementation work could wire `allowed_values` into select/multi-select rendering and complete checkbox/radio support.

## Heuristics (keep terse)
- No stable heuristics added.

## Testing instructions
- N/A - docs/instructions only.

## Reflection
- Targeted reads were effective here because the module surface is small and the draft spec did not contain module-specific guidance. Future agents can skip broader historical summaries after searching the task index unless they are continuing a prior Site Settings task.
