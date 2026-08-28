# Non-Trivial Task Orchestrator Prompt

Use this workflow whenever the orchestrated-path triggers in `docs/agents/workflow.md` apply. Small/local work stays on the direct path. Follow repository instructions, security guardrails, task-summary rules, and user direction throughout.

## Objective

Coordinate this task end to end:

- Goal: `<clear end state>`
- Acceptance: `<observable result and evidence sufficient to declare completion>`
- Scope: `<paths, features, docs, schemas, tests, or PR/issue links>`
- Out of scope: `<explicit exclusions>`
- Risk level: `<why this needs orchestration>`

## Operating Rules

- Keep the main agent responsible for integration, user communication, final decisions, and verification.
- Delegation is capability-conditional. When unavailable, keep the same bounded stages and checks in the main task rather than blocking.
- When the matching bounded stage and project profile/capability are available, route read-only discovery to `discovery_fast`, routine well-specified implementation with disjoint ownership to `implementation_fast`, independent review warranted by `review-routing.md` to `reviewer_high`, and material architecture/high-risk ambiguity/failed-attempt escalation to `architect_max`. Otherwise execute the same packet locally; do not drop the stage or weaken its checks.
- Delegate only bounded work with clear inputs, exclusive writable paths, verification, output format, and stop conditions.
- Maintain a file lease list. Read-only scopes may overlap; writable scopes must not. The main agent must not edit a leased file until the worker returns or the lease is explicitly revoked.
- Do not let sub-agents make broad repo-wide changes or resolve shared contracts without main-agent review.
- Preserve user changes and unrelated dirty worktree state.
- Pause for user direction only when requested, when implementation would materially expand beyond the stated outcome, or when a risky/destructive/shared-state step lacks authority.
- Record important decisions, commands, risks, and follow-ups in the task summary as the work evolves.

## Phase 1 - Intake And Map

Read the fast entry docs and task-specific entry points. Then produce a compact plan:

- Critical path: `<must happen in order>`
- Safe parallel work: `<independent research/checks/tests>`
- Tightly coupled work: `<keep with main agent>`
- Verification strategy: `<smallest checks that can falsify the change>`
- Review strategy: `<integrator plus triggered overlay(s); independent reviewer or local fallback>`
- Stop/replan triggers: `<conditions that require a plan change>`

## Phase 2 - Delegation Packets

Use packets like this for each bounded worker:

```md
Objective: <specific bounded result>
Acceptance: <observable result and sufficient evidence>
Context/evidence: <relevant facts, contracts, and constraints>
Writable paths: <exclusive file ownership, or none>
Read-only paths: <allowed evidence scope>
Do not: <explicit exclusions and authority limits>
Verification: <checks the worker must run or evidence it must return>
Expected output: <findings, changed paths, commands/results, or patch summary>
Stop/escalate if: <ambiguity, overlap, repeated failure, risky contract expansion, unsafe state, or missing dependency>
```

Good delegation targets:

- targeted codebase research
- independent docs/spec review
- schema/config parity checks
- focused test failure triage
- implementation in disjoint files
- post-implementation verification
- code review after the main integration pass

## Phase 3 - Implementation

Integrate the work in the main workspace:

- Implement the requested behavior first.
- Keep changes scoped to the requested behavior and nearby contracts.
- Prefer existing framework patterns and helpers.
- Update docs/tests alongside public behavior or workflow changes.
- Re-read worker outputs before relying on them.
- Reconcile and release each file lease before editing or integrating that scope.

## Phase 4 - Verification

Run focused checks first, then broader checks only when risk justifies them:

- Build/test command(s): `<commands>`
- Manual/browser checks: `<flows>`
- Static searches: `<patterns>`
- Text/line-ending checks: `<files>`

If a check fails, classify whether it is caused by this task, pre-existing, or environmental. Fix task-caused failures before closing.

## Phase 5 - Review Loop

Route the final diff through `docs/agents/review-routing.md`, then use `docs/agents/code_reviewer.md` for the one adjudicated verdict when the task affects runtime behavior, schemas, templates, scripts, tests, configuration, or risky workflow docs.

For the review, use `reviewer_high` when independent review is warranted and that profile/capability is available; otherwise perform the documented local second pass:

- Findings must be concrete and path/line grounded.
- Fix real issues in the main workspace.
- Repeat while Blocker, High, or Medium findings remain. Low observations do not keep the loop open.

## Phase 6 - Closeout

Before final response:

- Complete the task summary, including verification and residual risk.
- Note whether docs, heuristics, ADRs, or changelog entries were added or intentionally skipped.
- Make sure no machine-local details, secrets, bulky logs, or external app names leaked into shared files.
- Summarize what changed, what was verified, and any important follow-up.
