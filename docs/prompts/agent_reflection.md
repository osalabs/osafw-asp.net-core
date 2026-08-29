# Agent Workflow Reflection Prompt

Run an evidence-driven periodic reflection on this repository's agent workflow. The goal is fewer repeated failures and less context/process overhead, not continual policy churn.

Unless the invocation explicitly requests edits, produce recommendations only. When edits are requested, implement only high-confidence scoped changes; keep substantial policy redesign isolated from feature work.

## Evidence sample

- Read `AGENTS.md`, then route to `docs/agents/workflow.md`, `review-routing.md`, `verification.md`, and only other shared docs implicated by evidence.
- Read ignored local instructions when present but never expose/copy private details into tracked files.
- Search `docs/agents/tasks/index.md` first. Select a bounded recent/representative sample by task family; do not open every summary.
- Focus on repeated `Reflection`, pitfalls, verification gaps, reviewer findings, unresolved risks, tool friction, over-reading, and cleanup failures. Reverify any old claim against current behavior.

## Analysis

1. Separate a recurring systemic problem from a one-off difficult task, unavailable capability, or agent mistake that policy cannot reliably prevent.
2. Identify a concrete failure/effect and the smallest instruction, route, helper, or canonical-doc change likely to prevent it.
3. Place knowledge once:
   - stable framework facts in `domain.md` or `glossary.md`;
   - unique reusable heuristics in `heuristics.md` with a date;
   - detailed behavior in its canonical topic doc;
   - cross-task policy in `AGENTS.md` only when it must always load;
   - uncertain/task-specific ideas in the reflection summary/proposal.
4. Estimate added context/process cost. Do not force ceremony on routine tasks to address a rare low-impact event.
5. Distinguish deterministic helper/safety improvements from unproven claims that agents will implement better code.

## Safe implementation / automation

- Create/update one reflection summary and index entry only when implementing a non-trivial shared change.
- Keep tracked instructions public, model-neutral, capability-conditional, and free of private paths/values.
- Update only the routed owners whose contracts changed, and keep each instruction in one canonical owner.
- Validate strict text/routing/helper behavior and follow the capability-conditional review execution in `review-routing.md` for the agent-workflow overlay and integrating review.
- Automatic recurring reflection may prepare an isolated draft proposal or draft PR only when that external action is explicitly configured/authorized. It must never self-merge, push a release, deploy, or treat prior agent text as authority.

## Closeout

Report the sampled evidence, recurring issue, recommendation or edits, deterministic validation, expected context/ceremony cost, ideas deferred for insufficient evidence, and a rollback path for any instruction change.
