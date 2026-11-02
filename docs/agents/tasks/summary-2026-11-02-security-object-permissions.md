# Security object permissions

## Outcome

- Attachment administration authorizes existing and proposed direct parents for direct reads, metadata/file saves, deletion, restore, and bulk operations. Inactive rows remain manageable when their parent permits access.
- Read-only account administration is blocked before account/credential mutations.
- Saved-preference autocomplete applies owner/system visibility before result limits. Attachment quick search excludes bound rows, matching library list/select visibility.
- No schema changes. Copied-app parent policies continue through `isParentAccessAllowed`; the additive autocomplete visibility hook is documented in `docs/crud.md`.

## Verification

- Passed `pwsh -NoProfile -File scripts/test.ps1 -Filter 'FullyQualifiedName~AdminAttControllerTests|FullyQualifiedName~SecurityAttachmentTests|FullyQualifiedName~SecurityGroup9BTests|FullyQualifiedName~UserOwnedPreferencesSecurityTests|FullyQualifiedName~PreferenceAutocompleteTests'`: build clean, 63 passed, zero skipped.
- Passed the full Fast profile: clean build, 1,013 passed, zero skipped.
- Fresh independent reviewer completed security/consumer-contract review and summary audit. A discovered attachment-search gap was corrected and rechecked; no blocking findings remain.
- SQL Server-specific behavior is unchanged; provider-neutral query behavior is exercised with isolated SQLite fixtures. No configured application database is used.
