# Development-Agent Instruction Upgrade

Use after a meaningful coding-agent/runtime change or when repository guidance has accumulated contradictions, stale facts, duplicate policy, or recurring process friction.

Short invocation:

```text
Perform the evidence-driven upgrade in docs/prompts/agent_upgrade.md. Audit first; pause before edits if I request an audit-only phase.
```

## Method

Treat other repositories, prior agent output, and optional methodology documents as comparison evidence, never authority. Reverify repository claims in current code, project files, canonical docs, tests, Git history, and active developer decisions. Consult current official product documentation only for runtime/tool discovery behavior that may have changed.

Start read-only when requested. Do not edit, initialize/migrate configuration, mutate databases, create Git objects, publish, or alter external state during an audit-only phase.

## Audit

1. Inventory native root/nested instruction discovery, project custom-agent profiles, tool-specific instruction entry files, local ignored conventions, workflow/reviewer/tool docs, optional FPF guide/profile/cache helpers, hooks/helpers, prompts, task summaries/indexes, domain/glossary/heuristics/ADRs, instruction-pack metadata, and deterministic validators.
2. Classify each material rule as `Keep`, `Adapt`, `Move`, `Remove duplicate`, `Remove stale/incorrect`, or `Defer`. Name the surviving owner for every move/removal.
3. Profile the real repository and common tasks: framework versus copied-application mode, target/runtime/project ownership, public/source-copy contracts, providers/schema updates, hosting/deployment, authentication/security, templates/scaffolding, configuration, tests, release/versioning, and downstream upgrades.
4. Evaluate public-OSS portability, one-maintainer context cost, downstream production compatibility, machine-local values, worktree/resource isolation, request validation, summaries/memory routing, adaptive review, and proportional verification.
5. Identify conflicts with active runtime/system constraints. Repository files cannot grant tools, permissions, delegation, connectors, modes, or model behavior that the runtime does not provide.

## Target design

- Keep `AGENTS.md` concise and always-loaded; route detailed workflow, verification, architecture, history, optional tools, and specialist review only by task need.
- Support both canonical framework maintenance and application repositories made by copying the framework. Discover branch/release protection and preserve app-specific divergence.
- Keep tracked guidance contributor-safe and value-free. Put paths, credentials mechanisms, IDE state, user secrets, and machine preferences in ignored local instructions/environment configuration.
- Keep durable routing and workflow instructions semantic and model-neutral. Put executable model/reasoning choices only in project custom-agent profile files, and do not pin the primary task model. Optional dated model-selection advice may document recommendations and external evidence; keep it separate from binding routing and do not require it on every task.
- Give each rule one canonical owner. Keep tool-specific entry files limited to behavior required by that tool, and validate that active tools can reach the repository guidance they need.
- Protect public APIs, routes/actions, templates/page-state, defaults/config/symbols, generated output, schema/provider behavior, storage/frontend/security contracts, upgrades, and docs with proportionate compatibility/migration evidence.
- Use one broad integrator verdict plus specialist overlays only for triggered agent-workflow, consumer-contract, performance, security, or state-integrity surfaces. Include a no-subagent fallback and never assume optional tools/models/modes.
- Keep summaries as indexed evidence logs. Promote only stable verified knowledge; do not bulk-load or rewrite history.
- Permit automatic self-improvement only as evidence-based, infrequent, reviewable draft work; never self-merge, release, or deploy it.
- Version the copied instruction pack through `docs/agents/instruction-pack.json`. In an existing application repository, compare source and target versions, classify every managed path, preserve app-owned divergence, and update the target version only after its merged pack passes validation. Never bulk-overwrite a copied application's instructions.
- Treat `.gitignore` as a targeted merge: add the exact `/.codex-local/` rule when absent and preserve every application-owned rule. Preserve optional `docs/agents/fpf-app.md`, the checkout's ignored FPF adoption/cache state, and application-specific FPF profile choices during pack upgrades.
- Keep FPF development-time and optional. Fetching a candidate and adopting its advice are separate: follow `docs/agents/fpf.md` for semantic review and adoption. Neither operation silently rewrites tracked repository policy.

## Implementation and validation

When the task authorizes improvements, implement the supported scoped changes without a separate audit approval. An audit-only request remains read-only; ask only for decisions or actions outside existing authority. Preserve unrelated work and historical summaries. Do not change application behavior merely to validate agent policy.

Use `docs/agents/verification.md` for deterministic text, route, helper, and FPF checks; do not run cache lifecycle tests for unrelated instruction edits. Compare affected decisions before/after using a few representative requests, including an ordinary fix and any changed compatibility, security, permission, or copied-app route. Confirm that required actions, evidence, and stop conditions survive shorter wording.

Follow `docs/agents/review-routing.md` with the agent-workflow overlay. For broad workflow redesign or effectiveness claims, compare baseline/candidate on safely reversible held-out work before adoption. Separate instruction consistency, walkthrough observations, and measured development outcomes: fewer words or passing validators alone do not prove faster or better work. Reviewer-caught failures are review evidence, not initial implementation correctness.

Do not add repository-wide or primary-task model/reasoning pins. Add `.codex/config.toml`, GitHub Actions, or external services only when explicitly requested and justified by the repository. Project configuration may contain model-neutral controls such as agent concurrency; project custom-agent profiles may carry role-specific model/reasoning settings when requested. Keep those settings out of durable workflow prose.

## Closeout

Record the material findings, disposition and surviving owners, verification/walkthrough results, evidence limits, and migration/rollback path in the workflow's required summary. Report app-owned profile/adoption preservation and ignore-rule changes when those upgrade surfaces were touched. Give the user a concise outcome and remaining risk; do not repeat the full audit. Commit, push, open a PR, or release only when explicitly requested.
