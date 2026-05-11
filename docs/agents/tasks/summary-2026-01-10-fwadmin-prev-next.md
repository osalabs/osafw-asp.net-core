## What changed
- added shared prev/next navigation helpers in FwController/FwModel and reused them in FwDynamicController
- added Prev/Next navigation action for FwAdminController
- addressed review comments: inline list view selection and documented prev/next traversal logic

## Commands that worked (build/test/run)
- not run (not requested)

## Pitfalls - fixes
- none

## Decisions - why
- kept prev/next logic in a shared helper to avoid duplicating URL/id navigation logic across controllers

## Heuristics (keep terse)
- none
