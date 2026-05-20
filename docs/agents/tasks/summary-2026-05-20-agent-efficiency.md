## What changed
- Updated `docs/agents/heuristics.md` with generalized efficiency heuristics for large-file reading, bounded sub-agents, MCP fallback discipline, and isolated builds.
- Updated `AGENTS.md` with concise policies for large files, sub-agent/model use, worker delegation, VS/MCP fallback behavior, and `BaseIntermediateOutputPath` avoidance.
- Tightened the large-file rule to explicitly forbid whole-file reads for large files, clarified worker delegation triggers for independent file groups, and made VS MCP preference task-shape based rather than dependent on the user naming the tool.
- Defined task-summary `Reflection` as a future-process improvement section and allowed high-confidence, low-risk instruction improvements to be applied during closeout.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/agents/heuristics.md`
- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/agents/code_reviewer.md`

## Commands used / verification
- `git diff -- AGENTS.md .github\copilot-instructions.md docs\agents\heuristics.md docs\agents\tasks\summary-2026-05-20-agent-efficiency.md` - reviewed changes.
- `Get-FileHash AGENTS.md, .github\copilot-instructions.md` - matching hashes confirmed byte-identical sync.
- `git diff --check` - passed.
- Focused reviewer sub-agent using `gpt-5.3-codex-spark` checked the instruction changes; only summary traceability gaps were found and fixed.
- Follow-up focused reviewer sub-agent checked the refined large-file, worker delegation, and VS MCP wording; no issues found and AGENTS/Copilot hash sync confirmed.
- Final focused reviewer sub-agent checked the Reflection contract and high-confidence instruction-edit guardrails; no issues found.

## Decisions - why
- Kept broad efficiency rules in heuristics and concise operating rules in `AGENTS.md`.
- Added specific MCP guidance to pause after one validation/retry when user-requested MCP or VS/browser state is needed, while keeping standalone `dotnet` acceptable for pure compile/test checks.
- Added fast-model sub-agent guidance for read-only scans and first-pass triage, with final integration still owned by the main agent.
- Clarified that Visual Studio MCP should be preferred automatically for solution-aware/rebuild/restart/local VS-hosted app work; the prompt does not need to say "VS MCP".
- Made `Reflection` explicitly about what slowed work, what can be skipped next time, tool/delegation effectiveness, and candidate instruction changes.

## Pitfalls - fixes
- Existing task summaries often used `Reflection` as a recap or left it empty because AGENTS named the section but did not define its purpose. The template now includes an explicit prompt and closeout rule.

## Risks / follow-ups

## Heuristics (keep terse)
- Added new heuristics dated 2026-05-20.
- Added a Reflection heuristic dated 2026-05-20.

## Testing instructions
- N/A - docs/instructions only.

## Reflection
- This task showed that section names alone are not enough for consistent agent behavior. Future workflow sections should include a short operational prompt and a rule for when to act versus only recommend.
