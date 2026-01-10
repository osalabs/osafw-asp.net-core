## What changed
- Updated autocomplete separator handling and parsing/formatting in FormUtils plus controller quick search logic.
- Adjusted dynamic autocomplete save logic to parse label/id before lookup or creation.
- Refactored Vue quick search parsing into AppUtils with new separator.

## Commands that worked (build/test/run)
- Not run (not requested).

## Pitfalls - fixes
- None.

## Decisions - why
- Kept quick search parsing centralized in AppUtils for reuse with new separator.

## Heuristics (keep terse)
- None.
