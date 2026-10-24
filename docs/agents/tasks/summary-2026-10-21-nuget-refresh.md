# Stable NuGet package refresh

## Outcome

Checked all 29 unique direct NuGet dependencies, including conditional features, against the official NuGet stable-version index on 2026-10-21. Updated 20 unique packages (21 references across the app and test projects); nine were already current. Both projects remain net10.0. No application code, feature defaults, database, frontend assets, or IDE settings changed.

Negotiate 10.0.13 fixes [CVE-2026-70179](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-vrrp-6v32-2p8g). Pkcs moves from 9.0.21 to the current net10.0-compatible 10.0.13 package. The existing explicit security dependency remains. No framework API or migration change was found, so no breaking-change changelog entry was added. Deployment hosts still require a supported patched shared runtime.

| Package | Previous | Updated |
| --- | --- | --- |
| AWSSDK.Core | 4.0.102.3 | 4.0.102.13 |
| AWSSDK.S3 | 4.0.102.5 | 4.0.104.6 |
| coverlet.collector | 10.0.1 | 10.1.0 |
| Markdig | 1.3.2 | 1.4.2 |
| Microsoft.Agents.AI | 1.20.0 | 1.25.0 |
| Microsoft.Agents.AI.OpenAI | 1.20.0 | 1.25.0 |
| Microsoft.AspNetCore.Authentication.Negotiate | 10.0.11 | 10.0.13 |
| Microsoft.Data.Sqlite | 10.0.11 | 10.0.13 |
| Microsoft.Extensions.Caching.SqlServer | 10.0.11 | 10.0.13 |
| Microsoft.NET.Test.Sdk | 18.9.0 | 18.11.0 |
| Microsoft.Playwright | 1.62.0 | 1.64.0 |
| MSTest.TestAdapter | 4.4.0 | 4.5.1 |
| MSTest.TestFramework | 4.4.0 | 4.5.1 |
| OpenAI | 2.13.0 | 2.14.0 |
| QRCoder | 1.8.0 | 1.8.1 |
| Sentry.AspNetCore | 6.10.0 | 6.12.1 |
| System.Data.Odbc | 10.0.11 | 10.0.13 |
| System.Data.OleDb | 10.0.11 | 10.0.13 |
| System.Drawing.Common | 10.0.11 | 10.0.13 |
| System.Security.Cryptography.Pkcs | 9.0.21 | 10.0.13 |

## Verification

- Default Release solution build: passed, zero warnings/errors.
- Combined optional-feature Release build: passed, zero errors and one CS0162 warning at unchanged `Att.cs:771` under isS3. The compile-time enabled constant makes its disabled branch unreachable; no package-related warning appeared.
- Default regression run: 187 passed, zero failed/skipped. Covers renderer isolation, template read recovery, ParsePage/select cache, Vue controller and offline browser interactions, stored Markdown security, and Windows image operations.
- Production PDF renderer/local-asset policy tests: 7 passed, zero failed/skipped, covering allowed assets, denied URLs, resource limits, and cleanup with the upgraded Playwright/Chromium. Total selected tests: 207 passed.
- Optional disposable SQLite tests: 13 passed, zero failed/skipped. No configured database was accessed.
- Default and combined optional graphs: no known vulnerable packages (including transitives), no outdated direct references, and no audit problems. An initial optional audit without matching feature symbols reported an assets mismatch and was inconclusive; rerunning with matching process-local symbols resolved it. Restored the default graph afterward.
- Installed the browser binaries matching Playwright 1.64.0 and exercised Chromium through the offline browser suite.
- Strict UTF-8/no-BOM/CRLF and Git whitespace checks passed. Ignored build/results/version inventory remain under `artifacts/assistant_packages/`.

Commands were run from the repository root; OutDir values below were supplied as absolute paths resolving to these locations:

```powershell
dotnet build osafw-asp.net-core.sln -c Release -p:OutDir=artifacts/assistant_packages/default/net10.0/ -p:NuGetAuditMode=all --nologo -v minimal
dotnet build osafw-asp.net-core.sln -c Release '-p:DefineConstants=isSQLite%3BisMySQL%3BisWindowsAuth%3BisSentry%3BisExcelDataReader%3BisRowRegeneration%3BisS3' -p:OutDir=artifacts/assistant_packages/optional/net10.0/ -p:NuGetAuditMode=all --nologo -v minimal
& ./artifacts/assistant_packages/default/net10.0/playwright.ps1 install chromium
dotnet test artifacts/assistant_packages/default/net10.0/osafw-tests.dll --filter 'FullyQualifiedName~FwParserIsolationTests|FullyQualifiedName~ParsePageFileCacheTests|FullyQualifiedName~ParsePageTests|FullyQualifiedName~SelectTemplateCacheTests|FullyQualifiedName~FwVueControllerTests|FullyQualifiedName~VueInteractionBrowserTests|FullyQualifiedName~SecurityStoredRenderingTests|FullyQualifiedName~ImageUtilsTests' --logger 'trx;LogFileName=regressions.trx' --results-directory artifacts/assistant_packages/results --nologo -v quiet
dotnet test artifacts/assistant_packages/optional/net10.0/osafw-tests.dll --filter 'FullyQualifiedName~SQLiteDBTests|FullyQualifiedName~SQLiteFwTests' --logger 'trx;LogFileName=sqlite.trx' --results-directory artifacts/assistant_packages/results --nologo -v quiet
dotnet test artifacts/assistant_packages/default/net10.0/osafw-tests.dll --filter 'FullyQualifiedName~PdfLocalAssetTests' --logger 'trx;LogFileName=pdf.trx' --results-directory artifacts/assistant_packages/results --nologo -v quiet
# Audit the optional graph in a process with these symbols, before restoring default:
$env:DefineConstants='isSQLite;isMySQL;isWindowsAuth;isSentry;isExcelDataReader;isRowRegeneration;isS3'
dotnet list osafw-asp.net-core.sln package --vulnerable --include-transitive --no-restore --format json
dotnet list osafw-asp.net-core.sln package --outdated --no-restore --format json
# In a separate process without the optional symbols:
dotnet restore osafw-asp.net-core.sln -p:NuGetAuditMode=all --nologo -v quiet
dotnet list osafw-asp.net-core.sln package --vulnerable --include-transitive --no-restore --format json
dotnet list osafw-asp.net-core.sln package --outdated --no-restore --format json
git diff --check
```

## Review and limits

Independent `reviewer_high` review selected for security-sensitive and multi-subsystem package compatibility, using consumer-contract and security-boundary overlays. Initial verdict: no blocking findings. Checked actual consumers and Pkcs native net10.0 assets/SqlClient dependency compatibility. Summary/index audit found no factual, evidence, or private-data issues. Review loop can stop.

Skipped the unrestricted test suite because it includes configured-database mutations. Real domain Negotiate/LDAP login, SQL Server encrypted/AlwaysEncrypted connections, MySQL, OLE/ODBC, and external AI/S3/Sentry calls were not exercised. The existing Visual Studio app was not restarted and its live session cannot validate these newly built packages. No deployment or shared-resource changes were made. User authorized commit/push after successful checks; unrelated untracked work remains excluded.
