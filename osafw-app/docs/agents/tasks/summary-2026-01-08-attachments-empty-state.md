## What changed
- Added empty-state markup for attachment/attachment list/file list templates across ParsePage and Vue form controls.
- Refactored AdminDemos show/showform attachment sections to reuse common templates.
- Added JS helper to toggle attachment empty states on add/remove/upload actions.

## Commands that worked (build/test/run)
- Not run (not requested).

## Pitfalls - fixes
- None.

## Decisions - why
- Centralized empty-state handling in common templates and shared JS helper to keep dynamic/Vue/admin screens consistent.

## Heuristics (keep terse)
- None.
