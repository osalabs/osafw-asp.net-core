# ADR 001: Initial Framework Architecture

* **Status** | Accepted
* **Date** | 2020-05-07
* **Authors** | Oleg Savchuk
* **Affected parts** | Controllers, Models, Core

---

## Context
The project started as a lightweight alternative to full ASP.NET MVC frameworks. We wanted minimal dependencies and an MVC-like structure that is easy to maintain and extend.

## Decision
We build a custom dispatcher in `FW.cs` that maps routes to controllers under `App_Code/controllers`. Controllers derive from `FwController` and models from `FwModel`. The folder layout mirrors the original OSA framework.

## Consequences
The framework runs on ASP.NET Core but keeps a small footprint. Developers must follow the custom conventions rather than the built-in MVC pipeline. The decision simplifies onboarding for existing OSA developers but differs from standard ASP.NET practices.

## Alternatives Considered (optional)
- ASP.NET MVC: rejected as too heavyweight for our needs.

## References (optional)
Initial commit [`61dd522`](../../commit/61dd522c6c5114f5a0b9506eb07e2b80767e40ae)
Related: [ADR 002](20200518-custom-db-helper.md), [ADR 004](20210811-parsepage-engine.md).