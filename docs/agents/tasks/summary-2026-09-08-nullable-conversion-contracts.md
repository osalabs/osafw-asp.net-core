# Nullable conversion and model declarations

## Objective / acceptance

Express existing null-capable conversion behavior and optional schema columns accurately without changing conversion rules or narrowing attachment size types.

## What changed

Seven string conversion overloads accept nullable strings. Optional descriptions and audit IDs in attachment, attachment-category, demo-dictionary and role-related Rows now describe database nullability. Existing new-row empty-string and zero defaults are retained.

## Requirements / decisions

SQL Server `fwdatabase.sql`, `demo.sql` and `roles.sql` allow NULL for the changed description and audit columns; SQLite schemas agree. This is a targeted declaration correction, not broad model regeneration. File size stays `long` and materializer rules are unchanged.

## Changed contracts

Nullable audit IDs can require callers to handle null or use `GetValueOrDefault()`. Descriptions can produce nullable compiler warnings for unchecked dereferences. Canonical DB documentation and the changelog explain migration. No database update is required.

## Commands used / verification

`dotnet test osafw-tests/osafw-tests.csproj --filter 'FullyQualifiedName~NullableConversionContractTests|FullyQualifiedName~FwExtensionsTests' --verbosity quiet`: 56 passed. Tests cover nullable string calls, defaults and overflow, DBNull materialization for each affected Row, constructor defaults and 64-bit attachment size. The test build compiles the application.

## Risks / follow-ups

No live database was used. Existing source consumers must handle the more accurate nullable audit properties when upgrading.
