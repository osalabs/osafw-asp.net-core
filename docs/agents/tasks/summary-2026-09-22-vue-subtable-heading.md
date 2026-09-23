# Vue demo subtable heading

Matched the Vue demo's read-only and edit subtable label classes to the Dynamic demo: `hr-header fs-5`, with centered text for the edit heading. This uses existing CSS and changes only the demo configuration.

Validation feedback was inspected but not changed, as requested. The legacy Vue top danger alert already exists on `origin/master`; the branch adds the issue summary and an additional field issue renderer, which duplicates legacy field feedback. Proposed presentation changes remain a discussion, with legacy error response and failure-retention contracts unchanged.

Verification: parsed the JSON configuration, compared the two class values with the corresponding Dynamic definitions, and inspected the live Chrome edit Relations tab. The label rendered as a grid with both horizontal rules visible. No form saves or database writes were needed. `git diff --check` passed; UTF-8 without BOM and CRLF were verified. No build or automated tests were needed for the two styling values.

Review: local second pass for this bounded cosmetic demo change; no shared template or CSS contract changed. No findings; review loop can stop. No breaking-change changelog or asset version bump is needed because existing CSS is reused.
