## What changed
- Added remote modal autofocus support in `fw-modal.js`.
- Remote modals now focus a configured `data-modal-focus` selector after content loads and Bootstrap has shown the modal.
- If no selector is configured, remote modals focus the first visible form control, unless `data-modal-focus="none"` or Bootstrap `data-bs-focus="false"` disables focus.
- Simple `#id` focus selectors work in namespaced lookup modals by falling back to `data-fw-original-id`.
- Updated the DemosDynamic `demo_dicts_id` lookup Add/Edit config to use `data-modal-focus="#iname"`.
- Documented the option in `common/modal.html`, `docs/templates.md`, and `docs/dynamic.md`.

## Scope reviewed
- `docs/README.md`
- `docs/dynamic.md` lookup add/edit modal section
- `docs/templates.md` modal component section
- `docs/agents/tasks/index.md` entries for prior modal work
- `docs/agents/tasks/summary-2026-05-19-common-frontend-components.md`
- `docs/agents/tasks/summary-2026-05-26-modal-component-refactor.md`
- `docs/agents/code_reviewer.md`
- `osafw-app/wwwroot/assets/js/fw-modal.js`
- `osafw-app/App_Data/template/common/modal.html`
- `osafw-app/App_Data/template/admin/demosdynamic/config.json`
- `osafw-app/App_Data/template/admin/demodicts/showform/form.html`

## Commands used / verification
- `node --check osafw-app\wwwroot\assets\js\fw-modal.js` - passed.
- `Get-Content osafw-app\App_Data\template\admin\demosdynamic\config.json -Raw | ConvertFrom-Json | Out-Null` - passed.
- `git -c core.quotepath=false diff --check -- ...` for edited files - passed.
- CRLF/BOM byte check for edited files - passed; no BOM and no LF-only line endings.
- Playwright attempted `https://localhost:44315/Admin/DemosDynamic/1022/edit`, but the browser session was unauthenticated and redirected to `/`.
- Playwright public-page probe confirmed `https://localhost:44315` is serving an older `fw-modal.js` without the new focus code even with cache bypass, so browser smoke against the running app was inconclusive.
- Self-review using `docs/agents/code_reviewer.md` - no issues found after adding `data-bs-focus="false"` compatibility.

## Decisions - why
- Critical path: update `fw-modal.js` to focus after fetched modal content is inserted and after Bootstrap finishes showing the modal.
- Safe parallel side work: docs and demo config can be updated independently once the trigger attribute contract is known.
- Tightly coupled work: ID namespacing and focus selector resolution must stay together because lookup modals rewrite duplicate IDs.
- Kept the behavior in the shared modal component instead of adding controller/template-specific JavaScript because lookup Add/Edit modals are already centralized there.
- Preserved Bootstrap's explicit no-focus contract by treating `data-bs-focus="false"` like an opt-out when `data-modal-focus` is absent.

## Pitfalls - fixes
- `apply_patch` wrote LF endings; normalized touched files back to CRLF and verified byte-level line endings.
- Playwright MCP generated `.playwright-mcp/`; removed the generated folder after path verification.
- Running app asset verification was misleading because the server is not serving the edited workspace `fw-modal.js`.

## Risks / follow-ups
- Browser smoke on the exact admin lookup modal still needs a running app that serves this workspace's updated asset and an authenticated admin session.
- This is an additive frontend contract, so no `docs/CHANGELOG.md` entry was needed.

## Heuristics (keep terse)
- No stable facts, reusable heuristics, or ADRs added.

## Testing instructions
- Use `/Admin/DemosDynamic/1022/edit`, open the DemoDicts lookup Add/Edit menu, and click Add or Edit.
- The modal should focus the `DemoDicts` title input (`#iname`, namespaced in the rendered modal).
- For custom modal triggers, set `data-modal-focus="#field_id"` to select a target or `data-modal-focus="none"` to keep Bootstrap's default focus behavior.

## Reflection
- The main slowdown was the running local site serving an older modal asset than the workspace file. Future frontend smoke checks should first fetch the served asset and verify a unique new token before interacting with the UI.
- The self-review pass was useful: it caught the `data-bs-focus="false"` compatibility edge before closeout.
- No sub-agent was used because the diff is small and the code reviewer instructions are straightforward for a local self-review.
