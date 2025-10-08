# AGENTS.md — v5.nano (one‑page, DoD‑gated)

**You are a coding agent. Don’t finish until the Post‑Task step is done.**

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

## Bootstrap (first run only — then DELETE this block)
<!-- BEGIN: BOOTSTRAP -->
- Make folders: `docs/agents/{tasks,domain,tmp}`  
- Write: `docs/agents/config.yaml`, `docs/agents/domain/DOMAIN.md`, `docs/agents/domain/glossary.md`, `docs/agents/tasks/summary.template.md` (use payloads below).  
- Analyze repo NOW; add a short **Repo Map** and **Command Palette** beneath this file’s headings.  
- Use timestamps as `YYYY-MM-DDTHH:MM:SS`.  
- Commit: `chore(agents): bootstrap`  
- **Delete this block**.
<!-- END: BOOTSTRAP -->

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
```
