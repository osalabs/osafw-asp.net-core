## What changed
- Added recurring security guardrails to agent and reviewer instructions.
- Synced the shared assistant instruction file after updating the root agent instructions.
- Kept the reusable guidance self-contained so it does not depend on private assessment files.

## Scope reviewed
- Agent workflow instructions, reviewer guidance, and reusable heuristics.
- Public documentation entry points for agent workflow.

## Commands used / verification
- Searched the instruction docs for existing security guidance and overlap.
- Verified instruction sync and checked Markdown/diff formatting.
- Ran a focused reviewer pass for the instruction changes.

## Decisions - why
- Captured recurring classes of security work as durable review prompts instead of linking to private scan material.
- Avoided changing the docs map because it already routes agents to the relevant instruction files.

## Pitfalls - fixes
- Existing unrelated workspace changes were left untouched.
- The mirrored instruction file was kept synchronized with the root agent instructions.

## Risks / follow-ups
- Public sessions should rely on these self-contained guardrails and authorized private handoffs, not on private draft files.

## Heuristics (keep terse)
- Security guidance should be durable, self-contained, and free of private finding references.

## Testing instructions
N/A - docs/instructions only.

## Reflection
Reusable agent instructions are safest when they describe patterns and review triggers without carrying over incident-specific evidence.
