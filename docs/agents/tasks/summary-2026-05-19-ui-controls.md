## What changed
- Added range and switch support to static, Dynamic, and Vue demo screens.
- Added reusable Dynamic templates and Vue renderer/list-edit support for `range` and `switch`.
- Added `demos.frange` and `demos.is_switch` schema/demo model support plus SQL Server update script.
- Added MySQL additive update script for existing MySQL demo databases.
- Updated Dynamic docs and design-system examples.
- Feedback iteration: widened range controls to full-width content columns, removed Dynamic edit disabled duplicates for range/switch, improved switch/help-text alignment, and excluded switch inputs from Vue editable-list cell success animation.
- Follow-up feedback: removed the temporary `show_fallback.html` approach and use the shared `showform/range.html` and `showform/switch.html` renderers from the show dispatcher, with disabled state applied for show/view contexts.
- Review follow-up: made Dynamic/Vue show/view contexts force shared range/switch renderers disabled, and added Dynamic-to-static extraction support for `range` and `switch`.
## Scope reviewed
- Read local instructions, docs map, code reviewer instructions, demo controllers/configs/templates, shared dynamic form templates, Vue form/list components, and demo SQL schemas.
## Commands used / verification
- `Get-Content docs\agents\local_instructions.md`
- `Get-Content docs\README.md`
- `Get-Content docs\agents\code_reviewer.md`
- `git status --short`
- `ConvertFrom-Json` on `admin/demosdynamic/config.json` and `admin/demosvue/config.json`
- `rg -n "frange|is_switch|type=='range'|type=='switch'|input_type=='switch'|form-range|form-switch" ...`
- `dotnet build osafw-app\osafw-app.csproj` - failed because IIS Express locked `bin\Debug\net10.0\osafw-app.dll`
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\` - passed with 0 warnings/errors
- `sqlcmd -S "(local)" -d demo -E -i "osafw-app\App_Data\sql\updates\upd2026-05-19-demo-ui-controls.sql"` - applied local SQL Server demo update for browser smoke
- Visual Studio MCP `build_project` for `osafw-app\osafw-app.csproj` - passed with `FailedProjects: 0`
- Visual Studio MCP `debugger_launch_without_debugging` - started the configured startup project at `https://localhost:44315/`
- Playwright/in-app browser smoke:
  - Logged in at `https://localhost:44315/Login` with local credentials from machine instructions.
  - Static `/Admin/Demos/(ShowForm)/1` rendered `.form-range` and `.form-check.form-switch[role=switch]`; attempted save changed the backing row and reload showed persisted values.
  - Dynamic `/Admin/DemosDynamic/1/edit?tab=details` rendered range/switch, accepted a controller save to `frange=45` and unchecked switch, and reload showed persisted values.
  - Vue `/Admin/DemosVue/1/edit?tab=details` rendered range/switch, saved `frange=60` and unchecked switch through the responsive visible Save button, and reload showed persisted values.
  - Dynamic range reload confirmed shared range templates now render `min="0"`, `max="100"`, and `step="5"`.
  - Static `/Admin/Demos/1` and Dynamic `/Admin/DemosDynamic/1?tab=details` show pages rendered disabled/read-only range and switch controls with Bootstrap classes and `role="switch"`.
- Second code-review loop by sub-agent: no issues found. Residual gaps: full `dotnet test` not run, MySQL update script reviewed but not executed, Playwright coverage focused on smoke paths.
- Feedback verification:
  - Visual Studio MCP `build_project` for `osafw-app\osafw-app.csproj` - passed with `FailedProjects: 0`.
  - Dynamic `/Admin/DemosDynamic/1022/edit?tab=details` now renders one editable range and one editable switch; no disabled duplicate range/switch remained.
  - Static `/Admin/Demos/1022/edit` and Vue `/Admin/DemosVue/1022/edit?tab=details` render full-width range controls with `class_contents`/container `col`.
  - Fresh Playwright profile verified Vue editable list switches keep Bootstrap blue immediately while the parent cell has `cell-success-fade`; switch input animation is `none`.
  - `git diff --check`, JSON parsing for both demo configs, and CRLF checks passed.
- Feedback code-review loop by sub-agent: no issues found. Residual gaps unchanged: full `dotnet test` not run, MySQL update script reviewed but not executed, broad browser regression suite not run.
- Follow-up feedback verification:
  - Removed `common/form/showform/show_fallback.html` and the separate `common/form/show/range.html` / `common/form/show/switch.html` templates.
  - Visual Studio MCP `build_project` for `osafw-app\osafw-app.csproj` - passed with `FailedProjects: 0`.
  - Dynamic `/Admin/DemosDynamic/1022/edit?tab=details` rendered exactly one editable range and one editable switch.
  - Dynamic `/Admin/DemosDynamic/1022?tab=details` rendered exactly one disabled range and one disabled switch through the shared showform templates.
- Review-fix verification:
  - Visual Studio MCP `build_project` for `osafw-app\osafw-app.csproj` - passed with `FailedProjects: 0`.
  - In-app browser verified Dynamic edit still renders one editable range/switch and Dynamic show renders one disabled range/switch.
  - In-app browser verified Vue view Details tab renders the range and switch disabled through the shared Vue form control component.
- Final code-review loop by sub-agent: no issues found. Residual gaps unchanged: full `dotnet test` not run, MySQL update script reviewed but not executed, broad browser regression suite not run.
## Decisions - why
- Use dedicated `frange` and `is_switch` demo fields so existing numeric and checkbox examples remain separate.
- Add only range and switch now; color and datalist stay deferred because they are lower-value for business CRUD and overlap with existing autocomplete/select patterns.
- Vue Show configs mark `range` and `switch` as `readonly` so the shared Vue control component can render the same type in read-only and edit contexts.
- Emit range defaults directly in the shared range templates because ParsePage `if="min"` treats `0` as false; range controls should still render `min="0"`.
- Use a small shared `.fw-switch-control` class for switch row alignment instead of making the field content column itself a flex row; this keeps help text below the switch.
- Keep Vue list cell save feedback on the table cell but exclude switch inputs from the input background animation so checked switches show their active color immediately.
- Use the existing show dispatcher for Dynamic show pages instead of a separate fallback partial; this keeps show and edit dispatch explicit and leaves room for separate show/showform types when a control needs them.
- Keep the runtime range/switch templates only under `common/form/showform`; extraction-only helpers are allowed under `common/form/*/extract` so DevManage static generation can emit correct persisted templates.
## Pitfalls - fixes
- Normal app build output was locked by IIS Express; used isolated `OutDir` and removed generated build artifacts after verification.
- `apply_patch` wrote LF line endings; normalized all touched files to CRLF.
- Review finding: Vue list-edit defaults omitted the new controls; added `frange` and `is_switch` to `edit_list_defaults`.
- Review finding: MySQL had only from-scratch schema support; added a companion MySQL update script.
- Review finding: local editor artifacts were visible in `git status`; added ignore rules for common workspace/user files and left unrelated `.jshintrc` untouched.
- Browser smoke finding: shared Dynamic range templates omitted `min="0"` because of a falsey ParsePage guard; changed templates to range-specific default attributes.
- Browser smoke caveat: Vue desktop edit form hides its Save button inside the mobile-only button row in this layout, so the Playwright save check temporarily used the viewport override to expose the intended responsive Save button and reset the viewport afterward.
- Feedback finding: Dynamic edit showed disabled duplicates because showform dispatch fell through into the show dispatcher for `range`/`switch`; fixed by routing `range`/`switch` through the shared showform templates from the show dispatcher and applying disabled state in those templates for view contexts.
- Review finding: show/view disabled state depended on each field config carrying `readonly`/`disabled`; fixed by forcing Dynamic show `range`/`switch` definitions disabled and by disabling the Vue shared component whenever the current screen is view.
- Review finding: Dynamic-to-static extraction missed `range` and `switch`; fixed with extraction mappings and extraction-only range/switch helpers for show and showform generated templates.
## Risks / follow-ups
- Browser save smoke tests need the SQL update applied to the target local database first.
- Static/Dynamic/Vue smoke changed demo row `id=1` range/switch values in the local `demo` database; final observed value was `frange=60`, `is_switch=0`.
- Feedback smoke toggled a Vue editable-list switch in the local `demo` database and inspected record `id=1022`.
## Heuristics (keep terse)
- None yet.
## Testing instructions
- Apply `osafw-app/App_Data/sql/updates/upd2026-05-19-demo-ui-controls.sql` to existing SQL Server demo databases before opening demo screens.
- For existing MySQL demo databases, apply `osafw-app/App_Data/sql/mysql/updates/upd2026-05-19-demo-ui-controls.sql`.
- Run `dotnet build osafw-app/osafw-app.csproj -p:OutDir=artifacts/assistant_build/` if the normal app DLL is locked by IIS Express.
- Browser smoke target used in this run: `https://localhost:44315/`.
## Reflection
- Stable public control behavior was documented in `docs/dynamic.md`; no separate domain/glossary/ADR update needed.
