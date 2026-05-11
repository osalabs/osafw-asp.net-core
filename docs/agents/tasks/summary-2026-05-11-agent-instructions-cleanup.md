## What changed
- Reworked `AGENTS.md` into shorter, checkable sections for non-negotiables, task workflow, project conventions, documentation sync, testing, commands, and MCP usage.
- Synced `.github/copilot-instructions.md` byte-for-byte from `AGENTS.md`.
- Tightened `docs/agents/code_reviewer.md` wording and aligned its workflow-sync checks with the root instructions.
- Added `docs/agents/mcp.md` for detailed MCP troubleshooting so root instructions stay concise.
- Updated `docs/README.md` to include the MCP notes and the full agent-instruction sync set.

## Scope reviewed
- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/agents/code_reviewer.md`
- `docs/README.md`
- `docs/agents/local_instructions.md`
- `docs/drafts/FPF-Spec.md`
- `docs/agents/domain.md`
- `docs/agents/glossary.md`
- `docs/agents/heuristics.md`

## Commands used / verification
- `Get-ChildItem -Path . -Force | Select-Object Name,Mode`
- `Test-Path docs\agents\local_instructions.md`
- `Get-Content docs\agents\local_instructions.md`
- `Get-Content AGENTS.md`
- `Get-Content docs\agents\code_reviewer.md`
- `Get-Content docs\README.md`
- `git -c safe.directory=C:/DOCS_PROJ/github/osafw-asp.net-core status --short`
- `Compare-Object (Get-Content AGENTS.md) (Get-Content .github\copilot-instructions.md) | Select-Object -First 20`
- `Copy-Item -LiteralPath 'AGENTS.md' -Destination '.github\copilot-instructions.md' -Force`
- CRLF normalization/check for edited Markdown files.
- `git -c safe.directory=C:/DOCS_PROJ/github/osafw-asp.net-core -c core.excludesFile= diff --stat -- AGENTS.md .github/copilot-instructions.md docs/README.md docs/agents/code_reviewer.md docs/agents/mcp.md docs/agents/tasks/summary-2026-05-11-agent-instructions-cleanup.md`
- Code reviewer sub-agent review: no issues found; review loop can stop.

## Decisions - why
- Use the draft process spec only to guide instruction quality: keep guidance scoped, checkable, source-linked, and concise without introducing external jargon.
- Standardize ADR guidance on `docs/adr/` because that folder exists and is already listed in repo docs.
- Add a narrow read-only exception for task summaries so review-only/no-edit requests can remain no-edit.
- Reduce duplicated doc and command lists in root instructions and point to canonical docs.
- Add a sub-agent fallback so the workflow is usable by agents without delegation tools.

## Pitfalls - fixes
- `git status` initially failed due dubious ownership; used `git -c safe.directory=...` instead of mutating global Git config.
- `.github/copilot-instructions.md` had version-comment drift from `AGENTS.md`; synced it byte-for-byte after editing.

## Risks / follow-ups
- No active follow-up. The reviewer found no issues.

## Heuristics (keep terse)
- Instruction docs should name the canonical source and avoid restating long lists that already live there.

## Testing instructions
N/A - docs/instructions only.

## Reflection
- Stable facts: none added; this was workflow guidance, not domain knowledge.
- Heuristics: kept in this task summary only to avoid duplicating the same rule in both `AGENTS.md` and `docs/agents/heuristics.md`.
- ADR: not needed; no substantial business or architecture decision changed.
