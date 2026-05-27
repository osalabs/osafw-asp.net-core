## What changed
- Migrated static Bootstrap icon tags in ParsePage templates to `common/icons/*` includes.
- Created Bootstrap-name icon partials under `osafw-app/App_Data/template/common/icons/`, each with `me-1` spacing.
- Updated `common/icon.html` for server-rendered dynamic button helpers to add `me-1` and remove the trailing literal space.
- Removed duplicate sidebar/dropdown CSS icon gap rules that would stack with partial-level spacing.
- Removed now-unused `--fw-sidebar-icon-gap` token definitions after Copilot review confirmed the token no longer had a consumer.
- Updated template/layout/dynamic docs for the static icon convention and remaining dynamic-icon limitation.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/templates.md`
- `docs/layout.md`
- `docs/dynamic.md`
- Static icon usage under `osafw-app/App_Data/template/**`
- Sidebar/dropdown icon spacing rules in `site.css` and `theme30.css`

## Commands used / verification
- Static direct icon search outside `common/icons`: no remaining direct static `<i class="bi bi-...">` matches.
- Icon include spacing searches: no icon-include-to-label whitespace matches and no literal space before trailing `Next` icons.
- Missing partial search for `<~/common/icons/*>` includes: no missing partials.
- Malformed migration search for `<~/common/icons/...></i>`: no matches after reviewer fix.
- `git diff --check`: passed.
- `dotnet build osafw-app\osafw-app.csproj`: blocked first by sandbox temp-directory permissions, then by locked `bin\Debug\net10.0\osafw-app.dll` held by IIS Express Worker Process.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\`: passed with 0 warnings and 0 errors.
- Browser smoke was attempted but blocked: Browser/node runtime failed with `windows sandbox failed: spawn setup refresh`; curl/Invoke-WebRequest to local HTTPS also failed due local TLS/connection handling.
- Code reviewer sub-agent loop: first pass found a stray `</i>` in `datetime_popup.html`; fixed. Second pass reported no issues.
- Copilot feedback on unused `--fw-sidebar-icon-gap`: confirmed actual; removed the dead token definitions from base and theme CSS.

## Decisions - why
- Use Bootstrap icon names without the `bi-` prefix for partial filenames, per user-approved plan.
- Leave JSON/Vue/controller-driven class-string icon APIs in place for this pass to avoid changing dynamic config contracts.
- Keep contextual classes such as `text-danger`, `text-muted`, and sidebar toggler state classes on wrappers around icon includes.
- Remove sidebar/dropdown icon-gap CSS rules because partials now own default `me-1` spacing.
- Remove sidebar icon-gap token definitions as well, because keeping dead theme tokens after removing the consuming rule would make them misleading.

## Pitfalls - fixes
- Vue conditional sort icons cannot carry `v-if`/`v-else` on a ParsePage include, so the Vue directives were moved to wrapper spans.
- Existing JS that toggles error-page caret icons still targets `bi-caret-*`; the partials preserve those icon classes.
- The original `datetime_popup.html` had a doubled `</i></i>` pattern; migration left one stray closing tag, caught by review and removed.
- Applying a one-line patch converted one file to LF; CRLF was restored before final checks.
- Copilot correctly flagged the leftover sidebar icon-gap token definitions; the fix was to remove the definitions rather than reintroduce a second spacing source.

## Risks / follow-ups
- Full icon-library replacement still requires a later migration for dynamic icon config values and Vue/client-rendered dynamic icon helpers.
- Icon-only buttons now render the standard icon partial with `me-1`; automated build/search checks passed, but browser visual smoke could not run in this environment.

## Heuristics (keep terse)
- No stable facts, heuristics, or ADRs were added. No AGENTS.md update was needed.

## Testing instructions
- Re-run `git diff --check`.
- Re-run the static icon searches listed above.
- Re-run `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\` if normal build output is locked by IIS Express.
- When Browser MCP/runtime is available, manually smoke-check sidebar/header, list filters/actions, edit/view headers, attachment controls, reports buttons, and a Vue list/detail screen.

## Reflection
Mechanical ParsePage migrations need a malformed-tag search in addition to direct-markup searches, especially when the source already contains invalid HTML. The reviewer sub-agent was useful and caught a real defect quickly. Browser MCP was unavailable and shell HTTPS checks were blocked by local TLS behavior, so future UI-sensitive template migrations should either confirm the browser runtime before relying on smoke tests or document the blocked visual check early.
