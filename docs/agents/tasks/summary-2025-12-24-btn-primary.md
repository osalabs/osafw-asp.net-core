## What changed
- Standardized primary button styling for CRUD entry points: list “Add New” buttons now use `btn-primary`, view “Edit” buttons now use `btn-primary`, and Vue view delete uses `btn-danger`.
- Kept secondary actions (like Add New on view screens and cancel links) on secondary/default styles to preserve hierarchy.

## Commands that worked (build/test/run)
- Not run (template-only updates).

## Pitfalls - fixes
- None encountered.

## Decisions - why
- Treated list-level Add New as the primary entry action, so promoted to `btn-primary`.
- Promoted view-level Edit to `btn-primary` to match guidance while keeping destructive actions on `btn-danger` and Add New secondary.

## Heuristics (keep terse)
- Shared templates should express button hierarchy: primary actions use `btn-primary`, destructive actions `btn-danger`, and secondary navigation keeps default/secondary styling.
