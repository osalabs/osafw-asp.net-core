## What changed
- Added Vue date combo and time input handling in form controls and list editing, including a new list-cell-date-combo component and time helpers.
- Updated DemosVue config and vue component includes to enable date_combo/time inputs.
- Replaced template literals with string concatenation per instructions.
- Forced date combo selects to stay on a single line and normalized date combo values on save.
- Centralized Vue date combo normalization and post-save handling in dynamic controller logic, and avoided re-parsing time values already submitted as seconds.

## Commands that worked (build/test/run)
- Not run (not requested).

## Pitfalls - fixes
- None.

## Decisions - why
- Converted time inputs to seconds in Vue to match backend storage while displaying HH:MM in the UI.
- Generated date combo values client-side as YYYY-MM-DD to keep Vue SaveAction compatible with existing date conversion.
- Normalized date combo values server-side to avoid date parsing issues.
- Skipped post-save date combo recomputation when combo parts are not submitted.
- Avoided double-parsing time values already submitted as seconds from Vue.

## Heuristics (keep terse)
- None.
