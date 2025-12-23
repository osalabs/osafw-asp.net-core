# ASP.NET Core C# web framework optimized for building Business Applications

Created as simplified and lightweight alternative to other ASP.NET frameworks like ASP.NET MVC

![image](https://user-images.githubusercontent.com/1141095/75820467-0200b380-5d62-11ea-9340-e0942b460eb1.png)

## Features
- simple and straightforward in development and maintenance
- MVC-like
  - code, data, templates are split
  - code consists of: controllers, models, framework core and optional 3rd party libs
- uses [ParsePage template engine](https://github.com/osalabs/parsepage) ([detailed docs](osafw-app/docs/parsepage.md))
  - data stored by default in SQL Server database [using db.net](https://github.com/osalabs/db.net) ([detailed docs](osafw-app/docs/db.md))
- RESTful with some practical enhancements
- flexible CRUD flows with `FwDict`/`FwList` or typed DTOs ([guide](osafw-app/docs/crud.md))
- integrated auth - simple flat access levels auth
- UI based on [Bootstrap 5](http://getbootstrap.com) with minimal custom CSS and themes support - it's easy to customize or apply your own theme (see [README_THEMES](osafw-app/App_Data/README_THEMES))
- use of well-known 3rd party libraries: [jQuery](http://jquery.com), [jQuery Form](https://github.com/malsup/form), jGrowl, markdown libs, etc...
- extensible dashboard panels with charts, table and progress templates ([dashboard.md](osafw-app/docs/dashboard.md))
- dynamic controllers with JSON configuration (`FwDynamicController`) and Vue.js powered UI (`FwVueController`) ([detailed docs](osafw-app/docs/dynamic.md))
- base API controller (`FwApiController`) for building REST APIs
- attachments handling with optional Amazon S3 storage
- auto database updates and environment self-tests (`FwUpdates`, `FwSelfTest`)
- in-memory caching via `FwCache`
- optional Entity Builder for quick scaffolding
- per-user date/time and timezone handling (see "Per-user Date/Time and Timezones" below)

## Demo
http://demo.engineeredit.com/ - this is how it looks in action right after installation before customizations

## Documentation

- [CRUD workflows with `FwModel`](osafw-app/docs/crud.md) - compare `FwDict`/`FwList` and typed DTO approaches for standard operations.
- [Feature modules](osafw-app/docs/feature-modules.md) – generate or scaffold modules from database tables.

### Development
1. clone this git repository
  - delete files in `/osafw-app/App_Data/sql/updates` - these are framework updates, not need for your fresh app
2. in Visual Studio open `osafw-asp.net-core.sln` (you may "save as" solution with your project name)
3. press Ctrl+F5 to run (or F5 if you really need debugger)
4. open in browser https://localhost:PORT/Dev/Configure and check configuration. If database not configured:
  - create `demo` database (or other name you have in appsettings.json)
  - click "Initialize DB"
5. review debug log in `/osafw-app/App_Data/logs/main.log`
6. edit or create new controllers and models in `/osafw-app/App_Code/controllers` and `/osafw-app/App_Code/models`
7. modify templates in `/osafw-app/App_Data/template`

### Deployment
All the details can be found in the [Microsoft docs](https://learn.microsoft.com/aspnet/core/host-and-deploy/iis/?view=aspnetcore-8.0)
Short summary how to deploy without VS publish (from clone git repo):
1. on the server - install IIS and .NET Core Hosting Bundle
2. install latest .NET 8 SDK to have `dotnet` CLI
4. create website directory
  - make `git clone` from repo to website directory
  - make `dotnet publish --configuration Release` in the directory
5. create website in IIS with root directory to `/bin/Release/net8.0/publish`
6. set website's app pool to "No managed code" (so pool works like a proxy to app core)
7. open your website address in browser
8. create database and apply sql files in order:
  - fwdatabase.sql - core fw tables
  - database.sql - your app specific tables
  - lookups.sql - fill lookup tables
  - views.sql - (re-)create views
  - roles.sql (optional, only if RBAC used)
  - demo.sql (optional, demo tables)

### Directory structure
```
/osafw-tests         - application tests
/osafw-app           - application
  /App_Code          - all the C# code is here
    /controllers     - your controllers
    /fw              - framework core libs
    /models          - your models
  /App_Data          - non-public directory
    /sql             - initial database.sql script and update sql scripts
    /template        - all the html templates
    /logs/main.log   - application log (ensure to enable write rights to /logs dir for IIS)
  /upload            - upload dir for private files
  /wwwroot           - website public root folder
    /assets          - your web frontend assets
      /css
      /fonts
      /img
      /js
      /lib           - assets built from libman.json
    /upload          - upload dir for public files
    /favicon.ico     - change to your favicon!
    /robots.txt      - default robots.txt (empty)
  /appsettings.json  - settings for db connection, mail, logging and for IIS/.NET stuff too
```

### REST mappings
Controllers automatically directly mapped to URLs, so developer doesn't need to write routing rules:

  - `GET /Controller` - list view `IndexAction()`
  - `GET /Controller/ID` - one record view `ShowAction()`
  - `GET /Controller/new` - one record new form `ShowFormAction()`
  - `GET /Controller/ID/edit` - one record edit form `ShowFormAction()`
  - `GET /Controller/ID/delete` - one record delete confirmation form `ShowDeleteAction()`
  - `POST /Controller` - insert new record `SaveAction()`
  - `PUT /Controller` - update multiple records `SaveMultiAction()`
  - `POST/PUT /Controller/ID` - update record `SaveAction()`
  - `DELETE /Controller/ID` - delete record `DeleteAction()`
  - `GET/POST /Controller/(Something)[/ID]` - call for arbitrary action from the controller `SomethingAction()`

For example `GET /Products` will call `ProductsController.IndexAction()`
And this will cause rendering templates from `/App_Data/templates/products/index`

### Request Flow

highlighted as bold is where you could place your code.

- `FW.run()`
  - **`FwHooks.initRequest()`** - place code here which need to be run on request start
- `fw.dispatch()` - performs REST urls matching and calls controller/action, if no controller found calls `HomeController.NotFoundAction()`, if no requested action found in controller - calls controller action defined in contoller's `route_default_action` (either "index" or "show")
  - `fw._auth()`  - check if user can access requested controller/action, also performs basic CSRF validation
  - `fw.call_controller()`
    - **`SomeController.init()`** - place code here which need to be run every time request comes to this controller
    - **`SomeController.SomeAction()`** - your code for particular action
      - **`SomeModel.someMethod()`** - controllers may call model's methods, place most of your business logic in models
- `fw.Finalize()`

#### Examples:
- GET /Admin/Users
  - `FwHooks.initRequest()`
  - `AdminUsers.init()`
  - `AdminUsers.IndexAction()`
  - then ParsePage parses templates from `/template/admin/users/index/`

- GET /Admin/Users/123/edit
  - `FwHooks.initRequest()`
  - `AdminUsers.init()`
  - `AdminUsers.ShowFormAction(123)`
    - `Users.one(123)`
  - then ParsePage parses templates from `/template/admin/users/showform/`

- POST /Admin/Users/123
  - `FwHooks.initRequest()`
  - `AdminUsers.init()`
  - `AdminUsers.SaveAction(123)`
    - `Users.update(123)`
  - `fw.redirect("/Admin/Users/123/edit")` //redirect back to edit screen after db updated

- GET /Admin/Users/(Custom)/123?param1=1&param2=ABC - controller's custom action (non-standard REST)
  - `FwHooks.initRequest()`
  - `AdminUsers.init()`
  - `AdminUsers.CustomAction(123)` - here you can get params using `reqi("param1") -> 1` and `reqs("params") -> "ABC"`
  - then ParsePage parses templates from `/template/admin/users/custom/` unless you redirect somewhere else

- POST /Admin/Users/(Custom)/123 with posted params `param1=1` and `param2=ABC`
  - `FwHooks.initRequest()`
  - `AdminUsers.init()`
  - `AdminUsers.CustomAction(123)` - here you can still get params using `reqi("param1") -> 1` and `reqs("params") -> "ABC"`
  - then ParsePage parses templates from `/template/admin/users/custom/` unless you redirect somewhere else

#### Flow in IndexAction

Frequently asked details about flow for the `IndexAction()` (in controllers inherited from `FwAdminController` and `FwDynamicController`):

1. `initFilter()` - initializes `Me.list_filter` from query string filter params `&f[xxx]=...`, note, filters remembered in session
1. `setListSorting()` - initializes `Me.list_orderby` based on `list_filter("sortby")` and `list_filter("sortdir")`, also uses `Me.list_sortdef` and `Me.list_sortmap` which can be set in controller's `init()` or in `config.json`
1. `setListSearch()` - initializes `Me.list_where` based on `list_filter("s")` and `Me.search_fields`
1. `setListSearchStatus()` - add to `Me.list_where` filtering  by `status` field if such field defined in the controller's model
1. `getListRows()` - query database and save rows to `Me.list_rows` (only current page based on `Me.list_filter("pagenum")` and `Me.list_filter("pagesize")`). Also sets `Me.list_count` to total rows matched by filters and `Me.list_pager` for pagination if there are more than one page. Uses `Me.list_view`, `Me.list_where`, `Me.list_orderby`

You could either override these particular methods or whole `IndexAction()` in your specific controller.

The following controller fields used above can be defined in controller's `init()` or in `config.json`:
- `Me.list_sortdef` - default list sorting in format: "sort_name[ asc|desc]"
- `Me.list_sortmap` - mapping for sort names (from `list_filter["sortby"]`) to actual db fields, `FwDict` `sort_name => db_field_name`
- `Me.search_fields` - search fields, space-separated
- `Me.list_view` - table/view to use in `getListRows()`, if empty model's `table_name` used

### Additional Framework Components

- **FwCache** â€“ simple wrapper around `IMemoryCache` for application and request caching. Accessible in controllers and models via `fw.cache`
- **FwUpdates** â€“ applies SQL scripts from `/App_Data/sql/updates` automatically in development
- **FwSelfTest** â€“ runs configuration and controller tests to verify environment
- **FwActivityLogs** â€“ unified activity and change logging model. Can be used directly or via `fw.logActivity` helper
- **FwApiController** â€“ base class for building authenticated REST APIs
- **Entity Builder** â€“ text based definition to generate SQL and CRUD scaffolding

### Per-user Date/Time and Timezones

The framework supports per-user formatting and timezone conversion:
- Defaults come from `appsettings.json` (`appSettings.date_format`, `time_format`, `timezone`) 
- For each user can be overridden - see `users` table fields `date_format`, `time_format`, `timezone` (e.g. on login/profile save).
- Rendering in templates uses these values automatically via ParsePage. Inputs are interpreted using the userâ€™s format; output can be converted from database timezone to the userâ€™s timezone.

See the detailed guide with examples and constants in [datetime.md](osafw-app/docs/datetime.md).

### `FwConfig`

Application configuration available via `fw.config([SettingName])`.
Most of the global settings are defined in `appsettings.json` `appSettings` section. But there are several calculated settings:

|SettingName|Description|Example|
|-------|-----------|-------|
|hostname|set from server variable HTTP_HOST|osalabs.com|
|ROOT_DOMAIN|protocol+hostname|https://osalabs.com|
|ROOT_URL|part of the url if Application installed under sub-url|/suburl if App installed under osalabs.com/suburl|
|site_root|physical application path to the root of public directory|C:\inetpub\somesite\www|
|template|physical path to the root of templates directory|C:\inetpub\somesite\www\App_Data\template|
|log|physical path to application log file|C:\inetpub\somesite\www\App_Data\logs\main.log|
|tmp|physical path to the system tmp directory|C:\Windows\Temp|

### How to Debug

Main and recommended approach - use `fw.logger()` function, which is available in controllers and models (so no prefix required).
Examples: `logger("some string to log", var_to_dump)`, `logger(LogLevel.WARN, "warning message")`
All logged messages and var content (complex objects will be dumped wit structure when possible) written on debug console as well as to log file (default `/App_Data/logs/main.log`)
You can configure log level in `appsettings.json` - search for `log_level` in `appSettings`

Another debug function that might be helpful is `fw.rw()` - but it output it's parameter directly into response output (i.e. you will see output right in the browser)

### Best Practices / Recommendations
- naming conventions:
  - table name: `user_lists` (lowercase, underscore delimiters is optional)
  - model name: `UserLists` (UpperCamelCase)
  - controller name: `UserListsController` or `AdminUserListsController` (UpperCamelCase with "Controller" suffix)
  - template path: `/template/userlists`
- keep all paths without trailing slash, use beginning slash where necessary
- db updates:
  - first, make changes in `/App_Data/sql/database.sql` - this file is used to create db from scratch
  - then create a file `/App_Data/sql/updates/updYYYY-MM-DD[-123].sql` with all the CREATE, ALTER, UPDATE... - this will allow to apply just this update to existing database instances
- use `fw.routeRedirect()` if you got request to one Controller.Action, but need to continue processing in another Controller.Action
  - for example, if for a logged user you need to show detailed data and always skip list view - in the `IndexAction()` just use `fw.routeRedirect("ShowForm")`
- uploads
  - save all public-readable uploads under `/wwwroot/upload` (default, see `UPLOAD_DIR` in `appsettings.json`)
  - for non-public uploads use `/upload`
  - or `S3` model and upload to the cloud
- put all validation code into controller's `Validate()`. See usage example in `AdminDemosController`
- use `logger()` and review `/App_Data/logs/main.log` if you stuck
  - make sure you have `log_level` set to `DEBUG` in `appsettings.json`

### Reports

**How to quickly create a Report**
- all reports accessed via `AdminReportsController`
  - `IndexAction` - shows a list of all available reports (basically renders static html template with a link to specific reports)
  - `ShowAction` - based on passed report code calls related Report model
- base report model is `FwReports`, major methods (you may override in the specific report):
  - `getReportFilters()` - set data for the report filters
  - `getReportData()` - returns report data, usually based on some sql query (see Sample report)
- `ReportSample` model (in `\App_Code\models\Reports` folder) is a sample report implementation, that can be used as a template to build custom reports
- basic steps to create a new report:
  - copy `\App_Code\models\Reports\Sample.cs` to `\App_Code\models\Reports\Cool.cs` (to create Cool report)
  - edit `Cool.cs` and rename "Sample" to "Cool"
  - modify `getReportFilters()` to match your report filters
  - modify `getReportData()` to edit sql query and related post-processing
  - copy templates folder `\App_Data\template\reports\sample` to `\App_Data\template\reports\cool`
  - edit templates:
    - `title.html` - report title
    - `list_filter.html` - for filters
    - `report_html.html` - for report table/layout/appearance
  - add link to a new report to `\App_Data\template\reports\index\main.html`

**PDF Export for reports setup**
PDF Reports done by generating report html and then converting it into pdf using Playwright (Chromium).

To enable PDF export using Playwright (Chromium):

1. **Configure PLAYWRIGHT_BROWSERS_PATH** in `appsettings.json`. By default it's `C:\Program Files\pw-browsers`. (If you set it to empty it will use current user `AppData\Local` folder)
2. **Run scripts\install_playwright.bat** to setup folder permissions before first PDF export.
3. **Lazy Initialization**
   - Playwright browser install will run automatically the first time a PDF report is generated.
   - If there are issues with Playwright, normal app workflow is not affected.
4. **Manual Initialization**
   - Developers can manually trigger Playwright install from `/Dev/Manage` (look for the "Init Playwright" link).
5. **Troubleshooting**
   - If PDF export fails, check `/App_Data/logs/main.log` for errors.
   - Ensure the browser path is writable and accessible by the IIS user.

For more technical details, see comments in `ConvUtils.cs` and `FwReports.cs`.

### Background Service for Scheduled Tasks

Framework includes a background service for scheduled tasks (like send emails, run reports, etc...). Uses **Cronos** nuget package.
To enable:
- in Program.cs uncomment `builder.Services.AddHostedService<FwCronService>();`
- add tasks like `insert into fwcron(icode, cron) values ('example_sleep', '* * * * *')` - example task to run every minute
- update `FwCron.runJobAction` to call acutal code for tasks
