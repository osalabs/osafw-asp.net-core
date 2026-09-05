# Orchestration Template

Use only when the delegation criteria in `docs/agents/workflow.md` are met. That file owns eligibility, role selection, stage limits, file ownership, fallback, and evidence requirements. `docs/agents/review-routing.md` owns reviewer selection and handoff. This template supplies the task-specific plan and worker packets.

## Objective

- Goal: `<clear end state>`
- Acceptance: `<observable result and evidence sufficient to declare completion>`
- Scope: `<paths, features, docs, schemas, tests, or PR/issue links>`
- Out of scope: `<explicit exclusions>`

## Plan

- Critical path: `<what must happen in order>`
- Safe parallel work: `<independently useful workstreams>`
- Tightly coupled work: `<keep with the primary agent>`
- Delegation payoff: `<output consumed, work the primary will not repeat, and expected time or specialist-quality benefit>`
- Stages and ownership: `<selected roles, exclusive writable paths, and lease handoffs>`
- Verification: `<smallest checks that can falsify the change>`
- Review: `<router-selected execution mode and overlay(s)>`
- Stop/replan triggers: `<conditions that require a plan change>`

## Worker packet

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

## Integration and verification

Consume returned work and reconcile file leases using the workflow's ownership rules. The primary agent resolves shared contracts and inspects the integrated diff, including generated output and documentation.

Fill only the checks relevant to the changed behavior:

- Build/test commands: `<commands>`
- Manual/browser checks: `<flows>`
- Static searches: `<patterns>`
- Text/line-ending checks: `<files>`
- Public-contract controls: `<established entry paths and baseline consumers>`
- Behavior controls: `<ordinary/attacker negatives plus intended-safe/trusted and preserved-compatibility positives>`

Classify failures as task-caused, pre-existing, or environmental using evidence; fix task-caused failures before closeout.

## Review and closeout

Apply `docs/agents/review-routing.md`, then `docs/agents/code_reviewer.md` for review criteria, adjudication, and the loop stop rule. Complete the workflow's required task evidence and summary audit. Report the outcome, verification, relevant documentation/migration decisions, and remaining risk.
