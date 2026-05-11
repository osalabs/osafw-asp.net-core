## What changed
- Added empty-state markup for attachment/attachment list/file list templates across ParsePage and Vue form controls.
- Refactored AdminDemos show/showform attachment sections to reuse common templates.
- Added JS helper to toggle attachment empty states on add/remove/upload actions and fixed template placeholder filtering.
- Added Vue click handling tweaks for attachment link removal.

## Commands that worked (build/test/run)
- Not run (not requested).

## Pitfalls - fixes
- Empty-state toggling ignored template placeholders; now filters out .tpl clones.

## Decisions - why
- Centralized empty-state handling in common templates and shared JS helper to keep dynamic/Vue/admin screens consistent.

## Heuristics (keep terse)
- None.
