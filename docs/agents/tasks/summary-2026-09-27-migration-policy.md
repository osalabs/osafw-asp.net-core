# Practical framework migration policy

## Outcome

- Recorded the developer-approved migration policy in the canonical compatibility section of `AGENTS.md` and linked the consumer-contract review question to it.
- Updated instruction-pack metadata to `1.7.0` for deliberate adoption into copied applications.
- Validation storage redesign remains discussion only. No runtime, schema, or validation API changed; no breaking-upgrade changelog entry is needed for this documentation change.

## Verification

- `pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1` passed the instruction, metadata, encoding, privacy, link, and routing checks.
- Changed text was normalized to strict UTF-8 without BOM and CRLF; `git diff --check` passed.
- Runtime tests were not run because this task changes guidance only.
- Shared workflow changes received independent review with the agent-workflow and consumer-contract overlays, followed by a summary audit. No blocking findings or supplemental findings remained; the review loop can stop.
