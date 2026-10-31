# Site Settings

Site Settings are operational values stored in the `settings` table and managed at `/Admin/Settings`. Deployment/bootstrap configuration stays in `appsettings*.json`; framework defaults belong in `FwConfig`, application defaults and policy in `FwHooks.configure`, and model-specific constants with their model.

## Runtime API and defaults

```csharp
var settings = fw.model<Settings>();
var name = settings.read("SITE_NAME");
var enabled = settings.readBool("ASSISTANT_ENABLED");
var limit = settings.readInt("ASSISTANT_MAX_FILES_PER_MESSAGE", 5);
var password = settings.readSecret("mail.password");
settings.write("CUSTOM_CODE", "new value");
```

Reads use an application-level FwCache map keyed by the actual database connection identity. The map contains stored ciphertext, not decrypted credentials. The existing one-hour cache lifetime applies; supported model writes and imports invalidate it immediately, together with dependent menu data. One in-process lock coordinates loading and writes. Direct SQL changes require an application-pool restart or explicit invalidation. There is no distributed cache.

`basis=0` means inherit the owning default; `basis=1` means use the stored value, including an intentional empty string. Missing rows also inherit. `read(code)` uses `fw.config(code)` as its default, including dotted keys such as `mail.port`; partial nested configuration preserves unspecified defaults; `read(code, fallback)` supplies an explicit caller default. Customize application defaults in the pure, DB-independent `FwHooks.configure` hook rather than adding a second defaults registry to Settings. Integer readers use their supplied fallback for invalid/empty numbers. This replaces the old empty-string-always-means-default behavior.

Runtime reads and model writes are trusted system operations, not authorization entrypoints. Controllers must authorize user operations. `readSecret` is the explicit API for credential consumers; missing or inherited credentials use the same configuration-default path, while an explicit stored empty value remains empty. Generic model rows expose ciphertext; never place a secret runtime read in template globals or page state.

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

Normal Save cannot edit metadata or a non-editable row. Secret inputs never contain the current value. Keep, Replace, Clear, and Inherit distinguish intended changes. A Reveal action is available only for rows configured with that mask policy. All sensitive actions use POST and the framework XSS token; plaintext responses disable browser caching. The default production logging policy omits submitted values, and Settings changes use value-free audit records. Explicit local `log_pii`/DEBUG logging and development error pages retain full diagnostic values. Sentry excludes Settings request bodies and cookies.

Coupled values can be changed atomically through the trusted model `writeBatch` API. Settings stores credential rows independently; integration-specific requirements belong to the consuming model. SDK mode uses the normal SDK identity chain.

## Encryption and durable keys

Credential values are encrypted before database writes using ASP.NET Core Data Protection, a purpose containing the setting code, and the durable `fwkeys` repository. On Windows, DPAPI wraps key material for the local machine. Database SELECTs and backups contain ciphertext rather than the credential value. This does not protect against a compromised application process or an administrator who controls both host and database.

`DATA_PROTECTION_APPLICATION_NAME` is a stable deployment identity, independent of editable `SITE_NAME`. Preserve its value across normal upgrades. When upgrading an older application, set it to that application's previous effective SITE_NAME before using existing protected data. Keys are retained; age alone is not grounds to delete keys needed by stored values. Credential protection fails closed on repository read/write failure. Session-cookie protection tolerates an unavailable database during initial setup; its temporary keys are never used by the Settings credential provider.

Protection helpers live on the `Settings` model: startup uses `Settings.configureProtection(services, config, isRequired: false)` for session cookies; Settings uses its own cached strict provider with the same database repository, and trusted callers can use `fw.model<Settings>().protect(code, value)`, `fw.model<Settings>().unprotect(code, value)`, and `Settings.markSensitiveRequest(fw)`. Normal persistence should use model writes, which encrypt plaintext once and invalidate caches. Never pass already-protected values to a plaintext write API.

Encryption does not replace diagnostic hygiene. Do not log decrypted values or transfer payloads in application-specific code. The framework records value-free audit information.

## Database-free pages

Home and `/Dev/Configure` render with the defaults in `FwConfig`/`FwHooks` when the configured database is unavailable or uninitialized. Date/time formatting, language and timezone remain configuration defaults. Optional non-secret template overrides (site name, contact addresses, redirect links and UI appearance) use Settings when available; failure to load them leaves the configured defaults. Credentials are read only by their consumers and are never bulk-copied into template globals.

The existing SQL Server session provider tolerates unavailable storage; sessions are not usable until the database is ready. Session keys remain database-backed. There are no key files or new session fallback infrastructure. Development InitDB keeps its existing dev-mode/POST guard and session-token check when a token exists; no separate bootstrap token system is introduced.

## Fresh installation and upgrade

Provider `settings.sql` files own framework setting definitions and shipped values, with no credentials. Run after framework/application schema and before optional demo data. Developer database initialization includes this step. Demo-only settings remain in `demo.sql`.

Existing databases use the provider's additive `updates/upd2026-09-27-settings-foundation.sql`, never the destructive fresh schema. The update preserves existing values, adds metadata and missing definitions, and does not attempt application encryption in SQL. SQL Server is primary; SQLite has disposable integration coverage; the optional MySQL script does not imply full framework schema parity.

Complete the application-specific data migration before starting the upgraded runtime or enabling traffic. The [agent migration guide](agents/migrations/settings.md) maps old JSON values, metadata, plaintext credentials, and copied-app consumers to the current contracts. There is no built-in migration command, migration endpoint, automatic migration on read, or special legacy MFA minimum. Existing values that should remain effective must be migrated explicitly.

When upgrading copied applications, move their policy customizations into FwHooks and change operational readers to Settings. Preserve custom setting definitions and intentional overrides. Follow the old-to-new map in [CHANGELOG](CHANGELOG.md); Codex-assisted migration still needs application acceptance checks.

## Transfer and server moves

The trusted model supports `exportJson()` and `importJson(json)` for same-application transfer by code; target definitions and security metadata remain authoritative. These APIs carry plaintext credentials and require separately authorized handling, secure temporary storage, and value-free diagnostics. A public import/export screen is outside this foundation change.

A database backup alone cannot move Windows DPAPI-protected keys to a different machine. While the source host can still decrypt values, use a reviewed secure transfer, initialize durable protection on the destination, import through the model so values are encrypted for that host, and verify consumers before retiring the source. Preserve the application identity and required key history; do not delete old keys merely because they are old.
