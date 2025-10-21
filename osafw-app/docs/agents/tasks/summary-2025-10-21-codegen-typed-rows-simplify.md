## What changed
- Removed cached `cs_property`/`cs_type` augmentation from `DevEntityBuilder` so entity JSON stays minimal while still computing `fw_name`/`fw_type` metadata.
- Regenerated typed Row scaffolding in `DevCodeGen` by deriving property names and C# types at generation time using the existing field descriptors.
- Updated `ConfigJsonConverter` ordering to match the trimmed entity metadata surface.

## Commands that worked (build/test/run)
- dotnet build
- dotnet test *(fails: NotImplemented placeholders and missing SQL Server instance)*

## Pitfalls - fixes
- `dotnet` CLI absent in container; installed Microsoft packages and SDK 8.0.
- `apt-get update` reports 403 on mise.jdx.dev mirror; ignored as unrelated to build.
- Unit suite depends on SQL Server and unimplemented conversion helpers, so tests fail after compilation.

## Decisions - why
- Prefer recomputing typed metadata during generation to avoid persisting redundant fields in `db.json` and keep scaffolding deterministic.
- Treat SQL `bit` columns as `bool` and respect nullable flags when emitting DTO members for better typed ergonomics.

## Heuristics (keep terse)
- Sanitize fallback identifiers with `name2fw`, collapse punctuation, prefix underscores when needed.
- Append `?` to value types when `is_nullable` equals `1`.
