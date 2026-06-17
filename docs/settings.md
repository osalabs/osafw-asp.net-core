# Site Settings

Site Settings are database-backed application settings stored in the `settings` table and managed from `/Admin/Settings`. Use them for values that an administrator may need to change after deployment, such as feature flags, limits, provider choices, and integration configuration.

Use `appsettings*.json` or environment-specific hosting configuration for values that must be available before the database is online or should remain machine-local. Site Settings are loaded through the normal model/database path and are visible to admins with access to the settings screen.

## Runtime API

Read and write values through the `Settings` model:

```csharp
var settings = fw.model<Settings>();

string value = settings.read("SETTING_CODE");
string valueOrDefault = settings.read("SETTING_CODE", "default");
int legacyInt = settings.readi("SETTING_CODE");
bool enabled = settings.readBool("FEATURE_ENABLED");
int limit = settings.readInt("MAX_ITEMS", 25);
long bytes = settings.readLong("MAX_BYTES", 5_242_880);
object? startDate = settings.readd("START_DATE");

settings.write("SETTING_CODE", "new value");
```

Important behaviors:

- `read(icode)` returns an empty string when the row is missing or the value is empty.
- `read(icode, defaultValue)` uses the default when the stored value is empty.
- `readBool`, `readInt`, and `readLong` convert from the stored string value and return the supplied default for an empty value.
- `write(icode, value)` updates an existing row by `icode`; if the row does not exist, it inserts a minimal row with `icode`, `ivalue`, and `is_user_edit=0`.
- For settings that should appear cleanly in `/Admin/Settings`, seed a full row instead of relying on `write()` auto-creation.

## Schema

The canonical schema is in `osafw-app/App_Data/sql/fwdatabase.sql`, with provider variants under `osafw-app/App_Data/sql/mysql/` and `osafw-app/App_Data/sql/sqlite/`.

| Column | Contract |
| --- | --- |
| `id` | Identity primary key used by the admin edit route. |
| `icat` | Category shown as tabs on the admin list. Empty string is rendered as `Site`. |
| `icode` | Unique stable setting code. Runtime code should read settings by this value. |
| `ivalue` | Stored value. The framework treats it as text and callers convert to the type they need. |
| `iname` | Human-facing name shown in the admin list and edit form. |
| `idesc` | Help text shown under the setting name on the edit form. |
| `input` | Admin form input type selector. See "Admin inputs" below. |
| `allowed_values` | Reserved option source for select-like inputs. Values are space-separated; use `&nbsp;` inside a value that needs a space. |
| `is_user_edit` | Metadata flag for whether administrators are expected to edit the setting. Current `AdminSettingsController` does not enforce it. |
| `add_time`, `add_users_id`, `upd_time`, `upd_users_id` | Standard audit columns populated through model insert/update paths. |

`icode` is unique and `icat` is indexed. Choose `icode` names as stable public configuration keys; changing them breaks any code that reads the old key.

## Admin Module

`AdminSettingsController` inherits the normal admin list and form flow, with these settings-specific rules:

- Route: `/Admin/Settings`.
- Access: `Users.ACL_ADMIN`.
- Model: `Settings`.
- Search fields: `icode`, `iname`, and `ivalue`.
- Default sort: `iname asc`.
- Category tabs come from `Settings.listCategories()` and filter on `settings.icat`.
- The add route redirects back to the list. Settings are intended to be seeded by schema/update scripts or by runtime code.
- Save updates only `ivalue`; it does not allow changing `icode`, category, input type, labels, or descriptions from the admin form.
- `ivalue` is required by the current controller validation.
- Delete is blocked with `Site Settings cannot be deleted`.
- Saving clears the `main_menu` cache key.

Because the admin list displays a truncated `ivalue`, do not put secrets in Site Settings unless the operational model accepts that admins with this screen can inspect and edit them.

## Admin Inputs

The `input` column chooses which form partial renders `ivalue`:

| `input` | Partial | Current behavior |
| --- | --- | --- |
| `0` | `input_input.html` | Single-line text input. |
| `10` | `input_textarea.html` | Multi-line textarea. |
| `20` | `input_select.html` | Select element, but `AdminSettingsController` does not currently populate `select_options_ivalue`. |
| `21` | `input_selectmulti.html` | Multi-select element, but option population is not currently wired. |
| `30` | `input_checkbox.html` | Placeholder partial. Do not use without implementing rendering and save handling. |
| `40` | `input_radio.html` | Placeholder partial. Do not use without implementing rendering and save handling. |
| `50` | `input_date.html` | Date input with the shared calendar helper; value is formatted through the template date formatter. |

Prefer `0`, `10`, or `50` unless the option-based partials are completed for the target app. If you enable select, multi-select, checkbox, or radio settings, update `AdminSettingsController.ShowFormAction()` and the relevant partials so options and submitted values are handled consistently.

## Adding A Setting

For a new framework setting, update every provider's from-scratch schema and add an idempotent update script for existing databases when needed.

From-scratch schemas:

- SQL Server: `osafw-app/App_Data/sql/fwdatabase.sql`
- MySQL: `osafw-app/App_Data/sql/mysql/fwdatabase.sql`
- SQLite: `osafw-app/App_Data/sql/sqlite/fwdatabase.sql`

Existing database updates:

- SQL Server: `osafw-app/App_Data/sql/updates/`
- MySQL: `osafw-app/App_Data/sql/mysql/updates/`
- SQLite: `osafw-app/App_Data/sql/sqlite/updates/`

SQL Server idempotent insert pattern:

```sql
INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc)
SELECT 1, 0, 'Features', 'FEATURE_ENABLED', '0', 'Feature Enabled', 'Set to 1 to enable the feature.'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE icode='FEATURE_ENABLED');
```

Use provider-appropriate idempotent syntax in MySQL and SQLite update scripts, such as `INSERT IGNORE` or `INSERT OR IGNORE`.

When choosing metadata:

- Set `is_user_edit=1` for settings intended for admin editing.
- Pick a short `icat` that groups related settings. Leave it empty for the default Site tab.
- Keep `iname` and `idesc` precise enough for admins to edit without code context.
- Store canonical values in `ivalue`, such as `0`/`1` for booleans and integer strings for limits.
- Keep defaults in sync between seeded rows and any runtime fallback passed to `read(..., defaultValue)`.

## Common Pitfalls

- Missing rows silently read as empty strings. Use typed reads with explicit defaults when a missing setting should not disable a feature accidentally.
- `write()` can create settings that have no label, category, or admin input metadata. Seed administrator-facing settings explicitly.
- `is_user_edit` is not an authorization check. Restrict sensitive settings by route access, controller changes, or a dedicated configuration path.
- Option-based input metadata is schema-ready but not fully wired in the current admin module.
- If changing settings affects cached UI or derived state, clear the relevant cache keys after writes. The built-in admin save path only clears `main_menu`.
