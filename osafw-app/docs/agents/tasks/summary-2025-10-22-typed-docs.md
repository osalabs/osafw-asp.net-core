## What changed
- documented typed vs Hashtable model workflows with CRUD examples and controller integration notes
- recorded build/test status after installing .NET SDK locally for verification

## Commands that worked (build/test/run)
- `dotnet build`

## Pitfalls - fixes
- container image lacked .NET SDK; installed dotnet-sdk-8.0 from Microsoft apt feed before running builds
- integration/unit tests still depend on SQL Server and unimplemented helpers, producing expected failures

## Decisions - why
- extended existing db helper doc instead of adding a new file to keep model guidance alongside CRUD primitives developers already consult

## Heuristics (keep terse)
- prefer enhancing established docs when adding adjacent guidance to reduce doc sprawl
