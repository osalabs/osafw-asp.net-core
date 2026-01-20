## What changed
- Added subtable row id tracking and Vue save payload for subtable refresh/id reconciliation.
- Updated Vue store subtable submission to skip non-primitive fields and apply server subtable updates after save.
- Guarded subtable reconciliation on the client to avoid overwriting in-progress rows when save responses include validation errors.
- Send the active form tab during Vue autosave so server-side validation/save runs against the correct tab fields.

## Commands that worked (build/test/run)
- None.

## Pitfalls - fixes
- Autosave responses with validation errors could wipe client subtable rows; now reconciliation is skipped on error.
- Tab-specific form saves ignored subtable fields when the tab wasn't posted; sending tab fixes validation/save for subtable rows.

## Decisions - why
- Return subtable row id mappings and refreshed rows to let the client reconcile new ids without full reloads.
- Update subtables in-place on the client to preserve user focus during autosave.
- Skip applying server subtable payloads when the response reports validation errors.
- Include the active tab on autosave to ensure tab-scoped subtable fields are processed.

## Heuristics (keep terse)
- None.
