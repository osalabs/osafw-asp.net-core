# Request-local rendering and template read recovery

## Outcome and scope

All `FW.parser` overloads use one common rendering path that shallow-copies controller data with its comparer. Only a nested validation `error` dictionary needing `details` is copied; other nested datasets remain shared. Request layout overrides, JSON flag removal, derived validation details, and `form_issues` no longer mutate cached controller dictionaries. Controller-provided details and issues remain authoritative. Header/route PJAX detection, query/form overrides, explicit layouts, default layouts, and template access to layout overrides retain their current precedence.

Template reads now distinguish success (including empty files) from missing files and I/O/access failures. Failed reads do not create or overwrite cache entries. Optional missing includes remain quiet, and later reads retry without clearing the shared cache. Modification-time checks share the I/O recovery boundary. Empty inline templates return without file access, including when no template root exists. One simple filename/message WARN call remains commented out by default. The general-purpose file-reading helper is unchanged.

DEBUG diagnostics record format, selected layout, and character count for HTML/PJAX and JSON, without page bodies. The framework's Home example retains its intentional shared expensive-data cache.

## Compatibility decisions

- Adapted validation coverage to the current `FormIssues` and `getFormErrors()` contract, including controller-provided warnings and case-insensitive details keys.
- Used isolated configuration scopes and the existing `RejectingDb` adapter. Each synthetic request gets its own HTTP context/session while controller data is shared across requests.
- Navigation coverage follows the framework's current anonymous-page redirect to Login with `gourl`, then renders Login and refreshes Home after a partial request.
- Added coverage for explicit/query/form precedence, JSON payload objects, gated diagnostics, and failed refreshes of previously cached templates.
- Canonical behavior is documented in `docs/templates.md`. No public signature, route, configuration, schema, or intended consumer contract was removed; no breaking-upgrade changelog entry is needed.

## Verification

Passed on Windows / .NET 10:

- Initial regression run before production edits: 13 failed, 5 passed. Failures reproduced stale partial layouts, mutation of JSON/validation metadata, and failed/missing template reads retained as empty cache entries.
- Final focused test run: 75 passed, 0 failed, 0 skipped (22 renderer-isolation cases, 9 file-cache cases, 42 existing ParsePage cases, 2 existing select-template-cache cases).
- Pre-upgrade Release solution build: succeeded with 0 errors and four NU1902/NU1903 warnings for transitive `System.Security.Cryptography.Pkcs` 9.0.4. The follow-up package update below removes these warnings.
- `git diff --check` and strict UTF-8/no-BOM/CRLF validation passed for the implementation, tests, and canonical documentation.

Commands from the repository root:

```powershell
$taskOut = Join-Path (Get-Location).Path 'artifacts/assistant_renderer_recovery/build/'
$releaseOut = Join-Path (Get-Location).Path 'artifacts/assistant_renderer_recovery/release/'
dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter 'FullyQualifiedName~FwParserIsolationTests|FullyQualifiedName~ParsePageFileCacheTests|FullyQualifiedName~ParsePageTests|FullyQualifiedName~SelectTemplateCacheTests' -p:OutDir=$taskOut --logger 'trx;LogFileName=final.trx' --results-directory artifacts/assistant_renderer_recovery/test-results/final --nologo -v quiet
dotnet build osafw-asp.net-core.sln --no-restore -c Release -p:OutDir=$releaseOut --nologo -v quiet
git diff --check
./docs/agents/tools/Normalize-TextFiles.ps1 -Check -Path osafw-app/App_Code/fw/FW.cs,osafw-app/App_Code/fw/ParsePage.cs,osafw-tests/App_Code/fw/FwParserIsolationTests.cs,osafw-tests/App_Code/fw/ParsePageFileCacheTests.cs,docs/templates.md,docs/agents/tasks/summary-2026-10-21-renderer-cache-recovery.md,docs/agents/tasks/index.md
```

Initial regression evidence used the same test command with only `FwParserIsolationTests|ParsePageFileCacheTests` in the filter, before the additional coverage was added, and `baseline.trx` under the baseline results subdirectory. That run performed restore; subsequent runs used `--no-restore`. Build output and test results remain under ignored `artifacts/assistant_renderer_recovery/`. Tests remove their own temporary template directories.

## Browser smoke test and dependency follow-up

After the user started the application in Visual Studio, Playwright checked the running local application using anonymous GET requests. Home and Login rendered complete documents with their normal navigation, form, and footer; a full-page screenshot confirmed Home's layout. Header (`X-Requested-With`), route (`/Home/.pjax`), and query (`_layout=pjax`) requests returned partial responses with status 200. Subsequent ordinary Home requests consistently returned complete documents with status 200. A modal query override also took precedence over the PJAX header. Direct navigation to `/Main` redirected to `/Login` with `gourl=/Main`; Login and subsequent Home refreshes rendered normally.

Home's shared cache remained enabled throughout. The application assets loaded successfully. The only console error observed was a 404 for the Visual Studio development resource `/_vs/browserLink`; no application JavaScript exception was observed. No authenticated sign-in, form submission, business-data write, or schema update was performed. Browser snapshots, screenshot, and console logs are retained under ignored `artifacts/assistant_renderer_recovery/browser/`; the task-owned browser page was closed. An older pre-existing browser log was left untouched.

The user also requested fixing the build's NuGet vulnerability warnings. The default solution audit found only `System.Security.Cryptography.Pkcs` 9.0.4, introduced by `Microsoft.Extensions.Caching.SqlServer` 10.0.11 through `Microsoft.Data.SqlClient` 6.1.1. Added an explicit 9.0.21 reference in the application project; tests inherit it through their project reference. This stays on the existing dependency major version and leaves the SQL client unchanged. Both official advisories identify 9.0.21 as patched: [CVE-2026-84008](https://github.com/dotnet/runtime/security/advisories/GHSA-846f-f764-76mx) and [CVE-2026-70162](https://github.com/dotnet/runtime/security/advisories/GHSA-2fmv-rv2c-59x7). The [package supports .NET 9 and higher](https://www.nuget.org/packages/System.Security.Cryptography.Pkcs/9.0.21).

Post-upgrade verification passed:

- Restore with `NuGetAuditMode=all`; both projects resolve Pkcs 9.0.21.
- Vulnerable-package audit including transitive dependencies: no known vulnerable packages reported for either restored default `net10.0` project.
- The same 75 selected tests passed again, 0 failed/skipped.
- Release solution build: 0 warnings, 0 errors.
- Project-file whitespace and strict UTF-8/no-BOM/CRLF checks passed.

```powershell
dotnet restore osafw-asp.net-core.sln -p:NuGetAuditMode=all --nologo -v minimal
dotnet list osafw-asp.net-core.sln package --vulnerable --include-transitive --format json
dotnet nuget why osafw-app/osafw-app.csproj System.Security.Cryptography.Pkcs
$taskOut = Join-Path (Get-Location).Path 'artifacts/assistant_renderer_recovery/patched-build/'
$releaseOut = Join-Path (Get-Location).Path 'artifacts/assistant_renderer_recovery/patched-release/'
dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter 'FullyQualifiedName~FwParserIsolationTests|FullyQualifiedName~ParsePageFileCacheTests|FullyQualifiedName~ParsePageTests|FullyQualifiedName~SelectTemplateCacheTests' -p:OutDir=$taskOut --logger 'trx;LogFileName=patched.trx' --results-directory artifacts/assistant_renderer_recovery/test-results/patched --nologo -v quiet
dotnet build osafw-asp.net-core.sln --no-restore -c Release -p:OutDir=$releaseOut --nologo -v quiet
./docs/agents/tools/Normalize-TextFiles.ps1 -Check -Path osafw-app/osafw-app.csproj
git diff --check
```

The browser smoke test used the already-running renderer fix before the package update. The user's Visual Studio process was not restarted or rebuilt after the project edit. Optional compile-symbol package graphs were not audited. No breaking API or migration change was introduced.

The NuGet fix is verified at dependency-graph level. The isolated application outputs omit a private Pkcs assembly and Pkcs entry in their dependency manifests, so the ASP.NET shared framework supplies the runtime implementation. Independent read-only inspection (`dotnet --list-runtimes` and DLL product-version inspection) confirmed that patched Microsoft.AspNetCore.App 10.0.13 is installed locally. The loaded VS/IIS process was not inspected; no claim is made about its selected runtime. A 9.x NuGet pin alone does not secure an older unpatched 10.x shared runtime; deployed hosts also need a patched runtime. Restore/rebuild in Visual Studio will incorporate the project reference change.

## Review and remaining checks

Independent review used the ordinary `reviewer_high` profile for public renderer compatibility and shared cache lifecycle, with consumer-contract and state-integrity overlays. Initial verdict: **No blocking findings.** The subsequent summary/index audit found no factual, evidence, or private-data issues and independently checked the recorded test counts. Review loop can stop.

The full suite is deliberately skipped because it includes tests that mutate a configured SQL Server database. Automated regression tests did not access a configured database. Provider-specific checks are not required because no provider code changed. Anonymous browser flows are verified as described above; authenticated workflows and Unix file-lock behavior remain unverified. A failing read can still produce incomplete output for that request; recovery applies to later access, and successfully read empty files remain cacheable by design.

The bounded independent package follow-up review used consumer-contract and security-boundary overlays: **No blocking findings.** The updated summary/index audit also passed, including the shared-runtime qualification and browser evidence. Review loop can stop.

Pre-existing unrelated/untracked work is preserved. No commit, push, deployment, schema change, or business-data modification was performed.
