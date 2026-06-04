# Changelog

## Unreleased
- Breaking: renamed the report runtime/base class from `FwReports` to `FwReportsBase`; hardcoded reports must inherit `FwReportsBase`, and helper callers must use `FwReportsBase.createInstance()`, `FwReportsBase.createHtml()`, and `FwReportsBase.createFile()`.
- Breaking: `FwReports` is now the framework table model for the `fwreports` table. Existing apps upgrading custom reports must apply `osafw-app/App_Data/sql/updates/upd2026-06-03-custom-reports.sql`, copy the new report templates, and update app-specific hardcoded report classes/callers for the `FwReportsBase` rename.
- Added Site Admin-managed custom SQL reports stored in `fwreports`, with runtime SELECT/CTE-only validation, bound parameters, generic table output, preview support, report output context, and manual RBAC resource support.
- Clarified framework datetime docs so SQL `date` and other date-only values are documented as calendar-stable, while real `datetime` values still use per-user timezone conversion.
- Moved class-only template partials from `App_Data/template/common/` into `App_Data/template/common/cl/` and updated references to the new locations (for example: `common/active` -> `common/cl/active`, `common/disabledcl` -> `common/cl/disabled`, `common/hide` -> `common/cl/hide`, `common/text_end` -> `common/cl/text_end`).
- Renamed class partials to match class names within the new `common/cl/` folder (for example: `disabledcl.html` -> `disabled.html`, `selected0.html` -> `selected.html`).
- Moved attribute-related template partials into `App_Data/template/common/attr/` and updated template references (for example: `common/checked` -> `common/attr/checked`, `common/disabled` -> `common/attr/disabled`, `common/display_none` -> `common/attr/display_none`).
