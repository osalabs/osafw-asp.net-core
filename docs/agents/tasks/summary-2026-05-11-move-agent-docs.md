## What changed
- Moved agent instructions and framework docs from `osafw-app/` to the repository root.
- Preserved colliding app task summaries with `-app` suffixes because root task summaries already existed at those names.

## Commands that worked (build/test/run)
- `git -c safe.directory=C:/DOCS_PROJ/github/osafw-asp.net-core status --short`
- `Test-Path osafw-app\docs` returned `False`
- `rg -n "osafw-app/docs|osafw-app\\docs|osafw-app/AGENTS\.md|osafw-app\\AGENTS\.md|osafw-app/\.github/copilot-instructions\.md|osafw-app\\\.github\\copilot-instructions\.md" AGENTS.md .github README.md docs` returned no matches
- `git -c safe.directory=C:/DOCS_PROJ/github/osafw-asp.net-core diff --no-index -- AGENTS.md .github\copilot-instructions.md` returned no content differences

## Pitfalls - fixes
- Root `AGENTS.md` already existed as a redirect, so it was replaced by the app instruction file at the root path.
- Root `docs/agents/tasks` had filename collisions with app task summaries; app versions were moved to `-app` names to avoid data loss while keeping `git mv` history.
- `git diff --check` reports trailing whitespace in moved untracked `docs/FPF-Spec.md`; left content unchanged because it was a relocation, not a cleanup pass.

## Decisions - why
- Keep framework docs at `docs/` and the app project under `osafw-app/`, matching the requested repository layout.
- Preserve both sets of historical task summaries instead of overwriting one set.

## Heuristics (keep terse)
- When moving a directory into an existing tracked directory, give colliding moved files unique names so `git mv` can preserve source history.