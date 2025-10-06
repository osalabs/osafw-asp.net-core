# AGENTS.md — Universal, Auto‑Bootstrapping (v4)

<!--
This file is designed to be dropped into ANY repo and require no special "bootstrap" prompt.
On the very first normal task request, the coding agent should read this file, detect first‑run via the
AGENTS‑STATE sentinel and/or missing docs, run Bootstrap, then perform the task, then self‑reflect.
On subsequent tasks, it skips Bootstrap and only runs the task + self‑reflection.
-->

<!-- BEGIN: AGENTS-STATE
status: bootstrapped
version: v4
bootstrapped_at: 2025-10-06
last_reflection: 2025-10-06
-->
<!-- END: AGENTS-STATE -->

> **How to use:** Just start by asking for your *first task*. The agent must run the **Bootstrap Check** below before doing anything else.

---

## 0) Always run the **Bootstrap Check** before any task

Treat this as a preflight. If **any** of the checks below fail, run **§11 Bootstrap — Zero‑Edit Setup** (idempotent), then continue with the requested task.

**You MUST treat as first‑run if ANY of the following is true:**

1) The **AGENTS‑STATE** block above is missing, or `status: pending` (or `created_at: PENDING`).  
2) Any of these files are missing:  
   - `docs/agents/config.yaml`  
   - `docs/agents/heuristics.md`  
   - `docs/agents/domain/DOMAIN.md`  
   - `docs/agents/domain/glossary.md`  
   - `docs/agents/domain/knowledge.yaml`  
   - `docs/agents/tasks/discoveries.schema.json`  
3) Placeholders are still present after a run (must be replaced on first run):  
   - `heuristics.md` contains `<YYYY-MM-DD>` or `{{YYYY-MM-DD}}`.  
   - `config.yaml` or domain docs contain `TODO`, `…`, or empty stubs.  
4) The **Repo Map** (§4) or **Command Palette** (§5) sections are empty or obviously generic.  

**On first‑run success you MUST:**
- Update the AGENTS‑STATE block to:
  ```text
  status: bootstrapped
  version: v4
  bootstrapped_at: <today YYYY-MM-DD>
  last_reflection: <today YYYY-MM-DD>
  ```
- Remove the `<!-- BEGIN: BOOTSTRAP-TODO ... END: BOOTSTRAP-TODO -->` block at the end of this file.
- Commit: `chore(agents): bootstrap + analyze repo`

---

## 1) Purpose & Scope

A lightweight operating manual for coding agents that **self‑bootstraps**, **self‑updates**, and **self‑improves**:
- Creates `/docs/agents/**` docs (idempotent).
- Analyzes the repository and fills project‑specific knowledge (domain, glossary, knowledge graph).
- Runs tasks reproducibly (commands recorded per OS).
- Reflects after each task and promotes stable knowledge back into the docs.

Keep this file short; business/domain content belongs in `docs/agents/domain/`.

---

## 2) Governance — Constraints & Budgets

Defaults (edit in `docs/agents/config.yaml` if needed):

- **Allowed tools:** Git, shell (PowerShell/Bash), editor/IDE, GitHub, GitHub Actions, GitHub Copilot, ChatGPT (GPT‑x).
- **Per‑task budgets:** `time ≤ 90m`, `tokens ≤ 150k`, `cost ≤ $X`, `tool_calls_per_min ≤ 6`.
- **Prohibitions:** no secrets; no infra changes; no destructive data ops; no PII dumps.
- **Escalation:** if a budget or safety concern triggers, pause and ask a maintainer.
- **Telemetry in notes:** model/edition, tool versions, commands path, freshness/valid‑until windows.

Prefer **general, scalable** methods by default. If you use a narrow one‑off method, add a one‑line waiver in the task notes (owner, reason, expiry).

---

## 3) Environments & Shells

- **Detect runtime:** PowerShell on Windows (`$PSVersionTable.PSVersion`); Bash on Linux (`uname -s && echo $SHELL`).
- **Rule:** Prefer **PowerShell** on Windows; **Bash** on Linux. If commands diverge by OS, document only the differing parts in the commands files.

---

## 4) Repo Map (8–16 items)

Keep this concise and informative. Populate on Bootstrap and refresh when structure changes.

1. `osafw-app/App_Code/controllers/` — MVC-style controllers inheriting from `FwController` derivatives and handling routing logic.
2. `osafw-app/App_Code/models/` — Data access layer built on `FwModel`, encapsulating CRUD and business rules.
3. `osafw-app/App_Code/fw/` — Framework core helpers including caching, updates, and shared utilities.
4. `osafw-app/App_Data/sql/` — Baseline SQL schema plus incremental update scripts consumed by `FwUpdates`.
5. `osafw-app/App_Data/template/` — ParsePage templates organized by controller/view for HTML rendering.
6. `osafw-app/wwwroot/assets/` — Project-specific frontend assets (CSS, JS, images) shipped with the framework.
7. `osafw-app/wwwroot/lib/` & `libman.json` — Client-side dependencies managed via LibMan.
8. `osafw-app/appsettings.json` — Application configuration (DB connection, mail, logging, layout settings).
9. `osafw-tests/` — MSTest project providing automated coverage for framework components.
10. `docs/` — Developer documentation covering database helpers, ParsePage, dashboard, and datetime utilities.

---

## 5) Command Palette (environment‑aware)

Provide canonical commands per OS for: `build`, `test`, `run`, `format`, `lint`, `typecheck`. Prefer existing project scripts (npm/composer/make/dotnet/etc.). If multiple apply, pick the simplest general method; list alternates in task notes.

| Action    | Linux/macOS                                   | Windows PowerShell                               |
|-----------|-----------------------------------------------|--------------------------------------------------|
| build     | `dotnet build osafw-asp.net-core.sln`         | `dotnet build osafw-asp.net-core.sln`            |
| test      | `dotnet test osafw-asp.net-core.sln`          | `dotnet test osafw-asp.net-core.sln`             |
| run       | `dotnet run --project osafw-app/osafw-app.csproj` | `dotnet run --project osafw-app/osafw-app.csproj` |
| format    | `dotnet format osafw-asp.net-core.sln`        | `dotnet format osafw-asp.net-core.sln`           |
| lint      | `dotnet format --verify-no-changes`           | `dotnet format --verify-no-changes`              |
| typecheck | `dotnet build osafw-asp.net-core.sln`         | `dotnet build osafw-asp.net-core.sln`            |

---

## 6) Task Lifecycle (every task)

### Pre‑Task
- Read: `docs/agents/domain/DOMAIN.md`, `glossary.md`, `knowledge.yaml`, any ADRs.
- Validate versions from `docs/agents/config.yaml`; record the ones used.
- Create `docs/agents/tasks/<YYYY-MM-DD>-<TASK-ID>/` with:
  - `notes.md` (from template),
  - `discoveries.json` (validated by the local schema),
  - `commands.ps1` or `commands.sh`.
- Prefer general, scalable approaches; justify any specialized choice with a one‑line waiver in notes.

### During Task
- Work in small, testable steps; record only **final, working** commands in `commands.ps1/.sh`.
- Keep `notes.md` brief (pitfalls, decisions).
- Capture reusable knowledge in `discoveries.json` with file:line or command evidence when possible.

### Post‑Task (Auto‑Reflection & Promotion)
- Load `discoveries.json` and classify each item:
  - **STABLE FACT** → update **AGENTS.md** (process) *or* `docs/agents/domain/*` (domain).
  - **HEURISTIC** → append to `docs/agents/heuristics.md` with **today’s date** and optional `Expires: <YYYY-MM-DD>` (+90d default).
  - **ONE‑OFF** → keep only in the task’s `notes.md`.
- Update the **AUTO‑HEURISTICS** block in this file (≤7 bullets; drop oldest when adding the 8th).
- Record model/tool editions used and any validity windows in notes.
- If a substantial business decision changed, add an ADR under `docs/agents/adr/`.
- Update `AGENTS‑STATE.last_reflection` to **today**.

---

## 7) Outputs & Publication

- Implementation diff **plus** updated/added tests (passing locally).
- `docs/agents/tasks/<ID>/`: final commands + `notes.md` + `discoveries.json` (**committed**; scripts may be git‑ignored).
- Promotions applied (domain, heuristics, ADRs).
- A PR‑ready description referencing these artefacts.

---

## 8) Quality Bar

- Enforce format/lint.
- Keep branches/commits scoped; PR title ≤72 chars.
- All tests pass locally before opening a PR.

---

## 9) Boundaries & Safety

- Don’t store secrets or large logs; don’t change infra unless explicitly asked.
- Do not commit large binaries; link or stash outside version control.

---

## 10) Pointers

- Domain docs → `docs/agents/domain/`
- Heuristics → `docs/agents/heuristics.md`
- Machine config → `docs/agents/config.yaml`
- Specs/acceptance → `docs/agents/specs/`

---

## 11) **Bootstrap — Zero‑Edit Setup** (run when the Bootstrap Check indicates first‑run)

**Do all steps idempotently; then run the originally requested task.**

1. **Create folders**  
   `docs/agents/{tasks,domain,specs,adr}`

2. **Append to root `.gitignore`** (idempotent lines):  
   - `docs/agents/**/commands.ps1`  
   - `docs/agents/**/commands.sh`  
   > Do **not** ignore the entire `docs/agents/tasks/` folder; keep `notes.md` and `discoveries.json` committed.

3. **Write/merge Embedded Payloads** (below). Merge conservatively; prefer minimal, reversible diffs.

4. **Repository Analysis Pass (MANDATORY)** — extract concrete project knowledge **now**:
   - Detect languages/tooling: Node/TS, PHP, Python, .NET, Java, Go, etc.
   - **Environment Facts:** OS hints, shells, runtimes, frameworks, package managers.
   - **Architecture cues:** frameworks (e.g., MVC), entry points, route maps, controllers/handlers, CLI tools, configs.
   - **Data layer:** migrations/schema definitions; ORM models; key tables/collections.
   - **Tests & quality:** presence of tests, runner, linters, formatters, type‑checkers.
   - Produce:
     - **Repo Map** (§4) with 8–16 salient items.
     - **Command Palette** (§5) with commands that actually run.
     - **Domain docs** filled:
       - `DOMAIN.md` → entities (5–12), 3–6 key flows, 5–12 policies/invariants, non‑goals.
       - `glossary.md` → 12–30 initial terms (one‑liners).
       - `knowledge.yaml` → initial entities/relations inferred from code/schema.
     - **Heuristics** → add 3–7 repo‑specific “gotchas” with **today’s date**.
     - **Versions** → write detected tool/runtime editions in the current task’s `notes.md`.

5. **Update AGENTS‑STATE** (`status: bootstrapped`, stamp dates) and delete the **BOOTSTRAP‑TODO** block below.

6. **Commit**: `chore(agents): bootstrap + analyze repo`

### Embedded Payloads (authoritative content to write)

```path=docs/agents/config.yaml
os:
  windows: "Windows 10/11"
  linux: "Debian-based (cloud)"
shells:
  windows: "PowerShell 7+"
  linux: "Bash"
runtimes:
  node: "TODO"
  python: "TODO"
  php: "TODO"
  dotnet: "TODO"
packageManagers: ["winget","choco","npm","pnpm","pip","pipx","composer"]
entrypoints:
  build: { windows: "TODO", linux: "TODO" }
  test:  { windows: "TODO", linux: "TODO" }
  run:   { windows: "TODO", linux: "TODO" }
governance:
  version: "v1"
  budgets: { time: "90m", tokens: "150k", cost: "$X", tool_calls_per_min: "6" }
  tools_allowed: ["GitHub Copilot","ChatGPT (GPT-x)"]
  prohibitions: ["no secrets","no infra changes","no destructive data ops"]
  escalation: { on_budget_exhaust: "pause+ask", safety_override: "maintainer only" }
  telemetry: ["model_edition","tool_versions","commands_path","freshness_window"]
```

```path=docs/agents/heuristics.md
# Heuristics

## <YYYY-MM-DD>
- Prefer the smallest change that passes tests; expand only if needed.
- When commands differ by OS, document only the differing one. Expires: <YYYY-MM-DD>
- If multiple build systems exist, surface the simplest general method first; list alternates in notes.

# How to update this file
- Always replace dates with **today’s date (YYYY-MM-DD)**.
- Default **Expires** to **+90 days** unless otherwise specified.
```

```path=docs/agents/domain/DOMAIN.md
# Domain — Purpose & Bounded Context  (keep ≤200 lines; domain/business only)

## Core Entities & Invariants
- (autofilled from code, routes, migrations, models)

## Key Flows (3–6)
- (main request/CLI flows summarized from entry points and controllers)

## Policies / Rules (source/version)
- (invariants from code and config with file:line references)

## Non‑Goals
- (what the system explicitly does not do)

## Links
- ADRs: ../adr/
- Specs: ../specs/
```

```path=docs/agents/domain/glossary.md
# Glossary (≤60 terms)
# Term  — one‑line definition (source file:line if known)
```

```path=docs/agents/domain/knowledge.yaml
version: 1
entities: []   # {id, fields, invariants, source}
relations: []  # {from, to, type, source}
ids: []        # {name, location}
policies: []   # {rule, source}
events: []     # {name, payload, source}
```

```path=docs/agents/specs/example.feature
Feature: Core flow example
  Scenario: Skeleton runs
    Given the repository is cloned
    When I execute the run command
    Then I see a successful start signal
```

```path=docs/agents/tasks/notes.template.md
# Task Notes — <TASK-ID> <short title>

## Summary
* Objective:
* Affected areas:

## Environment used
* OS/Shell:
* Tool versions: node, python, php, dotnet (as applicable)

## Commands that worked
* build:
* test:
* run:

## Pitfalls & fixes
* Symptom → Root cause → Fix

## Decisions & rationale
* Choice → Alternatives → Why

## Candidates to promote
* (link to discoveries.json entries)

## Autonomy & budgets
* Tools used:
* Budgets used: time=__, tokens=__, cost=$__
* Escalation events (if any):

## Approach selection (general vs specialized)
* Candidates considered → chosen (why)
* Waiver (if any): owner, reason, expiry

## Editions & freshness
* Model/tool editions pinned:
* Valid‑until window for any evidence/benchmarks:

## Reflection Summary (post‑task)
* Promotions:
* Deferred:
* Assumptions:
```

```path=docs/agents/tasks/discoveries.schema.json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "Task Discoveries",
  "type": "array",
  "items": {
    "type": "object",
    "required": ["date","title","category","scope","evidence","recommendation"],
    "properties": {
      "date": { "type": "string", "pattern": "^\d{4}-\d{2}-\d{2}$" },
      "title": { "type": "string", "maxLength": 120 },
      "evidence": { "type": "string", "maxLength": 400 },
      "category": { "type": "string", "enum": ["tooling","domain","process"] },
      "scope": { "type": "string", "enum": ["task","project"] },
      "source_paths": { "type": "array", "items": { "type": "string" } },
      "recommendation": { "type": "string", "maxLength": 400 },
      "promotion_candidate": { "type": "boolean", "default": true },
      "expires": { "type": "string", "pattern": "^\d{4}-\d{2}-\d{2}$" },
      "tags": { "type": "array", "items": { "type": "string" } },
      "links": { "type": "array", "items": { "type": "string", "format": "uri" } }
    },
    "additionalProperties": false
  }
}
```

---

## Heuristics (auto‑curated; keep ≤7 bullets)

<!-- BEGIN: AUTO-HEURISTICS -->
* 2025-10-06 — Prefer editing controllers under `osafw-app/App_Code/controllers` and keep action methods suffixed with `Action` to match routing.
* 2025-10-06 — Maintain SQL schema in `App_Data/sql/database.sql` and versioned updates in `App_Data/sql/updates/`.
* 2025-10-06 — Store ParsePage templates in lowercase directories under `App_Data/template/<controller>/<view>` to align with dispatcher conventions.
* 2025-10-06 — Run `dotnet test` before committing to catch regressions in framework components.
<!-- END: AUTO-HEURISTICS -->

