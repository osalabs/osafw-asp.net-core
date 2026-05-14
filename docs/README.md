# Documentation Map

Use this file to find the right document quickly instead of searching the whole `docs/` tree every time.

## Start Here

- Read [../AGENTS.md](../AGENTS.md) first for workflow, repo expectations, and task-summary rules.
- Read [templates.md](templates.md) when touching ParsePage templates, shared UI fragments, or template engine behavior.
- Read [dynamic.md](dynamic.md) when the task involves `FwDynamicController`, `FwVueController`, or `config.json`.
- Read [crud.md](crud.md) and [db.md](db.md) when the task is mostly model/data-access work.

## Framework Docs

- [templates.md](templates.md): ParsePage templates, shared partials, dynamic-controller template conventions, and parser rules that matter during implementation.
- [dynamic.md](dynamic.md): `config.json` reference for dynamic and Vue controllers.
- [crud.md](crud.md): `FwModel` CRUD workflows using `FwDict`/`FwList` or typed DTOs.
- [db.md](db.md): low-level `DB` helper usage, raw SQL helpers, and SQL Server/SQLite/MySQL provider setup.
- [naming.md](naming.md): standard framework naming conventions for methods, casing, result-shape prefixes, and side-effect prefixes.
- [layout.md](layout.md): shared layout structure, CRUD headers, and theming extension points.
- [dashboard.md](dashboard.md): dashboard pane types and how to add custom ones.
- [datetime.md](datetime.md): per-user date/time formatting, timezone conversion, and save-path normalization.
- [feature_modules.md](feature_modules.md): module scaffolding from `/Dev/Manage` or manual setup.

## Agent Docs

- [agents/domain.md](agents/domain.md): stable framework/domain facts.
- [agents/heuristics.md](agents/heuristics.md): terse working heuristics discovered during tasks.
- [agents/glossary.md](agents/glossary.md): project terms and framework vocabulary.
- [agents/code_reviewer.md](agents/code_reviewer.md): review loop instructions for code reviewer agents.
- [agents/mcp.md](agents/mcp.md): MCP usage and troubleshooting notes.

## Which Doc to Use

- If the task changes templates or screen composition: start with `templates.md`, then `layout.md`, then `dynamic.md` if a dynamic controller is involved.
- If the task changes models, queries, or save flows: start with `crud.md`, then `db.md`, then `datetime.md` if date fields are involved.
- If the task adds or renames framework methods, variables, constants, or generated module helpers: read `naming.md`.
- If the task affects repo workflow or agent instructions: read `AGENTS.md`, `agents/code_reviewer.md`, `.github/copilot-instructions.md`, and the active task summary in `docs/agents/tasks/`.
