## What changed
Updated `FwDynamicController.prepareShowFormFields()` so classic dynamic ShowForm preparation loads attachment display data for readonly `att`, `att_links`, and `att_files` field types as well as their editable counterparts.
Updated the readonly `att_links` show partial so it suppresses its hidden posting input when rendered inside ShowForm.

## Scope reviewed
Reviewed dynamic attachment handling in `FwDynamicController`, matching Vue behavior in `FwVueController`, shared classic field selector templates, attachment partials, and `docs/dynamic.md` attachment type documentation.

## Commands used / verification
- `dotnet build osafw-app/osafw-app.csproj` - failed because `osafw-app/bin/Debug/net10.0/osafw-app.dll` was locked by IIS Express Worker Process PID 53124.
- `dotnet build osafw-app/osafw-app.csproj -p:OutDir=artifacts/assistant_build/` - passed with 0 warnings and 0 errors.
- Code reviewer sub-agent found that readonly `att_links` would render hidden `att[...]` inputs inside ShowForm; fixed in the shared readonly partial.
- `git diff --check -- osafw-app/App_Code/fw/FwDynamicController.cs osafw-app/App_Data/template/common/form/show/att_links.html docs/agents/tasks/summary-2026-05-12-dynamic-att-files-showform.md` - passed.
- CRLF check for touched files - passed with zero bare LF bytes.
- `dotnet build osafw-app/osafw-app.csproj -p:OutDir=artifacts/assistant_build/` rerun after review fix - passed with 0 warnings and 0 errors.
- Second code reviewer sub-agent pass found no issues; review loop can stop.

## Decisions - why
Kept save-time processing limited to `att_links_edit` and `att_files_edit` so readonly display fields cannot mutate or delete attachments during PATCH/save flows.

## Pitfalls - fixes
Plain `att_files` in `showform_fields` rendered the readonly show partial but lacked prepared `att_files` data in the classic controller path. The fix loads the data without setting upload-only defaults unless the type is `att_files_edit`.
Readonly `att_links` in ShowForm would otherwise render hidden inputs from the show partial and could collide with editable attachment links that use the default `att` post prefix. The show partial now omits that hidden input when `PARSEPAGE.TOP[is_showform]` is true.

## Risks / follow-ups
Highest-risk follow-up not run: full `dotnet test`.

## Heuristics (keep terse)
No reusable heuristic added.

## Testing instructions
Build with `dotnet build osafw-app/osafw-app.csproj -p:OutDir=artifacts/assistant_build/` if the normal debug output is locked by IIS Express.

## Reflection
No stable framework facts, glossary entries, or ADRs added; this aligns existing documented behavior with implementation.
