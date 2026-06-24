## What changed
- Completed bootstrap: documented project in `AGENTS.md`, created `docs/agents/{heuristics,domain,glossary}.md`.
- Removed bootstrap block from `AGENTS.md`.

## Commands that worked (build/test/run)
- dotnet build

## Pitfalls - fixes
- Ensure SQL Server session and keys tables exist when running app; otherwise startup can fail due to distributed cache/data protection.

## Decisions - why
- Kept documentation concise and colocated with repo conventions.

## Heuristics (keep terse)
- Prefer Dynamic/Vue controller configs before custom UI.
- Use `FormUtils` and `DateUtils` helpers; keep controllers thin.
- Cache with `FwCache` for expensive lookups.
