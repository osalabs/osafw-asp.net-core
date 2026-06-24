# ADR 002: Custom DB Helper

* **Status** | Accepted
* **Date** | 2020-05-18
* **Authors** | Nikita Kramarenko
* **Affected parts** | Models, DB

---

## Context
Entity Framework and other ORMs introduce overhead and complexity. We wanted a simple layer compatible with the previous OSA framework and suitable for porting existing code.

## Decision
`DB.cs` wraps ADO.NET and exposes helpers like `row`, `array` and `exec`. Models use this helper through `FwModel` to access SQL Server or MySQL.

## Consequences
The codebase avoids heavy ORM abstractions, keeping queries explicit. It requires manual SQL but remains easy to debug and port.

## Alternatives Considered (optional)
- Entity Framework Core: declined for performance and migration control reasons.

## References (optional)
Commit [`b3bff60`](../../commit/b3bff607cbcc3b6008d7c71e0d26a28de2e0c206)
See [ADR 001](20200507-initial-architecture.md) for the broader context.