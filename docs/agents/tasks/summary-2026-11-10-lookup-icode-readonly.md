# Lookup code editing default

- Existing virtual lookup rows show `icode` read-only. Standard saves preserve the existing code even when a custom form omits it. Add New can still set the code when configured.
- A controller-specific `config.json` can set `"is_lookup_icode_editable": true` for codes users intentionally rename. Explicit field-level read-only settings still apply.
- The default applies to virtual controllers because Lookup Manager lists active module definitions regardless of the legacy `is_lookup` flag. Custom C# controllers keep their own field configuration.

The developer confirmed this behavior change belongs in the configuration documentation. The changelog entry was removed.

## Verification

- `./scripts/test.ps1 -Profile Browser -Filter 'FullyQualifiedName~ValidationRevealsTabAndFieldsetAndReadonlyOnEditFieldsStayReadOnly'` passed the shared Vue rendering regression for existing and new rows.
- `./scripts/test.ps1 -Filter 'FullyQualifiedName~DevCodeGenTests|FullyQualifiedName~VueInteractionBackendTests|FullyQualifiedName~FwDynamicControllerTests|FullyQualifiedName~FwVirtualControllerTests|FullyQualifiedName~VirtualModuleSecurityTests'` passed 47 tests. The regression checks the initial Vue JSON response and standard saves with the default, the file override, an omitted form field, and Add New. The changed files passed strict UTF-8, CRLF and whitespace checks.
- Live browser smoke on the running Visual Studio instance passed for Lookup Manager > Log Types. An existing record showed `icode` as plain text and kept `iname` editable. Add New showed an empty, editable code input. No lookup rows were created or saved during the smoke test. The per-lookup opt-in was verified in the automated regression.
- Provider-specific SQL verification was unnecessary for the existing provider-independent save path.

Independent review verified the revised Vue initial-state regression and audited the task summary. No blocking findings. Review loop can stop.
