## What changed
- Added concise guidance to `AGENTS.md` to prefer behavior-level tests, avoid production helper extraction solely for test access, and keep one-use wrappers out of production code unless they clarify or protect real contracts.
- Updated `docs/agents/code_reviewer.md` so reviewer feedback applies the same balance when judging simplicity and test coverage.
- Synced `.github/copilot-instructions.md` from `AGENTS.md`.

## Scope reviewed
- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/agents/code_reviewer.md`
- `docs/agents/tasks/index.md`
- Existing local instructions and docs entry points required by `AGENTS.md`

## Commands used / verification
- Byte-for-byte hash check for `AGENTS.md` and `.github/copilot-instructions.md`: passed.
- CRLF/no-BOM check for touched files: passed.
- `git diff --check`: passed.
- Fresh-context sub-agent wording test: passed. The agent judged the instructions clear and chose controller/action boundary verification over extracting five production helpers only for private branch coverage.

## Decisions - why
- Kept the new guidance universal rather than tied to developer-only features because the same tradeoff appears in framework, app, template, and controller work.
- Put production-code simplicity in `Coding Style` and coverage strategy in `Testing Guidance` so future agents can find the rule at the point of decision.
- Updated the reviewer prompt because review feedback often drives unnecessary helper extraction after implementation.

## Pitfalls - fixes
- The instruction should not discourage tests for risky logic. The wording still calls out shared, complex, security-sensitive, deterministic, and hard-to-reach logic as good lower-level test candidates.
- The fresh-context sub-agent suggested optional tightening around manual/browser checks and hard-to-reach logic. No extra wording was added because the current files already prefer public-boundary checks only when they can falsify the changed behavior and still preserve lower-level tests for meaningful risk.

## Risks / follow-ups
- Future reviewers may still over-request lower-level tests for every branch. If that recurs, tighten the reviewer wording after observing a concrete case.

## Heuristics (keep terse)
- Prefer the simplest production shape that expresses the behavior clearly; test through the nearest public boundary that can falsify the change.

## Testing instructions
- N/A - docs/instructions only.

## Reflection
The recurring friction was not lack of tests, but tests pushing production code toward artificial structure. Future agents should decide the production shape first, then choose the nearest useful verification boundary. This belongs in shared instructions because it is likely to recur across framework and app repositories.
