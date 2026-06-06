## What changed
- Updated agent coding-style guidance to prefer concise, contract-focused XML method docs instead of full param/return comments for obvious signatures.
- Updated code-review guidance to flag XML docs that restate obvious method signatures.
- Kept the comment guidance in `AGENTS.md` instead of adding `docs/comments.md`.

## Scope reviewed
- `AGENTS.md` coding-style guidance.
- `.github/copilot-instructions.md` sync requirement.
- `docs/agents/code_reviewer.md` simplicity review priority.
- `docs/README.md` documentation map; no update needed because no new documentation page was added.

## Commands used / verification
- `git status --short`
- `Get-Content docs\agents\local_instructions.md | Select-Object -Index (0..80)`
- `Test-Path docs\agents\tasks\summary-2026-06-06-method-comments.md`
- `git diff -- AGENTS.md docs\agents\code_reviewer.md docs\agents\tasks\summary-2026-06-06-method-comments.md`
- `git diff -- .github\copilot-instructions.md`
- `git diff --check -- AGENTS.md .github\copilot-instructions.md docs\agents\code_reviewer.md docs\agents\tasks\summary-2026-06-06-method-comments.md` - passed.
- Byte comparison of `AGENTS.md` and `.github\copilot-instructions.md` - identical.
- CRLF check for touched Markdown files - all touched files had `BareLF=0`.
- `Test-Path docs\comments.md` - `False`.
- Final diff reviewed using `docs/agents/code_reviewer.md`; no issues found.

## Decisions - why
- Kept the method-comment policy compact in `AGENTS.md` so agents see it during normal startup without another documentation hop.
- Did not create `docs/comments.md` because this policy is short and a separate page would add sync and reading overhead.
- Did not add a heuristic entry because the reusable instruction is already captured in the canonical agent guidance.

## Pitfalls - fixes
- `apply_patch` converted touched Markdown files to LF; normalized touched files back to CRLF before final verification.

## Risks / follow-ups
- Existing verbose XML docs remain as-is; this task changes future guidance only.

## Heuristics (keep terse)
- Stable facts, heuristics, and ADRs intentionally not added; `AGENTS.md` is the canonical reusable instruction for this task.

## Testing instructions
- N/A - docs/instructions only.

## Reflection
- The concise policy fit best in `AGENTS.md`; adding a separate comments guide would slow future agents with another doc lookup and create sync risk.
- Future instruction-only changes should still check line endings after `apply_patch`, because Markdown files in this repo must remain CRLF.
- Self-review was enough for this documentation-only workflow tweak; no sub-agent was needed because the diff is small and local.
