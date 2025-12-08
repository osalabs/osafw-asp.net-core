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

**"show_fields" and "showform_fields"**

Each entry in `show_fields` or `showform_fields` configures one block on the Show or ShowForm screen. Use the reference below to
pick a `type`, set the required keys, and copy an example you can paste into `config.json`.

### Common field keys

- **type**: required type identifier (see the type reference below).
- **field**: database column or virtual name; some non-data blocks do not need it.
- **label**: text for the visible label.
- **lookup_model / lookup_tpl / lookup_table / lookup_field / lookup_key / lookup_params**: sources for lookup values (model-based,
  template-based or direct table lookup).
- **class / attrs / class_label / class_contents**: wrapper and label sizing/styling.
- **class_control / attrs_control**: styling and behaviour on the control itself (for example `on-refresh` to resubmit the form
  on change).
- **help_text**: muted helper text under the control.

### Input-only helpers

- **required**, **maxlength**, **max**, **min**, **step**, **placeholder**, **rows**: standard HTML validation/appearance options.
- **validate**: simple validation codes (`exists`, `isemail`, `isphone`, `isdate`, `isfloat`).
- **is_inline**: render radio/yesno choices inline.
- **autocomplete_url**: data source for `autocomplete` fields (called as `?q=...`).
- **conv**: converter for display/save (for example `time_from_seconds`).
- **default_time**: default time for `datetime_popup`.
- **is_custom**: mark placeholder for manual processing.

### Lookup and filtering helpers

- **is_option0 / is_option_empty / option0_title**: include a blank option in `select` fields.
- **filter_for / filter_by / filter_field**: wire dependent selects (example provided in the `select` type section).
- **lookup_by_value**: for `autocomplete` fields, store the typed value instead of id.
- **lookup_id / admin_url**: build links for `plaintext_link`.

### Attachments and subtables

- **att_category / att_post_prefix**: configure upload bucket and input prefix for `att*` fields.
- **model / save_fields / save_fields_checkboxes / required_fields / is_by_linked**: configure `multi*` and `subtable*` blocks.

### Button addons (Dynamic and Vue)

- **prepend / append**: arrays of buttons rendered before/after controls.
  ```json
  [
    {
      "event": "add", // only used in FwVueController
      "class": "",
      "icon": "bi bi-plus",
      "label": "",
      "hint": "Add New"
    }
  ]
  ```

### Type reference (TOC)

**Layout helpers**: [row](#type-row) · [col](#type-col) · [col_end](#type-col_end) · [row_end](#type-row_end) · [header](#type-header)

**Show & ShowForm display**: [plaintext](#type-plaintext) · [plaintext_link](#type-plaintext_link) · [plaintext_autocomplete](#type-plaintext_autocomplete) · [plaintext_yesno](#type-plaintext_yesno) · [plaintext_currency](#type-plaintext_currency) · [markdown](#type-markdown) · [noescape](#type-noescape) · [float](#type-float) · [checkbox](#type-checkbox) · [date](#type-date) · [date_long](#type-date_long) · [multi](#type-multi) · [multi_prio](#type-multi_prio) · [att](#type-att) · [att_links](#type-att_links) · [att_files](#type-att_files) · [subtable](#type-subtable) · [added](#type-added) · [updated](#type-updated)

**ShowForm inputs only**: [group_id](#type-group_id) · [group_id_addnew](#type-group_id_addnew) · [select](#type-select) · [input](#type-input) · [textarea](#type-textarea) · [email](#type-email) · [number](#type-number) · [password](#type-password) · [currency](#type-currency) · [autocomplete](#type-autocomplete) · [multicb](#type-multicb) · [multicb_prio](#type-multicb_prio) · [radio](#type-radio) · [yesno](#type-yesno) · [cb](#type-cb) · [date_popup](#type-date_popup) · [date_combo](#type-date_combo) · [datetime_popup](#type-datetime_popup) · [time](#type-time) · [att_edit](#type-att_edit) · [att_links_edit](#type-att_links_edit) · [att_files_edit](#type-att_files_edit) · [subtable_edit](#type-subtable_edit)

### Type details

#### type: row
- Starts a Bootstrap `div.row` wrapper; pairs with `row_end`.
- Combine with `col`/`col_end` to create multi-column layouts.
```json
{"type": "row"}
```

#### type: col
- Starts a column inside a row; default class `col`. Use `class_contents` to change width.
```json
{"type": "col", "class_contents": "col-md-6"}
```

#### type: col_end
- Ends the current column wrapper.
```json
{"type": "col_end"}
```

#### type: row_end
- Ends the current row wrapper.
```json
{"type": "row_end"}
```

#### type: header
- Section header rendered as `<h5>` with horizontal rule.
```json
{"type": "header", "label": "General"}
```

#### type: plaintext
- Read-only text value; standard `label`, `class_*` keys apply.
```json
{"type": "plaintext", "field": "iname", "label": "Title"}
```

#### type: plaintext_link
- Displays text with a link to `admin_url`/`lookup_id`; accepts `lookup_table`/`lookup_field` or `lookup_model`.
```json
{
  "type": "plaintext_link",
  "field": "user_id",
  "label": "Owner",
  "admin_url": "/Admin/Users",
  "lookup_model": "Users"
}
```

#### type: plaintext_autocomplete
- Shows the lookup name (via `lookup_model` or `lookup_table`/`lookup_field`) for the stored id.
```json
{"type": "plaintext_autocomplete", "field": "demo_dicts_id", "label": "Category", "lookup_model": "DemoDicts"}
```

#### type: plaintext_yesno
- Renders `Yes`/`No` from a boolean/flag value.
```json
{"type": "plaintext_yesno", "field": "is_active", "label": "Active"}
```

#### type: plaintext_currency
- Read-only currency; optional `currency_symbol` (default `$`) and `conv` for formatting.
```json
{"type": "plaintext_currency", "field": "price", "label": "Price", "currency_symbol": "EUR"}
```

#### type: markdown
- Server-side rendered markdown block; accepts `noescape` style content.
```json
{"type": "markdown", "field": "idesc", "label": "Description"}
```

#### type: noescape
- Outputs the raw value without HTML escaping.
```json
{"type": "noescape", "field": "html_block", "label": "Raw HTML"}
```

#### type: float
- Shows a numeric value with two decimal places.
```json
{"type": "float", "field": "amount", "label": "Amount"}
```

#### type: checkbox
- Read-only checkbox; mark as checked when the value equals the true flag (defaults to `1`).
```json
{"type": "checkbox", "field": "is_done", "label": "Completed"}
```

#### type: date
- Formats a date value as `M/d/yyyy`.
```json
{"type": "date", "field": "due_date", "label": "Due"}
```

#### type: date_long
- Formats date/time as `M/d/yyyy hh:mm:ss`.
```json
{"type": "date_long", "field": "updated_time", "label": "Updated"}
```

#### type: multi
- Read-only list of related records with checkboxes; set `lookup_model`/`lookup_field`. Supports `is_by_linked` for alt ids.
```json
{"type": "multi", "field": "tags", "label": "Tags", "lookup_model": "DemoDicts", "lookup_field": "iname"}
```

#### type: multi_prio
- Read-only multi-select with priorities; usually paired with a junction model.
```json
{"type": "multi_prio", "field": "roles", "label": "Roles", "lookup_model": "Roles", "lookup_field": "iname"}
```

#### type: att
- Displays a single attachment (first match) for the current record.
```json
{"type": "att", "field": "photo", "label": "Photo", "att_category": "photos"}
```

#### type: att_links
- Shows multiple attachment links.
```json
{"type": "att_links", "field": "docs", "label": "Documents", "att_category": "general"}
```

#### type: att_files
- List of uploaded files with optional filtering by `att_category` and custom upload prefix.
```json
{
  "type": "att_files",
  "field": "attachments",
  "label": "Files",
  "att_category": "photos",
  "att_post_prefix": "docs"
}
```

#### type: subtable
- Read-only table of related records; supply `model` plus `save_fields`/`lookup_*` on the model side as needed.
```json
{"type": "subtable", "field": "items", "label": "Items", "model": "DemosItems"}
```

#### type: added
- Standard added-on/by block (uses framework metadata).
```json
{"type": "added", "label": "Added"}
```

#### type: updated
- Standard updated-on/by block.
```json
{"type": "updated", "label": "Updated"}
```

#### type: group_id
- Hidden id field with Submit/Cancel buttons. Accepts `class`/`attrs` for layout tweaks.
```json
{"type": "group_id"}
```

#### type: group_id_addnew
- Same as `group_id` plus "Submit and Add New" button.
```json
{"type": "group_id_addnew"}
```

#### type: select
- Dropdown fed by `lookup_model` or `lookup_tpl`; supports dependent selects with `filter_for`/`filter_by`/`filter_field` and
  blank options via `is_option0` or `is_option_empty`.
```json
{
  "type": "select",
  "field": "demo_dicts_id",
  "label": "DemoDicts",
  "lookup_model": "DemoDicts",
  "is_option0": true,
  "class_contents": "col-md-3",
  "class_control": "on-refresh"
}
```

Filtered select pair example:
```json
{
  "field": "parent_demo_dicts_id",
  "type": "select",
  "label": "Parent DemoDicts Filter",
  "lookup_model": "DemoDicts",
  "filter_for": "parent_id",
  "filter_field": "demo_dicts_id",
  "class_control": "selectpicker on-refresh",
  "is_option_empty": true,
  "option0_title": "- select to show only Parents with this DemoDicts -",
  "attrs_control": "data-live-search=\"true\""
},
{
  "field": "parent_id",
  "label": "Parent",
  "lookup_model": "Demos",
  "lookup_params": "parent",
  "filter_by": "parent_demo_dicts_id",
  "filter_field": "demo_dicts_id",
  "type": "select",
  "is_option0": true,
  "option0_title": "- none -",
  "class_contents": "col-md-3",
  "attrs_control": "data-noautosave=\"true\""
}
```

#### type: input
- Single-line text input; supports `maxlength`, `placeholder`, `validate`, `prepend`/`append` button addons.
```json
{"type": "input", "field": "iname", "label": "Title", "maxlength": 255}
```

#### type: textarea
- Multiline text; configure `rows`, `maxlength`, `placeholder`.
```json
{"type": "textarea", "field": "idesc", "label": "Description", "rows": 5}
```

#### type: email
- Email input with browser validation.
```json
{"type": "email", "field": "email", "label": "Email", "required": true}
```

#### type: number
- Numeric input with `min`, `max`, `step` and optional `conv`.
```json
{"type": "number", "field": "qty", "label": "Quantity", "min": 0, "step": 1}
```

#### type: password
- Password input; often used without `label` when embedded.
```json
{"type": "password", "field": "pass", "label": "Password"}
```

#### type: currency
- Input-group with currency symbol (default `$`); accepts `currency_symbol` and `conv`.
```json
{"type": "currency", "field": "price", "label": "Price", "currency_symbol": "EUR"}
```

#### type: autocomplete
- Text input with AJAX suggestions at `autocomplete_url?q=...`; `lookup_by_value` stores typed value instead of id.
```json
{
  "type": "autocomplete",
  "field": "demo_dicts_id",
  "label": "Category",
  "autocomplete_url": "/Admin/DemoDicts/(Autocomplete)",
  "lookup_model": "DemoDicts",
  "lookup_by_value": false
}
```

#### type: multicb
- Multi-select with checkboxes; uses `model` for junction table and `save_fields`/`lookup_*` to render choices.
```json
{
  "type": "multicb",
  "field": "demo_dicts_id",
  "label": "Categories",
  "lookup_model": "DemoDicts",
  "model": "DemosDemoDicts"
}
```

#### type: multicb_prio
- Multi-select with priorities; stores order in the junction model.
```json
{
  "type": "multicb_prio",
  "field": "demo_dicts_id",
  "label": "Categories (prioritized)",
  "lookup_model": "DemoDicts",
  "model": "DemosDemoDicts"
}
```

#### type: radio
- Radio buttons from lookup values; `is_inline` lays them out horizontally.
```json
{"type": "radio", "field": "status", "label": "Status", "lookup_tpl": "/common/sel/status.sel", "is_inline": true}
```

#### type: yesno
- Convenience radio group for Yes/No (1/2); supports `is_inline`.
```json
{"type": "yesno", "field": "is_active", "label": "Active", "is_inline": true}
```

#### type: cb
- Single checkbox input.
```json
{"type": "cb", "field": "is_active", "label": "Active"}
```

#### type: date_popup
- Date picker with calendar popup.
```json
{"type": "date_popup", "field": "due_date", "label": "Due Date"}
```

#### type: date_combo
- Separate day/month/year combos; useful for locales without date pickers.
```json
{"type": "date_combo", "field": "dob", "label": "Birth Date"}
```

#### type: datetime_popup
- Date and time picker; supports `default_time` and `conv`.
```json
{"type": "datetime_popup", "field": "start_time", "label": "Start", "default_time": "09:00"}
```

#### type: time
- HH:MM time input; combine with `conv` for storage as seconds if needed.
```json
{"type": "time", "field": "start_at", "label": "Start Time", "conv": "time_from_seconds"}
```

#### type: att_edit
- Single attachment picker/uploader; use `att_category` to bucket files and `att_post_prefix` to support multiple upload slots.
```json
{
  "type": "att_edit",
  "field": "photo",
  "label": "Photo",
  "att_category": "photos",
  "att_post_prefix": "att"
}
```

#### type: att_links_edit
- Attach multiple existing files or upload via Att modal.
```json
{"type": "att_links_edit", "field": "docs", "label": "Documents", "att_category": "general"}
```

#### type: att_files_edit
- Direct file upload with progress; supports multiple components via `att_post_prefix` and category filtering.
```json
{
  "type": "att_files_edit",
  "field": "attachments",
  "label": "Files",
  "att_category": "photos",
  "att_post_prefix": "docs",
  "multiple": true
}
```

#### type: subtable_edit
- Editable subtable rows; configure `model`, `save_fields`, `save_fields_checkboxes`, and `required_fields` for validation.
```json
{
  "type": "subtable_edit",
  "field": "items",
  "label": "Items",
  "model": "DemosItems",
  "save_fields": "iname qty",
  "save_fields_checkboxes": "is_active|0",
  "required_fields": "iname qty"
}
```

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