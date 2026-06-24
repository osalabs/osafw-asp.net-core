## What changed
Added `osafw-app/web.config` so IIS publishes the ASP.NET Core Module handler plus app-level header hardening for `Server` and `X-Powered-By`. Updated `docs/deploy.md` with the matching IIS instance caveat.

## Scope reviewed
Reviewed current startup IIS options in `osafw-app/Program.cs`, deployment guidance in `docs/deploy.md`, and Microsoft IIS/ASP.NET Core docs for `requestFiltering removeServerHeader`, custom headers, and publish-time `web.config` transformation.

## Commands used / verification
- `powershell -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 osafw-app\web.config docs\deploy.md docs\agents\tasks\summary-2026-06-24-iis-header-hardening.md -Check` - CRLF/UTF-8 check passed.
- `dotnet build osafw-app/osafw-app.csproj` - passed, 0 warnings, 0 errors.
- `dotnet publish osafw-app/osafw-app.csproj --configuration Release -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_publish\` - passed.
- Inspected `osafw-app/bin/Release/net10.0/publish/web.config`; generated output retained `removeServerHeader="true"` and `X-Powered-By` removal, and transformed `aspNetCore` publish settings.
- Self-reviewed final diff using `docs/agents/code_reviewer.md`; no issues found.

## Decisions - why
Used `web.config` rather than ASP.NET Core middleware because IIS owns the identifying `Server: Microsoft-IIS/...` header. Kept the ASP.NET Core Module handler and `aspNetCore` element in the project file so Web SDK publish transforms continue to own final process path/arguments.

No `docs/CHANGELOG.md` entry was added because this is hardening of identifying response headers, not a breaking public framework API, schema, route, storage, frontend, or app override contract.

## Pitfalls - fixes
The app-level setting depends on IIS support and unlocked configuration sections; deployment docs now call out server/site-level hardening as the fallback.

## Risks / follow-ups
Production/beta instances should verify response headers after deployment because locked IIS configuration can prevent app-level settings from taking effect.

## Heuristics (keep terse)
No shared heuristic added.

## Testing instructions
Deploy or publish the app, then check a response from the IIS-hosted site and confirm the `Server` header no longer exposes IIS and `X-Powered-By` is absent. If IIS rejects the app-level setting or still emits the header, configure the equivalent setting at the IIS server/site level.

## Reflection
This was a narrow config/docs change. The useful verification was publish inspection, not only build, because the Web SDK transforms `web.config` at publish time. No sub-agent was needed; the local review loop was sufficient. No stable facts, heuristics, or ADRs were added because the deploy doc now captures the reusable operational caveat.
