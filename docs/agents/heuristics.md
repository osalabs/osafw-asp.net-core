# Heuristics

- 2025-10-06 — Prefer editing controllers under `osafw-app/App_Code/controllers` and keep action methods suffixed with `Action` to follow framework routing expectations.
- 2025-10-06 — When adding database schema changes, update `osafw-app/App_Data/sql/database.sql` and mirror incremental scripts under `osafw-app/App_Data/sql/updates/`.
- 2025-10-06 — Templates should live in `osafw-app/App_Data/template/<controller>/<view>`; keep directory names lowercase to match routing conventions.
- 2025-10-06 — Run `dotnet test` before committing framework changes to ensure MSTest coverage stays green.
