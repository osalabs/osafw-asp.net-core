## What changed
- Reworked `FwExtensions` with cached property/membership lookups, new `toKeyValue`/`getReadableMembers`, and converted callers (`DB`, `ParsePage`) to the extension-style APIs.
- Simplified `DB` generics to rely on the new extensions, removed legacy `toClass`, and hardened typed row readers.
- Moved `FwModel<TRow>` into its own file with typed CRUD/caching logic that interoperates with existing logging/cache invalidation semantics; adjusted S3 to remain untyped.
- Updated developer docs to reference the new conversion helpers and cleaned up typed DTO wiring quirks.

## Commands that worked (build/test/run)
- `dotnet build` *(fails: LibMan cannot resolve remote packages in offline environment)*

## Pitfalls - fixes
- Installing the .NET SDK succeeded, but the build stops on LibMan CDN fetches—left the failure documented for awareness.

## Decisions - why
- Typed model `add/update` delegate back to Hashtable workflows after stripping identity/meta fields so logging/cache flows stay intact without double reflection.
- Added request-cache parity for typed retrieval to avoid cross contamination between DBRow and DTO caches.

## Heuristics (keep terse)
- Strip identity/meta fields before delegating to existing Hashtable-based persistence helpers.
