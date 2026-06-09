## What changed
- Added modal-aware Escape handling for Bootstrap Markdown fullscreen mode in `common/markdown_editor.html`.
- The handler binds to the generated `.md-editor` wrapper after plugin initialization, exits markdown fullscreen on Escape, and stops the key from closing the surrounding Bootstrap modal.
- Left non-modal markdown editors unchanged and did not edit vendor `bootstrap-markdown.js`.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/agents/tasks/index.md`
- `docs/agents/code_reviewer.md`
- `osafw-app/App_Data/template/common/markdown_editor.html`
- `osafw-app/wwwroot/assets/lib/bootstrap-markdown/js/bootstrap-markdown.js`
- `osafw-app/wwwroot/assets/css/theme30.css`

## Commands used / verification
- `node -e "...new Function(scriptBlock)..."` against `common/markdown_editor.html` - passed, 1 script block parsed.
- `git diff --check -- osafw-app\App_Data\template\common\markdown_editor.html docs\agents\tasks\summary-2026-06-09-markdown-modal-escape.md docs\agents\tasks\index.md` - passed.
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check ...` for touched files - passed after CRLF normalization.
- Browser smoke at `https://localhost:44315/Login`: logged in with local test credentials, opened `/Admin/DemosDynamic/new`, confirmed the served `markdown_editor` inline component included `bindModalFullscreenEscape` and `keydown.fwMarkdownFullscreen`.
- Browser smoke opened `/Admin/Spages/new` and `/Admin/DemosDynamic/new` through temporary `.on-fw-modal` triggers; both modal responses loaded real Bootstrap modals with markdown textareas but did not include the markdown editor initializer in modal layout.
- Browser smoke created a disposable Bootstrap modal with a modal-local `textarea.markdown`, replayed the served markdown component initializer, and verified:
  - Escape from textarea focus exited `.md-fullscreen-mode`, removed `body.md-nooverflow`, and kept the modal open.
  - Escape from a focused toolbar button exited `.md-fullscreen-mode`, removed `body.md-nooverflow`, and kept the modal open.
  - A later Escape outside fullscreen closed the modal normally; after Bootstrap transition, `modal-backdrop` count was 0 and `body.modal-open` was false.
- Self-reviewed final diff using `docs/agents/code_reviewer.md`; no issues found, review loop can stop.

## Decisions - why
- Rejected textarea-only handling because Escape can originate from editor toolbar/preview controls once fullscreen is active.
- Kept the fix in the framework template instead of the vendor plugin so library code remains unchanged.
- No `docs/CHANGELOG.md` entry needed; this is a bug fix for existing modal/editor behavior, not a breaking app-facing contract change.

## Pitfalls - fixes
- Existing unrelated modified/untracked files were present before this task; this task only touches the markdown component and task docs.
- In-app Browser setup failed with a sandbox startup error after the required retry; used the available Playwright browser tool as fallback.
- Real modal responses for `/Admin/Spages/new` and `/Admin/DemosDynamic/new` omit the markdown editor initializer, so browser verification used a disposable modal-local editor initialized by the served component script.

## Risks / follow-ups
- The fix is verified at the component behavior level. Separately, modal layout currently does not include page-level markdown editor initialization for tested admin form routes.

## Heuristics (keep terse)
- None added.

## Testing instructions
- Run the three static checks listed above.
- In a page with the served `common/markdown_editor` component and Bootstrap loaded, initialize a `textarea.markdown` inside a `.modal`; Escape in fullscreen should exit editor fullscreen first, and a later Escape should close the modal normally.

## Reflection
- The main slowdown was assuming target admin form modal responses would include their page-level editor initializer. Future checks of shared component behavior can first confirm whether modal layout includes the relevant component script before waiting on plugin wrappers.
- Browser tooling fallback was useful: in-app Browser failed during setup, while Playwright completed the behavior check with a disposable modal and avoided database writes.
- No stable facts, heuristics, or ADRs were added; the modal-layout initializer gap is noted as a follow-up rather than a new heuristic.
