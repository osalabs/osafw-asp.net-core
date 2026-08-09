# Security Boundary Review Overlay

Use when authentication, authorization, mutation, rendering, redirects, uploads/attachments, secrets/privacy, dev/admin exposure, or external tool/data boundaries change. Return candidate findings to the integrator; do not issue a separate verdict.

## Checks

- Trace identity, access level/roles/resources, tenant/host, owner, and parent-object scope from request entry to the exact row/file/render/serve/write boundary.
- State-changing custom actions enforce POST before side effects and require the current XSS token unless a documented exemption intentionally applies. GET, preview, retry, and helper paths must not mutate accidentally.
- Direct-id read/update/delete predicates include authorization. User preferences use owner-or-system scope; dynamic child and attachment operations authorize through the parent business object.
- Redirect destinations satisfy the local-URL policy unless a Site Admin-managed external allowlist explicitly governs them.
- User/stored/editor HTML and markdown are escaped or sanitized before output. `noescape`, raw markdown HTML, Vue `v-html`, and HTML email content require server-controlled or already-sanitized data.
- Attachment link/view/download/S3 redirect decisions validate the parent business object. Active browser content is blocked or forced to inert download; image decode/dimension/resource limits remain bounded.
- SQL is parameterized through `DB`; file paths/archives and generated file/schema writes use normalized explicit allowlists and cannot escape their owned roots.
- Dev/admin/scaffold/assistant tools have environment and exposure gates plus ordinary authorization/resource checks. Production behavior cannot be enabled merely by spoofable host/header input.
- Secrets, tokens, session IDs, request bodies, user content, SQL/tool payloads, connection strings, and production exception details are not committed or exposed through logs, responses, telemetry, task summaries, or agent output.
- External services/accounts and optional connectors use least privilege and do not turn a read/review request into a write/message/deploy action.

Include an abuse path or concrete violated boundary with every candidate finding. Do not report generic hardening ideas without a reachable threat.
