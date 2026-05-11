# ADR 004: Use ParsePage Template Engine

* **Status** | Accepted
* **Date** | 2021-08-11
* **Authors** | Oleg Savchuk
* **Affected parts** | Views, Controllers

---

## Context
Razor views are powerful but verbose. The original OSA framework used ParsePage for simple token-based templates. Maintaining compatibility with existing templates eased the migration to .NET Core.

## Decision
We integrate the [ParsePage](https://github.com/osalabs/parsepage) engine. Templates live under `App_Data/template` and are loaded and parsed at runtime. Controllers prepare a view hash that the engine expands into HTML.

## Consequences
Developers familiar with the old framework can reuse their templates without rewriting them in Razor. However, IDE tooling is limited compared to Razor views.

## Alternatives Considered (optional)
- Razor view engine: rejected for simplicity and backward compatibility reasons.

## References (optional)
Commit [`8043982`](../../commit/80439823dca70f078fba3de62d24739eedbbb2f1)
See [ADR 001](20200507-initial-architecture.md) for overall framework design.
