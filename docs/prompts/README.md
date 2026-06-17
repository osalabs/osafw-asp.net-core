# Development Prompts

Reusable prompts for recurring framework and downstream-app maintenance workflows.

Use these as starting points, then fill the placeholders and remove anything that does not fit the current repository. Keep the prompt focused on the current work: objective, scope, source material, constraints, verification, and closeout.

## Prompt Catalog

- `fw_upgrade.md` - upgrade a downstream app from an older framework snapshot while preserving app-specific behavior.
- `pr_code_review.md` - review a GitHub PR, fix confirmed issues, verify, and repeat until ready for rereview.
- `agent_reflection.md` - periodically inspect task summaries and improve shared agent instructions only where the pattern is stable and worth the added guidance.
- `security_hardening.md` - review and harden a scoped feature, PR, or finding set using the repository security guardrails.
- `docs_consistency.md` - synchronize docs, changelog, navigation, and agent instruction docs after a behavior or workflow change.
- `test_stabilization.md` - reproduce and stabilize a failing build or test command without hiding real regressions.

## Selection Notes

Task-summary history shows repeated work in PR review/fix loops, security hardening, framework upgrades, docs and agent-instruction hygiene, test stabilization, and focused feature work. Prefer one of those specific prompts over a broad generic request when starting that workflow.

Do not let prompts override repo instructions. The local `AGENTS.md`, task summary rules, security guardrails, documentation sync rules, and testing guidance remain authoritative.
