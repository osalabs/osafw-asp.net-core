# Update Assistant Navigation Catalog Prompt

Update `osafw-app/App_Data/template/assistant/prompts/navigation_catalog.json` for this application.

Goal: keep the AI Assistant navigation tool accurate, curated, and safe without runtime controller discovery.

## Work

1. Follow the repo instructions, line-ending policy, task-summary rules, and review workflow.
2. Read `docs/assistant.md`, `docs/dynamic.md`, and the current `navigation_catalog.json`.
3. Scan application controllers, dynamic configs, route base URLs, list filter templates, showform templates, and relevant docs.
4. Include only useful screens that a business user or administrator would reasonably ask the Assistant to open. Skip login, password reset, attachments, dev/test routes, plumbing endpoints, and one-off custom actions unless the app explicitly needs them.
5. For each catalog entry, verify the real base URL, controller name, access level, supported actions, search/list filters, and new-form prefill fields from code or templates.
6. Preserve good manual descriptions and keywords unless they are stale or misleading. Keep descriptions short and business-facing.
7. Add filters only when the screen really supports the simple `f[field]` URL shape. Do not invent hidden filters from model fields alone.
8. Add prefill fields only from real save fields or showform fields that are safe to prepopulate. Do not include password, token, secret, audit, or system-only fields.
9. Use numeric access levels from controller code or explicit access rules. When action-level access differs, prefer the safer higher access level or split the screen into separate entries only if that keeps behavior clear.
10. Keep JSON deterministic: valid JSON, two-space indentation, stable route ordering, no comments, no trailing commas.

## Verification

- Parse `navigation_catalog.json` as JSON.
- Run focused assistant/navigation tests when available.
- Search for stale prompt paths and deleted/renamed routes.
- Record whether any screens were intentionally skipped.

## Closeout

Summarize changed entries, skipped route categories, verification run, and any catalog areas that need app-owner review.
