## What changed
- Sanitized June security-related task summaries so they are suitable for a public framework repository commit.
- Removed exact reproduction probes, embedded secret references, private scan artifact paths, local machine paths, internal finding identifiers, and remaining-risk details from commit-candidate summaries.
- Renamed CAN-coded task-summary filenames to plain public task ids.
- Moved the detailed reconciliation summary to ignored agent artifacts so it remains local/private.

## Scope reviewed
- `docs/agents/tasks/summary-2026-06-*security*.md`
- Related June hardening summaries without `security` in the filename but tied to the same remediation work.
- Prior public-safety audit notes from `docs/drafts/security2026-06_review.md`.

## Commands used / verification
- `git status --short docs/agents/tasks docs/drafts/security2026-06_review.md`
- `rg` scans for local paths, exact probes, private artifact references, embedded secret references, internal finding identifiers, and remaining-risk language.
- Rewrote public summaries with CRLF/UTF-8-no-BOM output.
- Final status and pattern scans were run after edits.

## Decisions - why
- Kept public summaries useful for maintainers by preserving changed behavior, scope, verification category, risks, and future process notes.
- Kept detailed reconciliation private instead of redacting it in place because it is most useful as a trusted local audit note.
- Did not edit ignored draft review artifacts because they are not part of the public commit path.

## Pitfalls - fixes
- Some useful verification notes included local ports, request payloads, and private artifact roots; those were replaced with higher-level verification outcomes.
- The task itself needed a public-safe summary so it does not recreate the same disclosure problem.

## Risks / follow-ups
- Do not force-add `docs/drafts/` or `docs/agents/artifacts/` security review material.
- If old detailed summaries were copied elsewhere, review those copies before publication.

## Heuristics (keep terse)
- Commit-ready security summaries should explain the behavior change, not the old failure path.

## Testing instructions
N/A - docs/task summaries only.

## Reflection
A public/private summary split would save time in future security work. Agents should write public-safe task summaries by default and keep exact probes or assessment evidence in ignored artifacts.
