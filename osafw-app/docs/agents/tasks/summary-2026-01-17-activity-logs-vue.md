## What changed
- Replaced template literal usage in the Vue activity logs component with string concatenation for ParsePage compatibility.

## Commands that worked (build/test/run)
- Not run (not requested).

## Pitfalls - fixes
- ParsePage treats backticks specially, so the return_url string now uses concatenation.

## Decisions - why
- Keep Vue templates compatible with ParsePage by avoiding JS template literals.

## Heuristics (keep terse)
- None.
