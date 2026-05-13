# Heuristics for osafw-asp.net-core

Updated: 2026-05-12

- Prefer adding features via controllers/models over modifying core `fw` unless it’s a cross-cutting concern.
- For CRUD screens, first try `FwDynamicController` or `FwVueController` with `config.json` before writing bespoke UI.
- Keep controller actions thin: parse input, call model, prepare `ps`, set `ps["_json"]` when returning JSON.
- For framework method names, follow `docs/naming.md`: prefer result-shape or side-effect prefixes like `list*`, `one*`, `count*`, `add*`, `update*`, and `save*`.
- Use `FormUtils.filter` and `filterCheckboxes` for whitelisting fields and handling checkboxes; avoid manual parsing.
- Use `Fw.model<T>()` singletons; don’t new models directly except through `fw.model("Name")`.
- When returning files, use `fw.fileResponse` to set headers correctly.
- Use `fw.routeRedirect` to chain actions to avoid duplicate logic and keep responses consistent.
- For templates, place overrides under `/osafw-app/App_Data/template/<controller>/<action>`; keep common bits in `/common`.
- Respect access control: set controller `access_level` and add route rules in `FwConfig.access_levels` when needed.
- For DB code, always parameterize via `DB` helper; avoid string concatenation.
- Use `FwCache` for expensive lookups; cache keys should be namespaced and include input parameters.
- Prefer `DateUtils` helpers for formatting and parsing with user timezone (`fw.userTimezone`).
- When adding routes or prefixes, update `FwConfig.route_prefixes` and test `FW.getRoute()`.
- For migrations, add SQL scripts under `osafw-app/App_Data/sql/updates` and register via `fwupdates` flow.
- Log at appropriate level; avoid verbose logs on production (`log_level` INFO).
- In Vue templates, bind disabled states to buttons (not anchors) to avoid `disabled="false"` being rendered and to honor read-only flags.
- 2026-01-17: For Vue form tabs, sync the active tab with the URL query string to keep deep links stable.
- 2026-02-25: Keep OpenAI model constants in canonical provider format (for example `gpt-5-mini`) and avoid alias normalization layers in runtime code.
- 2026-02-25: OpenAI .NET embeddings API (`GenerateEmbedding`) returns `ClientResult<OpenAIEmbedding>`; read `.Value` before converting vector data.
- 2026-04-13: In ParsePage, do not timezone-convert date-only values; only shift real datetimes.
- 2026-04-13: Avoid culture-dependent `DateTime.TryParse` for user-facing dates; use SQL or explicit user format parsing, and rebuild the session when date/time/timezone preferences change.
- 2026-05-11: Keep ParsePage route literal templates such as `App_Data/template/**/url.html` single-line with no trailing newline byte.
- 2026-05-11: If normal build output is locked, build into repo-root `artifacts/` with `-p:OutDir=artifacts/assistant_build/`.
- 2026-05-11: When agent workflow changes, keep `AGENTS.md`, `.github/copilot-instructions.md`, `docs/agents/code_reviewer.md`, and task-summary expectations aligned.
- 2026-05-11: Keep dictionary single-row reads empty-row based, but use `null` for typed single-row reads so missing records cannot masquerade as default DTOs.
- 2026-05-11: For ParsePage recursion protection, prefer a file-include depth limit over cycle detection so legitimate recursive tree templates can render.
- 2026-05-12: When reading `appSettings`, load its direct children into `FwConfig` settings; do not introduce an `appSettings` key inside the runtime settings dictionary.
