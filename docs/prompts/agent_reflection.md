# Agent Reflection Prompt

Run a periodic agent-workflow reflection for this repository.

Goal: inspect recent task summaries and shared agent instructions, then make only high-confidence improvements that help future agents do higher-quality work with less unnecessary reading, fewer wasted tool calls, and better verification discipline.

## Scope

- Review `AGENTS.md`, `docs/README.md`, `docs/agents/code_reviewer.md`, `docs/agents/heuristics.md`, `docs/agents/domain.md`, and `docs/agents/glossary.md`.
- Read `docs/agents/local_instructions.md` if it exists, but do not copy machine-local details into shared docs.
- Search `docs/agents/tasks/index.md` first. Use it to select relevant recent summaries instead of opening every task summary.
- Focus on `Reflection`, `Pitfalls - fixes`, `Commands used / verification`, `Risks / follow-ups`, and repeated task classes.

## Work

1. Create or update the current task summary before editing shared docs.
2. Identify recurring slowdowns, repeated reviewer findings, repeated verification gaps, repeated over-reading, stale assumptions, and prompts that would have avoided rework.
3. Separate stable facts from local preferences:
   - Put stable framework facts in `docs/agents/domain.md` or `docs/agents/glossary.md`.
   - Put reusable working heuristics in `docs/agents/heuristics.md` with a date.
   - Put risky or broad workflow changes in `AGENTS.md` only when they are recurring, high-confidence, and low-risk.
   - Keep uncertain recommendations in the task summary rather than shared instructions.
4. Keep edits compact. Prefer one precise instruction that prevents a known repeated failure over broad process text.
5. Do not add instructions that force extra work on small tasks unless the repeated failure is costly enough to justify that overhead.
6. If `AGENTS.md` changes, copy it byte-for-byte to `.github/copilot-instructions.md` before closing.
7. If the change affects the docs map or review workflow, update `docs/README.md` or `docs/agents/code_reviewer.md` as needed.

## Verification

- Run focused text checks for the changed instructions.
- Check line endings and UTF-8 no-BOM status for edited markdown files.
- For docs-only changes, set task-summary testing to `N/A - docs/instructions only` unless a stronger check was run.
- Review against `docs/agents/code_reviewer.md` when the instruction change affects agent workflow; use an independent reviewer when available, otherwise disclose a deliberate local review.

## Closeout

Report the summaries sampled, shared docs changed, recommendations left for later, stable facts or heuristics added, and why no broader instruction changes were made.
