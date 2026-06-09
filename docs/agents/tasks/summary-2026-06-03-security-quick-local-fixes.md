## What changed
- Applied a set of small local hardening fixes around request parsing, redirect handling, password-reset response consistency, development diagnostics, and report-email validation.
- Kept behavior compatible for valid requests while making unsafe or malformed inputs fail closed.

## Scope reviewed
- Request parsing in the core framework pipeline.
- Login and redirect target handling.
- Password reset response behavior.
- Development setup diagnostics.
- Report email submission validation.

## Commands used / verification
- Ran focused source searches for the touched request, redirect, diagnostic, and email paths.
- Ran targeted regression tests and a focused app build.
- Checked formatting and diff hygiene.

## Decisions - why
- Preferred small guard changes at the existing boundaries instead of broad rewrites.
- Kept public-facing responses generic where detailed state would be unnecessary.

## Pitfalls - fixes
- Several issues were independent and small; grouping them worked because each had a narrow, easily verified boundary.

## Risks / follow-ups
- Broader security posture still depends on future custom actions using the same patterns.
- No schema changes were involved.

## Heuristics (keep terse)
- Parse untrusted input defensively and validate redirects at both input and use sites.

## Testing instructions
Run the focused quick-fix security tests and build the app.

## Reflection
Small security fixes are easiest to review when each is kept at the existing trust boundary and verified with a focused test.
