# Changelog

This changelog records breaking upgrade changes for end-user apps based on this framework. It is organized by commit date. Commits since 2025-06-01 were reviewed; changes not listed here were treated as additive, internal, documentation-only, or bug/security fixes that should not require app code, template, config, data, or schema changes.

## 2026-06-08

- Breaking: request hosts must match the configured `appSettings.ROOT_DOMAIN` host or an explicit full-string `appSettings.override.*.hostname_match` pattern before framework dispatch. Arbitrary wildcard host patterns are ignored for trust, unconfigured hosts receive a diagnostic `Bad Host` 400 response, and password/security email origins now come from the configured or matched canonical origin instead of an arbitrary request Host.
- Breaking: passwordless developer login now requires `IS_DEV`, a `Development*` config override, blank or `Development*` runtime environment, localhost/loopback Host, and loopback remote IP. Developer overrides such as `DevelopmentOleg` can continue using different localhost ports, but beta/live IIS deployments cannot enable the shortcut through Host selection alone.
- Breaking: `/Dev/Configure/(InitDB)` now requires POST plus the current XSS token while still allowing anonymous `IS_DEV` bootstrap. Public `/Dev/Configure` diagnostics remain available, but rendered DB connection/table and path guidance no longer includes raw exception text, DB host details, connection strings, or filesystem log paths.
- Breaking: `/Dev/Configure/(ApplyUpdates)` no longer executes pending SQL updates from a browser GET. Dev Home now redirects to read-only `/Dev/Configure/(PendingUpdates)`, and `/Admin/FwUpdates` applies pending updates through POST-only `/Dev/Manage/(ApplyPendingFwUpdates)` with the current XSS token.

## 2026-06-07

- Breaking: AdminUsers privileged mutations now enforce hierarchy; non-Site Admins cannot save, delete, restore, reset, or bulk-delete users with equal/higher access and cannot grant equal/higher `access_level`.
- Breaking: user-submitted activity-log comments now derive `users_id` from the current session and require model-level view access to the target record; forms or API callers that posted another `users_id` can no longer impersonate comment authors. The built-in `demos` sample entity remains commentable through an explicit default allowlist.
- Breaking: Data Protection key XML persisted in `fwkeys` is now protected with Windows DPAPI by default; non-Windows deployments must intentionally enable the source-level local/dev plaintext fallback or add a platform key-protection strategy before startup.
- Breaking: in test mode, non-empty `appSettings.test_email` now takes priority over the logged-in user's session email as the delivery sink; leave `test_email` blank to keep routing test emails to the current session user.
- Breaking: framework route/action authorization now treats URL controller/action casing case-insensitively; apps cannot rely on duplicate controllers, actions, virtual-controller icodes, route rules, or access rules that differ only by case.
- Breaking: sample configuration now uses `appSettings.access_levels`, and framework XSS exclusion now honors `appSettings.no_xss_prefixes`; apps carrying typoed `accesss_levels` or `no_xss_prefixes_prefixes` keys should rename them.
- Breaking: the default member dashboard keeps the same sample panes visible, but lower-access users see conventional current-user scoped page/upload/user/activity counts, charts, and latest events. Existing pane links still rely on normal access checks when clicked, and sample aggregates such as `Users by Type` should be replaced or scoped by production apps with app-specific predicates.
- Breaking: active framework request/session/body/DB logging no longer writes raw form/session/JSON payloads, email bodies, or DB parameter values unless `appSettings.log_pii=true`; production and shared configs should keep the default `false`.
- Compatibility: the sample `Development` config override sets `appSettings.log_pii=true` so local debug logs include full SQL parameters and other guarded debug context.
- Breaking: sample Sentry defaults now disable `SendDefaultPii` and request-body capture (`MaxRequestBodySize=None`) so deployments must opt in explicitly if they need richer Sentry request context.
- Compatibility: S3-backed attachments still default to `att.icode` object keys (`att/{icode}/{icode}[_{size}]`), but apps with existing id-based buckets can set `S3.IS_ATT_KEY_BY_ID` to keep using legacy `att/{id}/{id}[_{size}]` keys during framework upgrades.
- Compatibility: the default attachment "Open" path now serves trusted PDF uploads inline for browser preview; explicit `/Att/Download/{icode}` links still force download, and browser-active uploads such as HTML/SVG/JavaScript/XML/CSS/WASM/XHTML still use inert download metadata.

## 2026-06-06

- Breaking: S3-backed attachments default to `att.icode` in S3 object keys (`att/{icode}/{icode}[_{size}]`) instead of numeric `att.id`; existing S3 objects stored under id-based keys must be copied/migrated or the app must set `S3.IS_ATT_KEY_BY_ID`.
- Breaking: `Att.getUrl()` now returns the framework `/Att/{icode}` route for S3-backed attachments so signed S3 redirects are issued only after attachment authorization; app code expecting an immediate presigned S3 URL must call through the authorized attachment route or explicitly sign after its own access check.
- Breaking: direct object-bound attachments (`att.fwentities_id > 0`) now require authorization through the parent model's `checkAccess()` before local serving, S3 redirect, preview fallback, or same-target link/use decisions; `att_links` remain reusable references and do not make an otherwise unbound library attachment private.
- Breaking: the default attachment pipeline serves inline only for safe raster image uploads and trusted PDF uploads; text files, browser-active types such as HTML/SVG/JavaScript/XML/CSS/WASM/XHTML, and other non-image uploads are forced to download with inert content metadata unless an app implements a separate trusted-serving path.
- Breaking: image uploads are rejected before thumbnail work when file size, dimensions, pixel count, or image headers exceed safe decode limits; apps that intentionally accepted very large images must resize before upload or adjust the framework limits.
- Breaking: S3 upload and signed redirect metadata now uses server-selected attachment content type and disposition instead of browser-supplied upload MIME for active content.

## 2026-06-05

- Breaking: dynamic `att_links_edit`, dynamic/Vue saves, dynamic `SaveAttFiles`, and existing subtable-edit saves now prove the current parent row and child/link ownership before side effects. Apps with custom dynamic ownership or tenant scopes must make controller `modelOneOrFail`, model `one`, and `checkAccess` predicates reflect that scope.
- Breaking: ParsePage markdown disables raw HTML and arbitrary markdown attributes by default. Use `markdown="trusted"` only for server-controlled or already-sanitized markdown; dynamic markdown fields need `"trusted": true`, Vue custom list renderers need `view_list_custom_trusted`, and markdown editor previews need `data-markdown-trusted="1"` or `markdown-trusted` for trusted raw HTML behavior.
- Breaking: non-Site Admins can no longer save static-page executable fields (`custom_head`, `custom_css`, `custom_js`); upgrades relying on lower-access page editors to change those fields must adjust roles or workflow.

## 2026-06-04

- Breaking: the report runtime/base class was renamed from `FwReports` to `FwReportsBase`; hardcoded reports must inherit `FwReportsBase`, and helper callers must use `FwReportsBase.createInstance()`, `FwReportsBase.createHtml()`, and `FwReportsBase.createFile()`.
- Breaking: `FwReports` is now the framework table model for the `fwreports` table. Existing apps upgrading custom reports must apply `osafw-app/App_Data/sql/updates/upd2026-06-03-custom-reports.sql`, copy the new report templates, and update app-specific hardcoded report classes/callers for the `FwReportsBase` rename.

## 2026-06-03

- Breaking: custom mutating actions hardened with `enforcePost()` now require POST plus a valid XSS token. Affected framework actions include report email sending, admin user login-as/password/MFA/password-hash maintenance, Dev Manage menu deletion, and My Lists toggles/add/remove; app templates/custom JS calling similar routes via GET must switch to POST and include `XSS`.
- Breaking: user-owned preferences (`UserLists`, `UserViews`, `UserFilters`) now enforce owner-or-system predicates in `one`/save/delete paths. Direct-id access to another user's list/view/filter now returns not found/denied, and non-admin users cannot create or modify system views/filters.
- Breaking: redirect targets for login `gourl`, static pages, and Assistant redirects now must pass the app-local URL policy. External redirect workflows need an explicit trusted path/allowlist.

## 2026-05-26

- Breaking: `<~/common/modal>` now loads cacheable `/assets/js/fw-modal.js` and disposes scoped component/plugin state before modal content replacement/removal. Apps overriding `common/modal.html`, using stale assets, or custom modal scripts must include/sync the new asset and account for `fw.disposeComponents(scope)`.

## 2026-05-20

- Breaking: optional feature switches moved to `osafw-app/osafw-app.csproj` `DefineConstants` (`isSQLite`, `isMySQL`, `isWindowsAuth`, `isFwCronService`, `isSentry`, `isExcelDataReader`, etc.) with conditional package references, and `Program.cs` now uses `AddFwSessionCache(connStr, dbType)`. Apps that enabled providers/features by uncommenting package references or code blocks must migrate to compile symbols.
- Breaking: DB paging helpers now validate offset paging arguments. Calls to `DB.limit(sql, limit, offset)` need `ORDER BY` for SQL Server offset paging; `DB.array(...)`/`array<T>(...)` offset paging requires both a deterministic `order_by` and a non-negative `limit`.
- Breaking: DB helper reads/writes treat fields ending in `_utc` and `datetimeoffset` fields as instant values. Apps that used `_utc` column/parameter names for non-UTC local datetimes must rename fields or migrate data.
- Breaking: `libman.json` switched the default provider from `unpkg` to `cdnjs` and changed several library file paths. App forks that restore or merge LibMan assets must keep the new per-library provider overrides and file paths.

## 2026-05-18

- Breaking: raw SQL date/time parameters named with an `_utc` suffix, or passed as `DateTimeOffset`, now skip database-timezone conversion and are handled as UTC/offset-aware instants. Apps relying on raw SQL to store local time under `_utc` parameter names must adjust names or values.
- Breaking: browser-native `datetime-local` strings are now parsed as user-local wall time during model input conversion. Custom Vue/dynamic form code that posted these strings as already-UTC values must remove the extra conversion.

## 2026-05-13

- Breaking: lookup option helpers (`listSelectOptions`, name-valued variants, autocomplete, multicheckboxes) now return active rows by default and include inactive rows only when they are already selected on the edited record. Apps that intentionally showed inactive lookup values broadly must override the lookup model method or adjust status filters.

## 2026-05-12

- Breaking: ParsePage `number_format` and `currency` conversions now use `toDouble()` instead of `toFloat()`. Apps depending on single-precision rounding/overflow behavior should verify formatted numeric output.

## 2026-05-11

- Breaking: typed DB/model single-row helpers (`DB.row<T>`, `DB.rowp<T>`, `FwModel<TRow>.oneT*`) now return `null` for missing records instead of a new/default DTO. Callers must handle nullable rows or use `*OrFail` when the record is required.
- Breaking: ParsePage file-template recursion is capped at 100 nested file includes. Intentionally deep recursive templates now stop rendering beyond the limit and log a warning instead of recursing indefinitely.

## 2026-03-10

- Breaking: `FwCron` no longer runs jobs immediately just because `start_date` is empty; it initializes `start_date`/`next_run` and waits for the next matching CRON occurrence. Apps relying on immediate first execution must trigger the first run manually or seed schedule fields.
- Breaking: `FwCron` now advances `next_run` from the run start time and marks jobs completed when no future occurrence is available within `end_date`. Long-running jobs and finite schedules should be rechecked after upgrade.

## 2026-02-25

- Breaking: RBAC resources for the old lookup manager are migrated to `AdminLookups`. Apps using roles must apply `osafw-app/App_Data/sql/updates/upd2026-02-25.sql` so permissions formerly on `AdminLookupManager`/`AdminLookupManagerTables` continue under `AdminLookups`.

## 2026-01-20

- Breaking: shared Vue app/store setup moved into `common/vue/app_core.js` and `common/vue/store_core.js`, and `AppUtils` moved from `layout/vue/apputils.js` to `/assets/js/apputils.js` loaded by `layout/vue/sys_footer.html`. Apps with copied Vue layout/store templates or direct `fwStore.formatDate*`/`timeToSeconds` calls must sync to `createFwApp`, `buildFwStore`, and `AppUtils.*`.
- Breaking: `layout/vue/apputils.js` was removed. Custom import maps, copied Vue layout templates, or direct references to that path must use `/assets/js/apputils.js`.

## 2026-01-19

- Breaking: `theme1.css` was removed/replaced during theme cleanup/pink-theme work. Apps selecting or overriding the legacy theme 1 stylesheet must move to a supported theme stylesheet.
- Breaking: common JS components such as bootstrap-select and the modal loader moved toward `fw.initComponent`/component-scoped initialization. Apps overriding these common partials or loading plugin controls inside replaced modal content should sync component includes and initialization hooks.

## 2026-01-16

- Breaking: dashboard charts switched from Chart.js to Apache ECharts. Custom dashboard panel templates/scripts that instantiate `Chart`, use `<canvas id="chart_*">`, or override `main/index/theme*.js` Chart defaults must migrate to ECharts helpers (`dashboardChartConfig`, `initDashboardChart`) and the `echarts` asset.

## 2026-01-14

- Breaking: common one-line template partials moved/renamed. Attribute partials moved from `common/*` to `common/attr/*` (`checked`, `disabled`, `display_none`, `multiple`, `required`, `selected`), class partials moved to `common/cl/*` (`active`, `disabled`, `hide`, `selected`, `text_end`, etc.), and `common/clactive.html` / `common/error.html` were removed. Custom templates must update include paths.

## 2026-01-12

- Breaking: list-card styling switched from `.fw-card` to `.fw-list-card`, and shared CSS/theme tokens were reorganized around `data-bs-theme`/framework CSS variables. Apps with custom CSS targeting old list/form/card/sidebar/table utility classes should retest and update selectors.

## 2026-01-07

- Breaking: status-filter table-button partial `common/list/filter_std_status_export.html` was renamed to `common/list/filter_std_status_tbuttons.html`, and filter action/table-button markup moved beside search controls. Custom list/filter overrides that include the old partial or assume the old toolbar placement must sync.

## 2026-01-05

- Breaking: list toolbar/table-control buttons moved from `common/list/th_filter_customize.html`/table header area into filter action partials (`filter_actions`, `filter_table_buttons`, `filter_actions_static`, `filter_table_buttons_static`). Custom list/filter overrides that expected controls in the old table header need to sync partials.

## 2025-12-26

- Breaking: shared page headers replaced per-screen title/button patterns. List/form/show templates now use `common/list/page_header`, `common/form/page_header`, `page_header_edit`, and per-screen `page_header_actions*`; many `btn_std_more.html` override files were removed. Apps with custom templates overriding `nav_title`, `btn_top_save*`, `btn_std_more`, or manually-rendered `<h1><~title> (<~count>)</h1>` should migrate to page-header slots/partials.

## 2025-12-23

- Breaking: `FW.SessionHashtable(...)` was renamed to `FW.SessionDict(...)`. Custom code using the old helper must update.

## 2025-12-16

- Breaking: direct access to `FwConfig.settings` was replaced by `FwConfig.GetCurrentSettings()` / `FwConfig.GetCurrentSetting(name)` as host-scoped settings moved behind a cache. Custom startup/config code reading `FwConfig.settings` must update.

## 2025-12-15

- Breaking: framework public collection signatures migrated from `Hashtable`/`ArrayList` to `FwDict`/`FwList`/`DBRow`/`DBList`/typed list aliases. Legacy conversion helpers exist for compatibility, but overridden controller/model methods with old signatures and code casting framework results to `Hashtable`/`ArrayList` must be updated.

## 2025-12-08

- Breaking: typed model examples and generated typed models now use `FwModel<TRow>` with nested `Row` classes. Custom typed models using the early external DTO pattern such as `TDemos`, or old static typed conversion helper names, must migrate to the nested-row/extension-method pattern.

## 2025-12-03

- Breaking: datetime values are normalized as UTC internally and converted to/from configured DB timezone (`appSettings.db.main.timezone`) on DB helper reads/writes; SQL `date` values stay calendar-stable. Existing apps storing local naive datetimes must set the DB timezone explicitly and verify old data before/after migration.

## 2025-11-17

- Breaking: projects target `net10.0` with nullable enabled and package updates. End-user apps must build with the .NET 10 SDK/runtime and address nullable/API/package changes in custom code.

## 2025-11-12

- Breaking: password reset moved from `/Password/Reset` templates/routes to `PasswordReset` (`App_Data/template/passwordreset/index`). Custom reset links/templates/controllers using `/password/reset` must update to the new route/template folder.
- Breaking: default list export page size changed from `100000` to controller field `list_pagesize_export` defaulting to `10000`. Apps relying on larger single export batches must override `list_pagesize_export`.

## 2025-09-09

- Breaking: `HttpMiddleware.cs`, `MyHandlerMiddleware`, and `UseMyHandler()` were removed; equivalent CORS preflight, Windows auth challenge, and `FW.run` handling now live inline in `Program.cs`. Apps that customized/referenced that middleware must port changes to `Program.cs`.

## 2025-09-08

- Breaking: per-user date/time formats/timezones were added. The `users` table needs `date_format`, `time_format`, and `timezone` from `osafw-app/App_Data/sql/updates/upd2025-09-02.sql`; form save/display code should use `FwModel.convertUserInput` and `fw.formatUserDateTime`. Apps with custom date parsing/formatting must verify display/save behavior.

## 2025-09-02

- Breaking: `.on-refresh` now posts the triggering element's `id`/`name` in hidden `refresh` instead of always `1`; server-side form logic comparing exactly `"1"` should also accept the control id/name or treat the value as truthy.
- Breaking: `toBool()` now treats trimmed `"true"`/`"false"` through `bool.TryParse`, then treats any non-empty string other than `"0"` as true. Apps depending on arbitrary non-empty strings converting to false must update checks.

## 2025-08-27

- Breaking: sidebar/layout templates were refactored during the sidebar cleanup. Apps overriding `layout/sidebar*` or relying on old sidebar DOM/classes need to sync templates and CSS.

## 2025-08-18

- Breaking: Chart.js was upgraded to v4. Custom templates/scripts still using Chart.js v2/v3 APIs may need migration. Later dashboard defaults moved to ECharts on 2026-01-16.

## 2025-07-14

- Breaking: `bootstrap-simple-autocomplete` upgraded from 1.1.0 to 2.0.0. Custom autocomplete integrations relying on old plugin behavior/events/options should be tested/updated.

## 2025-07-09

- Breaking: report PDF export template changed from `/admin/reports/common/pdf.html` to `/layout_print.html`. Custom report PDF layouts/styles that override the old template must move to or adjust `layout_print.html`.

## 2025-06-17

- Breaking: ASP.NET hosting migrated from `Startup.cs` to minimal hosting in `Program.cs`; `Startup.cs` was removed. Apps with custom service/middleware setup in `Startup` must port it to `Program.cs`.
- Breaking: schema update scripts `osafw-app/App_Data/sql/updates/upd2025-03-26.sql`, `upd2025-04-01.sql`, `upd2025-05-20.sql`, and `upd2025-06-16.sql` are required for upgrades from older apps, including framework update tracking, session/data-protection tables, page fields, and user/email schema changes.
- Breaking: lookup manager controllers/models/templates `AdminLookupManager`, `AdminLookupManagerTables`, `LookupManager`, `LookupManagerTables`, and `admin/lookupmanager*` were removed/replaced by `AdminLookups` and `admin/lookups`. Update URLs, RBAC resources, templates, and custom references.
- Breaking: public attachment URLs switched to `icode` identifiers instead of numeric `id`; stored links/custom code using numeric `/Att/{id}` style routes need to regenerate/update URLs.
- Breaking: `DB` no longer stores an `FW` reference and its constructor no longer accepts `FW`; use `fw.getDB(...)` for framework-managed DB contexts or `new DB(conf, dbName)` / `new DB(connStr, type, dbName)` plus `setLogger`/`setContext` when needed.
- Breaking: `ParsePage` construction was decoupled from `FW`; custom code constructing parsers directly must pass `ParsePageOptions` or use `fw.parsePage(...)`/`fw.parsePageInstance()`.
- Breaking: form validation errors moved from template key `ERR[...]` to `error[details][...]`. Custom forms/templates and JSON handlers reading `ERR` must update.
- Breaking: CSV/Excel import helpers were consolidated from `Utils.importCSV`/`Utils.importExcel` into `Utils.ImportSpreadsheet` (requires the ExcelDataReader feature when enabled). Custom import code must call the new helper and row callback shape.
- Breaking: image helpers moved from `Utils` to `ImageUtils`; custom calls such as `Utils.resizeImage`/image rotation helpers must use `ImageUtils`.
- Breaking: file helpers such as `FW.setFileContent` moved to `Utils.setFileContent` and related `Utils` file helpers. Custom utility code using the old static location must update.
- Breaking: report Excel export now emits real `.xlsx` via `ConvUtils.exportNativeExcel`; custom code expecting old `.xls` HTML output/extension should update.
- Breaking: PDF generation switched from wkhtmltopdf configured by `pdf_converter`/`pdf_converter_args` to Playwright/Chromium with Letter defaults. Deployments must allow Playwright browser install or preinstall Chromium, and PDF CSS/layout should be verified.
- Breaking: `FwRoute`, framework exceptions, and logging types were split out of `FW.cs` / logging refactored to `FwLogger`; direct references to nested/old locations or `FW` logger internals need namespace/API updates.
- Breaking: Vue simple store helper `useFwStore` was renamed to `fwStore`. Custom Vue simple templates using `useFwStore` need update.
- Breaking: front-end assets were refreshed, including Bootstrap 5.3.6 and removal of the old `typeahead.js-bootstrap.css`. Custom themes/plugins relying on old Bootstrap or typeahead asset paths/classes should retest/update.
- Breaking: layout CSP now has explicit `script-src` and `connect-src` directives. Custom external scripts, analytics, WebSocket, or API endpoints must be added through the appropriate CSP template variables.
