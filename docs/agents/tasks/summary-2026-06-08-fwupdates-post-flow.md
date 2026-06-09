## What changed
- Split developer update discovery from update execution.
- Kept the discovery path read-only and moved update execution behind the existing authenticated POST/request-token workflow.
- Hardened neighboring update-management mutations and added focused regression coverage.

## Scope reviewed
- Developer configuration update discovery.
- Admin update-management execution paths.
- Login return-target preservation for safe local navigation after authentication.
- Related docs, tests, and changelog wording.

## Commands used / verification
- Ran focused update-management, developer-configuration, and login-target tests.
- Built the app with isolated output when needed.
- Ran route/source searches to confirm the old execution path was removed.
- Checked formatting and diff hygiene.

## Decisions - why
- Kept status/discovery pages read-only and put state-changing update execution in the authenticated management area.
- Reused existing POST/request-token form mechanics rather than inventing a separate flow.

## Pitfalls - fixes
- The previous implementation combined discovery and execution concerns; separating them made the security boundary easier to verify.

## Risks / follow-ups
- Lower-access users rely on normal access-denied behavior for management actions.
- No schema changes were involved.

## Heuristics (keep terse)
- Developer/bootstrap status pages should stay read-only; maintenance actions belong behind authenticated POST flows.

## Testing instructions
Run the focused update-management and developer-configuration tests, then build the app.

## Reflection
The cleanest fix was to separate read-only status from state-changing maintenance, using the management UI that already had the right form mechanics.
