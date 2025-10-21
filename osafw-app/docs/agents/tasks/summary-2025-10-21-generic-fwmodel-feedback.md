## What changed
- Renamed newly introduced helpers to lowercase, rewired `FwExtensions` caches, and exposed the typed conversion API via `@as`, `asList`, and `toKeyValue`.
- Reworked `FwModel<TRow>` to call typed `DB` methods directly, inlined caching by id/icode, and synchronized add/update logic without routing through hashtable overloads.
- Updated `DB` hydration to return `null` when readers are empty, expanded `opIN`/`opNOTIN` to accept `IEnumerable`, and disabled LibMan restore during builds.
- Adjusted typed controller hooks and tests (switching to `Demos.Row`) to align with the new DTO shape and verified builds with the local SDK.

## Commands that worked (build/test/run)
- `dotnet build`
- `dotnet test` *(fails: missing SQL Server + NotImplemented test stubs)*

## Pitfalls - fixes
- LibMan restore still runs offline—set `LibraryRestore` to `False` in the csproj to unblock builds.
- C# reserves `as`, so the extension must be declared as `@as` and callers updated/documented accordingly.
- `Dictionary<string, object>` lacks `Contains`; prefer `ContainsKey` when working with typed rows.

## Decisions - why
- Keep typed CRUD in `FwModel<TRow>` dictionary-based to reuse existing normalization while avoiding double conversions through `Hashtable`.
- Cache typed rows directly via request cache to mirror untyped behavior and prevent stale `Row` instances.
- Leave tests failing that require external services or unimplemented utilities; document the environment gaps instead of stubbing logic.

## Heuristics (keep terse)
- When porting VB-style helpers, audit reserved keywords and rename or escape them early.
- Prefer dictionary flows for DTO mutation when both typed and untyped paths must stay in sync.
