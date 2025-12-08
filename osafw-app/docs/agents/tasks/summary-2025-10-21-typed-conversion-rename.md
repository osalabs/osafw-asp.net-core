## What changed
- Renamed the generic conversion helpers to the `to`/`toList` naming pattern and updated docs and code samples accordingly.
- Defaulted newly scaffolded entities to the primary database config name explicitly via empty `db_config` seed.
- Investigated operator overloading for DTO conversion; concluded framework-wide support would require intrusive changes to every generated Row class, so deferred.

## Commands that worked (build/test/run)
- `dotnet build`

## Pitfalls - fixes
- NuGet downloads timed out on first restore attempt; rerunning after installing .NET SDK completed successfully.

## Decisions - why
- Chose extension-method renaming over operator overloads because implicit/explicit operators must be declared on the DTO types themselves, which would bloat generated code and complicate customization.

## Heuristics (keep terse)
- Prefer extension helpers for cross-workflow conversions; operators demand ownership of both operand types.
