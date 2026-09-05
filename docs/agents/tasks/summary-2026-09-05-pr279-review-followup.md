# PR #279 runtime model lookup review follow-up

## Objective / acceptance

- Apply the reviewed simplification and coverage improvements to PR #279 while preserving the existing generic and string lookup contracts.
- Refresh the PR against current master and resolve its task-index conflict without dropping history entries.

## What changed

- Replaced the redundant post-construction type branch with a cast after the existing model-type validation.
- Added public-entry coverage for repeated runtime lookup, generic-first and runtime-first initialization, string/runtime sharing using Users, incompatible same-name cache entries, and null/abstract/non-model types.
- Clarified the closed, non-abstract type and public parameterless constructor requirements in XML and CRUD documentation.
- Integrated master at 06073c2d; retained both sides of the task-index conflict. The original task summary remains historical evidence.

## Changed contracts

- No further runtime contract changes beyond the additive FW.model(Type) API already in the PR.
- Existing generic/string overloads, simple-class-name cache identity, and initialization-before-insertion behavior remain intact.
- No schema/provider, route, configuration, or migration change. No changelog entry is required for this additive API and behavior-preserving follow-up.

## Commands used / verification

- `dotnet test osafw-tests/osafw-tests.csproj --filter FullyQualifiedName~osafw.Tests.FwTests --logger "console;verbosity=normal"` - 17 passed.
- `dotnet build osafw-asp.net-core.sln --no-restore --verbosity minimal` - 0 warnings, 0 errors.
- `dotnet test osafw-tests/osafw-tests.csproj --no-build --no-restore --logger "console;verbosity=minimal"` - 759 passed, 0 failed, 0 skipped.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Normalize-TextFiles.ps1 -Check osafw-app/App_Code/fw/FW.cs osafw-tests/App_Code/fw/FwTests.cs docs/crud.md docs/agents/tasks/index.md` - strict UTF-8 without BOM and CRLF passed.
- `git diff --check` - passed.
- Tests ran in an isolated task worktree. Removed only the test-created avatars and docs directories from that checkout; retained ignored build outputs there.

## Risks / follow-ups

- Optional provider-symbol variants were not run because this change does not touch provider-specific code.
- The established simple-name cache still rejects incompatible types sharing a name; the focused tests now verify the rejection leaves the original instance intact.
- PR #275 was closed as superseded by #279 during the preceding task.

## Final review

- Trigger: public/source-copy model lookup and cache compatibility; consumer-contract overlay.
- Execution: fresh independent reviewer_astra_high, selected for the ordinary public-contract review gate; initial runtime review followed by a supplemental active-summary audit.
- Result: no blocking findings and no supplemental summary findings. The reviewer independently checked the bounded diff and whitespace; build/test results are the implementing agent's executed evidence recorded above.
- Final strict UTF-8/CRLF validation also covered this summary and its index entry.

No blocking findings.
Review loop can stop.
