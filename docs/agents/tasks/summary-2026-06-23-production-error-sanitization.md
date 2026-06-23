## What changed
- Masked detailed framework/server errors from frontend responses when `IS_DEV` is false.
- Added a production exception-handler backstop in startup that returns the generic framework server-error message.
- Changed developer exception page gating from `app.Environment.IsDevelopment()` to the resolved framework `IS_DEV` startup setting.
- Added focused JSON error-response tests and a source guard for the developer exception page gate.
- Added changelog and agent-domain notes for the new error-detail policy.

## Scope reviewed
- `docs/README.md`
- `docs/agents/tasks/index.md` search for related error/production history
- `docs/agents/code_reviewer.md`
- `osafw-app/Program.cs`
- `osafw-app/App_Code/fw/FW.cs`
- Error templates under `osafw-app/App_Data/template/error`
- Existing FW/test helpers and report error-detail tests
- `docs/CHANGELOG.md`
- `docs/agents/domain.md`

## Commands used / verification
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 ...`
- `dotnet test osafw-tests\osafw-tests.csproj --no-restore --filter FullyQualifiedName~FwErrorHandlingTests -p:OutDir=C:\DOCS_PROJ\github\_core_master\artifacts\assistant_tests\` (failed once on invalid test assertion overload, then passed after fix)
- `dotnet test osafw-tests\osafw-tests.csproj --no-restore --filter "FullyQualifiedName~FwErrorHandlingTests|FullyQualifiedName~ErrorHandling_DeveloperExceptionPageRequiresFrameworkDevMode" -p:OutDir=C:\DOCS_PROJ\github\_core_master\artifacts\assistant_tests\` (passed: 4 tests)
- `dotnet build osafw-app\osafw-app.csproj --no-restore -p:OutDir=C:\DOCS_PROJ\github\_core_master\artifacts\assistant_build\` (passed: 0 warnings, 0 errors)
- `git -c core.quotepath=false diff --check` (passed)
- `powershell -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check ...` (passed for all edited files)
- Review loop: attempted code reviewer sub-agent, but it hit a GPT-5.3-Codex-Spark usage limit before returning a review. Per workflow fallback, performed the `docs/agents/code_reviewer.md` checklist locally; no findings.

## Decisions - why
- Preserve explicit user-facing exceptions (`UserException`, `AuthException`, `NotFoundException`) because controllers use those for validation, authorization, and missing-resource UX.
- Mask `ApplicationException` and general exceptions outside `IS_DEV` because these commonly carry SQL, configuration, file path, or infrastructure details.
- Gate ASP.NET developer exception middleware with the same `IS_DEV` setting so `ASPNETCORE_ENVIRONMENT=Development` alone cannot expose detailed pages in beta/production.
- Record this as a breaking/default-behavior change because apps relying on detailed browser/API errors outside `IS_DEV` must change configuration or logging workflows.

## Pitfalls - fixes
- JSON encoding escapes apostrophes in raw response bodies; tests assert decoded payloads plus sensitive identifiers instead of exact raw apostrophe-containing sentences.
- PowerShell blocked the repo normalization script under the default execution policy; reran it with `-ExecutionPolicy Bypass`.
- Code reviewer sub-agent was unavailable due to model usage limits; local reviewer checklist covered final diff, framework error callers, tests, docs, and line-ending status.

## Risks / follow-ups
- The startup developer-exception gate uses `FwConfig.settingsForEnvironment(builder.Configuration)`, which is environment-override based at app startup. Per-request host overrides still control framework-rendered error detail in `FW.errMsg`.
- Highest-risk follow-up not run: full `dotnet test`; focused tests and app build passed.

## Heuristics (keep terse)
- Stable fact added to `docs/agents/domain.md`; no reusable heuristic added.

## Testing instructions
- Focused regression: `dotnet test osafw-tests\osafw-tests.csproj --no-restore --filter "FullyQualifiedName~FwErrorHandlingTests|FullyQualifiedName~ErrorHandling_DeveloperExceptionPageRequiresFrameworkDevMode" -p:OutDir=C:\DOCS_PROJ\github\_core_master\artifacts\assistant_tests\`
- App compile check: `dotnet build osafw-app\osafw-app.csproj --no-restore -p:OutDir=C:\DOCS_PROJ\github\_core_master\artifacts\assistant_build\`

## Reflection
- Existing report-error tests gave a useful policy precedent and avoided over-designing a new setting.
- The biggest slowdown was confirming startup `IS_DEV` resolution versus per-request host overrides; future tasks touching startup/error behavior should check `FwConfig.settingsForEnvironment()` early.
- Local tool choice was effective: direct `rg`/targeted reads were enough, and no sub-agent was needed before the required review loop because the diff is small and focused.
