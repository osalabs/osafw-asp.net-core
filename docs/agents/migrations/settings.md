# Settings upgrade for an existing application

Use for a deliberate upgrade of a copied application to the database-backed Site Settings runtime. This is agent-owned migration guidance, not a runtime compatibility layer. Read [Settings](../../settings.md) and the application's actual configuration, consumers, provider, and deployment entry path first. Do not deploy the new runtime to a live application before its schema, values, and consumers are ready.

## Plan the application mapping

Inventory effective values for each environment and supported named/host override, including private deployment sources without copying secrets into tracked files. Confirm the selected database and stable `DATA_PROTECTION_APPLICATION_NAME`; retain the application's previous effective protection identity and required keys. Obtain explicit authority before changing a configured database. Rehearse on an approved disposable copy, with jobs and external side effects disabled.

| Old source or consumer | Current destination |
| --- | --- |
| Database definitions, host trust, deployment identity, storage, worker switches, logging/hosting | `appsettings*.json`; environment files preferred, existing named/host overrides supported |
| Route/XSS policy, signup, API origin, timezone, application defaults | Pure, DB-independent `FwHooks.configure(FwDict)`; framework fallbacks stay in `FwConfig` |
| Site name, redirect defaults, contact/sender/test email, MFA policy, UI defaults | Settings rows with the same stable codes; runtime consumers use `fw.model<Settings>()` |
| `appSettings.mail.host`, `.port`, `.is_ssl`, `.username`, `.password` | `mail.host`, `mail.port`, `mail.is_ssl`, `mail.username`, `mail.password` setting codes |
| `AWSAccessKey`, `AWSSecretKey` | Credential rows plus `AWS_CREDENTIAL_SOURCE=static` for a complete static pair; `sdk` selects the SDK identity chain |
| API credentials and custom secrets | Explicit credential metadata (`input=90`), then protected model writes; names alone confer no protection |
| `FwSettingsProtection` consumers from an intermediate branch | `Settings.configureProtection`, `Settings.protect`, `Settings.unprotect`, `Settings.markSensitiveRequest` |
| Intermediate migration helper, guards, CLI, or browser action | Remove after completing this one-time migration; no compiled replacement |

Read nested JSON as nested objects and map mail fields to dotted setting codes; do not rename the JSON fields blindly. `Settings.read(code)` inherits `fw.config(code)`, including dotted lookup, for a missing row or `basis=0`. An explicit caller fallback overrides that default. Use `readSecret(code)` for trusted credential consumers; never project decrypted credentials into generic page state.

## Prepare schema and values

1. Stop writes for the final cutover and retain approved rollback material outside the repository. Select the target environment and connection explicitly; a development launch profile must not choose the production database.
2. Apply the active provider's additive `upd2026-09-27-settings-foundation.sql` to an existing database. Never run fresh-schema scripts there. Review custom definitions and new metadata against the app's existing meanings.
3. Before writing secrets, establish `input=90`, Site Admin access, and a non-normal mask for every credential, including custom codes. Classification changes are an explicit reviewed metadata migration; ordinary model updates reject identity/classification changes. Do not rely on known secret names.
4. Build a value-free review of source precedence. Preserve existing `basis=1` values, including intentional empty strings, unless a specific replacement is authorized. Use `basis=0` only for deliberate inheritance. Older rows without basis need an application-specific decision where empty formerly meant fallback; do not infer every blank to be inherit or clear. Preserve an enforced legacy MFA policy explicitly before removing old configuration.
5. Materialize required legacy operational overrides by environment. Resolve legacy test-email fallback explicitly; an empty stored value now stays empty, and `current_user` selects the signed-in user's test recipient. Keep unused secret JSON out of the deployed configuration once migration succeeds.
6. Encrypt existing plaintext credential rows and imported secrets through model writes on the original host while durable keys are available. Retain valid ciphertext without feeding it back into plaintext APIs. Preserve inherited rows as inherited; clear stale ignored storage rather than turning it into an explicit value. Batch coupled AWS source/key changes atomically.

A temporary app-specific offline tool can construct the reviewed changes and call the existing model boundary. The following fragment assumes `fw` already targets the authorized database with durable protection configured; the variables come from a secure, app-specific source:

```csharp
Settings.markSensitiveRequest(fw);
var settings = fw.model<Settings>();
settings.writeBatch([
    new Settings.ValueChange {
        icode = "mail.host", basis = Settings.BASIS_VALUE,
        ivalue = legacyMailHost
    },
    new Settings.ValueChange {
        icode = "mail.password", basis = Settings.BASIS_VALUE,
        ivalue = legacyMailPassword
    }
]);
```

Metadata must already be correct. Supply plaintext exactly once; normal model writes encrypt credentials and invalidate caches. Do not log values, payloads, SQL parameters, or decrypted verification results. Keep temporary tooling outside permanent compiled runtime source and remove only task-owned temporary files when finished. The runtime supplies no `settings-migrate` command or migration endpoint, and does not mutate old data on reads.

## Verify before reopening traffic

Check the real request and offline consumers: site identity, local redirects, SMTP/test delivery, AWS SDK/static mode, API authentication, MFA, UI defaults, and app-specific integrations. Compare effective behavior per environment, rather than just row counts. On a disposable copy, prove missing/inherited values use configuration defaults, explicit empty remains empty, custom `input=90` values encrypt, unreadable/plaintext credentials fail closed, and an ordinary Admin cannot read or change credentials while an authorized Site Admin can use the intended controls. Check value-free diagnostics and cache invalidation after writes.

Build and run the affected application with its actual provider/symbols. SQL Server is primary; SQLite rehearsal does not prove SQL Server-specific behavior. Remove old operational consumers and temporary migration tooling, then deploy and perform the application's acceptance checks before enabling traffic. Retain rollback material under the application's approved retention policy. For a separate host move, follow the [transfer and server-move procedure](../../settings.md#transfer-and-server-moves) while the original host can still decrypt values.