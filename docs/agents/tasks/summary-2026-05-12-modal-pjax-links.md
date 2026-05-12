## What changed
- Implemented `.on-fw-modal-link` handling in the shared modal component so links clicked inside a framework modal load into the same modal via the existing fetch/pjax modal content path.
- Documented the new class in the modal component usage block.

## Scope reviewed
- Reviewed `docs/agents/local_instructions.md`, `docs/README.md`, `docs/templates.md` component list, `osafw-app/App_Data/template/common/modal.html`, and `FW.getResponseExpectedFormat()` / parser layout handling.
- Noted existing uncommitted edits in `common/modal.html`, `libman.json`, and several untracked files before editing; this task only builds on the current `common/modal.html` state.

## Commands used / verification
- `node -e "... new Function(scripts) ..."` against `common/modal.html` script block: passed.
- Playwright MCP against `https://localhost:44315/Admin/DemosDynamic/new`: opened a real `.on-fw-modal`, injected an `.on-fw-modal-link`, clicked it, and verified the same modal fetched `https://localhost:44315/Admin/DemoDicts/new?modal_link_test=1&_layout=modal` with `X-Requested-With: XMLHttpRequest` while the page URL stayed `https://localhost:44315/Admin/DemosDynamic/new`.
- Verified edited files have CRLF line endings.
- Code reviewer sub-agent: no issues found; review loop can stop.

## Decisions - why
- Reused `loadModalContent()` so modal link navigation keeps the same `_layout=modal`, same-origin credentials, and `X-Requested-With` behavior as opening the modal.
- Limited interception to plain same-origin primary clicks inside `.fw-modal`, leaving modified clicks, downloads, `_blank`, hash-only, and external links to normal browser behavior.

## Pitfalls - fixes
- `apply_patch` introduced LF-only lines; normalized edited files back to CRLF with a focused PowerShell rewrite.
- A temporary static server probe was not needed after the user confirmed the VS-hosted app at `https://localhost:44315/`.

## Risks / follow-ups
- Dotnet build was not run because the change is template JavaScript/docs only and browser verification exercised the affected behavior directly.
- Low residual risk around the broader pre-existing modal rewrite in the worktree; the link-specific path was reviewed and smoke-tested.

## Heuristics (keep terse)
- None.

## Testing instructions
- Include `<~/common/modal>` on the source page, open a modal with `.on-fw-modal`, and add a same-origin link inside the loaded modal content with `class="on-fw-modal-link"`.
- Clicking that link should replace the current modal content with the linked action rendered with `_layout=modal`, without navigating the parent page.

## Reflection
- Stable facts were documented in `docs/templates.md`; no domain/glossary/ADR updates were needed.
