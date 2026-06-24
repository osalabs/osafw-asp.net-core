## What changed
- Hardened several custom mutating framework actions so valid requests must use the framework's existing POST and request-token flow.
- Preserved the existing user-facing behavior for legitimate form submissions.

## Scope reviewed
- Custom admin, report, and list-management mutation paths that sit outside the standard CRUD save/delete flow.
- Existing templates and forms that submit to those paths.

## Commands used / verification
- Ran focused source searches to confirm the intended mutation paths were covered.
- Built the app and ran targeted security regression tests for the affected flows.
- Checked text formatting and diff hygiene after edits.

## Decisions - why
- Reused the existing framework request-token helper instead of adding a parallel security mechanism.
- Kept template changes minimal because existing form infrastructure already supports tokenized POSTs.

## Pitfalls - fixes
- Some custom actions looked similar to standard CRUD actions but sat outside the standard protection path; the fix was applied at each custom action boundary.

## Risks / follow-ups
- Future custom mutating actions should call the same helper before side effects.
- No schema changes were involved.

## Heuristics (keep terse)
- Custom mutation paths need explicit method and request-token checks.

## Testing instructions
Run the focused security tests for custom mutation guards, then build the app.

## Reflection
The main process lesson is to review custom actions separately from generated CRUD actions. They can look routine while sitting outside framework defaults.
