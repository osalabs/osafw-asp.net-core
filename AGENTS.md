# AGENTS.md — v5.nano (one‑page, DoD‑gated)

**You are a coding agent. Don’t finish until the Post‑Task step is done.**

## Repo Map (generated at 2025-10-07T00:00:00)
- Root: `osafw-asp.net-core/`
- Projects
  - `osafw-app/` — ASP.NET Core app (.NET 8)
    - `App_Code/` — framework (`fw/`), controllers (`controllers/`), models (`models/`), utilities
    - `Program.cs` — app entrypoint
    - `wwwroot/` — static assets (Bootstrap, etc.)
    - `App_Data/sql/` — database scripts (e.g., `fwdatabase.sql`)
  - `osafw-tests/` — xUnit tests (.NET 8)
    - `App_Code/fw/*Tests.cs` — unit tests for framework utilities
- Docs (agents): `docs/agents/{config.yaml, domain/*, tasks/*, tmp/}`

## Command Palette
- Build: `dotnet build`
- Test: `dotnet test`
- Run (app): `dotnet run --project osafw-app`

## DoD (every task)
1) Preflight passed (or Bootstrap done).  
2) Task delivered + local checks pass.  
3) Wrote `docs/agents/tasks/summary-<TASK-ID>-<YYYY-MM-DDTHH:MM:SS>.md` (template below).  
4) If Bootstrap ran, removed the BOOTSTRAP block from this file.

## Preflight
Run **Bootstrap** if this file still contains `<!-- BEGIN: BOOTSTRAP -->` **or** `docs/agents/config.yaml`/`docs/agents/domain/*`/`docs/agents/tasks/` are missing.

## Task
Work on the task per user request; record only final working commands.

## Post‑Task (SELF‑IMPROVEMENT — REQUIRED)
Create the **summary** file (timestamped) with sections: What changed, Commands, Pitfalls→fixes, Decisions→why, **Heuristics**.  
(Optional scratchpad during work: `docs/agents/tmp/notes-<TASK-ID>.md`, not committed.)

### Payloads
```path=docs/agents/config.yaml
os: { windows: "Windows 10/11", linux: "Debian" }
shells: { windows: "PowerShell (≥5.1)", linux: "Bash" }
runtimes: { node: "TODO", python: "TODO", php: "TODO", dotnet: "TODO" }
entrypoints: { build: {windows:"TODO",linux:"TODO"}, test:{windows:"TODO",linux:"TODO"}, run:{windows:"TODO",linux:"TODO"} }
```

```path=docs/agents/domain/DOMAIN.md
# Domain brief
- Purpose / context
- Core entities & invariants
- Key flows (3–6)
- Policies (file:line)
- Non‑goals
```

```path=docs/agents/domain/glossary.md
# Glossary (≤60 terms) — Term: one‑liner
```

```path=docs/agents/tasks/summary.template.md
# Summary — <TASK-ID> at <YYYY-MM-DDTHH:MM:SS>

## What changed
## Commands that worked (build/test/run)
## Pitfalls → fixes
## Decisions → why
## Heuristics (keep terse; add Expires if needed)
