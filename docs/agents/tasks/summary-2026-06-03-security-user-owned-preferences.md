## What changed
- Added owner-aware access helpers for saved user preference records.
- Updated saved view, filter, and list flows so users only load or mutate records they are allowed to use.
- Added focused tests for owner and shared/system preference behavior.

## Scope reviewed
- Saved views, filters, and lists.
- Controller helpers that load user preference records by id.
- Related tests and docs for the changed access contract.

## Commands used / verification
- Ran targeted source searches for preference record loading and mutation flows.
- Ran focused tests for saved user preference authorization.
- Built the app and checked formatting/diff hygiene.

## Decisions - why
- Put ownership checks in the model-level helpers so controllers share one access contract.
- Preserved shared/system preference behavior where the framework already supports it.

## Pitfalls - fixes
- Some flows loaded preference records directly by id; these were changed to use owner-aware helpers.

## Risks / follow-ups
- Future preference features should use the owner-aware helpers rather than raw id lookups.
- No schema changes were involved.

## Heuristics (keep terse)
- Direct id loads for user-owned records need an ownership or shared-record predicate.

## Testing instructions
Run the focused user preference authorization tests and build the app.

## Reflection
Centralizing the access check in model helpers made the fix smaller and reduced the chance of future controller drift.
