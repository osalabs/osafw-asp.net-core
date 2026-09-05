# PR #275 runtime model type lookup

## Objective / acceptance

- Supersede PR #275 against current `master` with a review-ready runtime `Type` overload for `FW.model`.
- Preserve existing generic and class-name model-cache behavior, initialize a runtime-resolved model once per `FW` instance, and return the shared cached instance.
- Add focused behavior coverage and public API documentation. Do not merge or close PR #275.

## What changed

- Added `FW.model(Type)` with concrete-`FwModel` validation, normal model initialization, and compatibility checks when reusing a cached class-name entry.
- Added focused tests showing that runtime and generic lookups share one initialized instance and that non-model types are rejected.
- Documented runtime-type lookup in the canonical CRUD guide.

## Scope reviewed

- PR #275 metadata, patch, discussion, and review state.
- Current `FW.model<T>()`, `FW.model(string)`, request model cache, `FwModel.init`, model call sites, and focused framework tests.
- Current framework compatibility, verification, task-record, and review-routing guidance.

## Requirements / decisions

- Kept `Type.Name` as the cache key because the existing generic and string overloads use the model class name; changing the key would create duplicate per-request model instances and break the established sharing contract.
- Required the cached entry to be assignable to the requested runtime type, matching the generic overload's collision guard instead of returning an unrelated `FwModel` with the same class name.
- Created a superseding `codex/` branch because the contributor's `patch-20` head is not an upstream branch and the contributor has read-only upstream access.

## Changed contracts

- Additive public C# API: callers may resolve a concrete `FwModel` subclass from a runtime `Type` through `FW.model(Type)`.
- No route, schema/provider, configuration, generated-output, security-default, or existing-overload behavior changes.
- No changelog entry is required because the change is additive and requires no downstream migration.

## Commands used / verification

- `dotnet restore osafw-tests\osafw-tests.csproj --verbosity minimal` - restored the app and test projects.
- `dotnet test osafw-tests\osafw-tests.csproj --no-restore --filter "FullyQualifiedName~osafw.Tests.FwTests" --logger "console;verbosity=normal"` - passed 11/11 focused tests, including both new runtime-type cases.
- `dotnet build osafw-asp.net-core.sln --no-restore --verbosity minimal` - succeeded with 0 warnings and 0 errors.
- `dotnet test osafw-tests\osafw-tests.csproj --no-build --no-restore --logger "console;verbosity=minimal"` - passed 717/717 default tests.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check osafw-app\App_Code\fw\FW.cs osafw-tests\App_Code\fw\FwTests.cs docs\crud.md docs\agents\tasks\index.md docs\agents\tasks\summary-2026-08-11-pr275-model-by-type.md` - all five touched text files passed strict UTF-8 without BOM and CRLF checks.
- `git diff --check` - passed.
- Removed only `osafw-tests/avatars/` and `osafw-tests/docs/`, the untracked artifacts created by the full test run.

## Final routed review

No blocking findings.

### Verification Reviewed

- Diff/files reviewed: `FW.cs`, `FwTests.cs`, the CRUD guide, and the task summary/index entry.
- Deterministic checks reviewed: focused tests, solution build, full default tests, text-format validation, and `git diff --check`.
- Specialist overlays consulted: consumer contract and state integrity.
- Review execution: local fallback, with a deliberate second pass after implementation and deterministic checks.
- Residual risk: the existing simple-class-name cache identity can reject two distinct model types with the same name; the new overload detects that collision rather than returning the wrong model.

Review loop can stop.

## Testing instructions

- Run the focused `FwTests` class, then the solution build and full default test project using the commands above.

## Risks / follow-ups

- The cache intentionally retains the existing class-name identity contract; two model types with the same simple class name still conflict and fail the type compatibility check.
- PR #275 remains open for its author or maintainers; the superseding PR will reference it without closing it.
