# AGENTS.md — Universal, Auto‑Bootstrapping (v5, small)
# Purpose: tiny, reliable rules that ALL coding agents follow without extra prompts.

## Definition of Done (applies to EVERY task)
**Do not end your turn until all items are true:**
1) **Preflight** ran (or **Bootstrap** completed if first run).  
2) **Task** implemented and local checks pass.  
3) **Post‑Task** file written: `docs/agents/tasks/summary-<TASK-ID>-<YYYY-MM-DDTHH:MM:SS>.md` using the embedded template.  
4) If Bootstrap was performed, the **BOOTSTRAP block below was deleted** in this file.

---

## Lifecycle (run these three steps on EVERY task)
### 1) Preflight
Treat as **first run** and execute **Bootstrap** if ANY is true:
- This file still contains `<!-- BEGIN: BOOTSTRAP -->`.
- Any of these are missing: `docs/agents/config.yaml`, `docs/agents/domain/DOMAIN.md`, `docs/agents/domain/glossary.md`, `docs/agents/tasks/`.
- `docs/agents/domain/DOMAIN.md` or `glossary.md` are obvious stubs (empty/bare "TODO").

### 2) Task
- Work on the task per user request
- Prefer project scripts; record only **final working** commands.
- Respect repo conventions; add brief code comments for new/changed code.

### 3) Post‑Task (SELF‑IMPROVEMENT — REQUIRED)
- Create: `docs/agents/tasks/summary-<TASK-ID>-<YYYY-MM-DDTHH:MM:SS>.md` from the template below.
- Include: what changed, commands that worked (per OS if relevant), pitfalls→fixes, decisions→why, and **Heuristics** (see template).  
- (Optional) If heuristics are stable across tasks, also append a one‑liner to `docs/agents/heuristics.md` with the same timestamp.

> **Temporary scratchpad (optional):** You MAY use `docs/agents/tmp/notes-<TASK-ID>.md` during work. Do not commit it.

---

## Repo Map (short; refresh when structure changes)
*(Populate during Bootstrap. Keep 8–16 bullets.)*

## Command Palette (canonical commands by OS)
*(Populate during Bootstrap. Keep it to build/test/run/format/lint/typecheck.)*

---

## Bootstrap (run only on first run; then DELETE the whole block)
<!-- BEGIN: BOOTSTRAP -->
1) Create folders (idempotent):  
   `docs/agents/{tasks,domain,tmp}`
2) Write files (idempotent; merge conservatively if they exist):
   - `docs/agents/config.yaml` (payload below)
   - `docs/agents/domain/DOMAIN.md` (payload below)
   - `docs/agents/domain/glossary.md` (payload below)
   - `docs/agents/tasks/summary.template.md` (payload below)
3) **Repository Analysis, now:** infer tech stack, entry points, test/format/lint tools; fill **Repo Map** & **Command Palette** above with concrete entries.  
4) Stamp **current timestamp** `YYYY-MM-DDTHH:MM:SS` in any generated files.  
5) Commit: `chore(agents): bootstrap + repo analysis`  
6) **Delete this entire BOOTSTRAP block from AGENTS.md** before ending the turn.
<!-- END: BOOTSTRAP -->

### Embedded payloads

```path=docs/agents/config.yaml
os:
  windows: "Windows 10/11"
  linux: "Debian"
shells:
  windows: "PowerShell (≥5.1)"
  linux: "Bash"
runtimes:
  node: "TODO"
  python: "TODO"
  php: "TODO"
  dotnet: "TODO"
entrypoints:
  build:  { windows: "TODO", linux: "TODO" }
  test:   { windows: "TODO", linux: "TODO" }
  run:    { windows: "TODO", linux: "TODO" }
formatters: []
linters: []
notes: "Fill during Bootstrap; keep values real."
```

```path=docs/agents/domain/DOMAIN.md
# Domain — concise brief (≤200 lines)
## Purpose
(autofill from README/routes/configs)

## Core entities & invariants
- (autofill from models/migrations)

## Key flows (3–6)
- (autofill from entry points/controllers)

## Policies / rules (file:line)
- (autofill from configs/guards)

## Non‑goals
- (autofill if declared)
```

```path=docs/agents/domain/glossary.md
# Glossary (≤60 terms)
# Term — one‑line definition (source file:line if known)
```

```path=docs/agents/tasks/summary.template.md
# Summary — <TASK-ID> at <YYYY-MM-DDTHH:MM:SS>

## What changed
- High‑level description (files touched; why).

## Commands that worked
- build:
- test:
- run:

## Pitfalls → fixes
- Symptom → Root cause → Fix

## Decisions → why
- Choice → Alternatives → Rationale

## Heuristics (project‑specific; keep terse)
- <YYYY-MM-DDTHH:MM:SS> — rule…  Expires: <YYYY-MM-DDTHH:MM:SS+90d>

## Next ideas
- Small follow‑ups with value/effort.
```
