## What changed
Added concise recurring security guardrails to `AGENTS.md`, `docs/agents/code_reviewer.md`, and `docs/agents/heuristics.md`; synced `.github/copilot-instructions.md` from `AGENTS.md`. Follow-up feedback changed custom mutation guidance to use `enforcePost()` and made the reusable instructions self-contained rather than dependent on private security drafts.

## Scope reviewed
Reviewed `docs/agents/local_instructions.md`, `docs/README.md`, the task-provided private security source material, `AGENTS.md`, `docs/agents/code_reviewer.md`, and `docs/agents/heuristics.md`. Source files reviewed were under 1 MB, so targeted reads were enough and no large-file special handling was required.

## Commands used / verification
- `git status --short`
- `rg -n "custom mutating|mutating|XSS|object-level|owner|saved user|redirect|v-html|markdown|raw HTML|attachment|dev/admin|admin tooling|sensitive|redact|generated SQL|allowlist|security" ...`
- Follow-up targeted searches confirmed `enforcePost()` guidance is present and reusable instructions do not reference private source files or the dated guardrail label.
- `Get-FileHash AGENTS.md,.github\copilot-instructions.md` - hashes matched after sync.
- `git diff --check -- AGENTS.md .github\copilot-instructions.md docs\agents\code_reviewer.md docs\agents\heuristics.md docs\agents\tasks\summary-2026-06-03-security-agent-instruction-updates.md` - clean.
- Targeted `rg` checks for guardrail phrases in the edited docs - expected phrases found.
- PowerShell bare-LF and trailing-whitespace checks over edited Markdown files - 0 matches.
- Code reviewer sub-agent re-review of task-owned files - no issues found; review loop can stop.
- Follow-up reviewer pass after feedback - no issues found; confirmed `enforcePost()` guidance and no reusable-instruction dependency on private security draft/report files.

## Decisions - why
Used the private source material only to distill self-contained evergreen reminders where future agents plan, implement, and review. The reusable instructions intentionally do not reference or rely on private draft/report files that may not be present in the public repository. Did not edit `docs/README.md` because it already routes agent workflow readers to the relevant instruction files.

## Pitfalls - fixes
The worktree already had unrelated changes, including `docs/README.md`; those were left untouched. The first reviewer pass flagged that pre-existing README diff, so the reviewer was given the pre-task status context and re-reviewed only task-owned files. Because `AGENTS.md` changed, `.github/copilot-instructions.md` must remain byte-for-byte synced.

## Risks / follow-ups
No runnable behavior changed. Future public-repo sessions should rely on the self-contained agent instructions and any authorized private handoff the user provides, not on private draft/report files being present.

## Heuristics (keep terse)
Added dated heuristics for `enforcePost()` custom mutations, direct-id authorization predicates, raw render/redirect/attachment review triggers, and generated/admin/telemetry guardrails.

## Testing instructions
N/A - docs/instructions only.

## Reflection
The private remediation material made the recurring classes easy to distill, but future public-repo agent sessions should not need that file. A concise guardrail section in planning instructions plus one reviewer prompt is enough to catch the main issue classes without adding process weight. A lightweight reviewer pass is still useful for agent-instruction changes because duplication and wording drift are the main risks.
