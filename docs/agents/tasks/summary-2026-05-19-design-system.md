## What changed
- Added `docs/design_system.html` as a static visual guide for framework UI conventions, active themes, tokens, and shared component examples.
- Linked the design system from the docs map, layout guide, template guide, dashboard docs, and feature-module docs.
- Updated layout guidance to point at the actual active CSS path under `osafw-app/wwwroot/assets/css/`.
- Added one reusable UI heuristic for future work.
- Aligned Bootstrap and Bootstrap Icons cache-busting version labels with `libman.json`.
- Renamed the Pink theme in the UI theme selector from Red to Pink to match the active theme.
- Routed Bootstrap card backgrounds through the existing `--fw-bs-card-bg` token.
- Removed historical Pink theme variant CSS files that were not exposed as selectable themes.

## Scope reviewed
- Reviewed `site.css`, active theme CSS files, shared layout/list/form/dashboard templates, `docs/README.md`, `docs/layout.md`, `docs/templates.md`, `docs/dashboard.md`, `docs/feature_modules.md`, and agent heuristics.
- Reviewed layout head/footer fragments and the UI theme selector for follow-up gap fixes.
- Left unrelated dirty worktree changes untouched.

## Commands used / verification
- Scoped terminology check returned no prohibited draft-specific matches in changed files.
- `rg -n "[ \t]+$" ...` returned no trailing whitespace matches in changed files.
- CRLF check reported `CRLF_OK` for all changed files.
- Static asset reference check for `docs/design_system.html` found Bootstrap CSS, Bootstrap Icons CSS, `site.css`, and Bootstrap JS.
- `git diff --check -- ...` passed for tracked edited docs.
- Attempted to open `docs/design_system.html` through the in-app browser, but direct `file://` navigation was blocked by browser URL policy.
- Follow-up checks confirmed no stale Bootstrap/Bootstrap Icons version labels, old Pink theme selector label, or removed historical theme file references in edited docs/templates.
- Scoped terminology, trailing whitespace, CRLF, and `git diff --check` checks passed after the runtime CSS/template follow-up.
- `dotnet build osafw-app\osafw-app.csproj` could not write to normal `bin/Debug` because IIS Express had `osafw-app.dll` locked.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\` succeeded with 0 warnings and 0 errors; generated build output was removed afterward.
- Review sub-agent found the design guide still used numeric theme labels; those visible labels were changed to Pink, Shadows, and Blue.
- Review sub-agent rechecked the final diff and reported no findings.

## Decisions - why
- Kept the guide as static HTML under `docs/` so it can be opened directly without adding a route or changing runtime behavior.
- Linked real project CSS assets and added only doc-local CSS/JS for previews and controls.
- Addressed implementation gaps that were small, source-backed, and compatible with existing Bootstrap 5 asset usage.
- Left optional theme font imports unchanged per user direction.

## Pitfalls - fixes
- Corrected outdated documentation references to old CSS/template locations.
- Avoided documenting historical Pink theme variants as active selectable themes, then removed those inactive files in the follow-up.
- Kept the card background fix token-based so themes can continue to override the Bootstrap card surface without custom component forks.

## Risks / follow-ups
- Browser visual verification still needs to confirm the static guide renders correctly in the local environment.
- Optional theme font hosting remains a future consideration for offline or strict-network deployments.

## Heuristics (keep terse)
- 2026-05-19: Before adding custom UI CSS, check `docs/design_system.html` and prefer Bootstrap utilities, shared fragments, and framework/theme tokens.

## Testing instructions
- Open `docs/design_system.html` directly in a browser for visual review of theme switching, light/dark mode, and responsive examples. Automated/static checks and isolated build have passed.

## Reflection
- Stable UI guidance was added as documentation and one heuristic. No domain facts, glossary terms, ADRs, or agent instruction changes were added.
