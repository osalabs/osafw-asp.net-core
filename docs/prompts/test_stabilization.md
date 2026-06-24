# Test Stabilization Prompt

Stabilize failing build or test command `<TEST_COMMAND>`.

Goal: reproduce the failure, identify whether it is a product bug, test bug, environment issue, or known unrelated failure, then fix only the confirmed cause.

## Work

1. Run `<TEST_COMMAND>` once and capture the first useful failure details.
2. If output is too large, save bulky logs under `docs/agents/artifacts/` or repo-root `artifacts/` and summarize only relevant lines.
3. Classify failures by shared root cause before editing.
4. Fix product bugs in production code and brittle/incorrect expectations in tests. Do not weaken tests to hide real regressions.
5. Avoid broad cleanup unless it is required to remove the failure cause.
6. Re-run the narrow failing test or build target after each fix batch.
7. Escalate to broader tests only after the focused failure is clean or when the fix touches shared behavior.

## Verification

- Finish with the originally failing command when practical.
- If the full command is blocked by environment or known unrelated failures, run the closest focused command and document the remaining blocker with evidence.

Placeholder:
- `<TEST_COMMAND>`: build or test command to stabilize, for example `dotnet test` or `dotnet build osafw-asp.net-core.sln`