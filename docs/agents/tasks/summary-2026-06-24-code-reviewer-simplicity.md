## What changed

- Tightened `docs/agents/code_reviewer.md` so reviewer simplicity checks cover both solution structure and local code shape.
- Added a guardrail that simplification findings should name the smallest behavior-preserving change and avoid broad rewrites or harmful deduplication.
- Added this task summary and indexed it.

## Scope reviewed

- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/agents/code_reviewer.md`
- `docs/agents/tasks/index.md`
- `docs/drafts/FPF-Spec.md` targeted matches for architecture structure, reusable structure, smallest useful output, duplication, and refactoring pressure.

## Commands used / verification

- `Test-Path docs\agents\local_instructions.md`
- `Get-Content docs\agents\local_instructions.md`
- `Get-Content docs\README.md | Select-Object -Index (0..220)`
- `Get-Content docs\agents\code_reviewer.md`
- `rg -n "Simplicity|unnecessary complexity|shallow wrappers|over-broad abstractions|duplicated logic" docs\agents\code_reviewer.md`
- `rg -n "code_reviewer|simplicity|complexity|review" docs\agents\tasks\index.md`
- `rg -n "^## C\.30|^## C\.31|^## C\.32|^## E\.8|smallest useful|minimal.*useful|refactoring opportunity|bespoke residue|over-broad|duplication" docs\drafts\FPF-Spec.md`
- `powershell -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 docs\agents\code_reviewer.md docs\agents\tasks\index.md docs\agents\tasks\summary-2026-06-24-code-reviewer-simplicity.md`
- `powershell -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check docs\agents\code_reviewer.md docs\agents\tasks\index.md docs\agents\tasks\summary-2026-06-24-code-reviewer-simplicity.md`
- `git -c core.autocrlf=false diff --ignore-space-at-eol -- docs/agents/code_reviewer.md docs/agents/tasks/index.md docs/agents/tasks/summary-2026-06-24-code-reviewer-simplicity.md`

## Decisions - why

- The reviewer prompt already had a simplicity item, but it mostly covered local code smells. The update makes the architecture/code split explicit.
- The new wording keeps simplification subordinate to preserving requested behavior and lowering maintenance burden, so reviewers do not turn small tasks into broad redesigns.
- No `AGENTS.md` update was needed because the recurring instruction already says to avoid one-use wrappers and test-only entry points; this task only refined reviewer behavior.
- No changelog entry was needed because this is agent workflow documentation only, not an end-user-app breaking change.

## Pitfalls - fixes

- The draft specification is large. An initial full read produced excessive output; follow-up review used targeted `rg` matches only.
- The worktree already had unrelated uncommitted changes, including existing edits to `docs/agents/code_reviewer.md`; changes here were kept to the reviewer simplicity wording and one related guardrail.

## Risks / follow-ups

- Low risk: documentation/instruction change only.
- Follow-up: observe whether reviewers over-report stylistic simplification. If so, narrow the wording further toward behavior-preserving simplification with concrete maintenance impact.

## Heuristics (keep terse)

- No stable heuristics added; the reusable guidance lives in the reviewer prompt.

## Testing instructions

N/A - docs/instructions only.

## Reflection

The most useful framing was separating solution shape from local code shape, then adding a stop rule so simplification findings stay bounded. Future agents should search the large draft for exact pattern headings or terms first, not stream it. No sub-agent or MCP use was needed for this small prompt update.
