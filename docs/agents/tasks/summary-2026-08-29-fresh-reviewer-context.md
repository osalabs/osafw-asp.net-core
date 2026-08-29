## Objective / acceptance

- Require the post-implementation independent reviewer, when that capability exists, to be a fresh separate agent that receives no implementing-agent conversation history.
- Make the explicit review handoff the reviewer's only task context while preserving the existing local fallback when independent capability is unavailable.
- Keep the normative rule in the canonical review router without vendor-specific launch parameters, duplicated guidance, configuration, or unrelated changes.

## What changed

- Tightened the capability-conditional independent-review rule so the reviewer starts after implementation as a fresh separate agent and receives context only through the explicit review handoff.
- Replaced three non-owner eligibility statements with concise references to the canonical capability-conditional review path, avoiding duplicated policy.
- Left the adjacent no-independent-capability fallback unchanged.

## Scope reviewed

- Root agent routing, workflow/delegation guidance, review routing and integrating-review procedure, the agent-workflow overlay, verification requirements, related prompts, and targeted task history.
- The bounded optional agent-workflow reference was consulted only for cold-reader and explicit-handoff review principles; repository policy and the requested outcome controlled the change.

## Changed contracts

- Shared review delegation now requires history-isolated reviewer context whenever an independent reviewer/subagent is available after implementation.
- No runtime, public API, schema/provider, configuration, generated-output, security, or downstream application contract changed; no changelog entry is needed.

## Commands used / verification

- `git diff --check` passed.
- `docs/agents/tools/Test-AgentInstructions.ps1` passed in current PowerShell and Windows PowerShell 5.1, covering instruction routes, strict UTF-8/CRLF, local links, private-path leakage, task-summary indexing, and ParsePage route literals.
- Targeted active-policy searches found the detailed fresh-reviewer/history-isolation/handoff rule only in `review-routing.md` and found no remaining `when useful/available` independent-review eligibility statement.
- Byte checks confirmed all task-edited text is UTF-8 without BOM with CRLF line endings.
- A fresh read-only independent reviewer using the integrating procedure plus `reviewers/agent-workflow.md` could not retrieve the withheld parent-only context marker. Its two Medium findings identified conflicting non-owner optionality wording and this summary's stale pending evidence; both were corrected for the final review loop.
- A second fresh read-only reviewer reran the integrating procedure plus the agent-workflow overlay, confirmed both Medium findings were resolved, could not retrieve the withheld marker, and returned `No blocking findings.` with `Review loop can stop.`

## Risks / follow-ups

- Runtimes that cannot create a history-isolated separate agent continue to use and disclose the existing local second-pass fallback.
