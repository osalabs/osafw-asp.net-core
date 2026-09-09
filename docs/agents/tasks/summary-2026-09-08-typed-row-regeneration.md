# Typed Row Regeneration

## Objective / acceptance

Add a usable Developer Tools flow that previews and explicitly applies current schema-driven typed `Row` output to selected existing model source files. Preserve all source outside a safely recognizable generated Row, reject ambiguous or customized Rows and stale previews, and keep browser writes behind development, Site Admin, POST, and XSS boundaries. Bulk regeneration of framework model sources is outside this task.

Acceptance requires offline tests through the DevManage preview/apply actions with a task-owned source directory and controlled metadata DB. Negative controls cover non-development use, GET, a missing XSS token, a stale preview, a customized Row, and an unknown selection. The preserved-positive control verifies custom model inheritance, methods, and string content outside Row remain intact.

## What changed

- Added a Roslyn-based regeneration service that inventories matching compiled model sources without following reparse points, parses each selected file, and replaces only one direct nested generated-style Row syntax span.
- Added review hashes over the full source and regenerated output. Preview and apply read provider metadata through an uncached path that cannot consume or publish a competing full-schema cache result, so an intervening schema change invalidates the reviewed output even when an older cached read completes concurrently.
- Apply replans the whole selection, rejects any stale or missing hash, serializes in-process, and acquires every selected source with exclusive sharing before validating or writing. It writes and flushes in place, and restores already-written files through their held handles if a later write fails.
- Added a Developer Tools page with explicit file selection, escaped old/new Row diffs, and a separate apply form.
- Documented the copied-application package requirement and the boundary that plain auto-properties still require human preview review.

## Scope reviewed

Reviewed DevManage routing and write guards, current model/schema discovery, typed Row generation, model DB selection, source-root configuration, ParsePage form/repeat conventions, and existing generator/security tests. The review report's typed-row regeneration item was used only to identify the requested outcome and was validated against current code.

## Requirements / decisions

- `Microsoft.CodeAnalysis.CSharp` 5.9.0 is the approved source parser dependency for the current .NET target.
- Selection uses exact compiled model names resolved to one matching source file. The browser never submits an arbitrary path.
- The tool reads schema metadata from each model's configured DB wrapper, matching normal model runtime selection, and does not mutate database state.
- Row eligibility is deliberately narrow. It accepts only the scalar type and empty-string initializer forms emitted by current or legacy framework generation. Primary constructors, custom types or initializers, and other custom Row behavior are refused with a manual-review explanation.

## Changed contracts

The change adds two Site Admin Developer Tools actions and one page when `IS_DEV=true`. It adds a direct application project dependency on `Microsoft.CodeAnalysis.CSharp` 5.9.0. Existing model source is unchanged unless a developer selects it, reviews a preview, and explicitly applies it. Database and provider schemas are unchanged.

## Commands used / verification

- `dotnet build osafw-app\osafw-app.csproj --artifacts-path artifacts\r45_build_cache_race --verbosity minimal` — passed with no warnings after restoring into the isolated output path.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~DevRowRegeneratorTests --artifacts-path artifacts\r45_tests_cache_race --verbosity minimal` — passed 10/10 after review fixes. Controls include preserved public warm-cache behavior, an older in-flight cached metadata read publishing before a fresh regeneration read, schema changes before preview and apply, unsupported Row primary-constructor/type/initializer forms, and failure to acquire exclusive source ownership.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~Dev --artifacts-path artifacts\r45_dev_cache_race --verbosity minimal` — passed 74/74 generator, regeneration, and Developer Tools security tests.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~DBOperationTests --artifacts-path artifacts\r45_db_cache_core --verbosity minimal` — passed 20/20 provider and schema-cache operation tests.
- `dotnet test osafw-tests\osafw-tests.csproj --filter FullyQualifiedName~ParsePageTests --artifacts-path artifacts\r45_parse_cache_race --verbosity minimal` — passed 42/42.
- `dotnet test osafw-tests\osafw-tests.csproj '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite' --filter FullyQualifiedName~DevRowRegeneratorTests --artifacts-path artifacts\r45_sqlite_cache_race --verbosity minimal` — passed 10/10 with the optional SQLite symbol.
- `dotnet list osafw-app\osafw-app.csproj package --vulnerable --include-transitive` — reported no vulnerable packages from the configured current sources.
- Encoding inspection of all task-touched text files found no UTF-8 BOM or bare LF; `git diff --check` passed.

## Testing instructions

Run the focused regeneration tests above. In a development-only local app with current schema access, open `/Dev/Manage/(ModelRows)`, select a generated model, preview its complete Row diff, and apply only after review. Do not use a shared or production database for exploratory metadata checks.

## Risks / follow-ups

- A supported public auto-property alone cannot prove whether a human curated it. Explicit selection, the full Row diff, and the separate apply action are the review boundary.
- Exclusive sharing prevents overlapping writes in the current process and cooperating Windows file opens. Other platforms and filesystems depend on processes honoring .NET sharing behavior.
- Writes occur in place under held streams. A process, operating-system, or filesystem failure during write or rollback can leave partial source that requires recovery from version control.
- No framework source Rows were bulk-regenerated in this task.

## Reflection

Reusing current typed Row generation prevented a second mapping contract. The useful safety boundary came from syntax classification plus exact source/output hashing; a text-block pattern would not have preserved surrounding C# reliably.
