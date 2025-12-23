# Creating Feature Modules

Feature modules bundle a database table, its model, controller, and templates. You can scaffold them automatically from **Developer Tools** or build them manually by copying demo assets.

## Quick path: Developer Tools at `/Dev/Manage`
1. **Add the table** to your schema: mirror the demo tables in `App_Data/sql/demo.sql`, then append the `CREATE TABLE` to `App_Data/sql/database.sql` and create a dated script under `App_Data/sql/updates/` for deployments.
2. **Open Developer Tools** at `/Dev/Manage` and use the *Create Model* form. Pick your table and optional model name; the action reads the schema and generates the model file for you.
3. **Create the controller** from the same screen. Select the model, provide a target URL/title, and choose controller type (dynamic, Vue, lookup, or API). The generator copies demo templates, rewrites URLs/titles, configures `config.json`, writes the controller class, and adds a menu item.
4. **Restart the project or apply hot reload**, then navigate to the new controller URL.
5. **Review and tweak `config.json`** in the generated template folder (see [dynamic controller config](dynamic.md)).
6. **Prune unused partials/includes** in the generated template folder so only the needed pages and widgets remain.

## How `/Dev/Manage` scaffolding works
- `CreateModelAction` converts the selected table into an entity description (`DevEntityBuilder.table2entity`) and passes it to `DevCodeGen.createModel`, which clones demo model templates and adjusts names/fields based on schema metadata.
- `CreateControllerAction` builds a temporary entity with the chosen model and controller options, loads `dev/db.json`, and calls `DevCodeGen.createController`. The generator copies the demo controller/templates (dynamic or Vue), rewrites URLs/titles, regenerates `config.json`, writes the controller class, and appends/updates `menu_items`.

## Manual creation from the demo module
If you need full control, replicate what the generators do:

1. **Database table**
   - Define the table in `App_Data/sql/database.sql` and add a migration under `App_Data/sql/updates/` for environments that need incremental updates.
   - Keep naming consistent: snake_case plural table names (e.g., `orders`), include system columns (`status`, `add_time`, `add_users_id`, `upd_time`, `upd_users_id`) for built-in behaviors.

2. **Model class**
   - Copy `App_Code/models/DemoDicts.cs` (or `DemosDemoDicts.cs` for junction tables) to a new file named after your model.
   - Update `table_name`, optional field mappings (`field_id`, `field_iname`, `field_status`, etc.), and row properties to match your columns.
   - Add helper methods (select options, validations, derived calculations) similar to `Demos` and related demo models.

3. **Controller**
   - Copy the closest demo controller (static: `AdminDemosController`; dynamic: `AdminDemosDynamic` or `AdminDemosVue`) and rename the class/file.
   - Adjust `base_url`, `required_fields`, `save_fields`, and related model wiring in `init`. Tailor list/show/showform logic and validation to your schema.
   - Expose extra actions (autocomplete, file uploads, junction updates) as needed by your feature.

4. **Templates and config**
   - Duplicate the matching folder under `App_Data/template/admin/` (for example, `demos` or `demosdynamic`) to a folder named after your controller URL.
   - Replace hardcoded titles/URLs inside `url.html`, `title.html`, and other snippets. Update `config.json` so `save_fields`, list columns, and lookup dropdowns mirror your schema and foreign keys (see [dynamic controller config](dynamic.md)).
   - Prune unused partials or fields in `index/`, `show/`, and `showform/` templates and keep layout hooks (return URLs, list filters, buttons) aligned with your controller logic.

5. **Navigation and permissions**
   - Either add a static link to the admin sidebar template or insert/update a `menu_items` row that points to the controller URL and display name so the sidebar shows your module.
   - Confirm `access_level` on the controller (e.g., `Users.ACL_MANAGER` or, with RBAC enabled, `Users.ACL_VISITOR` gated by roles) matches who should reach the module and ensure any lookup controllers are registered in `fwcontrollers` if they support dropdowns.

Following these steps replicates what the Developer Tools automate while letting you tailor every file.
