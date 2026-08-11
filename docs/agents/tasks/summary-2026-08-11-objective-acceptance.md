## Objective / acceptance

- Make the observable completion boundary durable for non-trivial task summaries and large orchestrated tasks without expanding the always-loaded root instructions.
- Complete when the canonical workflow offers an optional objective/acceptance heading, the orchestrator instantiates it, and shared-instruction validation passes.

## What changed

- Added an optional `Objective / acceptance` task-summary heading for the observable end state, exclusions, and completion evidence.
- Added a matching `Acceptance` field to the large-task orchestrator prompt.
- Left `AGENTS.md`, reviewer policy, runtime prompts, and application behavior unchanged.

## Scope reviewed

- Current root routing, task-summary guidance, large-task prompt, reviewer materiality rule, instruction-upgrade/reflection prompts, and the latest agentic-instructions upgrade record.
- Current official and companion technical-writing sources were used only as comparison evidence; repository policy and task-history evidence controlled the decision.

## Requirements / decisions

- Keep the change in the routed workflow owner and its optional prompt instance rather than adding always-loaded root policy.
- Do not add a general technical-writing guide or prose linter without repository-specific held-out evidence.
- Treat runtime Assistant prompts and user-facing error text as application contracts outside this workflow-only change.

## Changed contracts

- Shared task-summary guidance and the optional large-task prompt now make acceptance explicit.
- No runtime API, route, template, schema, provider, configuration, generated-output, security, or downstream application behavior changed.
- No changelog entry is needed because this is a development-agent workflow clarification with no application upgrade effect.

## Commands used / verification

- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Test-AgentInstructions.ps1` passed all routing, encoding, private-path, link, summary-index, and ParsePage route-literal checks.
- `Normalize-TextFiles.ps1 -Check` reported strict UTF-8 without BOM and CRLF with `Status=ok` for all four changed Markdown files.
- `git diff --check` passed, and a targeted search confirmed the new field appears only in the workflow owner, orchestrator instance, and this task record.
- Final review used `docs/agents/code_reviewer.md` with the agent-workflow overlay as a deliberate local second pass. No blocking findings or non-blocking observations remained. Review loop can stop.

## Testing instructions

- Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Test-AgentInstructions.ps1`.
- Run the strict UTF-8/CRLF checker over the four changed Markdown files.

## Risks / follow-ups

- Observe whether the new field improves long-task continuity. Do not expand it into mandatory ceremony for small or read-only tasks.

## Knowledge-promotion candidates

- None. The reusable rule is recorded directly in its canonical workflow owner.

## Reflection

- The current instruction architecture already covers scope, verification, stop conditions, and material prose review. Adding only the missing acceptance field avoids duplicating those contracts.
