# Clearer error-page guidance

## Objective / acceptance

Explain bad input, denied access, missing pages and server failures in plain language without exposing production exceptions, changing status classification or losing sign-in return destinations.

## What changed

Added safe status-specific presentation fields to FW error responses and a common escaped error template. Anonymous access-denied pages offer sign-in with a validated return URL. Standard missing-page presentation is simpler and responsive. Existing debug sections retain their development gate.

## Changed contracts

Presentation fields are additive. Existing JSON messages and error.details, HTTP status classification and dispatch authentication redirects remain intact. Canonical template docs describe adoption; no breaking changelog entry is needed.

## Commands used / verification

`dotnet test osafw-tests/osafw-tests.csproj --filter 'FullyQualifiedName~ErrorPageGuidanceTests|FullyQualifiedName~FwErrorHandlingTests|FullyQualifiedName~FwLoginRedirectTests' --verbosity quiet`: 17 passed. Real HTML/JSON rendering covers 400/403/404/500, signed-in and anonymous access denial, escaped input, production exception masking, preserved field errors and prefixed login return URLs. Existing development-error and login dispatch tests are included.

## Risks / follow-ups

No shared database or live site was used. Tests render the changed error content; a full-layout browser visual check was not run. The standalone static-page 404 template remains compatible with its existing caller's page state.
