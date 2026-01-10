## What changed
- Simplified attachment list templates to use direct cards without row/col wrappers.
- Updated modal selection JS and fw.js empty-state/remove handling for the new structure.
- Added flex-based layout styles for attachment cards.

## Commands that worked (build/test/run)
- Not run (UI-only changes).

## Pitfalls - fixes
- None observed.

## Decisions - why
- Used flex layout on `.att-list` to keep spacing consistent after removing Bootstrap row/col wrappers.

## Heuristics (keep terse)
- Prefer container-scoped selectors (e.g., `.fw-att-block`) when layout wrappers change.
