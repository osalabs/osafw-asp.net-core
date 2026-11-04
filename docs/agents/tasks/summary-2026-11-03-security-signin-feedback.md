# Sign-in review feedback

Addressed PR #304 feedback against pre-edit dd97bd74. Missing or stale form tokens and missing credentials quietly redisplay Login; POST enforcement remains. Login handles token recovery locally through its existing framework exemption, and logout explicitly checks its token. Removed the query-key rejection loop and duplicate MFA token check. Remember-cookie creation only creates the cookie; the single login caller owns MFA issuance eligibility, while restoration retains active-account/MFA/age validation.

Windows sign-in uses the full trusted principal and the organization's Windows authentication policy without an added application MFA challenge. Retained only the concrete full-principal migration in CHANGELOG: existing short Windows login mappings need an application-owned update. Removed the broader unreleased-fix changelog text; canonical authentication docs describe current behavior.

Verification:
- `pwsh -NoProfile -File scripts/test.ps1 -Profile Full -SqlServer localhost`: Default 980, Fast 1028, Browser 87, SQL Server 36 passed; zero failed/skipped, builds without warnings/errors.
- Independent review found that initRequest creates a replacement token before a stale form arrives. Corrected quiet recovery to include unequal tokens. Final `-Profile Fast -Filter 'FullyQualifiedName~SigninSecurityTests|FullyQualifiedName~SecurityQuickFixTests'`: 79 passed, including silent stale-token recovery and MFA completion with remember requested. This focused rerun follows the review fix; the broader profile preceded it.
- Live browser with a task-owned SQL Server database: missing/stale token responses displayed Login without technical errors; stale tokens with valid test credentials did not authenticate; normal password login and local development shortcut reached Main.
- `git diff --check`: passed.

Fresh reviewer_high security review raised the stale-token recovery finding above; the finding was resolved and supplemental recheck/summary audit completed with no remaining blocking findings. Real Windows Negotiate/domain deployment remains unverified; principal/MFA policy has deterministic controller coverage. No schema changes or configured application-database mutation. Historical summaries retain previous verification and behavior.

Closeout: all task-owned app process trees and the disposable live database were removed; the temporary bootstrap credential was deleted. Browser evidence remains in ignored artifacts.
Optional `dotnet build osafw-app/osafw-app.csproj -p:DefineConstants=isWindowsAuth -p:OutputPath=../artifacts/feedback-windows-build/ --verbosity quiet`: passed, zero warnings/errors.
