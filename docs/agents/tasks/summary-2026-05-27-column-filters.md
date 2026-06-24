## What changed
Created `docs/drafts/column_filters.md` with an implementation plan for optional typed column filters on list view screens.
Updated the draft after user feedback:
- opt-in only for `FwDynamicController` and `FwVueController`
- saved user filters include per-column filters
- added datetime boundary conversion guidance
- resolved inclusive `between` / strict outside `not_between`
- added `autocomplete`, blank/not blank, date quick buttons, starts-with syntax, `.sel` dropdown support, and compact custom-filter hooks

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/drafts/FPF-Spec.md` was accidentally opened as a whole file first; follow-up use was limited to targeted planning concepts instead of further whole-file reads.
- `docs/templates.md` sections around list template structure and `list_filter_more.html`.
- `docs/dynamic.md` sections around shared config keys, lookup helpers, and field types.
- `docs/db.md` schema inspection section.
- `osafw-app/App_Code/fw/FwController.cs` filter initialization, search parsing, and `setViewList()`.
- `osafw-app/App_Code/fw/FwVueController.cs` lookup scope behavior.
- `osafw-app/App_Code/fw/DB.cs` schema/type helpers and SQL expression helpers.
- `osafw-app/App_Data/template/common/list/thead.html`
- `osafw-app/App_Data/template/common/vue/list-table-header.html`
- `osafw-app/App_Data/template/common/vue/store.js`
- `docs/datetime.md` sections on date-only values, real datetime timezone pipeline, `_utc`, `datetimeoffset`, and save conversion.
- Microsoft Access `Between...And` operator support doc.
- Microsoft Excel filter options and advanced filter comparison operator support docs.

## Commands used / verification
- `Get-Content docs\agents\local_instructions.md`
- `Get-Content docs\README.md`
- `Get-Content docs\drafts\FPF-Spec.md` (mistake: whole large file read)
- `Get-ChildItem -Path docs\drafts -Force | Select-Object Name,Length,Mode`
- `rg -n 'list_filter|search_fields|search_str|search' osafw-app\App_Code\fw osafw-app\App_Code\controllers osafw-app\App_Code\models`
- `rg -n '<tr class="search"|class="search"|list_filter_more|filter_std|search_fields|list_filter' osafw-app\App_Data\template osafw-app\App_Code docs\templates.md docs\dynamic.md`
- `rg -n 'search\[|list_headers|search_value|list_table|filter_' osafw-app\App_Data\template\common osafw-app\App_Data\template\admin osafw-app\App_Data\template\my`
- Targeted `Get-Content ... | Select-Object -Index (...)` reads for relevant code and docs ranges.
- `git status --short`
- CRLF byte check for `docs/drafts/column_filters.md` and this task summary.
- Read-back inspection of the new plan and task summary.
- Web verification for Access/Excel range filter behavior using Microsoft support docs.

Verification result:
- Files have CRLF line endings.
- `docs/drafts/column_filters.md` exists under ignored `docs/drafts/`.

## Decisions - why
- Planned to keep `search[field]` as the request key to preserve current URLs, sessions, templates, and Vue store flow.
- Planned typed filter values as JSON strings because the current `FW.prepare_FORM` only groups one bracket level and would not parse nested form names cleanly.
- Planned explicit `list_column_filters.enabled` instead of automatic global enablement to avoid surprising existing screens.
- Planned server-side field whitelisting and parameterized SQL for new typed predicates to avoid opening unsafe filter surfaces.
- Planned a small filter type set first: text, date range, multi-select, number conditions, boolean, none.
- Keep feature activation in Dynamic/Vue controllers only; `FwController` can hold compact shared helpers but should stay no-op for simple derived controllers.
- Use inclusive `between` and strict outside `not_between` (`< from OR > to`) based on Access's inclusive `Between...And` and Excel's range/comparison-filter behavior.
- Add `autocomplete` as an explicit filter type for large lookups; default `multi_select` can load active lookup rows because framework lookup tables are normally small.
- Use `^WORD` for legacy starts-with syntax because plain text currently means contains.

## Pitfalls - fixes
- Whole-file read of `docs/drafts/FPF-Spec.md` violated the large-file reading guidance. Mitigation: stopped further whole-file reads, used targeted repo/code reads, and recorded the pitfall here.
- Initial `rg` command for `<tr class="search"` had bad quoting. Re-ran with single-quoted pattern.

## Risks / follow-ups
- Future implementation should test both server-rendered and Vue lists because they share server parsing but have different filter-row rendering.
- Existing text parser interpolates quoted values; new typed parser should use `list_where_params`.
- For datetime date filters, implementation must follow `docs/datetime.md`: date-only values do not shift, but real datetime day boundaries must be converted through the user's timezone and database/UTC storage rules.
- Confirm during implementation whether the Week quick button should mean "last 7 days" as drafted or current calendar week.

## Heuristics (keep terse)
- For query-string feature additions, prefer preserving existing top-level request keys when framework form parsing is shallow.
- For typed list filters, infer conservatively and require config overrides for aliases, subqueries, or large lookups.

## Testing instructions
N/A - docs/plan only.

## Reflection
The main slowdown was recovering from an accidental whole-file read of a large draft spec and then narrowing back to targeted evidence. Future agents should check file sizes in `docs/drafts` before opening and use `rg` for headings/keywords first. The old Vue reference was useful, but only after targeting filter-specific files and ranges. No sub-agent was needed for this docs-only planning task. No stable facts, reusable heuristics, or ADRs were added because this is a draft plan awaiting user feedback.
