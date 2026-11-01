# Good passwords by default

## Outcome and contracts

The four built-in user-facing password-setting paths (My/Password, Admin/Users create/edit, Signup, PasswordReset) now reject supplied new passwords unless Users.isAcceptablePwd returns true. Per the final developer correction, the default accepts the browser meter's good or strong ratings (score greater than 60). A comment beside the threshold documents 60 = good and 100 = strong for project customization. The server heuristic now retains fractional contributions and matches JavaScript ASCII categories. Both sides score the first 32 characters, then trim the same whitespace as existing password hashing. Controller-specific pre-trimming was removed so validation and hashing use the same effective password.

All four forms show a concise BAD field error with password-strength hints only after validation fails; there is no permanent password guidance text. Signup's maximum input length is now 32, matching hashing and the other forms. Existing login/password verification, reset-token/MFA hashing, blank/omitted admin passwords, and direct model writes keep their previous contracts. Blank admin creation retains automatic password generation. Custom copied-app password forms must opt into the shared policy before saving. This heuristic does not consult breached-password lists. Per developer feedback, this is not classified as a breaking change; the initial CHANGELOG and unrelated CRUD documentation additions were removed. SITE_VERSION is bumped to 0.26.1101.1 so browsers reload the updated fw.js.

## Verification

Passed on Windows, targeting net10.0 with the installed .NET SDK. The following commands used an absolute OutDir resolved to the task-owned ignored artifacts/assistant_password_strength/build directory:

```powershell
$taskOut = Join-Path (Resolve-Path .).Path 'artifacts/assistant_password_strength/build/'
dotnet test osafw-tests/osafw-tests.csproj --filter FullyQualifiedName~PasswordStrengthTests -p:OutDir=$taskOut --no-restore --logger 'console;verbosity=normal'
dotnet build osafw-asp.net-core.sln --no-restore -p:OutDir=$taskOut --verbosity quiet
git diff --check
```

- Focused tests: 24 passed again after the final good-default correction. Includes good/strong acceptance and weak rejection controls, threshold boundaries, discarded suffix/whitespace controls, all four production validators, all four weak-password SaveAction paths with DB writes rejected by a strict fake, optional admin passwords, old-password reauthentication, existing hash/token compatibility, all four ParsePage form renders, and Chromium meter/server parity plus visible field feedback. Browser parity covers repeated characters, ASCII/non-ASCII categories, Unicode whitespace and surrogate pairs.
- Solution build: passed with zero warnings and zero errors on the initial implementation. The follow-up focused test run rebuilt both projects successfully.
- git diff --check and strict UTF-8 without BOM/CRLF checks passed for the task files. Follow-up checks also confirmed valid appsettings JSON, the new SITE_VERSION binding in the fw.js script URL, and no remaining CHANGELOG.md or crud.md diff.
- The final threshold test initially used an incorrectly scored boundary sample; it was corrected to an eight-character sample scoring exactly 60, and the full focused rerun passed.
- Initial test run found two fixture errors (an incorrect numeric expectation and an unimplemented database-clock read); both fixtures were corrected before the successful run. The follow-up visibility assertion initially lacked page CSS; loading the actual Bootstrap/site styles corrected the fixture, and all 24 checks passed again, including visible error feedback and hidden hints after clearing errors.
- No configured database was mutated. Provider/schema checks are not required: password validation changes no SQL or schema. A live database-backed successful signup/reset was not exercised; success-side validation is covered with model collaborators and login hashing remains unchanged. The full repository suite was not run; checks target all consumers of the changed password heuristic and form feedback.
- Task build output remains ignored. Existing untracked user work was preserved. No branch changes, commit, push, or deployment.

## Review

Independent reviewer_high review under security-boundary and consumer-contract overlays found no blocking findings. The reviewer audited this summary and index entry for code/evidence consistency and private-data leakage; no supplemental findings. Follow-up review also found no blocking findings; the updated summary and index passed the supplemental audit. The final good-default correction passed the focused rerun, independent follow-up review, and updated summary/index audit with no blocking or supplemental findings. Review loop can stop.
