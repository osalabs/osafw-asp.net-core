## What changed
- Removed theme-hostile background/text utilities from sidebar templates and main sidebar layout wrapper.
- Added data-fw-theme attribute to HTML layouts for stable theme identification.
- Added aria-sort updates for sortable table headers.
- Updated theme20 sidebar active background to improve active link contrast.
- Updated default sidebar active background token to ensure visible selection state.

## Commands that worked (build/test/run)
- 

## Pitfalls - fixes
- 

## Decisions - why
- Use data-fw-theme with ui_theme defaulting to "default" to provide a stable theming hook.
- Set theme20 active sidebar background to a translucent white for visible selection state.
- Use accent-based translucent background for default sidebar active state.

## Heuristics (keep terse)
- 
