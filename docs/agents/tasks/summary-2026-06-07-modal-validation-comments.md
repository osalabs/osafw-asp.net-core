## What changed
- Updated `osafw-app/wwwroot/assets/js/fw-modal.js` so modal AJAX submits run browser form validation before building/submitting the request.
- Added `canSubmitForm(form, submitter)` with `reportValidity()` / `checkValidity()` feature detection and `novalidate` / `formnovalidate` bypass support.
- Added concise comments around modal helper methods and non-obvious rewrite/submit blocks.
- Updated `docs/agents/tasks/index.md` for this task summary.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/agents/tasks/index.md`
- `docs/agents/tasks/summary-2026-05-26-modal-component-refactor.md`
- `docs/agents/tasks/summary-2026-05-20-fw-js-reportvalidity-guard.md`
- `docs/agents/tasks/summary-2026-05-12-modal-pjax-links.md`
- `docs/agents/code_reviewer.md`
- `osafw-app/wwwroot/assets/js/fw.js`
- `osafw-app/wwwroot/assets/js/fw-modal.js`

## Commands used / verification
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check osafw-app\wwwroot\assets\js\fw-modal.js docs\agents\tasks\summary-2026-06-07-modal-validation-comments.md docs\agents\tasks\index.md` - passed after normalization.
- `node --check osafw-app\wwwroot\assets\js\fw-modal.js` - passed.
- `git diff --check -- osafw-app\wwwroot\assets\js\fw-modal.js docs\agents\tasks\summary-2026-06-07-modal-validation-comments.md docs\agents\tasks\index.md` - passed.
- Browser MCP at `https://localhost:44315/Login`: logged in with local test credentials, opened `https://localhost:44315/Admin/DemosDynamic/new`, confirmed `/assets/js/fw-modal.js?v0.26.0526` was loaded, and injected a disposable `.fw-modal` form to avoid DB writes.
- Browser injected modal check: blank required field kept `fetchCount=0`, `form.checkValidity()=false`, and retained the form; filling the field submitted once with `POST` to `?_layout=modal` and replaced content.
- Browser injected `formnovalidate` check: blank required field with `formnovalidate` submitter submitted once, confirming bypass behavior.
- Self-reviewed final diff using `docs/agents/code_reviewer.md`; no issues found, review loop can stop.

## Decisions - why
- Mirror the existing `fw.js` `reportValidity()` / `checkValidity()` feature-detection pattern, but keep it local to modal AJAX submits.
- Respect `novalidate` and submitter `formnovalidate` so AJAX modal submit behavior does not become stricter than native form submission.
- Keep comments short and focused on helper intent or non-obvious control flow.

## Pitfalls - fixes
- Existing untracked task/security summary files and `osafw-app/App_Data/db/` were present before editing; this task does not touch them.
- Direct in-app Browser setup failed twice with a sandbox setup error; used Playwright MCP as the browser fallback.
- `apply_patch` created LF-only edits; normalized touched files with `docs/agents/tools/Normalize-TextFiles.ps1`.

## Risks / follow-ups
- No breaking changelog entry expected; this is a bug fix / documentation-in-code clarification for existing modal submit behavior.
- No full app build was run; this is static JavaScript and was checked with `node --check` plus browser behavior probes.
- Browser probes used injected disposable modal forms instead of saving real demo records.

## Heuristics (keep terse)
- None added.

## Testing instructions
- Run `node --check osafw-app\wwwroot\assets\js\fw-modal.js`.
- In a page with `<~/common/modal>`, open or inject a `.fw-modal` form with a required field and `.on-fw-modal-submit`; blank required fields should show browser validity UI and not issue the modal fetch.
- Add `formnovalidate` to the modal submitter to confirm the validation bypass still submits.

## Reflection
- Direct in-app Browser setup was the only tooling slowdown; after one retry, Playwright MCP was the right fallback and gave enough behavioral coverage.
- A disposable injected modal is a low-risk way to verify shared modal submit behavior without creating database records or depending on specific demo lookup data.
- No stable facts, heuristics, or ADRs were added; the task did not reveal a recurring repo convention beyond existing frontend verification practice.
