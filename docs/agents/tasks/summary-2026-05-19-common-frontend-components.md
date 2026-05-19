## What changed
- Reworked `common/bootstrap_select.html` into the `fw.initComponent` pattern using the project reference version as the base.
- Updated `common/modal.html` lookup saves to select saved values, refresh enhanced selects, and emit `fw-lookup-saved` before compatibility `change`.
- Added modal-local `.on-submit` handling so modal footer save buttons use the AJAX lookup/HTML submit path instead of the global full-page submit helper.
- Added modal ID namespacing support with lookup modals defaulting on and generic modals opt-in.
- Refactored `common/modal.html` after feedback to inline one-off helpers and remove modal submit normalization that the capture-phase handler made redundant.
- Changed markdown editor autosave signaling to use textarea `input`/`blur` paths instead of immediate form `change`.
- Converted `common/autocomplete.html` to `fw.initComponent` asset loading after browser smoke exposed duplicate script loads on dynamic forms.
- Added `component usage` comments to top-level common frontend includes and updated related docs.

## Scope reviewed
- Reviewed `docs/agents/local_instructions.md`, `docs/README.md`, `docs/design_system.html`, `docs/templates.md`, `docs/dynamic.md`, `docs/agents/code_reviewer.md`.
- Compared existing `common/bootstrap_select.html` with untracked reference `common/bootstrap_select_new.html`.
- Inspected `common/modal.html`, `common/markdown_editor.html`, `fw.js` autosave/component helpers, and dynamic lookup modal docs.

## Commands used / verification
- `git diff --check` - passed.
- Inline script syntax extraction with `new Function(...)` for changed common frontend includes - passed.
- Follow-up modal inline script syntax check after simplification - passed.
- CRLF byte check for all edited/created files - passed.
- `dotnet build osafw-app\osafw-app.csproj` - normal output build was blocked by a locked IIS Express debug DLL.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=..\artifacts\assistant_build\` - passed with 0 warnings and 0 errors.
- Browser smoke on `https://localhost:44315/Admin/DemosDynamic/new`:
  - autocomplete asset deduped to one loaded script;
  - lookup modal loaded with ID namespacing enabled, `data-fw-original-id` set, and labels rewritten;
  - lookup add saved a new value, selected it on the parent select, and fired `fw-lookup-saved` then `change` once each with expected detail/value;
  - component usage comments did not leak rendered ParsePage include text.
- Code-review sub-agent reviewed the final diff; no tracked runtime/template correctness issues were found.

## Decisions - why
- Used a new `fw-lookup-saved` event instead of adding detail to `change` so existing change handlers stay compatible and custom handlers get a clear contract.
- Made modal ID namespacing default-on only for lookup modals because generic modal content still has legacy global `#id` selectors.
- Kept `bootstrap_select_new.html` untouched as a reference/source artifact.

## Pitfalls - fixes
- Bootstrap-select still needs early `.form-select` cleanup before component init; the new include removes it before loading plugin assets and again during scoped init.
- Markdown editor plugin changes previously triggered form `change`, which bypassed the 30-second idle autosave path; it now triggers textarea `input`.
- Dynamic forms can include autocomplete many times; using `fw.initComponent` keeps the asset load idempotent.
- Modal save buttons use `.on-submit data-target="modal"`; handling them in capture phase prevents the older global helper from bypassing the modal AJAX submit flow.
- The first modal implementation had too many shallow one-use helpers; follow-up refactor inlined namespace policy, lookup event dispatch, enhanced select refresh, and button state handling.
- Lookup custom events fire before compatibility `change` so custom handlers still run when legacy `change` handlers submit or refresh the parent form.

## Risks / follow-ups
- Modal scripts inside namespaced lookup content must use scoped selectors; legacy global `#id` scripts can opt out with `data-fw-modal-namespace-ids="0"`.
- Markdown autosave behavior was verified by code-path/static checks against the textarea `input`/`blur` handlers; no dedicated browser timer test was run.
- Browser smoke created local DemoDict lookup test rows in the developer database.
- Untracked reference/workspace files existed during the task (`common/bootstrap_select_new.html`, `.jshintrc`, `.playwright-mcp/`, IDE files). They were left untouched because they appear user-provided or unrelated.

## Heuristics (keep terse)
- Added a heuristic for scoped selectors in modal content when ID namespacing is enabled.

## Testing instructions
- Static verification passed: `git diff --check`, inline script syntax extraction, CRLF check.
- Build verification passed using isolated output: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=..\artifacts\assistant_build\`.
- Manual/browser smoke passed for dynamic lookup add, modal ID namespacing, event dispatch, and autocomplete asset dedupe on the local app.

## Reflection
- Stable public behavior was documented in `docs/templates.md` and `docs/dynamic.md`; no ADR or domain/glossary update appears necessary.
