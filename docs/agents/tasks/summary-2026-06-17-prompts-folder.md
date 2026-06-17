## What changed
- Created `docs/prompts/` for reusable development workflow prompts.
- Moved `docs/fw_upgrade_prompt.md` to `docs/prompts/fw_upgrade.md` with `git mv` and refreshed the prompt structure.
- Added reusable prompts for PR code review, periodic agent reflection, security hardening, docs consistency, and test stabilization.
- Added `docs/prompts/README.md`, linked the folder from `docs/README.md`, and added this task to `docs/agents/tasks/index.md`.
- After feedback, restored the nullable warning prompt to its original task-history path and removed the too-generic targeted-change prompt from the reusable catalog.

## Scope reviewed
- Read `docs/agents/local_instructions.md`, `docs/README.md`, targeted sections from `docs/drafts/FPF-Spec.md`, `docs/agents/tasks/index.md`, `docs/agents/heuristics.md`, and `docs/agents/code_reviewer.md`.
- Searched existing prompt-like files and task-summary index entries for recurring workflow types.
- Relevant large-file sections used from `docs/drafts/FPF-Spec.md`: first practical prompt guidance around lines 620-690, tool-call planning, authoring guidance, wording repair, and review-comparison guidance. Targeted heading searches and line ranges were used instead of whole-file reads.

## Commands used / verification
- `Test-Path docs\agents\local_instructions.md`
- `Get-Content docs\README.md | Select-Object -Index (0..220)`
- `Get-Content docs\drafts\FPF-Spec.md | Select-Object -Index (...)` for targeted sections only.
- `rg --files | rg "(^|/)(fw_upgrade_prompt|agent_reflection|pr_code_review)\.md$|docs/agents/tasks/index\.md$|docs/prompts|prompt"`
- `rg -n "prompt|Prompt|agent_reflection|pr_code_review|fw_upgrade" docs AGENTS.md .github`
- `git -c core.quotepath=false status --short`
- `rg -n "FPF|First Principles|U\\.|EntityOfConcern|episteme|admissible|governing pattern|CallPlan|CheckpointReturn|NQD|Pareto|bounded comparative|Tech|Plain" docs\\prompts docs\\README.md`
- Applied feedback: restored `docs/agents/tasks/null-warnings-continue-prompt.md`, removed `docs/prompts/targeted_change.md`, and updated prompt catalog wording.
- `powershell -NoProfile -ExecutionPolicy Bypass -File docs\\agents\\tools\\Normalize-TextFiles.ps1 -Check ...` passed for touched markdown files.
- Manual self-review using `docs/agents/code_reviewer.md`: no material issues found; docs-only prompt changes need no `docs/CHANGELOG.md` entry and no `AGENTS.md` sync.

## Decisions - why
- Added a prompt catalog instead of only moving the named file because task history showed recurring PR review/fix loops, security hardening, docs/instruction hygiene, test stabilization and focused implementation workflows.
- Kept prompts plain-language and repository-oriented so they are runnable without requiring readers to know the source specification vocabulary.
- Did not update `AGENTS.md` because the new prompt folder is optional workflow material; the existing docs map is enough to make it discoverable.

## Pitfalls - fixes
- The workspace had many unrelated modified files before this task. This task touched only docs prompt/navigation/task-summary files.
- The prompt specification is very large, so only targeted sections were read using `rg` and `Select-Object` ranges.

## Risks / follow-ups
- A future pass could add workflow-specific prompts for release/changelog preparation or deployment operations if developers repeatedly ask for those as standalone workflows.
- No end-user-app breaking change was introduced; no `docs/CHANGELOG.md` entry is needed.

## Heuristics (keep terse)
- Reusable prompts belong under `docs/prompts/`; task summaries should stay task evidence, not prompt storage.

## Testing instructions
- N/A - docs/prompts only. Verified touched markdown files are UTF-8 without BOM and use CRLF line endings; reviewed diff/status for docs-only scope.

## Reflection
Task-history scanning was useful, but the folder should stay small: a prompt catalog plus a few recurring workflows gives better reuse than many specialized one-off prompts. The large specification should be consulted with targeted heading/section reads only; opening broad chunks burns tokens without improving these plain-language prompts. No stable framework facts, reusable agent heuristics, or ADRs were added because this task created optional prompt assets rather than changing project behavior.