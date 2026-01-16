## What changed
- Added Vue date combo and time input handling in form controls and list editing, including a new list-cell-date-combo component and time helpers.
- Updated DemosVue config and vue component includes to enable date_combo/time inputs.
- Replaced template literals with string concatenation per instructions.

## Commands that worked (build/test/run)
- Not run (not requested).

## Pitfalls - fixes
- None.

## Decisions - why
- Converted time inputs to seconds in Vue to match backend storage while displaying HH:MM in the UI.
- Generated date combo values client-side as YYYY-MM-DD to keep Vue SaveAction compatible with existing date conversion.

## Heuristics (keep terse)
- None.
