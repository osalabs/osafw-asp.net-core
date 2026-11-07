# Scheduled Tasks and report emails

## Scope and decisions

Implement the approved `docs/drafts/fwcron.md` specification on `codex/scheduled-tasks`, then open a PR to master. Preserve unrelated local files. One fwcron row per schedule, existing optional hosted service, Site Admin controls, shared database claim, plain recipient addresses, PDF/HTML delivery, captured parameter rows, NanoID task codes, and JSON latest results. No external scheduler, run-history table, recipient account requirement, or automatic uncertain-send retry.

The implementation uses bounded parallel ownership under the repository workflow: scheduler/schema/tests, report backend/tests, and admin/templates/editor/tests. The primary owns integration, documentation, verification, review, and PR. Manual adaptation of the current Dynamic demo is explicitly requested for the existing fwcron module; no development database scaffolding or mutation is needed.

FPF decision evidence remains in the approved local draft, pinned to a5ac87ac15e78517d12bc66155b9da3ca254b643. A.15.2 separates schedules from executions; SIE.7 preserves filter/time meaning; CMP.14 informs competing claims and uncertain external effects; SYSE.34 informs worker/schema rollout. Runtime contracts and the developer's decisions govern the implementation.

## Verification

- `pwsh -NoProfile -File scripts/test.ps1`: final 1,086/1,086 passed, zero skipped, after all review fixes. Includes scheduler/provider, actual route/config/ParsePage, report capture/replay, authorization, SMTP delivery and privacy-log controls.
- `pwsh -NoProfile -File scripts/test.ps1 -Profile SqlServer -SqlServer localhost -Filter 'FullyQualifiedName~FwCron'`: final 7/7 passed on a uniquely named disposable fixture, including case-sensitive/case-insensitive filtering and DST-storage controls. The default LocalDB attempt failed during instance initialization; the installed local SQL Server supplied the required provider checks without using application connection settings. SQLite provider checks also passed in Fast.
- `pwsh -NoProfile -File scripts/test.ps1 -Profile Browser -Filter 'FullyQualifiedName~CronEditorBrowserTests|FullyQualifiedName~ScheduledReport'`: final 3/3 passed. Exercises rendered shared-editor markup with the real framework JavaScript and mocked preview transport, plus installed Edge rendering of custom/Sample PDFs delivered to a loopback SMTP sink. Attachment presence/signature and cleanup are asserted; this does not establish real recipient inbox delivery.
- `dotnet build osafw-app/osafw-app.csproj '-p:DefineConstants=isFwCronService%3BisRoles' -p:OutDir=<task-owned-output> --verbosity quiet`: passed with zero warnings/errors. Earlier runner-only and ordinary builds passed as well.
- `node --check osafw-app/wwwroot/assets/js/fw-cron-editor.js`, config JSON parse, `git diff --check`, and strict UTF-8/no-BOM/CRLF checks: passed. URL template remains one line without a trailing newline.

Verification logs/results are ignored task artifacts. No configured application database, real recipient, live application worker, or user IDE process was used. Initial fixture compile/redirect/timezone/log-file/Edge-temp cleanup failures were corrected; final focused checks above pass.

## Contracts and remaining limits

Apply the provider's `upd2026-11-07-scheduled-tasks.sql` before using the new runtime and stop old workers during rollout. The runner remains opt-in through `isFwCronService`. SQL Server and SQLite are supported; other providers are explicitly unsupported for claims. Existing job handlers can return structured details through `runJobAction`; copied handlers require the adaptation described in `docs/cron.md` and the changelog. End bounds are exclusive. Ambiguous database-local next/start/end values cannot retain their UTC offset in the legacy columns: save/preview rejects them and the worker pauses affected schedules before dispatch. An eligible unambiguous current attempt can finish while its ambiguous following occurrence pauses future work. Ordinary non-UTC storage and recurrence-zone DST remain supported; no DB class, timezone setting, or timestamp schema conversion was introduced.

Actual route testing exposed a pre-existing dispatcher ordering defect: a wrapped RedirectException was rethrown before redirect handling. The narrow dispatcher fix lets inherited CRUD completion and interactive email return normal redirects. Scheduled report execution additionally rejects a live request context before adopting the rendering user's identity.

Sample preserves its declared sort. Custom report schedules use their SQL-defined order because interactive sort columns are discovered from query results rather than a declared scheduling contract. Monthly day 31 skips shorter months, fixed dates remain fixed, and relative periods remain the report developer's responsibility. Email submission cannot guarantee inbox delivery, and interrupted/uncertain outcomes require operator review.

The browser checks do not constitute a human walkthrough of a copied application's reports, real SMTP, IIS lifetime, branding, or deployment. Those need application-specific validation before activation.

## Independent review

The fresh reviewer used the state-integrity and security-boundary overlays. Initial findings led to exact SQLite report-prefix filtering with SQL Server collation controls, the explicit database-local fold boundary, SMTP-split-compatible address validation, filter-aware hardcoded report authorization, and mail-log privacy fixes. Bound preview/save also preserves an existing instant's fold and fractional precision. The reviewer rechecked the code, final test evidence, and amended active summary: no blocking findings; review loop can stop.