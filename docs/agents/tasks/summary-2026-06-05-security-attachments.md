## What changed
- Hardened attachment access so attachment operations are checked against the related business record before linking, serving, or redirecting to storage.
- Tightened attachment content handling for browser delivery.
- Added focused regression tests around attachment authorization and content handling.

## Scope reviewed
- Attachment upload, link, local serve, storage redirect, and content-disposition paths.
- Dynamic-controller attachment linkage flows.
- Related tests and docs for changed attachment behavior.

## Commands used / verification
- Ran targeted searches across attachment models, controllers, upload helpers, and dynamic-controller linkage.
- Ran focused attachment security tests and a focused app build.
- Checked text normalization and diff hygiene.

## Decisions - why
- Kept authorization tied to the parent business object instead of treating attachment ids as globally sufficient.
- Preserved legitimate inline behavior only for content types the framework intentionally allows.

## Pitfalls - fixes
- Attachment behavior crosses multiple layers; verification covered link, serve, redirect, and upload helper paths together.

## Risks / follow-ups
- Apps with custom attachment endpoints should apply the same parent-record authorization contract.
- No schema changes were involved.

## Heuristics (keep terse)
- Attachment ids are not enough; authorize against the parent object before use.

## Testing instructions
Run the focused attachment security tests and build the app.

## Reflection
Attachment work is easy to under-scope because storage, linking, and serving are separate paths. Treating them as one authorization surface made the review clearer.
