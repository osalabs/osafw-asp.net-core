## What changed
- Added server-side POST/request-token enforcement before DevManage generator, schema, and file-write side effects.
- Preserved existing valid UI submissions because built-in forms already use the required submission pattern.
- Added a changelog note for automation that must use the supported form flow.

## Scope reviewed
- DevManage generator and maintenance actions that can write source, templates, configuration, schema descriptors, SQL, or database changes.
- Built-in DevManage templates that submit to those actions.
- Related changelog and verification commands.

## Commands used / verification
- Ran targeted source and template searches for DevManage generator/maintenance callers.
- Built the app using isolated output after normal output was locked by the running app.
- Checked text normalization and diff hygiene.
- Performed local review against the security reviewer guidance.

## Decisions - why
- Used the existing framework POST/request-token helper at each side-effect boundary.
- Did not add a broader environment gate in this task because that would be a product-policy change beyond the requested scoped hardening.

## Pitfalls - fixes
- The templates already used the right submission method, so the fix stayed in the controller boundary.
- Normal build output was locked; isolated output avoided interfering with the running app.

## Risks / follow-ups
- Broader resource limits, confirmations, or local-only policy for developer generators can be considered separately.
- No live forged-request replay was run in this task.

## Heuristics (keep terse)
- Generator and maintenance write actions need server-side method/token checks even when the UI form is already correct.

## Testing instructions
Run a focused app build; use isolated output if the running app locks normal build output.

## Reflection
Checking templates first confirmed there was no need to change the UI. The durable rule is still server-side enforcement before generator side effects.
