# Reports

The Reports module lives at `/Admin/Reports` and supports two report types:

- hardcoded reports implemented as `FwReports` subclasses;
- custom reports stored in `fwreports` and managed by Site Admins.

Hardcoded reports are resolved first. If a report code does not match a compiled report class, the framework looks for an active custom report with the same `icode`.

## Hardcoded Reports

Create a report class under `osafw-app/App_Code/models/Reports` and inherit from `FwReports`.

Common overrides:

- `setFilters()` defines default filter values and lookup data.
- `getData()` builds `list_rows`, `list_count`, and any extra template values.
- `saveChanges()` supports editable reports when needed.

Templates live under `osafw-app/App_Data/template/admin/reports/{code}`. A typical report has:

- `title.html`
- `main.html`
- `list_filter.html`
- `report_html.html`

The framework supports HTML, CSV, PDF, XLS, XLSX, and JSON through `FwReports.render()`, `FwReports.createHtml()`, and `FwReports.createFile()`.

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

Custom reports render through a generic table. They do not need per-report templates. Numeric columns are right-aligned, and the table footer sums numeric columns except identifier/status-style fields such as `id`, `*_id`, and `status`.

## SQL Rules

Custom report SQL must:

- start with `SELECT` or `WITH`;
- be one statement without semicolons;
- use `@param` placeholders for dynamic values;
- avoid write/admin statements such as `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `DROP`, `ALTER`, `CREATE`, `TRUNCATE`, `EXEC`, `GRANT`, `REVOKE`, `BACKUP`, `RESTORE`, `SELECT INTO`, and `sp_`/`xp_` system-proc style names.

Runtime values are bound as DB parameters. Do not concatenate user input into SQL.

The runtime stops reading after the configured row limit for every custom report. Simple `SELECT` queries also receive a provider SQL limit. For CTE queries, include an explicit provider-specific limit when possible so the database can avoid extra work.

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

Lookup `source` can also be `users`, `fwentities`, `log_types`, `model:Users`, or `sql:SELECT id, iname FROM ...`. Model-based lookups call the model's `listSelectOptions()` method, so use an existing model class name and keep the SQL placeholder aligned with the selected id field.

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
