## What changed
- Added concise performance guardrails to `AGENTS.md` for repeated/hot paths, unbounded data, blocking I/O, large allocations, expensive per-request resources, and measured versus obvious optimization.
- Updated `docs/agents/code_reviewer.md` with a performance review priority and a focused project check for repeated expensive work in loops/request paths.
- Synced `.github/copilot-instructions.md` from `AGENTS.md`.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/agents/code_reviewer.md`
- `docs/agents/tasks/index.md`

## Commands used / verification
- Byte-for-byte hash check for `AGENTS.md` and `.github/copilot-instructions.md`: passed.
- CRLF/no-BOM check for touched files: passed.
- `git diff --check`: passed.

## Decisions - why
- Kept the performance wording broad enough for DB, remote calls, file I/O, config/template work, allocations, and blocking calls instead of only mentioning DB-in-loop.
- Included a measurement requirement for non-obvious or invasive changes so reviews catch obvious scale problems without pushing speculative rewrites.
- Added preservation constraints for authorization, ordering, empty results, and staleness because performance fixes can silently change behavior.
- Reviewed `docs/README.md`; no update was needed because the existing agent docs map already points to `AGENTS.md` and `docs/agents/code_reviewer.md`.

## Pitfalls - fixes
- Avoided a long checklist of micro-optimizations. The rule targets repeated expensive work, unbounded data, blocking I/O, and resource lifetime mistakes that commonly matter in web apps.

## Risks / follow-ups
- Future review output may still need concrete examples if agents miss repo-specific expensive work patterns. Add examples only after repeated misses.

## Heuristics (keep terse)
- In repeated request paths, first ask whether the same expensive work can be done once, done in the data source, paged, cached, or skipped.

## Testing instructions
- N/A - docs/instructions only.

## Reflection
Performance review guidance should identify scale risks without turning every review into tuning work. The useful convention is to flag clear repeated work and unbounded data from code structure, while requiring evidence before introducing complex optimization.
