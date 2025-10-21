## What changed
- Renamed the typed fetch helper to `oneTOrFail` for consistency with the `oneT*` prefix convention and updated controller usage.

## Commands that worked (build/test/run)
- dotnet build

## Pitfalls - fixes
- None.

## Decisions - why
- Align method naming with framework feedback while keeping the generic controller flow untouched.

## Heuristics (keep terse)
- Keep typed helper prefixes consistent across controllers/models.
