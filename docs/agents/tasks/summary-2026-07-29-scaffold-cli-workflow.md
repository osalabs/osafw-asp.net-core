## What changed

- Replaced the conditional scaffolding preference in `AGENTS.md` with a concise rule requiring the documented CLI workflow for new standard CRUD modules.
- Added an explicit approval step before an agent applies a missing table to the configured development database.
- Clarified in `docs/feature_modules.md` that agents prepare schema files first, request approval to apply them, then run `scaffold crud`; manual generation is limited to declined approval, an unavailable database, or scaffolder failure.
- Reverted the previously created Contacts model, controller, templates, schema, migrations, tests, and task summary at the user's request.
- Kept `.github/copilot-instructions.md` byte-for-byte synchronized with `AGENTS.md`.

## Scope reviewed

- Repository authority and canonical feature-module workflow documentation.
- The current task's Contacts-only source, schema, template, test, and summary changes.
- Unrelated untracked worktree files and the pre-existing empty Contacts directories.

## Commands used / verification

- Confirmed `AGENTS.md` and `.github/copilot-instructions.md` have matching SHA-256 hashes.
- Ran the repository UTF-8/CRLF validation on changed documentation.
- Ran `git diff --check` and inspected final status/diff to confirm the Contacts implementation was removed without touching unrelated files.

## Decisions - why

- Kept the decision rule short in `AGENTS.md` and left detailed sequencing and fallback conditions in canonical `docs/feature_modules.md` to avoid duplicated always-loaded instructions.
- Required approval at the point a configured development database needs mutation, so the workflow works consistently for all developers without machine-local authorization policy.
- Explicitly prohibited choosing manual generation solely because the table is initially absent; this closes the ambiguity that led to the manual Contacts implementation.

## Risks / follow-ups

- The workflow still depends on an available configured development database and a provider-appropriate way to apply the prepared schema; the documented fallback applies if either is unavailable.
- No runtime, schema, public API, or end-user behavior remains changed, so no automated application tests or changelog entry were required.
- No stable domain fact, heuristic, or ADR was added.

## Testing instructions

For the next standard CRUD-module request, verify that the agent prepares schema files, requests approval if the table is absent from the configured development database, applies it only after approval, and runs `dotnet run --project osafw-app -- scaffold crud <table>` before customizing generated output.

## Reflection

Routing detailed steps to the canonical module guide keeps the always-loaded agent policy small while making the approval and CLI decision points unambiguous.
