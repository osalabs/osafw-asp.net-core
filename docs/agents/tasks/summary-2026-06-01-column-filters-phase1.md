## What changed
- Added opt-in typed column filters for Dynamic/Vue list screens behind `list_column_filters.enabled`.
- Added server-side typed `search[field]` JSON parsing with parameterized predicates for text, date range, multi-select, autocomplete, number conditions, boolean, blank/not blank, and `none`.
- Preserved legacy `search[field]` behavior and session restore for simple controllers and legacy text values, including new `^starts-with`.
- Stored saved user-list filters as separate `f` and `search` payloads while preserving old saved-filter JSON.
- Added server-rendered filter partials, a Vue `list-column-filter` component, Vue store serialization support, demo opt-in config, docs updates, asset cache version bump, and focused tests.
- Follow-up: moved server filter partials under `common/list/filters/` and scrubbed repo-root/prohibited local absolute path strings from task summaries.
- Follow-up UI polish: filter summary buttons now use `btn-default`, empty summaries render blank, text operators include an empty top option plus short descriptions, operator selects refocus the related text input, structured filter labels/placeholders/hints were adjusted, date quick buttons use explicit small buttons, search hints are hidden for custom-filter lists, and Vue Apply/Clear hides the open dropdown.
- Follow-up Access parity: empty text operator is the visible default and serializes as contains, summary buttons use `&nbsp;` fallback to preserve height, text adds does-not-begin/end-with, and number conditions add not-equal.
- Follow-up performance: Vue list-row and lookup-only scopes now skip typed filter UI option enrichment, repeated Vue lookup loads are deduped, and Vue text operator changes focus the input without reloading rows unless the operator is blank/not blank.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/drafts/column_filters.md`
- `docs/datetime.md`
- `docs/agents/code_reviewer.md`
- Dynamic/Vue controller config and list rendering paths.
- Server templates under `osafw-app/App_Data/template/common/list` and `common/vue`.
- No >1 MB files required whole-file reads.
- Latest feedback pass reviewed only active filter/template/JS/CSS/server files; old task-summary files were not rescanned for local-path tokens.
- Reviewed Access reference screenshots under `docs/drafts/2026-06-09(filters)/`: text, number, and date filter menus.
- Reviewed `osafw-app/App_Data/logs/main.log` request-end timings for `/Admin/DemosDynamic`, `/Admin/DemosVue`, `/Admin/Users`, `/Admin/Roles`, `/Admin/Lookups`, and Lookup Manager destination `/Admin/Permissions`.

## Commands used / verification
- Visual Studio MCP: verified solution/project list, build status clean, ran `build_project` on `osafw-app`; latest VS project build completed with `FailedProjects: 0`.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=..\artifacts\assistant_build\` - passed, 0 warnings/errors.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FwControllerColumnFilterTests -p:OutDir=..\artifacts\assistant_test_build\` - passed, 9 tests.
- Playwright `https://localhost:44315/Admin/DemosDynamic`: typed filters rendered, `fw.js?v0.26.0601` loaded, starts-with text control serialized to JSON.
- Playwright `https://localhost:44315/Admin/DemosVue`: Vue column-filter component resolved, filters rendered, no Vue warnings or console errors.
- Playwright `https://localhost:44315/Admin/Users`: simple controller kept legacy text search inputs and rendered no typed filters.
- Follow-up Playwright `https://localhost:44315/Admin/DemosDynamic`: loaded `SITE_VERSION` `0.26.0609.2`, row height stayed 40.5px with dropdown open, empty summaries were blank, summary buttons used `btn-default`, operator labels/descriptions rendered, operator select focused its input, multi/autocomplete labels/placeholders/hints rendered, date quick buttons were `btn-sm`, and no `Search hints` toast appeared.
- Follow-up Playwright `https://localhost:44315/Admin/DemosVue`: row height stayed 48.5px, empty summaries were blank, operator select padding was 2px in normal and dense modes, operator select focused its input after Vue tick, Vue date Apply and Clear closed the dropdown, and the only console error was the existing `/_vs/browserLink` 404.
- Latest Playwright `https://localhost:44315/Admin/DemosDynamic`: loaded `SITE_VERSION` `0.26.0609.3`, row stayed 40.5px, empty text operator serialized to `op:"contains"`, operator padding-right computed to 32px, summary HTML was `&nbsp;`, and number not-equal input rendered.
- Latest Playwright `https://localhost:44315/Admin/DemosVue`: row stayed 48px, normal/dense operator padding-right computed to 32px, summary HTML was `&nbsp;`, number not-equal input rendered, empty operator committed `op:"contains"`, no `Search hints` toast appeared, and the only console error was the existing `/_vs/browserLink` 404.
- Performance Playwright/log pass: `/Admin/DemosDynamic` loaded with the filter row hidden but 29 typed filter controls already rendered; current log request was 0.104489s and 8 SQL. `/Admin/DemosVue` loaded 29 typed filter controls and 238 filter option rows in header metadata; current logs showed shell/init/list-row requests at 1/8/13 SQL, with row payload no longer loading filter option queries. `/Admin/Users`, `/Admin/Roles`, `/Admin/Lookups`, and `/Admin/Permissions` rendered no typed filter controls while disabled.
- Vue interaction smoke: applying and clearing a multi-select filter closed the dropdown, opening another filter left only one panel open, and changing a text operator focused the input without a row reload.
- Code reviewer sub-agent reported four issues; all were fixed, and the final self-review found no remaining issues worth another loop.
- Follow-up self-review found one dense-mode operator padding miss; fixed and reran verification. No remaining issues found. Review loop can stop.
- `git diff --check` - passed.
- CRLF check for touched files - passed.
- Follow-up path cleanup: repo-root/prohibited local path search returned no matches.
- Follow-up template move: stale old partial-name include search returned no matches; `common/list/filters/` contains the server partials.
- Follow-up browser smoke was blocked by login state on the existing IIS Express instance; build/tests and template-reference checks passed.

## Decisions - why
- Use `list_column_filters.enabled` as the opt-in gate so existing simple controllers keep current behavior.
- Keep the framework hook small: `applyListColumnFilter(FwDict def, object rawValue)` can override server predicate handling, while `template`/`component` override the UI.
- Use `filter_field` as the trusted config alias to the SQL column and quote it with `db.qid`; user values always go through `list_where_params`.
- Treat malformed JSON as legacy text search so typed opt-in does not eat searches that happen to start with `{`.
- Bump `SITE_VERSION` because `fw.js` changed and the running browser had the old asset cached under the prior version.
- No changelog entry was added for the follow-up UI polish because it is still within the unreleased opt-in column-filter work and does not add a new breaking upgrade requirement.
- Custom list filters should stay opt-in for now. Current Dynamic screens render typed controls and load options even while the row is hidden; Vue can skip row-scope enrichment now, but initial `init,lookups` still carries filter metadata/options so first-open is instant.

## Pitfalls - fixes
- Shared server header partial initially replaced legacy search cells for disabled controllers; fixed with a legacy fallback when `filter_type` is absent.
- Review found Vue `none` filters fell through to enabled text inputs; fixed by rendering the Vue column-filter component for `filter_type: "none"`.
- Review found `filter_field` overrides were applied after schema inference; fixed by merging overrides before schema lookup while preserving explicit storage metadata.
- Review found autocomplete was only comma-token text; fixed with real server `data-autocomplete`, Vue `autocomplete` component usage, and ID extraction from formatted `label ::: id` values.
- ParsePage consumed a JavaScript template literal in the Vue component, causing `Unexpected token '{'`; fixed with string concatenation.
- Project-local relative `OutDir` created recursive `artifacts` under `osafw-app`; removed generated output and reran builds with repo-root artifact paths.
- Bootstrap `.input-group-sm > .form-select` and dense-mode list padding overrode the requested narrow operator select padding; fixed with narrowly scoped `.fw-column-filter-op` selectors and verified computed 2px padding in both modes.
- Vue summary fallback was kept as escaped text plus a literal `&nbsp;`, not `v-html`, so lookup/autocomplete labels remain escaped.
- Vue text operator changes originally triggered `setFilters({})` before focus could settle; fixed by matching the server-rendered path and applying immediately only for blank/not blank.

## Risks / follow-ups
- Autocomplete filtering supports selected formatted values and manual fallback text, but large lookup UX may need multi-value chips in a later phase.
- Date range tests cover UTC-suffixed datetime boundaries; broader timezone/manual DB checks would be useful for non-UTC DB-local datetime fields.
- Access-style date presets were reviewed but intentionally not implemented in this pass; UI/API options should be agreed before expanding date filter behavior.
- Existing unrelated untracked/generated paths are present in the worktree and should not be included with this task.
- Default-on typed filters should wait for lazy filter metadata/options. Vue can do this cleanly with a first-open `scope=column_filters` style load; Dynamic can stay simple with a first-open full-page reload to render the filter row, while instant no-reload Dynamic popovers would require a new partial/AJAX endpoint.

## Heuristics (keep terse)
- Static JS changes need `SITE_VERSION` cache busting for meaningful browser verification.
- Avoid JavaScript template literals inside ParsePage templates; backticks are template translation syntax.
- Use repo-root `artifacts\...` `OutDir` for app/test builds; project-local relative outputs can be picked up by compile/content globs.
- When measuring Vue list performance, separate shell, `init,lookups`, and `list_rows`; filter option enrichment belongs only in scopes that render the filter UI.

## Testing instructions
- Build: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=..\artifacts\assistant_build\`
- Focused tests: `dotnet test osafw-tests\osafw-tests.csproj --filter FwControllerColumnFilterTests -p:OutDir=..\artifacts\assistant_test_build\`
- Manual smoke: log in to `https://localhost:44315/`, check `/Admin/DemosDynamic`, `/Admin/DemosVue`, and `/Admin/Users` filter rows.
- Performance smoke: compare `osafw-app/App_Data/logs/main.log` request-end SQL counts for Dynamic, Vue shell/init/list rows, and disabled controller samples.

## Reflection
The Visual Studio MCP path was useful for solution validation and launch attempts, but CLI isolated builds remained the clearest compile signal. Playwright-side computed-style checks were important for Bootstrap specificity issues that static review missed; future UI polish passes should verify the actual computed style, not just class/rule presence. Reading request-end lines from `main.log` was more useful than broad trace output for performance checks. The reviewer sub-agent caught real contract misses in the larger pass; for smaller follow-ups, self-review plus browser/log checks was faster and still found real gaps. No stable framework facts, heuristics, or ADRs were added beyond the docs/config changes in this task; the reusable lessons are recorded above for user review.
