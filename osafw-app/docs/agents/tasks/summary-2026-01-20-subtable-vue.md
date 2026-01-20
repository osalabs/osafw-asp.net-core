## What changed
- Added subtable row id tracking and Vue save payload for subtable refresh/id reconciliation.
- Updated Vue store subtable submission to skip non-primitive fields and apply server subtable updates after save.

## Commands that worked (build/test/run)
- None.

## Pitfalls - fixes
- None.

## Decisions - why
- Return subtable row id mappings and refreshed rows to let the client reconcile new ids without full reloads.
- Update subtables in-place on the client to preserve user focus during autosave.

## Heuristics (keep terse)
- None.
