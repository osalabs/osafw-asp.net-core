## What changed
- Hardened trusted-origin handling for development/bootstrap flows and host-derived framework URLs.
- Converted database initialization from a browsable action into a tokenized POST flow.
- Reduced public development diagnostics to coarse status flags.
- Added tests and changelog/docs updates for the changed behavior.

## Scope reviewed
- Host trust configuration and request-host handling.
- Development configuration/bootstrap actions.
- Password-reset link origin construction.
- Related tests, docs, and changelog entries.

## Commands used / verification
- Ran focused tests for host trust, development configuration, and reset-link origin behavior.
- Built the app with isolated output when necessary.
- Performed local smoke checks without retaining exact hostile-host probes in this public summary.
- Checked formatting and diff hygiene.

## Decisions - why
- Trusted-origin checks now require explicit trusted host patterns rather than permissive matching.
- Kept public diagnostics useful for setup while avoiding detailed environment or database errors.

## Pitfalls - fixes
- Host trust bugs are easy to reintroduce through substring-style checks; tests now cover safer full-host matching behavior.

## Risks / follow-ups
- Deployments should configure trusted host patterns intentionally for their environment.
- No schema changes were involved.

## Heuristics (keep terse)
- Host-derived URLs and bootstrap actions need explicit trusted-origin policy and read-only public diagnostics.

## Testing instructions
Run the focused host/development-configuration tests and build the app.

## Reflection
Public summaries for origin fixes should describe the policy change, not publish exact old failure examples.
