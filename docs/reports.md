# Reports

The Reports module lives at `/Admin/Reports` and supports two report types:

- hardcoded reports implemented as `FwReportsBase` subclasses;
- custom reports stored in `fwreports` and managed by Site Admins.

Hardcoded reports and custom reports share one route-code namespace. Hardcoded reports are resolved first, and custom report saves reject `icode` values that collide with a compiled report class.

## Hardcoded Reports

Create a report class under `osafw-app/App_Code/models/Reports` and inherit from `FwReportsBase`.

Common overrides:

- `setFilters()` defines default filter values and lookup data.
- `getData()` builds `list_rows`, `list_count`, and any extra template values.
- `saveChanges()` supports editable reports when needed.

Templates live under `osafw-app/App_Data/template/admin/reports/{code}`. A typical report has:

- `title.html`
- `main.html`
- `list_filter.html`
- `report_html.html`

The framework supports HTML, CSV, PDF, XLS, XLSX, and JSON through `FwReportsBase.render()`, `FwReportsBase.createHtml()`, and `FwReportsBase.createFile()`.

## Custom Reports

Site Admins can create custom reports from `/Admin/Reports/new`. New reports default to Site Admin-only access and a suggested `repN` code.

Custom reports use:

- `icode`: route code used in `/Admin/Reports/{icode}`;
- `iname`: report title;
- `icon`: optional Bootstrap Icons suffix, such as `currency-dollar`;
- `access_level`: minimum user access level to run the report;
- `sql_template`: one read-only SQL query;
- `params_json`: optional parameter metadata;
- `render_options_json`: optional row limits, timeout, and export options;
- `status`: active, inactive, or deleted.

Custom reports render through a generic table. They do not need per-report templates. Numeric columns are right-aligned, and the table footer sums numeric columns except identifier/status-style fields such as `id`, `*_id`, and `status`. Generic column sorting applies only to displayed/materialized rows after the configured row limit has been read.

## SQL Rules

Custom report SQL must:

- start with `SELECT` or `WITH`;
- be one statement without semicolons;
- use `@param` placeholders for dynamic values;
- avoid write/admin statements such as `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `DROP`, `ALTER`, `CREATE`, `TRUNCATE`, `EXEC`, `GRANT`, `REVOKE`, `BACKUP`, `RESTORE`, `SELECT INTO`, and `sp_`/`xp_` system-proc style names.

Runtime values are bound as DB parameters. Do not concatenate user input into SQL. The read-only SQL contract is validated when a report is saved and again immediately before preview or runtime execution, so tampered stored SQL still cannot execute outside the SELECT/CTE subset.

Site Admin report authors may read any table reachable by the app DB connection and may choose render limits in `render_options_json`, but report SQL must remain read-only. The runtime stops reading after the configured row limit for every custom report. Simple `SELECT` queries also receive a provider SQL limit. For CTE queries, include an explicit provider-specific limit when possible so the database can avoid extra work.

Runtime SQL errors are shown on the report screen instead of the framework error page. Site Admins see database error details; other users see a short contact-administrator message.

Report output includes generated time, report code, applied non-empty parameters, rows shown, and the row-limit or preview-limit context so printed and exported results remain self-describing.

## Parameters

Use `@param_name` placeholders in SQL. `params_json` can be omitted; missing metadata is generated automatically.

Example:

```json
[
  {"name":"from_date","label":"From date","type":"date","default":"-30d"},
  {"name":"to_date","label":"To date","type":"date","default":"today"},
  {"name":"users_id","label":"User","type":"lookup","source":"users"},
  {"name":"s","label":"Search","type":"text"}
]
```

Supported types:

- `text`
- `int`
- `number`
- `date`
- `datetime`
- `lookup`
- `lookup_table`
- `lookup_model`
- `lookup_sql`
- `lookup_tpl`

`datetime` filters accept either a full user-formatted date/time or a date-only value; date-only values bind as midnight for that date.

Lookup options can be static:

```json
{
  "status": {
    "label": "Status",
    "type": "lookup",
    "options": {
      "0": "Active",
      "10": "Inactive"
    }
  }
}
```

Use the split lookup types when options come from framework sources:

```json
[
  {"name":"demo_dicts_id","label":"Demo Dict","type":"lookup_table","source":"demo_dicts"},
  {"name":"users_id","label":"User","type":"lookup_model","source":"Users"},
  {"name":"users_id","label":"User","type":"lookup_sql","source":"SELECT id, email AS iname FROM users WHERE status=0"},
  {"name":"is_active","label":"Is Active","type":"lookup_tpl","source":"/common/sel/yn.sel"}
]
```

`lookup_table` expects a table with `id` and `iname` columns. `lookup_model` calls the model's `listSelectOptions()` method. `lookup_sql` must be one safe read-only SQL statement returning `id` and `iname`. `lookup_tpl` reads an existing `.sel` template.

## Render Options

`render_options_json` is optional.

Common options:

```json
{
  "row_limit": 1000,
  "preview_limit": 50,
  "timeout_seconds": 30,
  "landscape": true,
  "pdf_filename": "report",
  "xls_filename": "report"
}
```

## Access

Creating, editing, previewing, and deleting custom reports is Site Admin-only.

The Reports controller is available to logged-in users so lower-access custom reports can be reached. Hardcoded reports still enforce their own report-class access checks.

Running a custom report checks:

1. the report `access_level`;
2. optional roles access for resource `{icode}Report` when roles are enabled.

When a custom report is saved and roles are enabled, the matching resource is created or updated automatically. Role grants are intentionally manual. When a report is deleted, the resource is marked deleted rather than hard-deleted.
