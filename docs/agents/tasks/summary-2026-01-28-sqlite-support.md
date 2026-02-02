## What changed
- Added SQLite support to the DB helper, including connection handling, schema inspection, identity retrieval, and foreign key discovery.
- Added SQLite schema/init SQL scripts under App_Data/sql/sqlite and a Users CRUD test that initializes SQLite schema.
- Added Microsoft.Data.Sqlite package reference for SQLite connectivity.
- Switched distributed session cache to in-memory when using SQLite to avoid SQL Server cache dependency.
- Adjusted SQLite foreign key metadata mapping to use string values.

## Commands that worked (build/test/run)
- /root/dotnet/dotnet build /p:LibraryRestore=false (succeeds; warning in ConvUtilsTests)

## Pitfalls - fixes
- dotnet build failed previously due to libman CDN resolution errors when restoring front-end libraries; using LibraryRestore=false allowed build to complete.

## Decisions - why
- SQLite schema scripts mirror SQL Server core tables to support local development and tests without SQL Server.
- Users CRUD test initializes SQLite schema from App_Data/sql/sqlite to validate DB helper + model workflows.
- SQLite does not ship a built-in distributed cache provider, so sessions use in-memory cache for standalone setups.

## Heuristics (keep terse)
- Prefer SQLite PRAGMA introspection for schema and foreign key metadata.
