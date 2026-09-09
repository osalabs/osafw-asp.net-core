# Creating Feature Modules

Feature modules bundle a database table, its model, controller, and templates. You can scaffold them from the command line, use **Developer Tools** in the browser, or build them manually by copying demo assets.

## Quick path: built-in CLI

For agent-driven module creation, prepare the fresh-install and additive schema files first. If the table is not present in the configured development database, request approval before applying it. Once approved, apply the schema and run `scaffold crud`. Use manual generation only when database approval is declined, the development database is unavailable, or the scaffolder fails; record the reason in the task summary.

After the table exists in the intended development database, run the scaffolder from the repository root:

```powershell
dotnet run --project osafw-app -- scaffold crud orders
```

`crud` reads the live table schema and generates the model, controller, templates, `config.json`, and menu item in one process. Names default from the table and can be overridden:

```powershell
dotnet run --project osafw-app -- scaffold crud sales_orders --model SalesOrders --url /Admin/SalesOrders --title "Sales Orders" --type vue
```

The focused commands use the same generators:

```powershell
dotnet run --project osafw-app -- scaffold model orders --name Orders
dotnet run --project osafw-app -- scaffold controller Orders --url /Admin/Orders --type dynamic
dotnet run --project osafw-app -- scaffold report sales-summary
dotnet run --project osafw-app -- scaffold --help
```

The controller command requires the model to be compiled. A normal second `dotnet run` rebuilds automatically after a separate model command; build first only when using `--no-build`. Controller type options are `dynamic`, `vue`, `lookup`, and `api`. The `api` option is reserved for future support; until an API-specific template exists, selecting it reports that it is not yet available and exits before generating files or database rows.

The command defaults `ASPNETCORE_ENVIRONMENT` to `Development` only when neither ASP.NET Core nor .NET environment is already selected, and it refuses to run unless the resolved application settings have `IS_DEV=true`. Existing generated model/controller source or controller template directories are preserved unless `--force` is explicit. Report targets are always preserved when they already exist.

Exit code `0` means success or help, `1` means generation/runtime failure, `2` means invalid command usage, and `3` means the resolved environment is not allowed to scaffold.

After generation, inspect the generated diff, customize the controller and `config.json`, prune unused template partials, and build the app. Controller generation also inserts or updates its development-database `menu_items` row, matching `/Dev/Manage` behavior.

## Browser alternative: Developer Tools at `/Dev/Manage`
1. **Add the table** to your schema: mirror the demo tables in `osafw-app/App_Data/sql/demo.sql`, then append the `CREATE TABLE` to `osafw-app/App_Data/sql/database.sql` and create a dated script under `osafw-app/App_Data/sql/updates/` for deployments. MySQL provider-specific overrides can use `osafw-app/App_Data/sql/mysql/updates/`. SQLite projects use the matching files under `osafw-app/App_Data/sql/sqlite/` and put SQLite updates under `osafw-app/App_Data/sql/sqlite/updates/`.
2. **Open Developer Tools** at `/Dev/Manage` and use the *Create Model* form. Pick your table and optional model name; the action reads the schema and generates the model file for you.
3. **Create the controller** from the same screen. Select the model, provide a target URL/title, and choose controller type (dynamic, Vue, lookup, or the reserved API option). API scaffolding is not yet available; the other choices generate or register the selected controller type.
4. **Restart the project or apply hot reload**, then navigate to the new controller URL.
5. **Review and tweak `config.json`** in the generated template folder (see [dynamic controller config](dynamic.md)).
6. **Review UI fit** against the [design system](design_system.html) before adding custom CSS; generated screens should usually rely on shared fragments and theme tokens.
7. **Prune unused partials/includes** in the generated template folder so only the needed pages and widgets remain.

In local development, Home can automatically redirect to a pending FwUpdates notice when update scripts exist. Set `appSettings.is_fwupdates_auto_apply` to `false` when you want to review and apply `/Admin/FwUpdates` manually.

### Refresh existing typed Rows

The **Regenerate typed model Rows** tool under `/Dev/Manage` updates selected existing model sources from current database metadata. It is available only when `IS_DEV=true` and through the Site Admin Developer Tools controller. Preview and apply both use POST with the current XSS token; preview reads metadata and source without writing, while apply is a separate explicit action.

The tool lists compiled models that have exactly one matching `.cs` file under `App_Code/models`. Select only the sources you intend to update, submit a preview, and review the complete old/new `Row` diff for every selected model before applying. Preview and apply each read current provider metadata through an uncached path that neither consumes nor publishes the shared table-schema cache entry. Apply regenerates the preview from the current source and current schema and refuses the entire batch when a hash is missing or stale.

Only a direct nested `public class Row` containing generated-style public auto-properties is eligible. A Row with methods, non-auto accessors, inheritance, class attributes, directives, ordinary comments, or other custom members is rejected for manual review. C# parse errors, duplicate model/Row declarations, invalid UTF-8 source, files reached through reparse points, and sources outside the model directory are also rejected. The replacement changes only the nested Row syntax span, so custom model base classes, constructors, methods, and other source stay in place. A plain auto-property can be either generated or manually curated; the preview is the required review boundary before apply.

This source tool depends on `Microsoft.CodeAnalysis.CSharp` 5.9.0. Copied applications that bring over the controller, model helper, and template must add the same package reference to their application project. It does not add or update that dependency automatically, and it does not modify customized or ambiguous Rows automatically.

Apply serializes regeneration in the current application process and opens every selected source with exclusive sharing before validating or writing any file. It writes and flushes through those held streams and attempts to restore already-written sources through the same handles if a later write fails. Windows enforces the sharing exclusion for other file opens; processes and filesystems on other platforms must honor the corresponding .NET sharing mode. Because writes are in place, an application, operating-system, or filesystem failure during a write can still leave partial source; keep model source in version control and inspect or restore it after such a failure.

## How `/Dev/Manage` scaffolding works
- `CreateModelAction` converts the selected table into an entity description (`DevEntityBuilder.table2entity`) and passes it to `DevCodeGen.createModel`, which clones demo model templates and adjusts names/fields based on schema metadata.
- The *Create Model* picker lists application tables and views and omits the framework persistence tables. Explicit CLI model scaffolding remains available when deliberate framework `Row` regeneration is required.
- `CreateControllerAction` builds a temporary entity with the chosen model and controller options, loads `dev/db.json`, and calls `DevCodeGen.createController`. The generator copies the demo controller/templates (dynamic or Vue), rewrites URLs/titles, regenerates `config.json`, writes the controller class, and appends/updates `menu_items`; lookup scaffolding registers `fwcontrollers` metadata instead of writing a controller class.
- The built-in `scaffold` command initializes `FW` in offline mode and calls the same entity-builder and code-generator layer without constructing an HTTP request or bypassing the browser actions' POST/XSS protections.
- Generated model `Row` properties use `long` for signed database `bigint`, `ulong` for unsigned `bigint`, `decimal` for decimal/numeric metadata, and `DateTimeOffset` for offset-aware columns. The generator emits `[DBName]` when it must normalize a column into a safe, unique C# property identifier and escapes both attribute strings and database column comments used as XML documentation. Nullable text columns use `string?`; required text uses `string` initialized to `string.Empty`. Identity and computed columns remain present in the typed read shape; computed fields stay excluded from generated save fields.

## Manual creation from the demo module
Use this fallback only under the conditions above; otherwise use the built-in CLI. To proceed manually, replicate what the generators do:

1. **Database table**
   - Define the table in `osafw-app/App_Data/sql/database.sql` and add a migration under `osafw-app/App_Data/sql/updates/` for environments that need incremental updates. MySQL deployments can override same-named scripts under `osafw-app/App_Data/sql/mysql/updates/`; SQLite deployments use `osafw-app/App_Data/sql/sqlite/database.sql` and `osafw-app/App_Data/sql/sqlite/updates/`.
   - Keep naming consistent: snake_case plural table names (e.g., `orders`), include system columns (`status`, `add_time`, `add_users_id`, `upd_time`, `upd_users_id`) for built-in behaviors.

2. **Model class**
   - Copy `osafw-app/App_Code/models/DemoDicts.cs` (or `DemosDemoDicts.cs` for junction tables) to a new file named after your model.
   - Update `table_name`, optional field mappings (`field_id`, `field_iname`, `field_status`, etc.), and row properties to match your columns.
   - Add helper methods (select options, validations, derived calculations) similar to `Demos` and related demo models. Follow [framework naming conventions](naming.md) for helper names.

3. **Controller**
   - Copy the closest demo controller (static: `AdminDemosController`; dynamic: `AdminDemosDynamic` or `AdminDemosVue`) and rename the class/file.
   - Adjust `base_url`, `required_fields`, `save_fields`, and related model wiring in `init`. Tailor list/show/showform logic and validation to your schema.
   - Expose extra actions (autocomplete, file uploads, junction updates) as needed by your feature.

4. **Templates and config**
   - Duplicate the matching folder under `osafw-app/App_Data/template/admin/` (for example, `demos` or `demosdynamic`) to a folder named after your controller URL.
   - Replace hardcoded titles/URLs inside `url.html`, `title.html`, and other snippets. Update `config.json` so `save_fields`, list columns, and lookup dropdowns mirror your schema and foreign keys (see [dynamic controller config](dynamic.md)).
   - Prune unused partials or fields in `index/`, `show/`, and `showform/` templates and keep layout hooks (return URLs, list filters, buttons) aligned with your controller logic.
   - Check [design_system.html](design_system.html) before adding custom styles; prefer shared page headers, `.fw-card`, `.fw-list-card`, list filters, and theme tokens.

5. **Navigation and permissions**
   - Either add a static link to the admin sidebar template or insert/update a `menu_items` row that points to the controller URL and display name so the sidebar shows your module.
   - Confirm `access_level` on the controller (e.g., `Users.ACL_MANAGER` or, with RBAC enabled, `Users.ACL_VISITOR` gated by roles) matches who should reach the module and ensure any lookup controllers are registered in `fwcontrollers` if they support dropdowns.

Following these steps replicates what the Developer Tools automate while letting you tailor every file.
