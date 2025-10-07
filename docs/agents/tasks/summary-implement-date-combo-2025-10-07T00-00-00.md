# Summary — implement-date-combo at 2025-10-07T00:00:00

## What changed
- Added dynamic Date combo support.
- New template: `App_Data/template/common/form/showform/date_combo.html`.
- Wired `date_combo` into `common/form/showform/one_fieldsel.html`.
- Enhanced `FwDynamicController.prepareShowFormFields` to prefill day/mon/year for `date_combo` using `FormUtils.comboForDate`.
- Ensured saving path: `processSaveShowFormFields` maps `date_combo` to single SQL datetime via `FormUtils.dateForCombo`.
- Updated demo config `admin/demosdynamic/config.json`: use `type: "date_combo"` for `fdate_combo`; switched `ftime` to `type: "time"`.

## Commands that worked (build/test/run)
- dotnet build

## Pitfalls ? fixes
- Needed a new showform template for `date_combo`; none existed.
- Ensure date values are populated on GET so selects show current value.
- Keep backward compatibility with existing date popup and time fields.

## Decisions ? why
- Implemented as a new `type` to avoid coupling with `date_popup` behavior.
- Used existing helpers (`FormUtils.dateForCombo`/`comboForDate`) for consistency with legacy non-dynamic forms.

## Heuristics (keep terse; add Expires if needed)
- Prefer adding minimal new template snippets and reuse utils.
- When adding a new dynamic field type, wire in: template, one_fieldsel mapping, prepare (prefill), save (serialize).