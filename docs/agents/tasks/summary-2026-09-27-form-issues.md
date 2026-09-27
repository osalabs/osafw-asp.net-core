# Unified form errors and warnings

## Outcome and contracts

- Applied the reviewed Vue fixes first and synced them into the separate validation branch. The Vue stage passed 89 controller/browser cases and independent consumer/state review.
- Replaced the two mutable feedback stores with request-owned `FW.FormIssues`, with error and warning severities only. `FwController` owns the three small addition helpers; `FW.getFormErrors()` derives the supported field/code error map.
- Kept generic JSON `error.details` and the `data-errors` attribute. Classic forms accept either the existing map or structured arrays. Coded errors retain matching field-template messages; message-only feedback is escaped. Warnings do not mark fields invalid or block saves.
- Shared standard Dynamic/Vue save preparation, retained their authorization/persistence flows, and moved translated built-in messages into the common form templates. Vue continues to preserve queued drafts, originating tabs, child IDs, and unresolved failures.
- Documented the intentional C# source migration in the canonical form documentation and breaking-change log. No info severity, schema change, Dynamic summary, or additional save coordinator was introduced.

## Scope and verification

- Backend implementation used a bounded worker with exclusive C#/backend-test/resource ownership; the primary owns frontend integration, browser checks, documentation, and final verification.
- Verification uses isolated build output and offline Playwright contexts. The local server, configured development database, and user browser state are untouched.
- `dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir=<checkout>/artifacts/assistant_form_issues/build/default/` passed all 943 default-suite tests with no skipped tests or compile warnings. Results are retained in the ignored task artifact directory.
- The earlier focused run passed 164 of 169 cases; its five failures were assertions for the former required-field string value. After updating them to the documented boolean projection, the full suite passed. Browser coverage includes both `data-errors` shapes, field-template precedence, escaped messages, warnings, generic server details, and per-tab failure retention.
- `pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1` and `git diff --check` passed. Changed text uses strict UTF-8 without BOM and CRLF.
- SQL Server/live-application acceptance and optional-provider builds were not run; this change adds no provider-specific logic or schema changes.
- Unrelated untracked work is preserved.
- Fresh independent consumer-contract/state-integrity review and the supplemental summary audit found no blocking findings. The documented copied-application migration remains necessary; the review loop is complete.
