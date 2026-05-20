## What changed
- Guarded `.on-submit` form submission in `fw.js` so missing/invalid form targets return without throwing.
- Added a shared local submit helper that uses `reportValidity()` when present, falls back to `checkValidity()`, and otherwise preserves submit behavior.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/agents/code_reviewer.md`
- `osafw-app/wwwroot/assets/js/fw.js` around the `.on-submit` handler

## Commands used / verification
- `node --check osafw-app\wwwroot\assets\js\fw.js`
- `git diff --check`
- Confirmed CRLF line endings for edited files.
- Code reviewer sub-agent reviewed the final diff and found no issues; review loop can stop.

## Decisions - why
- Kept the change local to the `.on-submit` handler because the reviewer feedback is specific to the external submit-button path.
- Used feature detection instead of optional chaining so the fallback branch is explicit and older browser behavior is easy to review.

## Pitfalls - fixes
- Empty jQuery sets can still safely no-op through earlier `.find()` calls, but `$form[0].reportValidity()` dereferenced `undefined`; the helper now returns before validation/submission when no form exists.

## Risks / follow-ups
- Manual browser verification of native constraint UI not run; static syntax and whitespace checks passed.

## Heuristics (keep terse)
- None added.

## Testing instructions
- Run `node --check osafw-app\wwwroot\assets\js\fw.js`.
- Run `git diff --check`.

## Reflection
- Stable facts, heuristics, and ADRs not added; this is a narrow bug fix to an existing frontend helper.
