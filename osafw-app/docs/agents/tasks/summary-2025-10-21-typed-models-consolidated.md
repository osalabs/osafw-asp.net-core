## What changed
- Unified the typed DTO groundwork across `FwExtensions`, `DB`, and `FwModel<TRow>` so generics provide first-class CRUD, caching, and controller integration alongside existing hashtable flows.
- Iteratively aligned naming and helper conventions (`oneT*`, lowercase methods, controller wrappers) to match framework style without redundant conversions.
- Simplified developer scaffolding by deriving typed `Row` classes directly from existing entity metadata during code generation and report/model templates.
- Documented both CRUD workflows in `docs/crud.md`, updated `db.md` to point at the guide, and linked the new documentation from the repository README for easy discovery.

## Commands that worked (build/test/run)
- `dotnet build`
- `dotnet test` *(fails: suite depends on SQL Server and unimplemented helper stubs in this environment)*

## Pitfalls - fixes
- Container lacked the .NET 8 SDK; installed it via Microsoft packages before compiling.
- LibMan attempted to fetch CDN assets offline, so restores were disabled in the project to unblock builds.
- Automated tests continue to fail because they expect SQL Server and completed helper implementations—documented instead of mocking heavy dependencies.

## Decisions - why
- Reused hashtable update paths for typed DTO persistence to keep logging/cache invalidation centralized and avoid double reflection work.
- Normalized property identifiers with `Utils.name2fw` so generated `Row` classes stay stable even when column names contain punctuation.
- Derived typed scaffolding from runtime metadata instead of storing extra JSON keys, reducing drift between generators and live schema.
- Split CRUD documentation into a dedicated guide so developers find model patterns without wading through low-level DB helper notes.

## Heuristics (keep terse)
- Prefer framework helpers (`name2fw`, extension setters) over bespoke regex pipelines.
- Mirror untyped method prefixes when introducing typed variants for easy discovery.
- Treat nullable numeric/date fields as `?` value types during generation; leave reference types as-is.
- Add new developer guides alongside existing documentation hubs to keep discovery friction low.

## Self-reflection
- Multiple feedback rounds highlighted the risk of over-design—next time, trust existing metadata earlier to reduce churn.
- Consolidating task notes exposed redundant documentation; maintain a single evolving summary per workstream to stay organized.
