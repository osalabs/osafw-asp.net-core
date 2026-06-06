## What changed
- Added reusable agent helper scripts for scoped repo search and CRLF/UTF-8 no-BOM normalization.
- Added a compact task-history index so agents can avoid opening long historical summaries unless relevant.
- Tightened agent workflow guidance for compact summaries, broad search scope, reviewer fallback, and isolated build/test output.

## Scope reviewed
- `AGENTS.md`, `.github/copilot-instructions.md`, `docs/README.md`, `docs/agents/code_reviewer.md`.
- Recent task-summary reflections around search scope, CRLF/BOM handling, locked build outputs, reviewer fallback, and full-suite baseline noise.
- `docs/agents/tools/` availability and existing `docs/agents/tasks/` summaries.

## Commands used / verification
- `git status --short`
- `Get-Content docs\agents\local_instructions.md | Select-Object -Index (0..80)`
- `Get-Content docs\README.md | Select-Object -Index (0..90)`
- `Test-Path docs\agents\tools; Test-Path docs\agents\tasks\index.md; Test-Path docs\agents\tasks\summary-2026-06-06-token-efficient-workflow.md`
- Targeted reads of `AGENTS.md`, `docs/agents/code_reviewer.md`, and recent task-summary metadata/reflections.
- Generated `docs\agents\tasks\index.md` from existing `summary-*.md` files.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Path ...` - normalized touched files.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check -Path ...` - passed for touched files.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Search-Repo.ps1 -Pattern "Monoculture bias"` - no output; drafts excluded by default.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Search-Repo.ps1 -Pattern "Monoculture bias" -IncludeDrafts` - returned `docs\drafts\FPF-Spec.md`.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Search-Repo.ps1 -Pattern "compact routing aid"` - no output; task history excluded by default.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Search-Repo.ps1 -Pattern "compact routing aid" -IncludeTaskHistory` - returned this task summary.
- `git diff --stat -- AGENTS.md .github\copilot-instructions.md docs\README.md docs\agents\code_reviewer.md docs\agents\tools\Search-Repo.ps1 docs\agents\tools\Normalize-TextFiles.ps1 docs\agents\tasks\index.md docs\agents\tasks\summary-2026-06-06-token-efficient-workflow.md`
- Targeted diff review of changed instruction files and helper scripts.
- Final `git diff --check -- AGENTS.md .github\copilot-instructions.md docs\README.md docs\agents\code_reviewer.md` - passed.
- Final `Normalize-TextFiles.ps1 -Check` over all touched files - passed; all reported `Status=ok`, `Utf8Bom=False`, `BareLF=False`, `CROnly=False`.
- Final byte comparison of `AGENTS.md` and `.github\copilot-instructions.md` - identical.
- Trailing-whitespace check over new untracked helper/index/summary files - passed.
- Final default-exclude smoke: `Search-Repo.ps1 -Pattern "Monoculture bias"` and `Search-Repo.ps1 -Pattern "compact routing aid"` returned no output.
- Final include smoke: `-IncludeDrafts` returned the ignored draft match; `-IncludeTaskHistory` returned this task-summary match.
- Local review using `docs/agents/code_reviewer.md` - no issues found.

## Decisions - why
- Kept the new guidance in existing agent entry points instead of adding a long process document.
- Added scripts because recent summaries show repeated ad hoc search and line-ending commands.
- Kept old detailed summaries in place and added an index rather than rewriting history.

## Pitfalls - fixes
- Direct script execution was blocked by Windows execution policy; smoke commands use `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ...`.
- `Normalize-TextFiles.ps1 -Path` initially did not handle comma-separated path lists from `-File`; added comma splitting and documented smoke usage.
- `Search-Repo.ps1 -IncludeDrafts` initially still respected `.gitignore`; switched to explicit `--no-ignore` plus exclude globs so draft/task switches work.
- Added BOM-character removal in the normalizer so files with UTF-8 BOMs are rewritten without leaving a leading U+FEFF character.

## Risks / follow-ups
- The task index is a compact routing aid, not a complete replacement for full summaries.

## Heuristics (keep terse)
- Stable facts, heuristics, and ADRs not added; this task updates the canonical workflow instructions and tools directly.

## Testing instructions
- N/A - docs/tools/instructions only.

## Reflection
- The task summaries made the recurring costs clear: broad historical reads, CRLF/BOM cleanup, locked build outputs, and reviewer waits. A compact index plus small scripts should reduce repeated setup tokens without weakening review or verification.
- The new helper scripts should be called through `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ...` in this environment; direct `.\script.ps1` execution may be blocked.
- No sub-agent was needed for implementation. A local review against `docs/agents/code_reviewer.md` is appropriate for this docs/tooling workflow change after final checks.
