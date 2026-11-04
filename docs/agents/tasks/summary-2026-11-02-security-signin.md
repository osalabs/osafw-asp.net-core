# Sign-in security

Password login requires a tokened POST; missing/stale tokens or missing credentials quietly redisplay Login. Removed query-key scanning and duplicate MFA token validation. The login caller owns remember-cookie eligibility; cookie creation only creates it. Restoration enforces age, active status and MFA policy, and account security changes revoke persistent tokens. Anonymous MFA setup requires a recent setup-purpose login for an active unenrolled account. Windows SSO matches the full trusted principal and does not add application MFA. Only the concrete short-to-full Windows identity migration remains in CHANGELOG; no schema change.

Removed SigninSqlServerTests, added during acceptance verification. Retained SigninSecurityTests (restoration, revocation, renewal and Windows policy), SecurityQuickFixTests (public login/MFA/redirect behavior) and UsersTimezoneTests. The removed native test repeated this security behavior without a distinct SQL Server regression.

Verification:
- Before test cleanup, `pwsh -NoProfile -File scripts/test.ps1 -Profile Full -SqlServer localhost`: Default 980, Fast 1028, Browser 87 and SQL Server 36 passed, zero skipped. Those SQL counts include the subsequently removed acceptance probes.
- After cleanup, `pwsh -NoProfile -File scripts/test.ps1 -Profile SqlServer -SqlServer localhost`: the original 35 tests passed, zero skipped; build reported zero warnings/errors.
- After the stale-token recovery correction, final focused SigninSecurityTests|SecurityQuickFixTests: 79 passed. A replacement session token is treated as a quiet retry, before authentication.
- Live browser on a disposable native SQL database: missing/stale tokens displayed Login without technical errors, stale tokens with valid test credentials did not authenticate, and normal password/local development login reached Main.
- Optional `dotnet build osafw-app/osafw-app.csproj -p:DefineConstants=isWindowsAuth -p:OutputPath=../artifacts/feedback-windows-build/ --verbosity quiet`: passed with zero warnings/errors.
- `git diff --check`: passed. Provider/browser verification uses disposable resources; task-owned apps/databases and the temporary bootstrap credential were removed, with evidence kept in ignored artifacts.

The three PR-local summaries were consolidated into this single current record at the user's request; unrelated merged history is unchanged. Prior independent runtime/security reviews found no remaining blocking findings. Independent cleanup review and consolidated-summary audit completed with no blocking findings. Existing short Windows login mappings require application-owned migration to exact trusted principals. Real Windows Negotiate/domain deployment remains unverified.
