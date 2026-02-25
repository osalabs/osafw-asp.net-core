# Heuristics for osafw-asp.net-core

Updated: 2026-02-25

- Prefer adding features via controllers/models over modifying core `fw` unless it’s a cross-cutting concern.
- For CRUD screens, first try `FwDynamicController` or `FwVueController` with `config.json` before writing bespoke UI.
- Keep controller actions thin: parse input, call model, prepare `ps`, set `ps["_json"]` when returning JSON.
- Use `FormUtils.filter` and `filterCheckboxes` for whitelisting fields and handling checkboxes; avoid manual parsing.
- Use `Fw.model<T>()` singletons; don’t new models directly except through `fw.model("Name")`.
- When returning files, use `fw.fileResponse` to set headers correctly.
- Use `fw.routeRedirect` to chain actions to avoid duplicate logic and keep responses consistent.
- For templates, place overrides under `/App_Data/template/<controller>/<action>`; keep common bits in `/common`.
- Respect access control: set controller `access_level` and add route rules in `FwConfig.access_levels` when needed.
- For DB code, always parameterize via `DB` helper; avoid string concatenation.
- Use `FwCache` for expensive lookups; cache keys should be namespaced and include input parameters.
- Prefer `DateUtils` helpers for formatting and parsing with user timezone (`fw.userTimezone`).
- When adding routes or prefixes, update `FwConfig.route_prefixes` and test `FW.getRoute()`.
- For migrations, add SQL scripts under `App_Data/sql/updates` and register via `fwupdates` flow.
- Log at appropriate level; avoid verbose logs on production (`log_level` INFO).
- In Vue templates, bind disabled states to buttons (not anchors) to avoid `disabled="false"` being rendered and to honor read-only flags.
- 2026-01-17: For Vue form tabs, sync the active tab with the URL query string to keep deep links stable.
- 2026-02-25: Keep OpenAI model constants in canonical provider format (for example `gpt-5-mini`) and avoid alias normalization layers in runtime code.
- 2026-02-25: OpenAI .NET embeddings API (`GenerateEmbedding`) returns `ClientResult<OpenAIEmbedding>`; read `.Value` before converting vector data.
