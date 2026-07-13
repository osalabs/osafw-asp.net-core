# Documentation Consistency Prompt

Update documentation for `<CHANGE_OR_TOPIC>`.

Goal: keep the documentation set accurate and easy to navigate without duplicating stale details across files.

## Work

1. Read repo instructions, `docs/README.md`, and the docs most directly affected by `<CHANGE_OR_TOPIC>`.
2. Search the task-summary index for related history before opening old summaries.
3. Identify the canonical doc for the topic and update that first.
4. Update `docs/README.md` when navigation or recommended reading order changes.
5. Update `docs/CHANGELOG.md` for breaking end-user-app upgrade changes.
6. When agent workflow changes, update the authoritative `AGENTS.md` rule first, regenerate its byte mirror, and update reviewer/navigation/summary docs only when their narrower contract changed.
7. Prefer links and concise cross-references over repeating the same detailed guidance in several docs.
8. Do not copy machine-local instructions, secrets, private scan details, bulky logs, or drafts into shared docs.

## Verification

- Run focused text searches for stale names, old paths, and contradictory guidance.
- Check edited markdown files for CRLF line endings and UTF-8 no BOM.
- For docs-only changes, record `N/A - docs only` or the exact text checks run.

Placeholder:
- `<CHANGE_OR_TOPIC>`: behavior, API, schema, workflow, prompt folder, or documentation area being synchronized
