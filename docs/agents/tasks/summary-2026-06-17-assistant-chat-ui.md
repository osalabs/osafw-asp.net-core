## What changed
- Fixed `/Assistant` chat UI balance, duplicate user messages, and worker-disabled queued-run behavior.
- Added `AssistantRuntimeStatus.worker_enabled`; the controller and client now treat the assistant as unavailable for new submissions when the worker is disabled.
- Updated the landing composer copy, sample prompt chips, standard Bootstrap Send buttons, compact pending-file badges, and message-list alignment.
- Clarified assistant docs for worker-required chat submissions.

## Scope reviewed
- `docs/README.md`, `docs/assistant.md`, `docs/templates.md`, `docs/design_system.html`, `docs/agents/local_instructions.md`, and Assistant controller/service/template/CSS files.
- Searched task history index; relevant Assistant history is the 2026-06-12 port/premerge work.
- Noted unrelated `.jshintrc`; left it untouched.

## Commands used / verification
- `git -c core.quotepath=false status --short`
- Visual Studio MCP `build_project` for `osafw-app/osafw-app.csproj`: succeeded, `FailedProjects=0`.
- `dotnet test --filter Assistant`: passed 23/23.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Normalize-TextFiles.ps1 -Check ...`: final changed files ok, CRLF/UTF-8 no BOM.
- Playwright login via local test credentials, then `https://localhost:44315/Assistant`:
  - Worker-disabled committed config showed `Assistant worker is not enabled...` warning and disabled the composer instead of queuing.
  - Temporary worker-enabled IIS Express launch verified active chips, standard Send button, one user message after submit, and status progression from `queued` to `processing` to visible `failed`.
  - Pending-file badge rendered with `policy-notes.txt` without submitting.
  - Desktop and narrow viewport screenshots saved under `docs/agents/artifacts/assistant-chat-ui/`.
- Code reviewer sub-agent reviewed the final diff and reported no issues; review loop stopped.

## Decisions - why
- Keep committed `ASSISTANT_WORKER_ENABLED` opt-in, but make disabled-worker setups unavailable for new submissions so runs do not silently remain queued.
- Use `AI Assistant`, treating `AI Assitatnt` as a typo.
- Add focused status tests in a new file to avoid merge risk with parallel test edits.
- No `docs/CHANGELOG.md` entry: this is a bug fix/clarification for the already documented assistant worker opt-in, not a new breaking upgrade requirement.

## Pitfalls - fixes
- Existing missing-OpenAI status behavior takes precedence in tests, so worker readiness is checked after enabled/OpenAI/tables status gates.
- VS no-debug run could not be stopped through debugger APIs; stopped/relaunched IIS Express directly only for the temporary worker-enabled UI test, then relaunched through VS with committed config.
- Playwright screenshots initially landed in the repo root; moved them to ignored agent artifacts.

## Risks / follow-ups
- Existing queued runs may still need a worker-enabled host to drain.
- The end-to-end chat run reached the worker and failed with the generic visible assistant failure message; root cause is outside this UI/queue fix and should be checked in logs/provider configuration if a real answer is required.

## Heuristics (keep terse)
- No stable facts, heuristics, or ADRs added.

## Testing instructions
- For normal local chat processing, keep `appSettings.ASSISTANT_WORKER_ENABLED=true` on exactly one local/hosted process intended to drain assistant queues.
- With the default committed worker-disabled config, `/Assistant` should show the setup warning and keep the composer disabled.

## Reflection
- Visual Studio MCP handled build/relaunch, but no-debug IIS Express stop still required a process-level fallback; future VS MCP work should validate stop/restart capability before assuming it can recycle Ctrl+F5 runs.
- The reviewer sub-agent was useful and quick for final diff validation. The main agent still needed to own test execution and browser verification.
- Temporarily editing launch settings for local-only environment flags is workable, but it should be reverted immediately and excluded from final line-ending/diff churn.
