## What changed
- Hardened dynamic-controller child-row and object-link flows so writes are checked against the parent or related record they modify.
- Preserved supported create-only public signup behavior while preventing broader record manipulation.
- Added focused regression coverage for the changed authorization contracts.

## Scope reviewed
- Dynamic controller child-row saves, attachment linkage, object-link helpers, and signup-style creation flows.
- Related tests and documentation for dynamic-controller behavior.

## Commands used / verification
- Ran targeted searches across dynamic-controller save/link paths and related models.
- Ran focused dynamic authorization tests and a focused app build.
- Checked formatting and diff hygiene.

## Decisions - why
- Kept authorization predicates close to the parent-record load or link write so each side effect has local proof of access.
- Treated create-only signup behavior as a separate allowed pattern rather than a general update capability.

## Pitfalls - fixes
- Some dynamic paths depended on generic ids; the fix was to re-check the related business object before side effects.

## Risks / follow-ups
- Custom dynamic modules should continue to define clear parent-record access predicates.
- No schema changes were involved.

## Heuristics (keep terse)
- Dynamic child and link writes need parent-record authorization, not just child ids.

## Testing instructions
Run the focused dynamic object-link authorization tests and build the app.

## Reflection
Dynamic-controller fixes are safest when tests describe the framework contract rather than one generated screen.
