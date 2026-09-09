# Attachment lookup helpers

## Objective / acceptance

Add explicit category, all-item entity and reverse attachment-link reads while preserving existing lookup defaults and authorizing parent records before returning data.

## What changed

Added `Att.listByCategory`, `Att.listAllByEntity` and `AttLinks.listByAtt`. Category item filters distinguish omitted/null from zero. New reads do not create entity metadata. Every returned attachment or linked parent is authorized; denied rows fail the whole request rather than returning partial data.

The September 9 PR #287 review fix routes every reverse-link parent binding through `Att`'s existing overridable parent-access policy. The bridge preserves attachment row context while substituting each linked binding. This preserves copied-app parent policy overrides, normalizes missing or invalid model resolution to a fail-closed `AuthException`, and retains attachment-first authorization before the active-link query.

## Changed contracts

Additive methods only. Existing zero/default semantics, attachment URL/delivery policy and schema remain unchanged. Canonical DB documentation records the new methods; no breaking changelog entry is needed.

## Commands used / verification

`dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter 'FullyQualifiedName~AttachmentLookupTests|FullyQualifiedName~SecurityAttachmentTests' --verbosity quiet`: 21 passed. Tests cover missing categories/entities, explicit zero versus omitted filters, existing defaults, active status, attachment-first authorization, conventional parent authorization and missing-parent metadata. The exact September 9 review controls are `ReverseLookupUsesCustomPolicyForNonconventionalParent`, `ReverseLookupChecksEveryLinkedParentAndRejectsDeniedOtherParent`, and `ReverseLookupNormalizesMissingParentModelToAuthException`. Existing attachment security tests are included.

## Risks / follow-ups

These helpers materialize their result set and perform per-row authorization; use them for bounded lookups. No shared database or external attachment storage was accessed. No schema changes.
