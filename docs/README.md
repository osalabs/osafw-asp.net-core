# Documentation Map

Open only the canonical documentation needed for the task. This map identifies topic owners and reading routes.

## Framework Docs

- [templates.md](templates.md): ParsePage syntax, template composition, shared fragments, parser rules, and frontend component conventions.
- [dynamic.md](dynamic.md): `config.json` reference for Dynamic and Vue controllers.
- [crud.md](crud.md): `FwModel` CRUD workflows using framework collections or typed DTOs.
- [db.md](db.md): `DB` helpers, SQL patterns, provider status/setup, schemas, additive updates, and provider-specific verification.
- [settings.md](settings.md): database-backed Site Settings schema, runtime API, admin behavior, and seeding.
- [naming.md](naming.md): framework naming, result-shape, side-effect, and empty/null conventions.
- [design_system.html](design_system.html): Bootstrap-based UI conventions, themes, tokens, and components.
- [layout.md](layout.md): shared layout structure, CRUD headers, and theming extension points.
- [dashboard.md](dashboard.md): dashboard pane types and extension patterns.
- [assistant.md](assistant.md): optional read-only RAG assistant, configuration, indexing, retrieval, and vector backends.
- [deploy.md](deploy.md): Windows/IIS deployment prerequisites, scripts, verification, and recovery.
- [reports.md](reports.md): hardcoded and Site Admin-managed reports.
- [datetime.md](datetime.md): per-user date/time formats, timezone conversion, and save normalization.
- [feature_modules.md](feature_modules.md): module scaffolding through Developer Tools or manual setup.
- [CHANGELOG.md](CHANGELOG.md): dated breaking upgrade changes for downstream apps.

## Agent Docs

- [agents/workflow.md](agents/workflow.md): request validation, framework/application role detection, work staging, permissions, summaries, and safe self-improvement.
- [agents/verification.md](agents/verification.md): proportional verification entrypoints and worktree/resource-isolation matrix.
- [agents/review-routing.md](agents/review-routing.md): adaptive risk routing, specialist triggers, adjudication, and no-subagent fallback.
- [agents/code_reviewer.md](agents/code_reviewer.md): integrating review procedure, severity, one final verdict, and loop stop rule.
- [agents/reviewers/](agents/reviewers/): focused overlays loaded only for agent workflow, consumer compatibility, performance, security, or state-integrity risk.
- [agents/mcp.md](agents/mcp.md): capability-conditional optional tooling, fallback, safety, and official product references.
- [agents/tasks/index.md](agents/tasks/index.md): compact task-history routing index; use targeted search when the index is insufficient.
- [agents/tools/](agents/tools/): scoped repository search and strict UTF-8/CRLF validation helpers.
- [agents/domain.md](agents/domain.md): verified stable framework/domain facts.
- [agents/glossary.md](agents/glossary.md): stable project terms.
- [agents/heuristics.md](agents/heuristics.md): unique reusable working heuristics not already owned by a canonical framework doc or `AGENTS.md`.
- [prompts/README.md](prompts/README.md): optional recurring workflow prompts, including framework upgrades and development-agent instruction upgrades.

## Canonical Owners

- Canonical framework docs above: detailed behavior, commands, and public contracts.
- `docs/agents/workflow.md` and `verification.md`: task-routed workflow detail and verification/isolation guidance.
- `docs/agents/review-routing.md`, `code_reviewer.md`, and `reviewers/`: adaptive review router, sole integrator verdict, and triggered specialist evidence.
- `docs/agents/tasks/summary-*.md`: current-task evidence and decisions, not reusable policy. Historical bodies are immutable.
- `docs/agents/domain.md`, `glossary.md`, and `heuristics.md`: stable facts, terms, and non-duplicated recurring heuristics respectively.
- `docs/prompts/`: optional task-specific workflows.

## Route by Task

- Templates, shared UI fragments, parser behavior: `templates.md`; add `layout.md` and `design_system.html` for layout/theme work, and `dynamic.md` for Dynamic/Vue screens.
- Models, queries, schemas, or provider behavior: `crud.md`, then `db.md`; add `datetime.md` for date/time fields and `settings.md` for Site Settings.
- Assistant, LLM, knowledge base, embedding, or retrieval: `assistant.md`, then only the relevant DB/CRUD/template docs.
- Deployment or IIS scripts: `deploy.md`.
- Naming or public method/result conventions: `naming.md`.
- Agent request validation, permissions, summaries, or staging: `agents/workflow.md`.
- Build/test/provider/worktree/cleanup decisions: `agents/verification.md` plus the affected canonical topic doc.
- Review: `agents/review-routing.md`, then `agents/code_reviewer.md` and only the triggered overlay(s).
- Recurring maintenance workflow: select the narrowest matching prompt from `prompts/README.md`.
