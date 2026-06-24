# Security Hardening Prompt

Review and harden `<SCOPE>` for security issues.

Goal: find exploitable or support-heavy risks, fix confirmed issues with minimal blast radius, and verify the affected flows.

## Focus Areas

- Mutating custom actions must require POST and valid XSS tokens before side effects.
- Direct id reads, saves, deletes, attachment links, and child-row writes need object-level authorization predicates.
- Redirect targets must be app-local unless an explicit allowlist governs external destinations.
- Stored/user/editor HTML, markdown, and `v-html` surfaces must be escaped, sanitized, or explicitly trusted.
- Attachments must authorize against the parent business object before linking, serving, or redirecting to storage, and active content should be blocked or forced to download.
- Dev/admin tooling, generated SQL, assistant tools, generated file/schema writes, and telemetry must have exposure gates, allowlists, resource checks, and sensitive-data redaction.

## Work

1. Read repo instructions, security guardrails, and relevant task history from the task-summary index.
2. Scope the review to `<SCOPE>` and avoid broad unrelated cleanup.
3. Trace each candidate issue from reachable entry point to sink before fixing.
4. Fix the smallest contract that removes the risk while preserving intended product behavior.
5. Update templates/forms/tests/docs when a server-side security contract changes.
6. Run the review-fix loop until no material issues remain.

## Verification

- Run focused tests or builds for changed code.
- Manually verify affected flows when automated coverage is not practical.
- Record any risk areas not covered by the verification.

Placeholder:
- `<SCOPE>`: files, controller/action, feature, PR, or security finding set under review