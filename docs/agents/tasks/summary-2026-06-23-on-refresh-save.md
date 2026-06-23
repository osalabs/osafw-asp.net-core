## What changed
- Added `.on-refresh-save` form-control support for classic forms. It posts the existing `refresh` trigger value plus `refresh_save=1`, then uses normal server validation/save and routes successful saves back to ShowForm.
- Kept `.on-refresh` as refresh-only and documented the distinction in `docs/dynamic.md`.

## Scope reviewed
- Reviewed `.on-refresh` handling in `osafw-app/wwwroot/assets/js/fw.js`.
- Reviewed refresh/save paths in `FwAdminController`, `FwDynamicController`, custom admin save overrides, autosave handlers, and dynamic docs.

## Commands used / verification
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=C:\DOCS_PROJ\github\_core_master\artifacts\assistant_build\` - passed with 0 warnings/errors.
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check ...` - passed for all touched files.
- `git -c core.quotepath=false diff --check` - passed.
- Self-reviewed final diff using `docs/agents/code_reviewer.md`; fixed one autosave-in-flight edge case.

## Decisions - why
- Used `refresh_save` as a separate request flag so existing server code that treats `refresh` as save-bypass remains backward compatible.
- Added shared controller helpers to avoid copying the `refresh && !refresh_save` condition across save actions.
- Forced successful refresh-save posts back to ShowForm for standard form controllers so `return_url` and `route_return` do not steal the refresh workflow.

## Pitfalls - fixes
- Existing autosave forms may have controls marked `data-noautosave`; `.on-refresh-save` deliberately uses full form submit so server validation/save still runs.
- If an autosave AJAX request is already in flight, the refresh helper now waits before setting hidden refresh fields so they do not leak into an AJAX autosave payload.
- Custom non-standard save actions can still opt in by using the shared helpers if they add refresh-save controls later.

## Risks / follow-ups
- Browser automation was not run; verification is build/static review only. Highest-risk manual check is changing a `.on-refresh-save` control on a data-autosave edit form and confirming validation errors return to ShowForm without save, while valid submissions save and reload ShowForm.
- No `docs/CHANGELOG.md` entry was needed because this is an additive CSS-class/request-flag contract and does not break existing `.on-refresh` behavior.

## Heuristics (keep terse)
- None added.

## Testing instructions
- For dynamic/classic forms, use `class_control: "on-refresh-save"` on a control whose change should save the current form and refresh dependent fields. Keep `on-refresh` for refresh-only behavior.

## Reflection
The main slowdown was confirming all duplicated refresh short-circuits before editing. The shared helper reduced drift, and the self-review pass was useful because it caught the in-flight autosave flag-leak edge case. No stable framework facts, heuristics, or ADRs were added; the behavior is documented in `docs/dynamic.md` and the implementation is small enough not to justify new agent guidance.
