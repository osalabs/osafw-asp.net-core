## What changed
- Added default list search placeholder page state based on `search_fields`.
- Added `setListPS()` as the clearer list page-state hook and kept `setPS()` as a compatibility wrapper.
- Marked `setPS()` obsolete so downstream overrides/calls are noticed during upgrades.
- Updated in-repo controller overrides to use `setListPS()`.
- Moved placeholder wording into templates and added `common/list/fs_placeholder.html` for localized default text with optional controller overrides.
- Documented that dynamic-controller `search_fields` drives the default list search placeholder fields.
- Added a changelog entry for the obsolete `setPS()` upgrade warning.

## Scope reviewed
- Read local machine instructions, `docs/README.md`, `docs/naming.md`, `docs/dynamic.md`, and `docs/agents/code_reviewer.md`.
- Reviewed `FwController` list search/page-state code, list search templates, dynamic config examples, template docs, and existing controller behavior tests.

## Commands used / verification
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~FwControllerBehaviorTests` - passed, 8 tests.
- `dotnet build osafw-app\osafw-app.csproj` - passed, 0 warnings/errors.
- `git diff --check -- <changed files>` - passed.
- Checked edited files for bare LF bytes - none found.
- Self-review using `docs/agents/code_reviewer.md` - no issues found; review loop can stop.

## Decisions - why
- Used `setListPS()` instead of `setIndexPS()` because the payload is list page state and the Vue controller can reuse it outside a literal index route.
- Marked `setPS()` obsolete after feedback, but kept framework internals dispatching through it with local warning suppression so existing project overrides still run.
- The C# placeholder value lists labels only; localized words such as `Search in` live in the template partial.

## Pitfalls - fixes
- The suggested snippet handled only space-separated fields; implementation also handles comma-separated search groups and de-duplicates repeated fields.
- Unmapped fields fall back to `[field]` so classic controllers without `view_list_map` still show useful placeholder text.
- A bare `<~list_filter_search_placeholder>` in common templates would resolve from the controller base dir, so common templates now include `<~/common/list/fs_placeholder>` instead.

## Risks / follow-ups
- Downstream controllers that override `setPS()` without calling `base.setPS()` will not inherit the new placeholder automatically.
- Warning-as-error builds must rename `setPS()` overrides/calls to `setListPS()`.

## Heuristics (keep terse)
- No stable heuristics added.

## Testing instructions
- Run `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~FwControllerBehaviorTests`.
- Run `dotnet build osafw-app\osafw-app.csproj`.

## Reflection
The main slowdown was protecting compatibility around the old `setPS()` hook while adding the clearer name. Reading the Vue controller call path early avoided choosing an index-specific name that would drift from actual use. No sub-agent was needed; the diff was small and the final self-review found no issues. No stable facts, heuristics, or ADRs were added.
