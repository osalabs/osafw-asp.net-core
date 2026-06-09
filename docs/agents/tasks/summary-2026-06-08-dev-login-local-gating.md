## What changed
- Restricted the developer convenience login path so it is only available in clearly local development conditions.
- Preserved normal login behavior for all other deployments and requests.
- Added regression coverage and a changelog note for the changed development-only behavior.

## Scope reviewed
- Login controller behavior for development convenience access.
- Host/config/environment checks used before allowing local-only development shortcuts.
- Related tests and changelog guidance.

## Commands used / verification
- Ran focused login/security tests for local and non-local conditions.
- Built the app using isolated output when normal build output was locked.
- Performed local smoke checks without retaining exact probe commands in this public summary.
- Checked formatting and diff hygiene.

## Decisions - why
- The shortcut now requires multiple local-development signals rather than trusting a single request-controlled value.
- Kept compatibility with named development configurations while blocking non-local use.

## Pitfalls - fixes
- Local test output was initially too specific for public docs; this summary now records the verification outcome without request payloads or probe details.

## Risks / follow-ups
- Custom local development hosts may need normal login or a separately reviewed local-only workflow.
- No schema changes were involved.

## Heuristics (keep terse)
- Development shortcuts need independent local-development gates and should not rely on request-controlled host data alone.

## Testing instructions
Run the focused login/security tests and build the app.

## Reflection
Verification details for security shortcuts should be useful to maintainers without publishing exact request probes.
