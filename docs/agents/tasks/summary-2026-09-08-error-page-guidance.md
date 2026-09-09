# Clearer error-page guidance

## Objective / acceptance

Explain bad input, denied access, missing pages and server failures in plain language without exposing production exceptions, changing status classification or losing sign-in return destinations.

## What changed

Added translated, status-specific HTML guidance and a common escaped message/actions fragment. A September 9 review correction moved English guidance out of `FW.errMsg`, shared 404 guidance through a template include while preserving the existing exception entry template, and removed redundant presentation aliases. A direct anonymous access-denied render offers sign-in with a validated return URL. Existing debug sections retain their development gate.

## Changed contracts

HTML guidance is now controlled by ParsePage templates under `error/400`, `error/403`, `error/404` and `error`, with `error/4xx` retained as the 400/403/404 exception entry. The shared fragment reads the existing `error.message`; only a direct anonymous 403 adds `error.login_url`. Existing JSON messages, legacy fields, `error.details`, HTTP status classification and normal dispatch authentication redirects remain intact. Canonical template docs describe customization; no breaking changelog entry is needed.

## Commands used / verification

`dotnet test osafw-tests/osafw-tests.csproj --filter "FullyQualifiedName~ErrorPageGuidanceTests|FullyQualifiedName~FwErrorHandlingTests|FullyQualifiedName~FwLoginRedirectTests" --verbosity quiet`: 19 passed. Direct `errMsg` HTML/JSON rendering covers 400/403/404/500, signed-in and anonymous access denial, per-status guidance, custom translated 404 content and legacy 4xx overrides, escaped input, production exception masking, preserved field errors and prefixed login return URLs. Existing development-error and normal login-dispatch tests are included.

## Risks / follow-ups

No shared database or live site was used. Tests render the changed error content; a full-layout browser visual check was not run. The anonymous 403 page/link coverage calls `FW.errMsg` directly; existing `FwLoginRedirectTests` cover normal anonymous dispatch redirects.
