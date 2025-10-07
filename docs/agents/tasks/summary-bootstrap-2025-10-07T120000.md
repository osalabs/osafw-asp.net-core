# Summary — bootstrap at 2025-10-07T12:00:00

## What changed
- Created agent docs structure: `docs/agents/{tasks,domain,tmp}`
- Added `docs/agents/config.yaml` with runtimes and entrypoints
- Added domain docs: `docs/agents/domain/DOMAIN.md`, `docs/agents/domain/glossary.md`
- Added task summary template: `docs/agents/tasks/summary.template.md`
- Updated `AGENTS.md`: added Repo Map and Command Palette; removed BOOTSTRAP block
- Committed as `chore(agents): bootstrap`

## Commands that worked (build/test/run)
- git add AGENTS.md docs/agents
- git commit -m "chore(agents): bootstrap"

## Pitfalls ? fixes
- PowerShell rejected `&&` separator; ran `git` commands separately.

## Decisions ? why
- Set `.NET` runtime to `8.0` and entrypoints to standard `dotnet` commands to match solution TFMs.
- Kept domain docs minimal per template to avoid speculation.

## Heuristics (keep terse; add Expires if needed)
- Prefer simple, cross-platform commands.
- Keep Repo Map and Command Palette near top of `AGENTS.md` for quick navigation.
