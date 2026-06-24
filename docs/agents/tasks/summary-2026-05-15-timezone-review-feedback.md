## What changed

- Addressed reviewer feedback for timezone handling:
  - `_utc` field detection now strips expanded list-parameter numeric suffixes such as `@sent_at_utc_0`.
  - ISO offset strings in `FwModel.convertUserInput` now parse with `CultureInfo.InvariantCulture` and round-trip styles.
  - Dev Configure now uses an explicit DB timezone detection status instead of treating fallback `UTC` as success.
  - `docs/templates.md` now documents `date="datetime-local"` as `yyyy-MM-ddTHH:mm`.

## Scope reviewed

- Reviewed DB `_utc` field detection, FwModel datetime string parsing, DevConfigure DB timezone status, and `docs/templates.md` datetime-local docs.

## Commands used / verification

- `Get-Content docs\agents\local_instructions.md`
- `Get-Content docs\README.md`
- Targeted file reads for `DB.cs`, `FwModel.cs`, `DevConfigure.cs`, and `docs/templates.md`.
- `dotnet build osafw-app\osafw-app.csproj` failed because IIS Express process 42996 locked normal `bin\Debug\net10.0\osafw-app.dll`.
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build_tz_feedback\` passed.
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~DBTests|FullyQualifiedName~FwModelDateTimeInputTests" --artifacts-path artifacts\assistant_test_tz_feedback_targeted2` passed, 57 tests.
- `dotnet test --artifacts-path artifacts\assistant_test_tz_feedback_full` ran; 4 known unrelated failures remain.
- Review loop via `docs/agents/code_reviewer.md` completed with no issues.

## Decisions - why

- Treat all four reviewer comments as valid; each pointed at current behavior.
- Do not cache fallback-to-UTC timezone detection failures, so transient/autodetect issues remain visible and can be retried.

## Pitfalls - fixes

- Normal app build output was locked by the running VS/IIS Express app; used isolated build output for compile verification.
- Fallback `UTC` is still displayed in Dev Configure, but `is_db_tz` is false unless timezone was configured or successfully detected.
- Reviewer found no remaining issues.

## Risks / follow-ups

- Full `dotnet test` still fails in pre-existing unrelated tests: autocomplete leading-id parsing, Dynamic prev/next null route, and culture-sensitive ParsePage time formatting.

## Heuristics (keep terse)

- 2026-05-15: For raw DB list parameters, generated names like `@field_utc_0` must preserve semantic suffixes before normalization.

## Testing instructions

- Build with isolated output if IIS Express is running: `dotnet build osafw-app\osafw-app.csproj -p:OutDir=artifacts\assistant_build_tz_feedback\`.
- Targeted tests: `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~DBTests|FullyQualifiedName~FwModelDateTimeInputTests"`.

## Reflection

- Stable behavior only; no ADR needed.
- Review loop can stop.
