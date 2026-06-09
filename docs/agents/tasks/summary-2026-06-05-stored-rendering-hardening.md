## What changed
- Made stored markdown and selected Vue-rendered content safe by default.
- Added explicit trusted-content opt-ins for bundled/server-controlled markdown and raw custom renderers.
- Limited lower-trust editors from saving executable static-page fields.
- Added focused rendering regression tests and updated rendering documentation.

## Scope reviewed
- ParsePage markdown rendering.
- Static page editable fields that can affect rendered HTML/CSS/JS.
- Vue markdown, activity-log, and custom-list rendering contracts.
- Dynamic configuration/code-generation paths that mark trusted markdown.

## Commands used / verification
- Ran focused stored-rendering tests and a focused app build.
- Ran a broader test suite; remaining failures were unrelated pre-existing test gaps noted at the time.
- Used reviewer passes to check fallback behavior and trusted/untrusted renderer coverage.
- Checked CRLF/no-BOM formatting after edits.

## Decisions - why
- Changed defaults to safe rendering while keeping explicit trusted opt-ins for framework-owned content.
- Avoided changing the general raw-rendering mechanism outside the scoped markdown/Vue surfaces.

## Pitfalls - fixes
- Fallback paths can fail open if only the primary renderer is reviewed; reviewer feedback led to safer fallback handling.
- Text normalization required care to preserve CRLF without adding a BOM.

## Risks / follow-ups
- Historical trusted static-page values still render; this task controls future writes by lower-trust editors.
- Vue coverage is primarily static/template-contract based rather than a full browser component test.

## Heuristics (keep terse)
- Stored rendering should be safe by default and require explicit trusted opt-ins for raw content.

## Testing instructions
Run the focused stored-rendering tests and build the app. Treat any broad-suite failures separately unless they involve rendering behavior.

## Reflection
Reviewer passes were useful because raw-rendering safety depends as much on fallback behavior as on the primary renderer path.
