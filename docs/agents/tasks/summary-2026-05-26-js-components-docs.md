## What changed
- Added concise `fw.initComponent()` / `fw.registerComponent()` guidance to `docs/templates.md` near the common JavaScript component includes.

## Scope reviewed
- Reviewed `docs/agents/local_instructions.md`, `docs/README.md`, `docs/templates.md`, `osafw-app/wwwroot/assets/js/fw.js`, and existing common component includes.

## Commands used / verification
- `git diff --check -- docs\templates.md docs\agents\tasks\summary-2026-05-26-js-components-docs.md` - passed.
- CRLF byte check for `docs\templates.md` and this task summary - passed.

## Decisions - why
- Kept the guidance in `docs/templates.md` because JS components are shipped as `/common/*.html` template includes and the existing component list already lives there.
- Recommended direct `fw.initComponent()` as the default pattern, with `fw.registerComponent()` reserved for named config reuse.

## Pitfalls - fixes
- Documented that the component registry is not asset load state and that callers should not invoke registered `init` functions directly.

## Risks / follow-ups
- No runtime change.

## Heuristics (keep terse)
- None added.

## Testing instructions
- N/A - docs only.

## Reflection
- The existing docs had component usage examples but not the lifecycle rule; future agents should add small convention notes next to the examples they govern instead of creating extra docs for narrow template patterns.
