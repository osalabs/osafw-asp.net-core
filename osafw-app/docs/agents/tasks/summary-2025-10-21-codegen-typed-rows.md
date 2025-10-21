## What changed
- Added cached C# metadata (`cs_type`, `cs_property`) for entity fields and ensured JSON serialization keeps it.
- Generated typed `Row` classes when scaffolding models from Demo templates, including DBName attributes when property names differ.
- Updated developer utilities (`CodeGen`, `EntityBuilder`, `ConfigJsonConverter`) to rely on shared metadata helpers and sanitize DTO conversion.

## Commands that worked (build/test/run)
- dotnet build

## Pitfalls - fixes
- dotnet SDK was missing in the container; installed `dotnet-sdk-8.0` via apt before building.
- Normalized `Split` usage and method overload names to avoid ambiguous compile errors.

## Decisions - why
- Stored `cs_type`/`cs_property` in entity definitions so subsequent scaffolding steps and JSON round-trips retain typed metadata without recomputing reflection.
- Reused metadata helpers during code generation to keep typed DTOs consistent with runtime conversion logic and minimize template drift.

## Heuristics (keep terse)
- Prefer metadata enrichment at parse/save boundaries to avoid ad-hoc recomputation.
- Keep codegen regex replacements minimal and scoped to exact Row blocks.
