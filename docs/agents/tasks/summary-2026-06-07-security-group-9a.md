## What changed
- Hardened route/action matching consistency, virtual-controller cache invalidation, logging defaults, telemetry PII defaults, and dashboard documentation.
- Corrected related configuration-key drift and added regression coverage.
- Documented that the default dashboard is sample framework output that production apps should scope or replace as needed.

## Scope reviewed
- Framework routing/auth identity comparisons.
- Virtual controller lookup/cache behavior.
- Logging and telemetry defaults.
- Default dashboard sample data and docs.
- Relevant configuration keys and tests.

## Commands used / verification
- Ran focused route/auth, virtual-controller, logging/config, and dashboard tests.
- Built the app and ran a broad test pass that was green at the time of this task.
- Checked formatting, diff hygiene, and final review notes.

## Decisions - why
- Treated route/controller/action identity comparisons as case-insensitive framework identifiers.
- Kept logging defaults redacted while allowing explicit local debug verbosity through configuration.
- Kept the framework dashboard as sample output rather than adding app-specific authorization policy to sample panes.

## Pitfalls - fixes
- Cache ownership and canonicalization order mattered; fixes were placed where controller identity is resolved.
- Local build/test output required isolated directories when the app was running.

## Risks / follow-ups
- Apps with tenant, company, or project scopes should replace or scope sample dashboard panes.
- Deployments with custom telemetry settings should verify production scrub settings.

## Heuristics (keep terse)
- Framework identity matching should be consistent, and sample dashboard data is not a substitute for app-specific authorization.

## Testing instructions
Run the focused route/auth, virtual-controller, logging/config, and dashboard tests, then build the app.

## Reflection
The useful distinction was separating framework sample behavior from app authorization policy. That avoided turning a security cleanup into a broad product decision.
