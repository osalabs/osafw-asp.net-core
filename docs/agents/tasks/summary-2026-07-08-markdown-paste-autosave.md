## What changed
- Fixed rich HTML paste handling in `common/markdown_editor.html`.
- Root cause: the markdown template intercepts clipboard HTML, prevents the browser default paste, converts HTML to markdown, and inserts it programmatically. Keyboard paste was often rescued by the editor `keyup` path, but browser context-menu Paste does not produce a `keyup`, so no markdown `onChange`, no bubbled `input`, no autosave dirty flag, and no unsaved-navigation warning.
- After the custom insertion, the paste handler now calls the initialized Bootstrap Markdown `change()` method so the existing `onChange` bridge emits the same `input` event used by autosave/plain textarea behavior.

## Scope reviewed
- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/agents/tasks/index.md`
- `docs/agents/tasks/summary-2026-06-09-markdown-modal-escape.md`
- `docs/templates.md`
- `docs/agents/code_reviewer.md`
- `osafw-app/App_Data/template/admin/demosdynamic/config.json`
- `osafw-app/App_Data/template/admin/demosdynamic/showform/main.html`
- `osafw-app/App_Data/template/admin/demosdynamic/showform/load_script.html`
- `osafw-app/App_Data/template/common/markdown_editor.html`
- `osafw-app/wwwroot/assets/js/fw.js`
- `osafw-app/wwwroot/assets/lib/bootstrap-markdown/js/bootstrap-markdown.js`

## Commands used / verification
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Search-Repo.ps1 -Pattern "autosave|unsaved|dirty|form change|change\(" -Path osafw-app\wwwroot osafw-app\App_Data\template osafw-app\App_Code`
- `rg -n "idesc|description|markdown|textarea|data-noautosave|attrs_control" osafw-app\App_Data\template\admin\demosdynamic\config.json osafw-app\App_Data\template\admin\demosdynamic -S`
- `node -e "...new Function(scriptBlock)..."` against `common/markdown_editor.html` - passed, 1 script block parsed.
- Playwright MCP smoke at `https://localhost:44315/Admin/DemosDynamic/1128/edit`: logged in with local credentials, confirmed `textarea.markdown` was initialized, dispatched a bubbling paste event with HTML clipboard data, verified the handler prevented default paste, inserted converted markdown, emitted one `input`, and set the form dirty flag to `true`; reloaded the page afterward and confirmed the field returned to original length with dirty flag `false`.
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check osafw-app\App_Data\template\common\markdown_editor.html docs\agents\tasks\summary-2026-07-08-markdown-paste-autosave.md` - passed after normalization.
- `git -c core.quotepath=false diff --check -- osafw-app\App_Data\template\common\markdown_editor.html docs\agents\tasks\summary-2026-07-08-markdown-paste-autosave.md` - passed.
- Self-reviewed final diff using `docs/agents/code_reviewer.md`; no issues found, review loop can stop.

## Decisions - why
- Kept the fix in the framework markdown include instead of editing vendor `bootstrap-markdown.js`.
- Called the plugin's existing `change()` path instead of directly duplicating autosave logic, so paste uses the same content-change bridge as toolbar/key events.
- No `docs/templates.md` update needed; it already states markdown edits follow plain-textarea autosave timing, and this restores that documented behavior.
- No `docs/CHANGELOG.md` entry needed; this is a bug fix for an existing editor/autosave contract, not a breaking upgrade change.

## Pitfalls - fixes
- The first Node syntax-check command failed because PowerShell stripped JavaScript quoting; reran with PowerShell-safe quoting.
- The first synthetic browser paste using `ClipboardEvent` did not preserve the test marker string as expected, so a second controlled paste event supplied a stubbed `clipboardData` object. Turndown escaped underscores in the marker, but the converted bold markdown, `input` count, and dirty flag validated the flow.
- Browser smoke changed only the in-memory page value; the dirty flag was cleared and the page reloaded before the 30-second autosave timer could save test content.

## Risks / follow-ups
- Residual risk is limited to other programmatic markdown edits on non-autosave forms; this task targeted the edit page autosave/navigation-warning regression and verified the shared rich-paste path.

## Heuristics (keep terse)
- None added.

## Testing instructions
- Run the Node script-block parse and diff/CRLF checks listed above.
- Browser smoke: on `/Admin/DemosDynamic/1128/edit`, paste HTML into `textarea.markdown` through the markdown paste handler; the converted markdown should be inserted, an `input` event should fire, and the form dirty flag should become true so autosave/navigation warnings can run.

## Reflection
- What slowed this task: browser-level paste simulation needed a controlled clipboard payload because `ClipboardEvent` did not expose the synthetic marker consistently.
- Future agents should check whether a custom paste handler calls `preventDefault()` and writes `.value` programmatically before chasing global autosave code.
- Playwright MCP was effective for confirming the real local page loaded the updated template and that the form dirty flag flipped. No sub-agent was needed for the small diff.
- No stable facts, heuristics, ADRs, or agent instruction changes were added; the behavior is a narrow bug fix in an existing component contract.
