# Login return navigation

## Objective / acceptance

Anonymous users opening a protected page through a normal browser link reach `/Login`, then return to the original application path and query after successful password login or an existing MFA challenge. Reject external return destinations. Preserve signed-in authorization and the existing remember-me initialization order.

## What changed

- `FW.dispatch()` sends anonymous ordinary GET page requests denied by authorization to `/Login` with one encoded `gourl`. It captures `Request.Path` and `QueryString`, excluding `PathBase` so `FW.redirect()` adds `ROOT_URL` once. It does not capture form/API/AJAX requests or effective non-GET method overrides.
- Already-signed-in visits to `/Login` now honor validated `gourl`; password and existing MFA completion reuse their existing validation. The existing login hidden field carries return navigation through retry.
- Signed-in authorization failures pass their `AuthException` to the error renderer and return HTTP 403 instead of 500.
- `Utils.isAppUrl()` rejects raw control characters, including tabs/newlines browsers can discard. Legitimate root-relative and configured-root absolute destinations remain supported.
- Canonical template documentation describes the feature and security behavior. After developer feedback, the dated changelog was narrowed to the actual migration for copied apps using a custom sign-in route via `UNLOGGED_DEFAULT_URL`. The feature, corrected HTTP status, and control-character security fix do not independently need breaking-upgrade entries.

## Scope reviewed / decisions

Framework source-copy repository, established from the upstream remote, documentation, and code. Implementation stayed direct because dispatch, login, validation, and their tests share one contract. The follow-up explicitly authorized live testing and, if successful, committing and pushing the scoped work on the current branch. No branch switch is needed.

Checked the actual dispatch/controller-access path, Login page/action/form, Users login/session behavior, `FwHooks.initRequest()` remember-me order, existing MFA challenge, optional Windows login, first-time MFA enrollment, error rendering, and URL validation consumers. Remember-me still runs before dispatch. Optional Windows sign-in and first-time MFA enrollment retain their existing landing/continuation behavior; enrollment continues to show recovery codes. URL fragments cannot be captured server-side. Non-page requests and logout retain `UNLOGGED_DEFAULT_URL`.

No schema, provider, package, or compile-symbol changes. The authorized live login/logout tests exercised normal application session, login-time, and activity-log writes. No business records, schema, IDE settings, or IIS Express process were deliberately changed. The task-created browser session was logged out and its temporary tab closed. Pre-existing untracked work was preserved.

## Commands used / verification

Final affected-suite command, from repository root:

```powershell
$taskOut = Join-Path (Resolve-Path .).Path 'artifacts\assistant_login_return\test\'
$taskTemp = Join-Path (Resolve-Path .).Path 'artifacts\assistant_login_return\temp'
$env:TEMP = $taskTemp
$env:TMP = $taskTemp
dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter "FullyQualifiedName~FwLoginRedirectTests|FullyQualifiedName~SecurityQuickFixTests|FullyQualifiedName~FwTests|FullyQualifiedName~FwErrorHandlingTests|FullyQualifiedName~UtilsTests|FullyQualifiedName~FwRouteTests" -p:OutDir=$taskOut
```

Result: app and test projects compiled; 211 tests passed, zero failed/skipped. The affected tests exercise real dispatch static-controller and configured access-rule failures, encoded query preservation, virtual-directory paths, existing non-page behavior, authenticated denial, password login destinations, already-authenticated Login destinations, HTML retry rendering, and MFA completion. Login model reads/writes use test doubles, with no real authentication database writes.

The seven implementation/test/canonical-doc files passed `docs/agents/tools/Normalize-TextFiles.ps1 -Check`; `git diff --check` passed. Task-summary/index formatting also passed the strict text check; final diff inspection included both new task files and preserved unrelated untracked files.

Earlier attempts: the ordinary build hit the active IIS Express assembly lock; an isolated attempt in the read-only sandbox hit temp-directory access denial; the first isolated focused test run had one test-fixture failure because Login does not expose JSON page data. The retry check was corrected to render the actual HTML form and verify its hidden return field. Focused tests then passed 51/51; control-character regression cases and broader utility/routing checks produced the final 211/211 result.

## Testing instructions / limits

After rebuilding/restarting the application normally, use a fresh signed-out browser session to open `/Admin/DemosDynamic` with query parameters, verify `/Login?gourl=...`, submit valid credentials, and confirm return to the original URL. Repeat with an invalid password first. With a signed-in account lacking manager access, expect an access error rather than another login redirect. Tampered external `gourl` must land on `LOGGED_DEFAULT_URL`.

Live browser verification against the developer-supplied running HTTPS application passed on 2026-09-02:

- A fresh signed-out visit to `/Admin/DemosDynamic?dofilter=1&f%5Bsearch%5D=login-return-check` opened `/Login` with the original path/query encoded in `gourl`.
- A rejected submission (missing email) showed the login error; a subsequent successful developer login returned to the exact original path/query and rendered Demo Dynamic.
- A signed-in direct visit to `/Admin/DemosDynamic` rendered the page. A signed-in `/Login` with a valid local `gourl` returned to that URL including its query.
- Signed-in Login requests with an external HTTPS destination or a tab-disguised protocol-relative destination fell back to `/Main`.
- A signed-out Login request with an external destination followed by successful login also landed on `/Main`, never leaving the application.
- Logout returned to the configured public home page. The test session was logged out and the temporary tab closed.

The running app exhibited the implemented behavior; no rebuild/restart was necessary in this follow-up. Source/test code did not change after the 211/211 passing suite, so it was not rerun for documentation-only edits. Real remember-me restoration, ordinary password credential failure, live MFA, Windows authentication, and first-time MFA enrollment were not exercised. Their applicable automated coverage/unchanged-code observations remain as stated above. Isolated build/test output is retained under ignored `artifacts/assistant_login_return/`. No deployment was requested.

## Review / route evidence

Fresh `reviewer_high` review was requested after deterministic checks, with security-boundary and consumer-contract overlays. The reviewer received the requested outcome, scoped final diff, constraints, and deterministic evidence without implementation narrative or this summary. Initial independent verdict: No blocking findings. The reviewer independently checked formatting and diff whitespace and inspected the actual request/authorization/login paths, existing consumers, and safe/unsafe controls. Supplemental audit of this summary and its index entry completed with no candidates or private-data leakage. Final integrator verdict: No blocking findings. Review loop can stop.

The independent review is the only delegated stage and overlaps preparation of this evidence record. Its initial pass took approximately four minutes of gross elapsed time; no approval/platform wait was reported by the reviewer. The gate remained on the critical path after documentation and final diff inspection finished. It supplies residual security/compatibility findings; implementation remained with the primary agent. Three failed verification attempts are explained above. No implementation delegation or architecture escalation was needed.

Follow-up local review: inspected the narrowed changelog against its breaking-upgrades scope and updated this summary to distinguish live browser evidence from automated coverage. No runtime changes or new blocking findings. Final staging and push verification follow the user-authorized successful smoke test.
