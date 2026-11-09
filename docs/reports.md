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

Reports use the main framework database by default. To run a hardcoded report against another configured connection, set `db_config` in the report class constructor. The value must be the name of an `appSettings.db` entry, the same as model `db_config`.

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

Custom report definitions remain stored in the framework `fwreports` table on the main database. Developers can set `db_config` in `FwCustomReport` to force every custom report's SQL and SQL/table-based parameter lookups to use a named configured connection instead. This is intended for read-only DB credentials as an extra protection layer around UI-authored SQL.

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

## PDF export with Microsoft Edge

`ConvUtils.html2pdf` prints trusted static HTML using installed Microsoft Edge's headless command line. `parsePagePdf` and report download/file output keep their existing entry points. The application has no Playwright or Node.js dependency and does not install browsers. Unrelated UI tests retain their own test-only Playwright dependency.

The `FwConfig` default for `pdf_converter` is empty, which discovers Edge under Program Files (x86), Program Files, or Local AppData on Windows. For an application-specific path, assign `defaults["pdf_converter"]` in `FwHooks.configure`. Deployment configuration can still override it through `appSettings.pdf_converter`. Verify the actual server image has Edge and that the IIS account can execute it, write to its temporary directory, and write the destination. Windows Server 2022 Desktop Experience is the primary deployment target; IIS execution and other operating systems must be checked on the target deployment.

Each conversion writes temporary HTML, runs headless Edge, and accepts only a completed PDF from that invocation. A 60-second timeout bounds the application's wait for printing. Edge may finish its launcher before its printing child, so exit code zero alone is not treated as success. HTML and staging PDF files use `Utils.getTmpFilename()` with `.html`/`.pdf` extensions in the existing shared framework temporary folder. Each is deleted once on completion; `parsePagePdf` calls `Utils.cleanupTmpFiles()` in its final cleanup, including saved-file output and conversion failures, to remove older leftovers. No per-report directories or directory-cleanup helpers are needed. Edge chooses its own temporary headless profile when no `--user-data-dir` is supplied; browser-internal temporary folders are outside the framework file sweep. Termination on timeout is best effort while the owned launcher is still alive; if Edge has handed off printing and exited that launcher, this lean implementation cannot guarantee termination of a stalled printing child. There is no persistent browser, pool, service, or debugging port.

### Print options

| Feature | Edge command-line implementation |
| --- | --- |
| Letter paper and `landscape` | CSS `@page { size: Letter portrait/landscape; }` |
| `margin_top`, `margin_right`, `margin_bottom`, `margin_left` | CSS page margins; defaults remain 5mm, 10mm, 5mm, 10mm; nonnegative mm/cm/in/px lengths or zero |
| `scale` | CSS `body { zoom: ...; }`; default 0.8, accepted range 0.1 to 2; check copied-app layout because CSS zoom is not the browser PDF scale API |
| Background colors/images | Print color adjustment plus Edge PDF printing; verified with Bootstrap 5 table stripes |
| Page breaks and table headings | Normal print CSS, including `break-before: page` and repeating table headers |
| Tagged output | Current Edge CLI produces tagged PDFs; verified on the installed version |
| Headers, footers and page numbers | CSS page-margin boxes as below |
| JavaScript | Disabled for the report document; reports must be fully rendered by the server |

`parsePagePdf` supplies `PDF_ASSETS_URL`, a file URI for `<site_root>/wwwroot/assets`. The standard print layout loads Bootstrap and site CSS from that directory, without calling the website or relying on its HTTPS certificate. Normal browser print views retain their website asset URLs.

`parsePagePdf` validates the print options and supplies them as `PDF_OPTIONS` before rendering. Both reports and Developer Docs use the normal print layout, whose `layout/sys_head_print.html` includes the single `admin/reports/common/pdf.html` fragment for PDF output. That fragment owns Letter paper, orientation, margin/scale defaults, print colors and the script policy. Customize print defaults there. Custom print layouts should include it inside their head, after other styles and before any scripts.

`html2pdf(fw, html, filename)` only prints the supplied HTML unchanged. It does not detect, repair or wrap HTML, apply options, or inject CSS/script policy. Pass print options to `parsePagePdf`; callers using the low-level method must supply their own completed document and print rules. More-specific template CSS can still affect layout; verify actual copied-app templates.

For trusted local images, use an absolute file URI, for example `new Uri(absoluteImagePath).AbsoluteUri`, then HTML-escape it in the template. CSS and fonts can likewise use absolute file URLs. Data URLs are also supported. Relative URLs resolve beside the temporary HTML, so use absolute URLs instead. Remote assets remain possible in ordinary reports. This is trusted-document rendering, not an isolation boundary: do not pass arbitrary user HTML or user-selected local paths. Missing or undecodable assets are not individually validated by this lean converter and may print blank.

Example print CSS (reserve adequate margins using the corresponding options):

```css
@page {
  @top-left {
    content: "Example company";
    font: bold 10px Arial;
  }
  @bottom-left {
    content: "Confidential report";
    font: 10px Arial;
  }
  @bottom-right {
    content: "Page " counter(page) " of " counter(pages);
    font: 10px Arial;
  }
}
```

Use `margin_top = "20mm"` and `margin_bottom = "20mm"` for this example. Page-margin content is CSS generated content, not arbitrary HTML. CSS page-margin boxes require a sufficiently recent Chromium-based Edge (Chromium 131 or newer). See [Chromium's page-margin documentation](https://developer.chrome.com/blog/print-margins).

Rendering failures before the destination copy preserve an existing destination. Successful output is copied into the destination to retain compatibility with existing Windows readers that allow writes but deny replacement. This copy is not atomic. Download PDFs are removed after `fileResponse` completes when possible, with the framework stale-file sweep retrying old locked or interrupted report files. The DEBUG logger records the executable and quoted arguments immediately before launch; it does not log the report HTML or data.

Run focused verification with `pwsh -NoProfile -File scripts/test.ps1 -Profile Browser -Filter 'FullyQualifiedName~PdfEdgeTests'` and `pwsh -NoProfile -File scripts/test.ps1 -Filter 'FullyQualifiedName~ConvUtilsTests'`. Edge tests require installed Edge and do not use Playwright. The Sample Report places the local site logo beside its title and uses repeated table headings and CSS page numbers; it returns up to 250 real matching activity rows, so multiple pages require enough rows in the selected date range. No synthetic rows are inserted into the database.

## Viewer-safe report metadata

Custom report `custom_report` page state contains only `id`, `icode`, `iname`, `idesc`, and `icon`. SQL templates, stored parameter/render definitions, and future administrative fields never enter that object, including JSON exports. Both outward parameter collections expose only rendered field metadata (`name`, `label`, `type`, `required`, `value`, and normalized `id`/`iname` options); lookup SQL/source and arbitrary stored properties stay internal. The normal result rows and generated filter controls remain available. Custom templates must not depend on administrative definitions in viewer page state.
## Scheduled report emails

Site Admins can save recurring PDF/HTML email delivery from a report's **Schedule email** control and manage its tasks through **Report schedules**. See [Scheduled Tasks](cron.md) for enabling the hosted worker, timezone and missed-run behavior, permissions, execution results, and interrupted-send recovery.

Scheduled values use parameter rows with `name`, `type`, and `value`, matching the existing custom-report metadata/runtime shape. Current definitions remain authoritative for validation. Captured values preserve their selected meaning: fixed dates stay fixed, and only explicit report-owned period selectors are recalculated. Recipient addresses are saved literally and receive one shared report generated as the selected Site Admin.

Hardcoded reports must opt into unattended scheduling and declare the inputs they can persist. Custom reports reuse their parameter definitions. Review schedules when changing a report's code, parameter contract, or data scope; do not infer recipient-specific permissions from the address list.
