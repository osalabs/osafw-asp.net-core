# Opt-in Model Row Regeneration

## Objective / acceptance

Provide a rarely used local development action that regenerates all existing model Row classes from current schema metadata. Keep Roslyn out of normal builds. Use Git diff/revert for review instead of selection, preview, or apply pages. Entire Row contents, including custom members, may be replaced; retain surrounding model code.

## What changed

- Added the disabled-by-default `isRowRegeneration` compile constant and a conditional `Microsoft.CodeAnalysis.CSharp` package reference.
- Added one POST action at `/Dev/Manage/(RegenerateModelRows)`, requiring `IS_DEV`, Site Admin access, and the current XSS token. Removed the earlier preview/apply actions and templates from this unmerged feature.
- Kept a small syntax-based helper to replace each direct nested Row, using existing schema-to-Row generation. It processes matching compiled models under `App_Code/models`, reports updated/unchanged/skipped/failed models, and continues after individual failures.
- Metadata reads bypass the shared full-schema cache without changing ordinary warm-cache behavior. Each model uses its configured DB wrapper. No database writes are performed.
- Files are held exclusively while read and written. Ambiguous, partial/generic, conditional, invalid, or missing Rows are skipped. Linked source paths are excluded. The current file is restored if writing throws, when restoration succeeds; earlier successful files remain changed.
- Updated canonical usage and copied-application setup guidance. Synced the branch with master after the schema metadata prerequisite merged.

## Changed contracts / decisions

Normal builds contain neither the action nor a Roslyn assembly reference/package dependency. Enabled builds deliberately replace custom Row members, attributes, and inheritance; developers review or revert affected model files in Git and rebuild. Line endings are normalized to CRLF. The existing public cached schema API and ordinary model scaffolding remain available without the constant.

The source helper remains separate from the controller to keep syntax parsing and file writing out of request orchestration. There is no preview store, hash protocol, selection UI, or batch rollback mechanism.

## Commands used / verification

All checks ran in the feature worktree using disposable source fixtures and controlled metadata; no existing model source or live database was regenerated.

- `dotnet test osafw-tests/osafw-tests.csproj --filter 'FullyQualifiedName~DevRowRegeneratorTests|FullyQualifiedName~DBOperationTests|FullyQualifiedName~DevCodeGenTests' --verbosity quiet`: 39 passed. The default action/assembly-reference regression passed, and the restored application package graph contained no `Microsoft.CodeAnalysis` packages.
- `dotnet test osafw-tests/osafw-tests.csproj '-p:DefineConstants=TRACE%3BDEBUG%3BisRowRegeneration' --filter 'FullyQualifiedName~DevRowRegeneratorTests|FullyQualifiedName~DBOperationTests|FullyQualifiedName~DevCodeGenTests' --verbosity quiet`: 47 passed. Includes explicit-action URL parsing, all-model regeneration, custom Row replacement, preserved outer code, repeat stability, fresh metadata with unchanged public cache, denied environment/verb/token/access controls, invalid/ambiguous source, and locked-file continuation.
- `dotnet test osafw-tests/osafw-tests.csproj '-p:DefineConstants=TRACE%3BDEBUG%3BisRowRegeneration%3BisSQLite' --filter 'FullyQualifiedName~DevRowRegeneratorTests|FullyQualifiedName~DBOperationTests|FullyQualifiedName~DevCodeGenTests' --verbosity quiet`: 47 passed with optional SQLite compilation, before the route-only fixture assertion was added.
- `git diff --check` and strict UTF-8/no-BOM/CRLF checks passed for all changed text files.

## Testing instructions / limits

Enable the commented constant block in the app project, rebuild, sign in as Site Admin in local development, and POST the current XSS form value to `/Dev/Manage/(RegenerateModelRows)`. Inspect returned results and Git diffs, revert unwanted files, then rebuild. Disable the constant afterward. This live manual flow was not run against a configured database; tests use the real action with controlled metadata and disposable files.

A process or filesystem failure during an in-place write can leave partial source. Git remains the recovery boundary. Windows is the verified platform; no live SQL Server, MySQL, OLE, or Linux integration run was performed.

## Review

Fresh independent review used `reviewer_high` with consumer-contract and security-boundary overlays for the generated-output, compile-gate, and source-writing contracts. The reviewer inspected the final diff before the active summary; the subsequent summary/index audit found no supplemental issues. Final integrator verdict: No blocking findings. Review loop can stop.
