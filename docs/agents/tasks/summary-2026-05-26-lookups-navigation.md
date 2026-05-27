## What changed
- Fixed shared Vue history handling so initial screen state uses `replaceState`, normal in-app navigation uses `pushState`, and `popstate` updates the current screen without adding another history entry.
- Removed the `screens` component mounted push that created a duplicate list history entry.
- Added `return_title` beside `return_url` in controller/template/Vue state.
- Added generic return breadcrumbs that render only when `return_url` is supplied and display the passed `return_title`; no Lookup Manager special case is in the shared breadcrumb partial.
- Moved the shared breadcrumb to `common/list/return_breadcrumbs.html` and added `common/list/return_inputs.html` for hidden `return_url`/`return_title` fields.
- Added `admin/lookups/title.html` and reused it from Lookup Manager title/sidebar/link templates.
- Updated Lookup Manager links to pass `return_url=/Admin/Lookups` and `return_title=<~/admin/lookups/title>`.
- Added server-side URL helpers in `Utils`: `isAppUrl`, `addUrlQueryParam`, `addReturnUrlQuery`, and `buildReturnUrlQuery`.
- Preserved return metadata through standard classic list row Edit/View links, list Add New, form View/Edit header buttons, classic filter/search forms, form posts, Go/quick-search redirects, prev/next, save redirects, and Vue screen URLs.
- Moved standard link suffix assembly back into cached ParsePage fragments: `common/list/urlq.html` for links that need `?` and `common/list/urlqa.html` for links that already have query parameters.
- Renamed `Utils.isReturnUrlApp()` to `Utils.isAppUrl()` and `FwController.setReturnContext()` to `FwController.setPSReturnContext()`.
- Removed `FwController.setListRowUrls()`; standard row URLs are defined by templates again.
- Removed trailing newlines from URL-fragment templates used inside `href`/`data-href` values: `common/form/cancel_url.html`, `common/form/ret_url.html`, and `common/form/showdelete/cancel_url.html`.
- Kept return URL validation server-side: only root-relative app paths or absolute URLs under configured `ROOT_DOMAIN` are accepted.
- Removed the JS return-context propagation approach from the implementation; shared `fw.js` is not part of the final behavior change.
- Documented the shared return breadcrumb/input convention in `docs/templates.md`.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/templates.md`
- `docs/agents/code_reviewer.md`
- Lookup Manager controller/templates, shared controller return handling, dynamic/admin/Vue controller flows, shared Vue navigation/header templates, shared classic list/form headers, and standard classic showform templates that preserve return inputs.

## Commands used / verification
- `git status --short`
- `git diff --check`
- `git diff --stat`
- Targeted `rg` and `Get-Content` reads for Lookup Manager, virtual controllers, Vue history, breadcrumbs, list row links, and return-url handling.
- Visual Studio MCP was reachable, but final `solution_info` showed a different open solution (`frycomm.sln`), so it was not used for the final repo build.
- `dotnet build osafw-app\osafw-app.csproj` was blocked by the running VS/IIS Express process locking `bin\Debug\net10.0\osafw-app.dll`.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\` succeeded with 0 warnings and 0 errors; the resulting `osafw-app\artifacts\assistant_build\` output directory was removed afterward.
- Browser plugin Node connection failed twice with local Windows sandbox startup errors, so Playwright MCP was used as fallback.
- Playwright smoke after simplification:
  - `https://localhost:44315/Admin/Lookups` -> `Log Types` -> first `View`; Back once returned to Log Types list, Back again returned to Lookup Manager.
  - Direct `https://localhost:44315/Admin/LogTypes` had no Lookup Manager breadcrumb.
  - Lookup Manager Log Types link encoded `return_title=Lookup+Manager` without a trailing newline.
  - `https://localhost:44315/Admin/Roles?return_url=%2fAdmin%2fLookups&return_title=Lookup%20Manager` showed the Lookup Manager breadcrumb, did not show `/Admin/Lookups` in filter text, kept hidden `return_url` and `return_title`, and standard Edit/View row links kept both return params.
  - Unsafe absolute external `return_url` did not render a Lookup Manager breadcrumb.
- Temporary app-run/browser logs created for this task were removed after browser verification; pre-existing agent artifact logs were left untouched.
- Review loop:
  - Reviewer found double-encoding risk in two templates, fragment handling in `Utils.addUrlQueryParam`, and stale task summary notes.
  - Fixed those findings and reran verification/review after the fix.
  - Second reviewer found custom `row_view_url` could be overwritten; fixed it and reran verification.
  - Final focused reviewer found no issues and said the review loop can stop.

## Decisions - why
- Used shared Vue history changes because the duplicate Back behavior came from shared runtime state handling, not Lookup Manager itself.
- Kept breadcrumbs generic and origin-aware by rendering them only when return metadata is supplied.
- Kept return metadata propagation server/template-owned instead of JS-owned to match the explicit `return_url`/`return_title` contract and reduce client-side complexity.
- Centralized return URL validation and URL-query assembly in `Utils` to avoid repeated ad hoc string concatenation.
- Preserved standard classic row URLs through templates so normal templates do not need JavaScript click rewriting.
- Kept `admin/lookups/title.html` without a trailing newline because the partial is also used inside URL-encoded query parameters.
- Kept URL-fragment templates single-line without trailing newline because ParsePage includes their bytes directly inside URL attributes.
- Added only a short templates doc note because the new convention is shared UI behavior, not a schema or architecture decision.

## Pitfalls - fixes
- Existing Vue `popstate` reused push-based navigation, which trapped Back on the list screen; `skipHistory` fixes that branch.
- Existing `screens` mounted behavior added a duplicate list entry; the main app now owns initial replacement.
- Feedback found visible `/Admin/Lookups` text in standard filters because inline ParsePage block tags reused data names; wrappers now use neutral block names in `return_inputs`.
- Feedback found the first implementation overcomplicated return propagation with JS; final implementation removes that path and keeps propagation in server/template URLs.
- The new Lookup Manager title partial initially had a trailing newline that encoded as `%0A`; the partial is now no-newline text.
- Follow-up check found three common form URL fragments had trailing newlines; those files are now no-newline single-line fragments.
- Reviewer found URL query appending after `#fragment`; `Utils.addUrlQueryParam` now inserts query params before the fragment.
- Reviewer found `return_title` template query values could be encoded after HTML escaping; affected templates now use `noescape urlencode`.
- Follow-up smoke found standard list Add New and classic form View/Edit buttons could drop return metadata; those shared buttons now preserve `return_url`/`return_title`.
- Follow-up reviewer found `setListRowUrls()` overwrote custom `row_view_url`; the method was later removed and row URL assembly moved back to templates.
- Final review pass found no remaining issues.

## Risks / follow-ups
- Custom classic templates that hard-code record links and do not use standard `row_click_url`/`row_view_url` or shared return helpers may still need explicit return-context handling.
- Existing non-Lookup return flows may pass a `return_url` without `return_title`; the breadcrumb intentionally stays hidden unless a title is present, while Return Back buttons can still use `return_url`.
- No isolated assistant build output remains. Existing `osafw-app\bin\Debug` belongs to the local app/Visual Studio workflow.

## Heuristics (keep terse)
- No reusable heuristics added.
- Stable template convention added to `docs/templates.md`; no ADR needed.

## Testing instructions
- Build with Visual Studio MCP project build or: `dotnet build osafw-app\osafw-app.csproj`.
- Browser: `https://localhost:44315/Admin/Lookups` -> `Log Types` -> first `View` -> Back once -> Back again.
- Browser: direct `https://localhost:44315/Admin/LogTypes` should not show a Lookup Manager breadcrumb.
- Browser: classic direct `https://localhost:44315/Admin/Roles?return_url=%2FAdmin%2FLookups&return_title=Lookup%20Manager` should show the breadcrumb on list and standard record edit/view links should preserve both return params.
- Browser: unsafe external `return_url` should not render the return breadcrumb.

## Reflection
- The first implementation leaned too much on client-side propagation. Future runs should prefer explicit server/template propagation when request metadata is already present and only add JS when the browser has state the server cannot know.
- VS MCP was reachable, but the final check showed it attached to another solution; standalone `dotnet build` was the reliable build verification for this repo. Browser plugin setup failed locally, but Playwright MCP gave reliable smoke coverage.
- The review loop was useful: it caught fragment handling, encoding, and stale summary problems after simplification.
- No agent instruction changes are recommended from this task. The existing workflow already covers task summaries, docs sync, reviewer loops, and artifact cleanup.
