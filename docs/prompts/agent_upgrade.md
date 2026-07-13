# Development-Agent Instruction Upgrade

Use this repository prompt after a meaningful coding-agent model or runtime release, or when active agent guidance has accumulated contradictions or unnecessary procedure.

Short invocation:

```text
Perform upgrade per docs/prompts/agent_upgrade.md.
```

## Upgrade brief

Upgrade this repository's development-agent instructions for current high-reasoning coding agents while keeping all tracked guidance model-neutral.

Audit first, then implement. Inspect the actual repository instruction stack, including `AGENTS.md`, mirrors, machine-local guidance, agent workflow/reviewer/tooling docs, active knowledge files, validation helpers, relevant recent task history, and the source code/schema/tests needed to verify repository-specific claims. If the user supplies a sister repository path or commit, use it only as comparison evidence; never copy its conventions without local verification.

Before editing, classify every existing `AGENTS.md` section and material rule as:

- Keep
- Consolidate
- Move
- Remove as duplicate
- Remove as stale or incorrect

For every consolidation, move, or removal, identify the surviving owner or explain why the rule is unnecessary. Do not delete repository-specific correctness, authorization, tenancy/account scoping, exception-boundary, security, data-integrity, schema, migration, deployment, or verification guidance unless equivalent active coverage is demonstrated.

Goals:

- Make active guidance compact, outcome-focused, and free of blind context-loading rituals.
- Resolve conflicting authority, summary, review-loop, delegation, shell, and tooling rules.
- Make delegation and optional tools capability-conditional, with shared-worktree protections and safe local fallbacks.
- Keep summaries and testing proportional to risk.
- Separate blocking findings from non-blocking observations.
- Replace copied volatile product behavior with short repository-specific addenda and current official links.
- Keep detailed commands and framework explanations in their existing authoritative docs rather than duplicating them in `AGENTS.md`.
- Do not add `.codex/config.toml` or pin models or reasoning settings unless the user explicitly requests repository configuration.
- Keep `AGENTS.md` and its Copilot mirror byte-identical when both exist.

Do not modify application code, runtime AI prompts, generated files, historical task-summary bodies, or machine-local guidance unless the audit proves a specific item is directly in scope. Preserve unrelated worktree changes.

Implement the changes, create or update the repository-required task summary, and validate strict UTF-8, the repository line-ending policy, whitespace, local and newly added external links, mirror equality, stale terms, invalid paths, and helper behavior. Run an independent review/fix loop when the runtime exposes a separate reviewer; otherwise perform and disclose a deliberate local review pass. Continue the loop only for Blocker, High, or Medium findings.

In the final response include:

1. Changed files and validation results.
2. Every contradiction found, marked `Fixed`, `Intentionally retained`, or `Out of scope`.
3. A section-disposition list showing where each original `AGENTS.md` section now lives, including anything deliberately removed and why.
4. Remaining non-blocking risks.
