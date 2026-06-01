## What changed
- Added opt-in typed column filters for Dynamic/Vue list screens behind `list_column_filters.enabled`.
- Added server-side typed `search[field]` JSON parsing with parameterized predicates for text, date range, multi-select, autocomplete, number conditions, boolean, blank/not blank, and `none`.
- Preserved legacy `search[field]` behavior and session restore for simple controllers and legacy text values, including new `^starts-with`.
- Stored saved user-list filters as separate `f` and `search` payloads while preserving old saved-filter JSON.
- Added server-rendered filter partials, a Vue `list-column-filter` component, Vue store serialization support, demo opt-in config, docs updates, asset cache version bump, and focused tests.
- Follow-up: moved server filter partials under `common/list/filters/` and scrubbed repo-root/prohibited local absolute path strings from task summaries.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/drafts/column_filters.md`
- `docs/datetime.md`
- `docs/agents/code_reviewer.md`
- Dynamic/Vue controller config and list rendering paths.
- Server templates under `osafw-app/App_Data/template/common/list` and `common/vue`.
- No >1 MB files required whole-file reads.

## Commands used / verification
- Visual Studio MCP: verified solution/project list, ran `build_project` on `osafw-app`; initial build succeeded. A later VS build reported `FailedProjects: 1` while the running app/output was locked, but Error List was empty and isolated CLI build passed.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=..\artifacts\assistant_build\` - passed, 0 warnings/errors.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FwControllerColumnFilterTests -p:OutDir=..\artifacts\assistant_test_build\` - passed, 6 tests.
- Playwright `https://localhost:44315/Admin/DemosDynamic`: typed filters rendered, `fw.js?v0.26.0601` loaded, starts-with text control serialized to JSON.
- Playwright `https://localhost:44315/Admin/DemosVue`: Vue column-filter component resolved, filters rendered, no Vue warnings or console errors.
- Playwright `https://localhost:44315/Admin/Users`: simple controller kept legacy text search inputs and rendered no typed filters.
- Code reviewer sub-agent reported four issues; all were fixed, and the final self-review found no remaining issues worth another loop.
- `git diff --check` - passed.
- Follow-up path cleanup: repo-root/prohibited local path search returned no matches.
- Follow-up template move: stale old partial-name include search returned no matches; `common/list/filters/` contains the server partials.
- Follow-up browser smoke was blocked by login state on the existing IIS Express instance; build/tests and template-reference checks passed.

## Decisions - why
- Use `list_column_filters.enabled` as the opt-in gate so existing simple controllers keep current behavior.
- Keep the framework hook small: `applyListColumnFilter(FwDict def, object rawValue)` can override server predicate handling, while `template`/`component` override the UI.
- Use `filter_field` as the trusted config alias to the SQL column and quote it with `db.qid`; user values always go through `list_where_params`.
- Treat malformed JSON as legacy text search so typed opt-in does not eat searches that happen to start with `{`.
- Bump `SITE_VERSION` because `fw.js` changed and the running browser had the old asset cached under the prior version.

## Pitfalls - fixes
- Shared server header partial initially replaced legacy search cells for disabled controllers; fixed with a legacy fallback when `filter_type` is absent.
- Review found Vue `none` filters fell through to enabled text inputs; fixed by rendering the Vue column-filter component for `filter_type: "none"`.
- Review found `filter_field` overrides were applied after schema inference; fixed by merging overrides before schema lookup while preserving explicit storage metadata.
- Review found autocomplete was only comma-token text; fixed with real server `data-autocomplete`, Vue `autocomplete` component usage, and ID extraction from formatted `label ::: id` values.
- ParsePage consumed a JavaScript template literal in the Vue component, causing `Unexpected token '{'`; fixed with string concatenation.
- Project-local relative `OutDir` created recursive `artifacts` under `osafw-app`; removed generated output and reran builds with repo-root artifact paths.

## Risks / follow-ups
- Autocomplete filtering supports selected formatted values and manual fallback text, but large lookup UX may need multi-value chips in a later phase.
- Date range tests cover UTC-suffixed datetime boundaries; broader timezone/manual DB checks would be useful for non-UTC DB-local datetime fields.
- Existing unrelated untracked/generated paths are present in the worktree and should not be included with this task.

## Heuristics (keep terse)
- Static JS changes need `SITE_VERSION` cache busting for meaningful browser verification.
- Avoid JavaScript template literals inside ParsePage templates; backticks are template translation syntax.
- Use repo-root `artifacts\...` `OutDir` for app/test builds; project-local relative outputs can be picked up by compile/content globs.

## Testing instructions
- Build: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=..\artifacts\assistant_build\`
- Focused tests: `dotnet test osafw-tests\osafw-tests.csproj --filter FwControllerColumnFilterTests -p:OutDir=..\artifacts\assistant_test_build\`
- Manual smoke: log in to `https://localhost:44315/`, check `/Admin/DemosDynamic`, `/Admin/DemosVue`, and `/Admin/Users` filter rows.

## Reflection
The Visual Studio MCP path was useful for solution validation and launch attempts, but build diagnostics were weaker than the CLI when the running app/output was locked. Future runs should use VS MCP first when requested, then switch quickly to absolute `OutDir` CLI builds for actionable diagnostics. The reviewer sub-agent caught real contract misses; delegation was worth it here because server, Vue, and template behavior changed together. No stable framework facts, heuristics, or ADRs were added beyond the docs/config changes in this task; the reusable lessons are recorded above for user review.
