## What changed
- Added `docs/naming.md` as the standard framework naming conventions guide.
- Linked the guide from `docs/README.md`, `docs/crud.md`, `docs/feature_modules.md`, `docs/agents/code_reviewer.md`, `docs/agents/heuristics.md`, and `AGENTS.md`.
- Trimmed repeated inline summaries after feedback so cross-links point to the canonical guide without duplicating its prefix list.

## Scope reviewed
- Read `docs/agents/local_instructions.md`, `docs/README.md`, the provided draft specification, `AGENTS.md`, `.github/copilot-instructions.md`, `docs/crud.md`, `docs/db.md`, `docs/feature_modules.md`, `docs/dynamic.md`, `docs/agents/code_reviewer.md`, `docs/agents/domain.md`, `docs/agents/heuristics.md`, and `docs/agents/glossary.md`.
- Searched existing docs and agent instructions for naming guidance to avoid duplicate or conflicting rules.

## Commands used / verification
- Read required local instructions and docs entry points before editing.
- Searched existing docs and agent instructions for prior naming guidance.
- Reviewed the final changed docs and links.
- Verified all created/edited files use CRLF line endings.
- Verified `AGENTS.md` and `.github/copilot-instructions.md` are byte-for-byte identical after sync.
- Verified developer-facing output does not contain draft-source-specific terms.

## Decisions - why
- Kept the guide convention-based rather than prohibitive because application projects built on the framework may have their own local naming requirements.
- Documented result-shape and side-effect prefixes first because those carry the most useful scan-time information in model-heavy code.
- Treated casing as a consistency tool, not a migration rule: controller actions and classes keep PascalCase, common framework model helpers keep lower camel case, constants keep ALL_CAPS where established, and locals prioritize immediate clarity.
- Kept one actionable `AGENTS.md` reference under Coding Style and removed the second catalog-style mention because `docs/README.md` already owns the broader documentation map.

## Pitfalls - fixes
- Avoided broad renaming of existing code because this task is setting conventions for future granular refactors.
- Avoided leaking draft-source terminology into developer-facing docs because framework contributors should be able to use the guide directly without extra context.

## Risks / follow-ups
- Existing code still contains names that do not follow the guide; future refactors should handle those locally with compatibility in mind.

## Heuristics (keep terse)
- Prefer names that reveal result shape or side effect before qualifier/filter.
- Do not rename unrelated working code just to satisfy naming guidance.

## Testing instructions
N/A - docs/instructions only.

## Reflection
- Stable naming guidance now lives in one canonical docs file, with short links from the places developers and coding agents already read.
