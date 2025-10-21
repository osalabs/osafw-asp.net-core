## What changed
- Added reflection-based conversion helpers in `FwExtensions` and refactored `DB`/`ParsePage` to reuse them.
- Introduced generic `FwModel<TRow>` plus typed controller helpers and converted all models to expose nested `Row` DTOs.
- Extended framework controllers with typed `Validate`/`modelAddOrUpdate` workflows for Row-first development.

## Commands that worked (build/test/run)
- _None (environment missing `dotnet` CLI)_

## Pitfalls - fixes
- `dotnet build` unavailable in the container; documented absence instead of running builds.

## Decisions - why
- Kept typed helpers returning null for missing records to parallel empty `DBRow` semantics while enabling `oneOrFailT` for strict flows.
- Leveraged a shared property metadata cache in `FwExtensions` to avoid duplicating reflection caches inside `DB`.

## Heuristics (keep terse)
- Prefer converting typed DTOs via `toHashtable()` + `applyTo` for round-tripping values between models and controllers.
