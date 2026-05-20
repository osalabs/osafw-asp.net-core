## What changed
- Updated `docs/agents/heuristics.md` with generalized efficiency heuristics for large-file reading, bounded sub-agents, MCP fallback discipline, and isolated builds.
- Updated `AGENTS.md` with concise policies for large files, sub-agent/model use, worker delegation, VS/MCP fallback behavior, and `BaseIntermediateOutputPath` avoidance.

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

## Decisions - why
- Kept broad efficiency rules in heuristics and concise operating rules in `AGENTS.md`.
- Added specific MCP guidance to pause after one validation/retry when user-requested MCP or VS/browser state is needed, while keeping standalone `dotnet` acceptable for pure compile/test checks.
- Added fast-model sub-agent guidance for read-only scans and first-pass triage, with final integration still owned by the main agent.

## Pitfalls - fixes

## Risks / follow-ups

## Heuristics (keep terse)
- Added new heuristics dated 2026-05-20.

## Testing instructions
- N/A - docs/instructions only.

## Reflection
