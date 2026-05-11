## What changed
- Ensured Vue init payload includes is_activity_logs for view screen rendering.
- Improved activity logs UI with autofocus on comment input and username/badge delimiter.

## Commands that worked (build/test/run)
- Not run (not requested).

## Pitfalls - fixes
- Activity block visibility required passing is_activity_logs in initial Vue payload.

## Decisions - why
- Keep parity with dynamic templates by mirroring activity log visual separators and focus behavior.

## Heuristics (keep terse)
- None.
