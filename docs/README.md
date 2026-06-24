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
- [settings.md](settings.md): database-backed Site Settings schema, runtime API, admin module behavior, and seeding guidance.
- [naming.md](naming.md): standard framework naming conventions for methods, casing, result-shape prefixes, and side-effect prefixes.
- [design_system.html](design_system.html): visual design system for the Bootstrap-based framework UI, active themes, tokens, and component examples.
- [layout.md](layout.md): shared layout structure, CRUD headers, and theming extension points.
- [dashboard.md](dashboard.md): dashboard pane types and how to add custom ones.
- [assistant.md](assistant.md): optional read-only RAG assistant, LLM configuration, KB indexing, and vector backends.
- [deploy.md](deploy.md): Windows/IIS deployment setup for production, staging, and develop instances.
- [reports.md](reports.md): hardcoded report development and Site Admin-managed custom SQL reports.
- [datetime.md](datetime.md): per-user date/time formatting, timezone conversion, and save-path normalization.
- [feature_modules.md](feature_modules.md): module scaffolding from `/Dev/Manage` or manual setup.

## Agent Docs

- [agents/domain.md](agents/domain.md): stable framework/domain facts.
- [agents/heuristics.md](agents/heuristics.md): terse working heuristics discovered during tasks.
- [agents/glossary.md](agents/glossary.md): project terms and framework vocabulary.
- [agents/code_reviewer.md](agents/code_reviewer.md): review loop instructions for code reviewer agents.
- [agents/mcp.md](agents/mcp.md): MCP usage and troubleshooting notes.
- [agents/tasks/index.md](agents/tasks/index.md): compact task-history index to search before opening full task summaries.
- [agents/tools/](agents/tools/): reusable helper scripts for scoped repo search and text normalization.
- [prompts/](prompts/): reusable prompts for recurring development workflows such as orchestration, framework upgrades, PR reviews, agent reflection, security hardening, docs consistency, and test stabilization.

## Which Doc to Use

- If the task changes visual styling, shared UI components, or theme behavior: start with `design_system.html`, then `layout.md`, then `templates.md` if shared fragments are involved.
- If the task changes templates or screen composition: start with `templates.md`, then `layout.md`, then `design_system.html`, then `dynamic.md` if a dynamic controller is involved.
- If the task changes models, queries, or save flows: start with `crud.md`, then `db.md`, then `datetime.md` if date fields are involved.
- If the task adds or changes database-backed application settings: read `settings.md`, then `db.md` for provider-specific update scripts.
- If the task changes report classes, report templates, exports, or custom report SQL behavior: read `reports.md`, then `db.md`, then `templates.md` if templates are involved.
- If the task changes assistant, LLM, knowledge base, embedding, or vector retrieval behavior: read `assistant.md`, then `db.md`, `crud.md`, and `templates.md` as needed.
- If the task changes Windows/IIS deployment scripts or setup instructions: read `deploy.md`.
- If the task adds or renames framework methods, variables, constants, or generated module helpers: read `naming.md`.
- If the task affects repo workflow or agent instructions: read `AGENTS.md`, `agents/code_reviewer.md`, `.github/copilot-instructions.md`, and the active task summary in `docs/agents/tasks/`; search `agents/tasks/index.md` before opening old summaries.
- If starting a recurring maintenance workflow from a reusable prompt: read `prompts/README.md`, then follow the chosen prompt plus the repo instructions.
