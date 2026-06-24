# MCP Tooling Notes

Use this file with the shorter MCP guidance in `AGENTS.md`.

## Visual Studio MCP

- Prefer Visual Studio MCP for solution-aware work.
- Validate it independently with `solution_info` or `project_list` before relying on it.
- Use `document_*`, `build_*`, `build_status`, `errors_list`, and debugger tools when they fit the task.

## Playwright MCP

- Prefer Playwright MCP for browser repros and UI verification.
- Re-run `browser_snapshot` after navigation or meaningful DOM changes.
- Use `browser_evaluate`, console, or network tools when snapshots omit needed details.
- If Playwright reports `EPERM` around `C:\Windows\System32\.playwright-mcp` but still returns a valid snapshot, URL, or title, treat it as usable and verify the real page state before abandoning it.

## Recovery Rules

- Check each MCP independently. Do not infer Visual Studio MCP health from generic MCP discovery or from Playwright health, and vice versa.
- If a required MCP is missing, cannot connect, or returns a blocking runtime error, do one quick validation and at most one lightweight retry.
- If still blocked, ask the user whether to use a non-MCP workaround or restart/fix that MCP first.
- Do not spend multiple turns looping on MCP recovery unless the user explicitly asks for fallback attempts.
- If the user explicitly asked to use MCP, prefer waiting for a working MCP path over silently switching to CLI.
- Keep machine-specific local app URLs, credentials, and browser notes in `docs/agents/local_instructions.md`.
