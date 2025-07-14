# Dynamic and Vue Controllers

`FwDynamicController` allows building CRUD controllers driven entirely by a JSON configuration file. The configuration lives in `/template/CONTROLLER/config.json` and defines which model is used, what fields are visible and how list and form screens behave. `FwVueController` extends this approach and renders list and form pages using Vue.js so that items can be edited inline.

Below is a description of the configuration format that controls behaviour of both controllers.

### config.json for Dynamic/Vue controllers

In `FwDynamicController` controller behaviour defined by `/template/CONTROLLER/config.json`. Sample file can be fount at `/template/admin/demosdynamic/config.json`

Common config keys include:
- `model` – main model name
- `required_fields` – space separated required fields
- `save_fields` and `save_fields_checkboxes` – fields saved from ShowForm
- `form_new_defaults` – defaults for new item
- `search_fields` – fields used for searching
- `list_sortdef` and `list_sortmap` – default sorting
- `related_field_name` – related id field
- `list_view` – table or SQL used for listing
- `is_dynamic_index` – enable dynamic list with `view_list_defaults` and `view_list_map`
- `is_dynamic_index_edit` – allow inline editing (`list_edit`, `edit_list_defaults`)
- `view_list_custom` – fields visible by default
- `is_dynamic_show` and `is_dynamic_showform` – enable dynamic screens
- `form_tabs` – optional tab definitions
- `route_return` – action to redirect after save
- `is_userlists` – enable UserLists support

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

More examples:
```json
  //display formatted currency value
  {
      "type": "plaintext_currency",
      "field": "price",
      "label": "Price"
  },

  //currency input with custom symbol
  {
      "type": "currency",
      "field": "price",
      "currency_symbol": "EUR", // if ommitted, defaults to "$"
      "label": "Price"
  },

  //list of uploaded files from `att` where entity = current's model table and item_id = current id
  {
      "type": "att_files",
      "field": "notused",
      "label": "Photos"
  },

  //Show ID, Submit and Add New buttons
  {
      "type": "group_id_addnew",
  },
```

|Field name|Description|Example|
|---|---|---|
|type|required, Element type, see values in table below|select - renders as `<select>` html|
|field|Field name from database.table or arbitrary name for non-db block|demo_dicts_id - in case of select id value won't be displayed, but used to select active list element|
|label|Label text|Demo Dictionary|
|lookup_model|Model name where to read lookup values|DemoDicts|
|lookup_tpl|template path to read lookup values, can be absolute (to templates root) or relative to current controller's template folder|/common/sel/status.sel|
|is_option0|only for "select" type, if true - includes `<option value="0">option0_title</option>`|false(default),true|
|is_option_empty|only for "select" type, if true - includes `<option value="">option0_title</option>`|false(default),true|
|option0_title|only for "select" type for is_option0 or is_option_empty option title|"- select -"(default)|
|required|make field required (both client and server-side validation), for `showform_fields` only|false(default),true|
|maxlength|set input's maxlength attribute, for `showform_fields` only|10|
|max|set input type="number" max attribute, for `showform_fields` only|999|
|min|set input type="number" min attribute, for `showform_fields` only|0|
|step|set input type="number" step attribute, for `showform_fields` only|0.1|
|placeholder|set input's maxlength attribute, for `showform_fields` only|"Enter value here"|
|autocomplete_url|type="autocomplete". Input will get data from `autocomplete_url?q=%QUERY` where %QUERY will be replaced with input value, for `showform_fields` only|/Admin/SomeLookup/(Autocomplete)|
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
|lookup_table|name of DB table for lookup one value|demo_dicts|
|lookup_field|field name from lookup table/model to display|iname|
|lookup_key|key column for lookup_table|code|
|lookup_params|additional params passed to lookup model methods|status=1|
|lookup_by_value|for autocomplete fields - store value instead of lookup id|true|
|is_by_linked|for `multi`/`multicb` - link by secondary id in junction model|true|
|model|junction or subtable model name for `multi*` and `subtable*` types|DemosItems|
|save_fields|comma-separated fields saved for each subtable row|iname qty|
|save_fields_checkboxes|checkbox fields saved for each subtable row|`is_active|0`|
|required_fields|required columns for validating each subtable row|iname qty|
|validate|Simple validation codes: exists, isemail, isphone, isdate, isfloat|"exists isemail" - input value validated if such value already exists, validate if value is an email|
|att_category|For type="att_edit", att category new upload will be related to|"general"(default)|
|att_post_prefix|For type="att_edit", name prefix for the inputs with ids `att[<~id>]`|"att"(default)|
|conv|value converter for display/save (e.g. time_from_seconds)|time_from_seconds|
|default_time|default time for datetime_popup fields|now|
|is_custom|placeholder processed manually in code|true|
|prepend|array of buttons to prepend to the cell in list edit mode (Vue) or to the form input (Dynamic)|same as for `append`|
|append|array of buttons to append to the cell in list edit mode (Vue) or to the form input (Dynamic)|
```
        [{
          "event": "add", // only used in FwVueController
          "class": "",
          "icon": "bi bi-plus",
          "label": "",
          "hint": "Add New"
        }]
```

##### type values

|Type|Description|
|---|---|
|_for defining rows/cols layout_||
|row|start of the `div.row`|
|col|start of the `div.col`|
|col_end|end of the `div.col`|
|row_end|end of the `div.row`|
|_available for both show_fields and showform_fields_||
|header|Section header text, rendered as `<h5>` tag and horizontal lines|
|plaintext|Plain text value|
|plaintext_link|Plain text with a link to `admin_url`|
|plaintext_autocomplete|Plain text name from `lookup_model` by id in field|
|plaintext_yesno|Plain text Yes/No value based on value|
|plaintext_currency|Formatted currency value, also uses `currency_symbol`(default $)|
|markdown|Markdown text (server-side rendered)|
|noescape|Value without htmlescape|
|float|Value formatted with 2 decimal digits|
|checkbox|Read-only checkbox (checked if value equal to true value)|
|date|Date in default format - M/d/yyyy|
|date_long|Date in long format - M/d/yyyy hh:mm:ss|
|multi|Multi-selection list with checkboxes (read-only)|
|multi_prio|Multi-selection list with priorities|
|att|Block for displaying one attachment/file|
|att_links|Block for displaying multiple attachments/files|
|att_files|Block for displaying multiple attachments/files|
|subtable|Block for viewing related records in a subtable|
|added|Added on date/user block|
|updated|Updated on date/user block|
|_available only showform_fields_||
|group_id|ID with Submit/Cancel buttons block|
|group_id_addnew|ID with Submit/Submit and Add New/Cancel buttons block|
|select|select with options html block|
|input|input type="text" html block|
|textarea|textarea html block|
|email|input type="email" html block|
|number|input type="number" html block|
|password|input type="password" html block|
|currency|input-group with `currency_symbol` (default $) and type="text"|
|autocomplete|input type="text" with autocomplete using `autocomplete_url`|
|multicb|Multi-selection list with checkboxes|
|multicb_prio|Multi-selection list with checkboxes and priority|
|radio|radio options block|
|yesno|radio options block with Yes(1)/No(2) only|
|cb|single checkbox block|
|date_popup|date selection input with popup calendar block|
|date_combo|date selection using separate day, month and year combos|
|datetime_popup|date and time selection input with popup calendar block|
|time|time input in HH:MM format|
|att_edit|Block for selection/upload one attachment/file|
|att_links_edit|Block for selection/upload multiple attachments/files (select existing or upload via Att modal)|
|att_files_edit|Block for selection/upload multiple attachments/files (direct upload)|
|subtable_edit|Block for editing related records in a subtable; uses `model`, `save_fields` and `save_fields_checkboxes`, validates `required_fields` on each row|

### form_tabs

`form_tabs` allows organising large forms into multiple tabs. When more than one tab is present, the template `/common/form/tabs.html` renders the navigation.

Configuration example:
```json
"form_tabs": [
  {"tab": "", "label": "Default"},
  {"tab": "general", "label": "General"},
  {"tab": "advanced", "label": "Advanced"}
],
"showform_fields": [
  // default tab form fields
],
"showform_fields_general": [
  {"field": "iname", "type": "input", "label": "Title"}
],
"showform_fields_advanced": [
  {"field": "idesc", "type": "textarea", "label": "Description"}
]
```

Each entry defines the tab code (`tab`) and the text shown on the tab (`label`).
Fields for a tab should be placed in `show_fields_TAB` and `showform_fields_TAB` arrays where `TAB` is the value from `form_tabs`. If only one tab is defined the tab bar is hidden.
Active tab is set by `tab` parameter in the URL, e.g. `/Admin/DemosDynamic/123?tab=advanced`. If no tab is specified, the default tab is active.