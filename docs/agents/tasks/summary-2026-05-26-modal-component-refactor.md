## What changed
- Moved inline `common/modal.html` JavaScript to cacheable `wwwroot/assets/js/fw-modal.js`.
- Added Bootstrap `data-bs-*` passthrough from modal triggers to generated modal roots, excluding trigger/dismiss-only attributes.
- Added `data-modal-class` for root `.modal` class extension.
- Added `fw.disposeComponents(scope)` and wired modal content replacement/removal to dispose scoped component/plugin state.
- Updated `docs/templates.md` with the external modal script, Bootstrap option passthrough, and disposal hook guidance.
- Follow-up: simplified `fw.disposeComponents(scope)` by removing repeated plugin-specific `try/catch` blocks and using a compact plugin cleanup table.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/templates.md`
- `osafw-app/App_Data/template/common/modal.html`
- `osafw-app/wwwroot/assets/js/fw.js`
- Common component includes for calendar/select2/bootstrap-select.

## Commands used / verification
- `node --check osafw-app\wwwroot\assets\js\fw.js` - passed.
- `node --check osafw-app\wwwroot\assets\js\fw-modal.js` - passed.
- `git diff --check -- osafw-app\App_Data\template\common\modal.html osafw-app\wwwroot\assets\js\fw.js docs\templates.md` - passed.
- CRLF/BOM check for edited docs/templates/js files - passed, no BOM and no LF-only line endings.
- `dotnet build osafw-app\osafw-app.csproj` - blocked first by sandbox temp-file access, then outside sandbox by IIS Express locking `bin\Debug\net10.0\osafw-app.dll`.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build\` - passed in isolated output; generated output was removed after verification.
- Browser MCP at `https://localhost:44315/Admin/DemosDynamic/new`:
  - Logged in with local test credentials from local instructions.
  - Verified `<~/common/modal>` loads `/assets/js/fw-modal.js?v0.26.0519`.
  - Opened normal lookup modal; verified remote modal title, `_layout=modal` URL, lookup ID namespacing, and modal display.
  - Saved a lookup through the modal; verified the source select gained the option and selected the saved value.
  - Injected browser-only `data-bs-backdrop="static" data-bs-keyboard="false" data-modal-class="fw-test-modal"` on a trigger; verified attributes/classes copied to modal, backdrop click did not close, and Escape did not close.
  - Injected a datepicker inside modal content; verified content replacement removes `body > .datepicker` from 1 to 0.
  - Injected datepickers inside modal content twice; verified modal close removes `body > .datepicker` from 1 to 0 after the Bootstrap hidden/detached transition.
  - Final page cleanup check showed `fwModalCount=0`, `modalBackdropCount=0`, `bodyDatepickerCount=0`, `modalOpen=false`.
- Follow-up simplification checks:
  - `node --check osafw-app\wwwroot\assets\js\fw.js` - passed.
  - `git diff --check -- osafw-app\wwwroot\assets\js\fw.js` - passed.
  - CRLF/BOM check for `fw.js` - passed.
  - Browser MCP with cache cleared verified updated `fw.disposeComponents`, modal script load, datepicker cleanup on content replacement (`1 -> 0`), datepicker cleanup on modal close (`1 -> 0` after detach), and final DOM cleanup (`datepickerCount=0`, `modalCount=0`, `backdropCount=0`, `modalOpen=false`).

## Decisions - why
- Keep existing modal dialog/content class overrides and add only `data-modal-class` so Bootstrap-native options stay under `data-bs-*`.
- Add scoped disposal through `fw.disposeComponents(scope)` so modal cleanup is reusable outside the modal component.
- Clear browser cache before browser verification because the active browser had cached old `fw.js` under the same `SITE_VERSION`; deployment should continue to rely on normal `SITE_VERSION` cache busting.
- Keep the datepicker cleanup explicit because its plugin implementation stores a separate body-level picker and global handlers; use a cleanup table for standard jQuery plugin `destroy` calls.

## Pitfalls - fixes
- Waiting only for `.fw-modal.show` to disappear checks the beginning of Bootstrap close, not the `hidden.bs.modal` cleanup point. Browser checks now wait for `.fw-modal` to detach before asserting disposal on close.
- Normal build output was locked by IIS Express, so the isolated output build path was used for compile verification.
- Code reviewer sub-agent found missing recorded verification; after browser/build checks this summary was updated.
- The first disposal implementation was correct but verbose; it was simplified after user review while preserving the browser-tested behavior.

## Risks / follow-ups
- Normal `bin\Debug` build remains blocked while IIS Express process 63592 keeps `osafw-app.dll` locked.
- Active browsers can cache `fw.js` until `SITE_VERSION` changes; clear cache or bump the version for deployment verification.

## Heuristics (keep terse)
- No stable facts, reusable heuristics, or ADRs added; this task used existing component and asset-cache conventions.

## Testing instructions
- Use `<~/common/modal>` on a page with `.on-fw-modal` triggers.
- For static backdrop, set `data-bs-backdrop="static" data-bs-keyboard="false"` on the trigger.
- For modal root classes, set `data-modal-class="..."` on the trigger.
- For component cleanup verification, open modal content with datepicker/select2/bootstrap-select components, close or replace the modal content, and assert out-of-scope plugin DOM does not accumulate.

## Reflection
- The main slowdown was browser cache state: the active browser kept old `fw.js` until cache was cleared, making disposal appear absent even though the file was updated. Future frontend runtime checks should first verify the loaded asset URL and whether the expected new function exists.
- Sub-agent review was useful as a process gate; it caught the incomplete task summary while local browser verification was still in progress.
- The modal close cleanup assertion needs to wait for Bootstrap's hidden/detached transition, not just removal of the `.show` class.
