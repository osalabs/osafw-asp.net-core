# Optional MCP and Developer Tooling

Use this repository-specific policy with the capabilities exposed by the active runtime. Tool names, schemas, installation paths, and supported operations are product-owned and can change; discover them at runtime instead of copying them into repository instructions.

## Selection and Fallback

- Use an available solution/IDE capability when solution-aware navigation, build state, debugging, or an already-running local app materially improves the task.
- Use an available browser automation capability for browser reproduction and UI verification. Capture fresh page state after navigation or meaningful DOM changes, and use console/network evaluation only when needed.
- Validate a relevant optional capability with one small read-only call and at most one lightweight retry. Treat a warning as non-blocking when the returned evidence is still valid.
- If the capability remains unavailable, use a safe repository-local fallback such as `dotnet`, the bundled test project, or Playwright/browser automation available through another interface.
- Do not silently switch when the user explicitly required a named tool, or when the task depends on an existing signed-in browser, IDE, debugger, or app state. Report the limitation and ask whether to restore that capability or use the fallback.

## Safety and Local State

- Keep machine-specific URLs, credentials, ports, and browser notes in ignored `docs/agents/local_instructions.md`.
- Do not add repository MCP/client configuration, credentials, or product-version pins unless the user explicitly requests repository configuration.
- Use least-privilege/read-only operations for discovery. External writes, messages, deployments, and destructive operations still require the authority implied by the user request and active runtime policy.
- Check independent capabilities independently; one tool's health does not establish another's health.

## Current Official References

- [Model Context Protocol documentation](https://modelcontextprotocol.io/docs/getting-started/intro)
- [Visual Studio MCP documentation](https://learn.microsoft.com/en-us/visualstudio/ide/mcp-servers?view=visualstudio)
- [Microsoft Playwright MCP repository](https://github.com/microsoft/playwright-mcp)
