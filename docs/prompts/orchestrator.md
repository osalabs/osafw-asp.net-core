# Large Task Orchestrator Prompt

Use this prompt when a task is too large, risky, or cross-cutting for a single linear implementation pass. It is an optional workflow prompt; repository instructions, security guardrails, task-summary rules, and user direction remain authoritative.

## Objective

Coordinate this task end to end:

- Goal: `<clear end state>`
- Scope: `<paths, features, docs, schemas, tests, or PR/issue links>`
- Out of scope: `<explicit exclusions>`
- Risk level: `<why this needs orchestration>`

## Operating Rules

- Keep the main agent responsible for integration, user communication, final decisions, and verification.
- Delegate only bounded work with clear inputs, owned paths, output format, and stop conditions.
- Do not let sub-agents make broad repo-wide changes or resolve shared contracts without main-agent review.
- Preserve user changes and unrelated dirty worktree state.
- Pause for user approval before implementation only when the user requested approval, the plan changes a public contract materially, or the next step is risky/destructive.
- Record important decisions, commands, risks, and follow-ups in the task summary as the work evolves.

## Phase 1 - Intake And Map

Read the fast entry docs and task-specific entry points. Then produce a compact plan:

- Critical path: `<must happen in order>`
- Safe parallel work: `<independent research/checks/tests>`
- Tightly coupled work: `<keep with main agent>`
- Verification strategy: `<smallest checks that can falsify the change>`
- Review strategy: `<self-review or reviewer sub-agent>`
- Stop/replan triggers: `<conditions that require a plan change>`

## Phase 2 - Delegation Packets

Use packets like this for each bounded worker:

```md
Task: <specific bounded result>
Paths: <owned/read-only paths>
Context: <relevant facts and constraints>
Do not: <explicit exclusions>
Expected output: <findings, changed paths, commands run, or patch summary>
Stop if: <ambiguity, failing command, risky contract change, or missing dependency>
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

## Phase 4 - Verification

Run focused checks first, then broader checks only when risk justifies them:

- Build/test command(s): `<commands>`
- Manual/browser checks: `<flows>`
- Static searches: `<patterns>`
- Text/line-ending checks: `<files>`

If a check fails, classify whether it is caused by this task, pre-existing, or environmental. Fix task-caused failures before closing.

## Phase 5 - Review Loop

Review the final diff using `docs/agents/code_reviewer.md` when the task affects runtime behavior, schemas, templates, scripts, tests, configuration, or risky workflow docs.

For the review:

- Findings must be concrete and path/line grounded.
- Fix real issues in the main workspace.
- Repeat only while the next loop is likely to improve the result.

## Phase 6 - Closeout

Before final response:

- Complete the task summary, including verification and residual risk.
- Note whether docs, heuristics, ADRs, or changelog entries were added or intentionally skipped.
- Make sure no machine-local details, secrets, bulky logs, or external app names leaked into shared files.
- Summarize what changed, what was verified, and any important follow-up.
