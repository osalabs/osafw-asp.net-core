# Report and virtual-module security

## Outcome

- Custom report page state allowlists presentation metadata for all formats. Both outward filter collections exclude stored lookup sources and arbitrary fields; private definitions remain internal for execution.
- Virtual controller routing/navigation reject inactive/deleted definitions. Management lookup remains status-neutral for regeneration of existing modules. Existing update cache invalidation restores routing after reactivation.
- No schema change. Template/export and module-lifecycle compatibility changes are documented in the changelog and canonical report/dynamic guides.

## Verification

- Passed the focused Fast profile with filter FwControllersModelTests|FwReportsTests|FwVirtualControllerTests|VirtualModuleSecurityTests|SecurityGroup9ATests|DevCodeGenTests: clean build, 72 passed, zero skipped.
- Passed full Fast profile: clean build, 1,010 passed, zero skipped.
- Independent review corrected filter projection and management lookup compatibility, then rechecked the fixes and active summary; no blocking findings remain after recorded-evidence and summary audit.
- Default-feature app build passed with zero warnings/errors.
- SQL predicates use provider-neutral parameters; no configured application database is used.
