# Sign-in security

## Outcome and compatibility

- Login requires tokened POST and body credentials; default form supplies the token. MFA completion also requires POST.
- Remember cookies require an active account, server-side age check, and no enrolled/enforced MFA. MFA users sign in interactively after session expiry; trusted-device infrastructure is outside this change.
- Password replacements and actual status/MFA changes revoke persistent tokens; unchanged status in profile payloads preserves them. Session creation rechecks current status. Token renewal refreshes issuance time.
- Pending MFA challenge and setup have distinct purposes. Anonymous setup requires a recent setup-purpose request for a currently active, unenrolled account, rechecked before saving.
- Windows login matches full domain-qualified identities and follows application MFA challenges/setup. Copied applications must migrate short Windows login values before importing the route. No schema change.
- The authentication guide and changelog record caller and identity migration requirements.

## Verification

- Initial focused run found fixture type/POST/token assumptions; corrected the tests to exercise the required contract.
- Passed focused Fast checks with filter SigninSecurityTests|SecurityQuickFixTests|FwLoginRedirectTests|UsersTimezoneTests after independent review corrections: clean build, 90 passed, zero skipped.
- Passed the full Fast profile after corrections: clean build, 1,028 passed, zero skipped.
- Independent reviewer rechecked the accepted corrections and active summary with no remaining blocking findings; no blocking findings remain after final evidence and summary audit.
- App build with `DefineConstants=isWindowsAuth` passed with zero warnings/errors.
- Optional Negotiate hosting/domain trusts and real browser behavior remain deployment acceptance checks. No live credentials, external identity provider, or configured application database is used.
