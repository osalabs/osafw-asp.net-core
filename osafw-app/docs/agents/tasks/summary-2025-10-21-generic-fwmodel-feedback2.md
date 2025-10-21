## What changed
- Renamed typed model helpers to keep the `oneT*`/`listT*` prefix ordering consistent with framework naming.
- Simplified `FwModel<TRow>.update` by delegating to the base hashtable overload while keeping DTO field sync intact.

## Commands that worked (build/test/run)
- `dotnet build`
- `dotnet test` *(fails: requires SQL Server + unimplemented conversion helpers)*

## Pitfalls - fixes
- .NET SDK not present in container—install via `dotnet-install.sh` before running builds.
- Integration/unit tests depend on SQL Server and several NotImplemented helpers; document the failure instead of stubbing.

## Decisions - why
- Align method prefixes to ease discovery and maintain parity with untyped variants.
- Reuse the established hashtable update flow to centralize logging/cache invalidation behavior.

## Heuristics (keep terse)
- When extending typed helpers, mirror existing naming/order conventions for quick grepability.
