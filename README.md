# osafw-asp.net
ASP.NET Core C# web framework optimized for creation of Business Applications

Created as simplified and lightweight alternative to other ASP.NET frameworks like ASP.NET MVC

![image](https://user-images.githubusercontent.com/1141095/75820467-0200b380-5d62-11ea-9340-e0942b460eb1.png)

## Features
- simple and straightforward in development and maintenance
- MVC-like
  - code, data, templates are split
  - code consists of: controllers, models, framework core and optional 3rd party libs
  - uses [ParsePage template engine](https://github.com/osalabs/parsepage)
  - data stored by default in SQL Server database [using db.net](https://github.com/osalabs/db.net)
- RESTful with some practical enhancements
- integrated auth - simple flat access levels auth
- UI based on [Bootstrap 5](http://getbootstrap.com) with minimal custom CSS and themes support - it's easy to customzie or apply your own theme
- use of well-known 3rd party libraries: [jQuery](http://jquery.com), [jQuery Form](https://github.com/malsup/form), jGrowl, markdown libs, etc...

## Demo
http://demo.engineeredit.com/ - this is how it looks in action right after installation before customizations

## Documentation

### Development
1. in Visual Studio open `osafw-asp.net-core.sln` (you may "save as" solution with your project name)
2. press Ctrl+F5 to run (or F5 if you really need debugger)
3. review debug log in `/osafw-app/App_Data/logs/main.log`
4. edit or create new controllers and models in `/osafw-app/App_Code/controllers` and `/osafw-app/App_Code/models`
5. modify templates in `/osafw-app/App_Data/template`

### Deployment
All the details can be found in MS docs https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-6.0
Short summary how to deploy without VS publish (from clone git repo):
1. on the server - install IIS and .NET Core Hosting Bundle
2. install latest .NET 6 SDK to have `dotnet` CLI
4. create website directory
  - make `git clone` from repo to website directory
  - make `dotnet publish configuration Release` in the direcotry
5. create website in IIS with root directory to `/bin/Release/net6/publish`
6. set website's app pool to "No managed code" (so pool works like a proxy to app core)
7. open your website address in browser

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
    /upload          - upload dir for public files
    /favicon.ico     - change to your favicon!
    /robots.txt      - default robots.txt (empty)
  /appsettings.json  - settings for db connection, mail, logging and for IIS/.NET stuff too
```

### REST mappings
Controllers automatically directly mapped to URLs, so developer doesn't need to write routing rules:

  - `GET /Controller` - list view `IndexAction()`
  - `GET /Controller/ID` - one record view `ShowAction()`
  - `GET /Controller/ID/new` - one record new form `ShowFormAction()`
  - `GET /Controller/ID/edit` - one record edit form `ShowFormAction()`
  - `GET /Controller/ID/delete` - one record delete confirmation form `ShowDeleteAction()`
  - `POST /Controller` - insert new record `SaveAction()`
  - `PUT /Controller` - update multiple records `SaveMultiAction()`
  - `POST/PUT /Controller/ID` - update record `SaveAction()`
  - `POST/DELETE /Controller/ID` - delete record (POST body should be empty) `DeleteAction()`
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
- `Me.list_sortmap` - mapping for sort names (from `list_filter["sortby"]`) to actual db fields, Hashtable `sort_name => db_field_name`
- `Me.search_fields` - search fields, space-separated
- `Me.list_view` - table/view to use in `getListRows()`, if empty model's `table_name` used

### fw.config()

Application configuration available via `fw.config([SettingName])`.
Most of the global settings defined in `appsettings.json` `appSettings` section. But there are several caclulated settings:

|SettingName|Description|Example|
|-------|-----------|-------|
|hostname|set from server variable HTTP_HOST|osalabs.com|
|ROOT_DOMAIN|protocol+hostname|https://osalabs.com|
|ROOT_URL|part of the url if Application installed under sub-url|/suburl if App installed under osalabs.com/suburl|
|site_root|physical application path to the root of public directory|C:\inetpub\somesite\www|
|template|physical path to the root of templates directory|C:\inetpub\somesite\www\App_Data\template|
|log|physical path to application log file|C:\inetpub\somesite\www\App_Data\logs\main.log|
|tmp|physical path to the system tmp directory|C:\Windows\Temp|

### config.json

In `FwDynamicController` controller behaviour defined by `/template/CONTROLLER/config.json`. Sample file can be fount at `/template/admin/demosdynamic/config.json`
This config file allows to define/override several properties of the `FwController` (for example: as `model`, `save_fields`, `search_fields`, `list_view`,...) as well as define configuration of Show (`show_fields`) and ShowForm (`showform_fields`)  screens. Note `is_dynamic_show` and `is_dynamic_showform` should be set to true accordingly.
There are samples for the one `show_fields` or `showform_fields` element:

```json
  //minimal setup to display the field value
  {
      "type": "plaintext",
      "field": "iname",
      "label": "Title"
  },
```
Renders:
```html
<div class="form-row">
  <label class="col-form-label">Title</label>
  <div class="col">
    <p class="form-control-plaintext">FIELD_VALUE</p>
  </div>
</div>
```

```json
  //more complex - displays dropdown with values from lookup model
  {
      "type": "select",
      "field": "demo_dicts_id",
      "label": "DemoDicts",
      "lookup_model": "DemoDicts",
      "is_option0": true,
      "class_contents": "col-md-3",
      "class_control": "on-refresh"
  },
```
Renders:
```html
<div class="form-row">
  <label class="col-form-label">DemoDicts</label>
  <div class="col-md-3">
    <select id="demo_dicts_id" name="item[demo_dicts_id]" class="form-control on-refresh">
      <option value="0">- select -</option>
      ... select options from lookup here...
    </select>
  </div>
</div>
```

|Field name|Description|Example|
|---|---|---|
|type|required, Element type, see values in table below|select - renders as `<select>` html|
|field|Field name from database.table or arbitrary name for non-db block|demo_dicts_id - in case of select id value won't be displayed, but used to select active list element|
|label|Label text|Demo Dictionary|
|lookup_model|Model name where to read lookup values|DemoDicts|
|is_option0|only for "select" type, if true - includes `<option value="0">option0_title</option>`|false(default),true|
|is_option_empty|only for "select" type, if true - includes `<option value="">option0_title</option>`|false(default),true|
|option0_title|only for "select" type for is_option0 or is_option_empty option title|"- select -"(default)|
|required|make field required (both client and server-side validation), for `showform_fields` only|false(default),true|
|maxlength|set input's maxlength attribute, for `showform_fields` only|10|
|max|set input type="number" max attribute, for `showform_fields` only|999|
|min|set input type="number" min attribute, for `showform_fields` only|0|
|step|set input type="number" step attribute, for `showform_fields` only|0.1|
|placeholder|set input's maxlength attribute, for `showform_fields` only|"Enter value here"|
|autocomplete_url|type="autocomplete". Input will get data from `autocomplete_url?q=%QUERY` where %QUERY will be replaced with input value, for `showform_fields` only|/Admin/SomeLookup/(Autocompete)|
|is_inline|type `radio` or `yesno`. If true - place all options in one line, for `showform_fields` only|true(default),false|
|rows|set textarea rows attribute, for `showform_fields` only|5|
|class|Class(es) added to the wrapping `div.form-row` |mb-2 - add bottom margin under the control block|
|attrs|Arbitrary html attributes for the wrapping `div.form-row`|data-something="123"|
|class_label|Class(es) added to the `label.col-form-label` |col-md-3(default) - set label width|
|class_contents|Class(es) added to the `div` that wraps input control |col(default) - set control width|
|class_control|Class(es) added to the input control to change appearance/behaviour|"on-refresh" - forms refreshes(re-submits) when input changed|
|attrs_control|Arbitrary html attributes for the input control|data-something="123"|
|help_text|Help text displayed as muted text under control block|"Minimum 8 letters and digits required"|
|admin_url|For type="plaintext_link", controller url, final URL will be: "<~admin_url>/<~lookup_id>"|/Admin/SomeController|
|lookup_id|to use with admin_url, if link to specific ID required|123|
|att_category|For type="att_edit", att category new upload will be related to|"general"(default)|
|validate|Simple validation codes: exists, isemail, isphone, isdate, isfloat|"exists isemail" - input value validated if such value already exists, validate if value is an email|

##### type values

|Type|Description|
|---|---|
|_available for both show_fields and showform_fields_||
|plaintext|Plain text|
|plaintext_link|Plain text with a link to "admin_url"|
|markdown|Markdown text (server-side rendered)|
|noescape|Value without htmlescape|
|float|Value formatted with 2 decimal digits|
|checkbox|Read-only checkbox (checked if value equal to true value)|
|date|Date in default format - M/d/yyyy|
|date_long|Date in logn forma - M/d/yyyy hh:mm:ss|
|multi|Multi-selection list with checkboxes (read-only)|
|att|Block for displaying one attachment/file|
|att_links|Block for displaying multiple attachments/files|
|added|Added on date/user block|
|updated|Updated on date/user block|
|_available only showform_fields_||
|group_id|ID with Submit/Cancel buttons block|
|group_id_addnew|ID with Submit/Submit and Add New/Cancel buttons block|
|select|select with options html block|
|input|input type="text" html block|
|textarea|textaread html block|
|email|input type="email" html block|
|number|input type="number" html block|
|autocomplete|input type="text" with autocomplete using "autocomplete_url"|
|multicb|Multi-selection list with checkboxes|
|radio|radio options block|
|yesno|radio options block with Yes(1)/No(2) only|
|cb|single checkbox block|
|date_popup|date selection input with popup calendar block|
|att_edit|Block for selection/upload one attachment/file|
|att_links_edit|Block for selection/upload multiple attachments/files|

### How to Debug

Main and recommended approach - use `fw.logger()` function, which is available in controllers and models (so no prefix required).
Examples: `logger("some string to log", var_to_dump)`, `logger(LogLevel.WARN, "warning message")`
All logged messages and var content (complex objects will be dumped wit structure when possible) written on debug console as well as to log file (default `/App_Data/logs/main.log`)
You could configure log level in `web.config` - search "log_level" in `appSettings`

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
- use `fw.route_redirect()` if you got request to one Controller.Action, but need to continue processing in another Controller.Action
  - for example, if for a logged user you need to show detailed data and always skip list view - in the `IndexAction()` just use `fw.routeRedirect("ShowForm")`
- uploads
  - save all public-readable uploads under `/wwwroot/upload` (default, see "UPLOAD_DIR" in `web.config`)
  - for non-public uploads use `/upload`
  - or `S3` model and upload to the cloud
- put all validation code into controller's `Validate()`. See usage example in `AdminDemosController`
- use `logger()` and review `/App_Data/logs/main.log` if you stuck
  - make sure you have "log_level" set to "DEBUG" in `web.config`

### How to quickly create a Report
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
