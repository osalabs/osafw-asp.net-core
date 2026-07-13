# Heuristics for osafw-asp.net-core

Reviewed: 2026-07-13

Keep this file for recurring working heuristics that are not already contracts in `AGENTS.md` or a canonical topic doc. Framework facts belong in `docs/agents/domain.md`; task-specific lessons belong in the active summary.

- Prefer adding app/example behavior through controllers, persistence models, templates, or config; modify `App_Code/fw` only for a reusable cross-cutting framework concern or contract bug.
- For standard CRUD screens, try `FwDynamicController` or `FwVueController` with `config.json` before building bespoke UI.
- Use `FormUtils.filter` and `filterCheckboxes` to whitelist form fields and normalize checkboxes instead of open-ended manual request parsing.
- Reuse models through the current `FW` instance (`fw.model<T>()` or `fw.model(name)`); do not construct persistence models directly unless a non-framework test or isolated tool genuinely requires it.
- Return files through `fw.fileResponse` so content type, disposition, and response completion stay consistent.
- Use `fw.routeRedirect` when one action intentionally reuses another action's flow rather than duplicating controller logic.
- Before custom UI CSS, check `docs/design_system.html` and prefer Bootstrap utilities, shared fragments, and framework/theme tokens.
- Use `FwCache` only for meaningfully expensive/repeated work; namespace keys and include every input that changes the cached result.
- When adding route prefixes, update `FwConfig.route_prefixes` and exercise the corresponding `FW.getRoute()` behavior.
- Keep production logging below verbose/debug levels unless a scoped diagnostic period is intentional.
- With database-backed ASP.NET Core session, avoid writing session state on every request; unnecessary writes can race and overwrite newer login state.
- Do not lowercase or otherwise normalize a complete encoded URL; preserve case-sensitive path/query data and cover mixed-case values in URL-helper tests.
