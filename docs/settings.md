# Site Settings

Site Settings are operational values stored in the `settings` table and managed at `/Admin/Settings`. Deployment/bootstrap configuration stays in `appsettings*.json`; framework defaults belong in `FwConfig`, application defaults and policy in `FwHooks.configure`, and model-specific constants with their model.

## Model ownership

`fw/FwSettings.cs` is the abstract framework model for storage, validation, encryption, transfer and application caching. `models/Settings.cs` inherits it and owns application-specific helpers, constants and `applyGlobals()`. Keep copied-app customizations in Settings; framework updates replace the shared core implementation. New setting codes normally need metadata/seed rows and consumers, not model overrides.

`fw.settings` lazily returns the same concrete Settings instance as `fw.model<Settings>()`, scoped to the current FW lifetime. It adds no second cache or eager database reads. All runtime consumers use the application model, so its overrides remain effective. Startup protection configuration uses `FwSettings.configureProtection`. Existing `Settings.Row`, `Settings.ValueChange`, `Settings.Transfer`, constants and inherited static methods remain available to source consumers.

The core `invalidate()` method clears shared/request caches; Settings overrides it to reset its display-globals flag. Further overrides must call base. The Row definition now lives in FwSettings; the optional application Row regenerator skips Settings because it has no directly declared Row to rewrite.

## Runtime API and defaults

```csharp
var settings = fw.settings;
var name = settings.read("SITE_NAME");
var enabled = settings.readBool("ASSISTANT_ENABLED");
var limit = settings.readInt("ASSISTANT_MAX_FILES_PER_MESSAGE", 5);
var password = settings.readSecret("mail.password");
settings.write("CUSTOM_CODE", "new value");
```

Reads use an application-level FwCache map keyed by the actual database connection identity. The map contains stored ciphertext, not decrypted credentials. The snapshot has no timed expiry; supported model writes and imports invalidate it immediately, together with dependent menu data. The next read reloads it once; later requests reuse it until another change, explicit cache clear, eviction, or application restart. One in-process lock coordinates loading and writes. Direct SQL changes require an application-pool restart or explicit invalidation. There is no distributed cache.

`basis=0` means inherit the owning default; `basis=1` means use the stored value, including an intentional empty string. Missing rows also inherit. `read(code)` uses `getDefault(code)` (normally `fw.config(code)`) as its default, including dotted keys for nested shared configuration; partial nested configuration preserves unspecified defaults; `read(code, fallback)` supplies an explicit caller default. Customize shared application defaults in the pure, DB-independent `FwHooks.configure` hook. Feature-only defaults are resolved by the application Settings.getDefault override, shared by consumers and the editor. It references existing feature constants where available; SMTP uses port 587 and TLS enabled, and S3 uses `sdk` credential mode. These defaults remain outside FW globals. Integer readers use their supplied fallback for invalid/empty numbers. This replaces the old empty-string-always-means-default behavior.

Runtime reads and model writes are trusted system operations, not authorization entrypoints. Controllers must authorize user operations. `readSecret` is the explicit DB-only API for credential consumers. Missing or inherited credentials mean "not configured"; no configuration or caller fallback is used for credential rows. Explicit stored empty values remain empty, and database/decryption failures still propagate. Generic model rows expose ciphertext; never place a secret runtime read in template globals or page state.

`write` updates by stable code or creates a non-user-editable row, restricted to Site Admin by default. Seed full metadata for any setting intended for the editor. Use `writeBatch` for coupled values such as an AWS key pair.

## Schema and controls

| Column | Contract |
| --- | --- |
| `icode` | Unique stable runtime code. Renaming requires consumer migration. |
| `icat`, `iname`, `idesc` | Category, label, and help text. |
| `ivalue` | Text, or versioned application-encrypted ciphertext for credentials. |
| `input` | Control type; 90 classifies a recoverable credential. |
| `allowed_values` | Space-separated `value\|Label` options, or numeric `min\|1 max\|100 step\|1` metadata. Use `&nbsp;` inside labels. Text controls may declare `local_url\|1` to require an app-local URL. |
| `is_user_edit` | Ordinary editor changes require this flag. |
| `access_level` | Minimum numeric user level; route permission and RBAC still apply. |
| `mask` | Display policy, independent of the input control. |
| `basis` | 0: inherit; 1: explicit value. |
| audit columns | Standard add/update time and actor metadata. |

Input codes remain 0 text, 10 textarea, 20 select, 21 multiselect, 30 checkbox group, 40 radio, 50 date, 60 number, 70 switch, 80 range, and 90 credential. Option membership and numeric constraints are validated on the server, including import.

Mask codes are 0 normal, 10 hidden, 20 first/last characters, 30 suffix, and 40 hidden with an authorized Reveal action. Short values do not expose fragments. Credentials cannot use normal display, and always require Site Admin. Only `input=90` classifies a credential: no setting name or suffix implies encryption. Declare custom sensitive settings as credentials before writing values; masking alone does not encrypt a value.

## Admin behavior

Ordinary Admins (90) can see only rows whose floor permits them. Site Admins (100) manage credentials, SMTP, AWS, and other restricted settings. Existing role-based route/action checks apply in addition to the numeric floor; RBAC cannot grant access below that floor. List filtering applies before counts and category discovery. Generic CSV export, quick-search and inherited mutations are not available for this module.

Normal Save cannot edit metadata or a non-editable row. Secret inputs never contain the current value. Credential controls offer Keep, Replace, and Clear. Non-credential settings also offer application-default inheritance; an existing inherited credential is shown as "Not configured". A Reveal action is available only for rows configured with that mask policy. All sensitive actions use POST and the framework XSS token; plaintext responses disable browser caching. The default production logging policy omits submitted values, and Settings changes use value-free audit records. Explicit local `log_pii`/DEBUG logging and development error pages retain full diagnostic values. Sentry excludes Settings request bodies and cookies.

Coupled values can be changed atomically through the trusted model `writeBatch` API. Settings stores credential rows independently; integration-specific requirements belong to the consuming model. SDK mode uses the normal SDK identity chain.

SMTP and AWS are edited through their category tabs, using the same row editors as other settings. The former grouped SaveEmail/SaveAWS endpoints and templates were removed before release. Selecting Server authentication uses the AWS SDK chain (EC2 role, environment or server profile) and ignores saved keys without deleting them; Saved uses the encrypted key rows. SMTP testing is available below the Email list to Site Admins, uses the runtime delivery policy, and returns errors through the standard index message area.

Export and file import use the standard page-header action slot and remain Site Admin-only. Custom-value plain text, select, number and switch rows have compact inline forms using the existing framework autosave: text/number edits save on blur and select/switch changes save automatically; complex/masked/credential rows use Edit. Default values appear as read-only text in the list. To customize one, open Edit, choose Set a custom value, enter the value and Save; supported controls then become editable in the list. Use default restores the standard value shown in the editor. Existing Save authorization, validation and XSS checks apply; The shared page-header saved status and standard autosave errors apply; failed edits remain available for correction. Complex/masked values retain the full editor.

The full editor shows application defaults read-only, with the row mask applied, and disables the saved-value controls until Set a custom value is selected. A failed save preserves the submitted source. `FwSettings.getDefault(code)` is a database-free extension point; the application Settings override owns the SMTP port/TLS and AWS-mode fallbacks and references the Assistant models' existing constants, and runtime consumers use the same resolution. Configured overrides win; explicitly supplied fallback parameters retain their existing semantics. Credential defaults never expose configuration secrets. These defaults are not copied to template globals.

## Encryption and durable keys

Credential values are encrypted before database writes using ASP.NET Core Data Protection, a purpose containing the setting code, and the durable `fwkeys` repository. On Windows, DPAPI wraps key material for the local machine. Database SELECTs and backups contain ciphertext rather than the credential value. This does not protect against a compromised application process or an administrator who controls both host and database.

`DATA_PROTECTION_APPLICATION_NAME` is a stable deployment identity, independent of editable `SITE_NAME`. Preserve its value across normal upgrades. When upgrading an older application, set it to that application's previous effective SITE_NAME before using existing protected data. Keys are retained; age alone is not grounds to delete keys needed by stored values. Credential protection fails closed on repository read/write failure. Session-cookie protection tolerates an unavailable database during initial setup; its temporary keys are never used by the Settings credential provider.

Protection helpers live on the `FwSettings` base model: startup uses `FwSettings.configureProtection(services, config, isRequired: false)` for session cookies; Settings uses its own cached strict provider with the same database repository, and trusted callers can use `fw.settings.protect(code, value)`, `fw.settings.unprotect(code, value)`, and `FwSettings.markSensitiveRequest()`. Normal persistence should use model writes, which encrypt plaintext once and invalidate caches. Never pass already-protected values to a plaintext write API.

Encryption does not replace diagnostic hygiene. Do not log decrypted values or transfer payloads in application-specific code. The framework records value-free audit information.

## Transfer and server moves

Site Admin's Export downloads one JSON file containing every stored setting value and basis, including decrypted credentials and non-editable values. There is no options dialog or preview. Import validates the file and known codes, then writes all supplied values in one transaction, encrypting credentials with the destination key ring. Definitions, permissions, mask policies, labels, and other metadata remain controlled by destination code/schema. Unspecified codes are unchanged; duplicate/unknown codes or invalid values reject the whole import. The transfer limit is 1 MiB.

The export is intentionally plaintext: store it privately and remove it after use. It does not contain DB connection/bootstrap configuration or the key ring. A SQL script cannot substitute for Import because it cannot perform destination application encryption.

For a planned move:

1. Export on the old working host before losing access to its keys.
2. Stop the old app pool and back up the database and deployment configuration.
3. Prepare the destination schema and local authentication so a Site Admin can sign in without external OAuth/SMTP.
4. If restoring the old database on a different Windows machine, its DPAPI-wrapped keys cannot be used there. With the old pool stopped and the backup/export secured, deliberately clear only Data Protection rows (`fwkeys.itype=10`) in the destination copy. This invalidates old protected sessions/tokens; do not clear keys during routine upgrades or while the old app is serving the same DB.
5. Start the destination with its stable application identity. It generates a new local key ring. Sign in locally and Import the JSON; all credentials are re-encrypted on that host.
6. Verify sign-in, SMTP, integrations, and runtime settings before serving traffic.

A database backup alone cannot recover encrypted credentials after the old machine's protection keys are lost. An unreadable credential can be replaced through Settings; it is never silently treated as blank.

## Database-free pages

Home and `/Dev/Configure` render with the defaults in `FwConfig`/`FwHooks` when the configured database is unavailable or uninitialized. Date/time formatting, language and timezone remain configuration defaults. `FW.G` starts as a deep copy of configuration except the `db` and `override` sections, which may contain database credentials. Custom non-secret configuration remains available to templates; integration credentials belong only in Settings and are read at their consumers.

`Settings.applyGlobals()` applies site name, contact addresses and UI appearance once per request from the shared application cache. It preserves session theme/mode preferences. Web and email rendering call it; unavailable storage leaves the bootstrap defaults. The small display-setting list belongs to the Settings model, with no such list or load flag in FW. Redirects, MFA and sender settings are read at their own consumers; developer documentation passes MFA/sender values in page state. Use `FwHooks.initRequest` for additional safe app-wide values, or controller page state for feature-specific values. Never put credentials in either.

The existing SQL Server session provider tolerates unavailable storage; sessions are not usable until the database is ready. Session keys remain database-backed. There are no key files or new session fallback infrastructure. Development InitDB keeps its existing dev-mode/POST guard and session-token check when a token exists; no separate bootstrap token system is introduced.

## Fresh installation and upgrade

Provider `settings.sql` files own framework setting definitions and shipped values, with no credentials. Run after framework/application schema and before optional demo data. The shared initializer in `models/Dev/DevCli.cs` includes this step for both the CLI and `/Dev/Configure`; the controller applies its web guards and calls DevCli.initializeDatabase(fw) directly. Demo-only settings remain in `demo.sql`.

Existing databases use the provider's additive `updates/upd2026-09-27-settings-foundation.sql`, never the destructive fresh schema. The update preserves existing values, adds metadata and missing definitions, and does not attempt application encryption in SQL. SQL Server is primary; SQLite has disposable integration coverage; the optional MySQL script does not imply full framework schema parity.

Complete the application-specific data migration before starting the upgraded runtime or enabling traffic. The [agent migration guide](agents/migrations/settings.md) maps old JSON values, metadata, plaintext credentials, and copied-app consumers to the current contracts. There is no built-in migration command, migration endpoint, automatic migration on read, or special legacy MFA minimum. Existing values that should remain effective must be migrated explicitly.

When upgrading copied applications, move their policy customizations into FwHooks and change operational readers to Settings. Preserve custom setting definitions and intentional overrides. Follow the old-to-new map in [CHANGELOG](CHANGELOG.md); Codex-assisted migration still needs application acceptance checks.
