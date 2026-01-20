## What changed
- Added shared Vue app/store helper templates to centralize setup logic.
- Updated Vue app/store templates to use shared helpers and reduce duplication.
- Moved AppUtils to a cached asset and relocated date/time helpers from store into AppUtils.

## Commands that worked (build/test/run)
- Not run (not requested).

## Pitfalls - fixes
- Updated layout footer to load AppUtils from assets instead of template include after moving the file.

## Decisions - why
- Centralized app/store setup in shared helpers to simplify overrides and reduce repeated code in Vue templates.
- Kept store-facing format helpers as store methods but delegated to AppUtils for reuse and maintainability.

## Heuristics (keep terse)
- None.
