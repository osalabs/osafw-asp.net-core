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
- Template: `/common/form/showform/row.html` (also used on Show).
- Options: `class` (extra classes on `.row`), `attrs` (custom attributes).
- Common sample:
```json
{
  "type": "row"
}
```
- Full sample with custom gutter and data attribute:
```json
{
  "type": "row",
  "class": "g-3 align-items-center",
  "attrs": "data-block=\"main\""
}
```

#### type: col
- Template: `/common/form/showform/col.html` (also used on Show).
- Options: `class` (size overrides such as `col-md-6`), `attrs`.
- Common sample:
```json
{
  "type": "col",
  "class": "col-md-6"
}
```
- Full sample that nests additional attributes:
```json
{
  "type": "col",
  "class": "col-lg-4 mb-3",
  "attrs": "data-role=\"meta\""
}
```

#### type: col_end
- Template: `/common/form/showform/col_end.html` (also used on Show).
- Options: none (use to close the previous `col`).
- Common sample:
```json
{
  "type": "col_end"
}
```
- Full sample in a two-column layout:
```json
[
  { "type": "col", "class": "col-md-6" },
  { "type": "header", "label": "Left" },
  { "type": "col_end" },
  { "type": "col", "class": "col-md-6" },
  { "type": "header", "label": "Right" },
  { "type": "col_end" }
]
```

#### type: row_end
- Template: `/common/form/showform/row_end.html` (also used on Show).
- Options: none (use to close the previous `row`).
- Common sample:
```json
{
  "type": "row_end"
}
```
- Full sample wrapping two columns:
```json
[
  { "type": "row" },
  { "type": "col", "class": "col-md-6" },
  { "type": "col_end" },
  { "type": "col", "class": "col-md-6" },
  { "type": "col_end" },
  { "type": "row_end" }
]
```

#### type: header
- Template: `/common/form/showform/header.html`.
- Options: `label` (required), plus wrapper `class`/`attrs` if using surrounding `row`/`col`.
- Common sample:
```json
{
  "type": "header",
  "label": "General"
}
```
- Full sample with extra spacing:
```json
{
  "type": "header",
  "label": "Metadata",
  "class": "mt-4",
  "attrs": "data-section=\"meta\""
}
```

#### type: plaintext
- Template: `/common/form/show/plaintext.html`.
- Options: inherits common layout keys plus single-value lookups (`lookup_model`/`lookup_field`, `lookup_table`/`lookup_key`, `lookup_tpl`, or inline `options`). `conv: "time_from_seconds"` converts stored seconds to `HH:mm:ss`.
- Common sample:
```json
{
  "type": "plaintext",
  "field": "iname",
  "label": "Title"
}
```
- Full sample with lookup and custom layout:
```json
{
  "type": "plaintext",
  "field": "category_id",
  "label": "Category",
  "lookup_model": "DemoDicts",
  "lookup_field": "iname",
  "class_label": "col-md-2",
  "class_contents": "col-md-10",
  "help_text": "Resolved via DemoDicts lookup"
}
```

#### type: plaintext_link
- Template: `/common/form/show/plaintext_link.html`.
- Options: single-value lookups plus `admin_url` (destination path for the link) and optional `lookup_id` override.
- Common sample:
```json
{
  "type": "plaintext_link",
  "field": "user_id",
  "label": "Owner",
  "lookup_model": "Users"
}
```
- Full sample with explicit lookup table and admin URL:
```json
{
  "type": "plaintext_link",
  "field": "manager_id",
  "label": "Manager",
  "lookup_table": "users",
  "lookup_key": "id",
  "lookup_field": "email",
  "admin_url": "/Admin/Users",
  "help_text": "Links to the user record"
}
```

#### type: plaintext_autocomplete
- Template: `/common/form/show/plaintext_autocomplete.html`.
- Options: same lookup keys as `plaintext`; intended for ids saved by an autocomplete control.
- Common sample:
```json
{
  "type": "plaintext_autocomplete",
  "field": "demo_dicts_id",
  "label": "Category",
  "lookup_model": "DemoDicts"
}
```
- Full sample resolving from a lookup table with custom label sizing:
```json
{
  "type": "plaintext_autocomplete",
  "field": "parent_id",
  "label": "Parent Demo",
  "lookup_table": "demos",
  "lookup_field": "iname",
  "class_label": "col-sm-2",
  "class_contents": "col-sm-10"
}
```

#### type: plaintext_yesno
- Template: `/common/form/show/plaintext_yesno.html`.
- Options: uses the truthiness of `value`; combine with `conv: "time_from_seconds"` only for time fields.
- Common sample:
```json
{
  "type": "plaintext_yesno",
  "field": "is_active",
  "label": "Active"
}
```
- Full sample with helper text:
```json
{
  "type": "plaintext_yesno",
  "field": "is_verified",
  "label": "Email Verified",
  "help_text": "Derived from verification timestamp"
}
```

#### type: plaintext_currency
- Template: `/common/form/show/plaintext_currency.html`.
- Options: `currency_symbol` (defaults to `$`) plus common layout keys.
- Common sample:
```json
{
  "type": "plaintext_currency",
  "field": "price",
  "label": "Price"
}
```
- Full sample with alternate currency symbol and help text:
```json
{
  "type": "plaintext_currency",
  "field": "budget",
  "label": "Budget",
  "currency_symbol": "€",
  "class_label": "col-md-2",
  "class_contents": "col-md-4",
  "help_text": "Formatted with two decimals"
}
```

#### type: markdown
- Template: `/common/form/show/markdown.html`.
- Options: markdown comes from `value`; respect common layout keys.
- Common sample:
```json
{
  "type": "markdown",
  "field": "idesc",
  "label": "Description"
}
```
- Full sample highlighting read-only rendering:
```json
{
  "type": "markdown",
  "field": "notes_md",
  "label": "Notes (Markdown)",
  "class_contents": "col-12",
  "help_text": "Rendered server-side with links and formatting"
}
```

#### type: noescape
- Template: `/common/form/show/noescape.html`.
- Options: renders raw HTML; combine with layout keys carefully.
- Common sample:
```json
{
  "type": "noescape",
  "field": "html_block",
  "label": "Raw HTML"
}
```
- Full sample for trusted snippets:
```json
{
  "type": "noescape",
  "field": "widget_embed",
  "label": "Embed",
  "class_contents": "col-12",
  "help_text": "Content is not escaped; ensure it is sanitized upstream"
}
```

#### type: float
- Template: `/common/form/show/float.html`.
- Options: displays `value` with two decimals; common layout keys apply.
- Common sample:
```json
{
  "type": "float",
  "field": "amount",
  "label": "Amount"
}
```
- Full sample with helper text:
```json
{
  "type": "float",
  "field": "tax_rate",
  "label": "Tax Rate",
  "help_text": "Stored as decimal, rendered with 2 digits"
}
```

#### type: checkbox
- Template: `/common/form/show/checkbox.html`.
- Options: uses truthy `value`; combine with layout keys.
- Common sample:
```json
{
  "type": "checkbox",
  "field": "is_done",
  "label": "Completed"
}
```
- Full sample with muted hint:
```json
{
  "type": "checkbox",
  "field": "is_featured",
  "label": "Featured?",
  "help_text": "Checked when the stored value is truthy"
}
```

#### type: date
- Template: `/common/form/show/date.html`.
- Options: displays `value` using `M/d/yyyy`; use `conv: "time_from_seconds"` only when the field stores seconds.
- Common sample:
```json
{
  "type": "date",
  "field": "due_date",
  "label": "Due"
}
```
- Full sample with label sizing:
```json
{
  "type": "date",
  "field": "ship_on",
  "label": "Ship On",
  "class_label": "col-md-2",
  "class_contents": "col-md-4",
  "help_text": "Uses user date formatting"
}
```

#### type: date_long
- Template: `/common/form/show/date_long.html`.
- Options: renders `value` in `M/d/yyyy hh:mm:ss`; combine with common layout keys.
- Common sample:
```json
{
  "type": "date_long",
  "field": "updated_time",
  "label": "Updated"
}
```
- Full sample with helper text:
```json
{
  "type": "date_long",
  "field": "processed_at",
  "label": "Processed",
  "help_text": "Includes time in the user timezone"
}
```

#### type: multi
- Template: `/common/form/show/multi.html`.
- Options: `lookup_model` (+ `lookup_field`, `lookup_params`) to render values without a junction model; `model` to use a junction table; `is_by_linked` to switch between `updateJunctionByMainId` and `updateJunctionByLinkedId`; `lookup_checked_only` to show only checked rows.
- Common sample (comma-separated ids in the same table field):
```json
{
  "type": "multi",
  "field": "tag_ids",
  "label": "Tags",
  "lookup_model": "DemoDicts",
  "lookup_field": "iname"
}
```
- Full sample using a junction model and showing only checked records:
```json
{
  "type": "multi",
  "field": "demo_dicts_link",
  "label": "DemoDicts via Junction",
  "model": "DemosDemoDicts",
  "is_by_linked": false,
  "lookup_checked_only": true,
  "help_text": "Rendered from the junction table for this record"
}
```

#### type: multi_prio
- Template: no dedicated Show partial is registered; add a custom template or reuse the `multi` template to display `multi_datarow` (which includes `_link[prio]`).
- Options: `model` (required, junction model providing `_link[prio]`), `is_by_linked` to flip main/linked behaviour, plus layout keys.
- Common sample:
```json
{
  "type": "multi_prio",
  "field": "roles",
  "label": "Roles",
  "model": "UsersRoles"
}
```
- Full sample with custom ordering note:
```json
{
  "type": "multi_prio",
  "field": "permissions",
  "label": "Permissions (prio)",
  "model": "RolesPermissions",
  "is_by_linked": true,
  "help_text": "Template shows checkboxes; extend the template to display priorities from _link[prio]"
}
```

#### type: att
- Template: `/common/form/show/att.html`.
- Options: relies on `field` holding an attachment id; layout keys apply (category is determined by the stored attachment).
- Common sample:
```json
{
  "type": "att",
  "field": "photo",
  "label": "Photo"
}
```
- Full sample with custom column width:
```json
{
  "type": "att",
  "field": "avatar_id",
  "label": "Avatar",
  "class_contents": "col-md-4",
  "help_text": "Shows the linked attachment preview and download"
}
```

#### type: att_links
- Template: `/common/form/show/att_links.html`.
- Options: lists attachments linked to the current entity; `field` may be empty because lookup uses the entity table/id.
- Common sample:
```json
{
  "type": "att_links",
  "field": "_att_links",
  "label": "Documents"
}
```
- Full sample scoped by label styling:
```json
{
  "type": "att_links",
  "field": "_att_links",
  "label": "Supporting Files",
  "class_label": "col-12",
  "help_text": "Displays all attachments linked to this record"
}
```

#### type: att_files
- Template: `/common/form/show/att_files.html`.
- Options: `att_category` (filter by category), `att_post_prefix` (reserved for parity with edit configuration), plus layout keys.
- Common sample:
```json
{
  "type": "att_files",
  "field": "_att_files",
  "label": "Files",
  "att_category": "general"
}
```
- Full sample filtered to a category with helper text:
```json
{
  "type": "att_files",
  "field": "_att_files_docs",
  "label": "Documents",
  "att_category": "docs",
  "class_contents": "col-12",
  "help_text": "Lists files in the docs attachment category"
}
```

#### type: subtable
- Template: `/common/form/show/subtable.html`.
- Options: `model` (required), plus any model-specific settings such as `related_field_name`, `lookup_params`, or a custom subtable template used by the model’s `prepareSubtable`.
- Common sample:
```json
{
  "type": "subtable",
  "field": "items",
  "label": "Items",
  "model": "DemosItems"
}
```
- Full sample with custom label styling and params passed to the model:
```json
{
  "type": "subtable",
  "field": "line_items",
  "label": "Line Items",
  "model": "OrdersItems",
  "class_label": "col-12 hr-header fs-5",
  "lookup_params": "with_products",
  "help_text": "Rendered via OrdersItems.prepareSubtable"
}
```

#### type: added
- Template: `/common/form/show/added.html`.
- Options: layout keys only (field value is taken from the model metadata).
- Common sample:
```json
{
  "type": "added",
  "label": "Added"
}
```
- Full sample with explicit field name:
```json
{
  "type": "added",
  "field": "add_time",
  "label": "Created",
  "class_contents": "col-md-4"
}
```

#### type: updated
- Template: `/common/form/show/updated.html`.
- Options: layout keys only (field value is taken from the model metadata).
- Common sample:
```json
{
  "type": "updated",
  "label": "Updated"
}
```
- Full sample targeting a specific field:
```json
{
  "type": "updated",
  "field": "upd_time",
  "label": "Last Updated",
  "class_contents": "col-md-4"
}
```

#### type: group_id
- Template: `/common/form/showform/group_id.html`.
- Options: layout keys only; renders the item id with Save/Cancel buttons.
- Common sample:
```json
{
  "type": "group_id"
}
```
- Full sample with custom wrapper class:
```json
{
  "type": "group_id",
  "class": "mt-2"
}
```

#### type: group_id_addnew
- Template: `/common/form/showform/group_id_addnew.html`.
- Options: layout keys only; adds “Save and Add New”.
- Common sample:
```json
{
  "type": "group_id_addnew"
}
```
- Full sample with helper text:
```json
{
  "type": "group_id_addnew",
  "help_text": "Adds Save and Save & Add New buttons"
}
```

#### type: select
- Template: `/common/form/showform/select.html`.
- Options:
  - Data sources: `lookup_model` (+ `lookup_params`), `lookup_tpl`, or inline `options` dictionary.
  - Blank options: `is_option0` (value `0`), `is_option_empty` (empty value), `option0_title`.
  - Filtering chains: `filter_for`/`filter_field` on the parent select; `filter_by`/`filter_field` on the child select.
  - Behaviour: `multiple`, `class_control`, `attrs_control`, `err_exists_msg`, `prepend`/`append` input-group buttons.
- Common sample:
```json
{
  "type": "select",
  "field": "demo_dicts_id",
  "label": "DemoDicts",
  "lookup_model": "DemoDicts",
  "is_option0": true,
  "class_control": "on-refresh"
}
```
- Full sample with filtering and live search:
```json
{
  "type": "select",
  "field": "parent_id",
  "label": "Parent",
  "lookup_model": "Demos",
  "lookup_params": "parent",
  "filter_by": "parent_demo_dicts_id",
  "filter_field": "demo_dicts_id",
  "is_option_empty": true,
  "option0_title": "- none -",
  "multiple": false,
  "class_contents": "col-md-3",
  "class_control": "selectpicker on-refresh",
  "attrs_control": "data-live-search=\"true\" data-noautosave=\"true\"",
  "prepend": [
    {
      "label": "Add",
      "class": "btn-secondary",
      "icon": "bi bi-plus",
      "url": "/Admin/Demos/new"
    }
  ]
}
```

#### type: input
- Template: `/common/form/showform/input.html`.
- Options: `maxlength`, `placeholder`, `required`, `validate` (`exists`, `isemail`, `isphone`, `isdate`, `isfloat`), `class_control`, `attrs_control`, `prepend`/`append` input-group buttons.
- Common sample:
```json
{
  "type": "input",
  "field": "iname",
  "label": "Title",
  "maxlength": 255
}
```
- Full sample with validation and addon button:
```json
{
  "type": "input",
  "field": "slug",
  "label": "Slug",
  "required": true,
  "maxlength": 128,
  "validate": "exists",
  "placeholder": "auto-generated or custom",
  "class_control": "text-lowercase",
  "append": [
    {
      "label": "Generate",
      "class": "btn-outline-secondary",
      "icon": "bi bi-magic",
      "url": "/Admin/DemosDynamic/(GenerateSlug)"
    }
  ]
}
```

#### type: textarea
- Template: `/common/form/showform/textarea.html`.
- Options: `rows`, `maxlength`, `placeholder`, `class_control` (e.g., `markdown`, `fw-html-editor`), `attrs_control`.
- Common sample:
```json
{
  "type": "textarea",
  "field": "idesc",
  "label": "Description",
  "rows": 5
}
```
- Full sample with HTML editor class:
```json
{
  "type": "textarea",
  "field": "idesc2",
  "label": "Rich Text",
  "rows": 10,
  "maxlength": 2000,
  "class_control": "fw-html-editor",
  "help_text": "Requires /common/html_editor script"
}
```

#### type: email
- Template: `/common/form/showform/email.html`.
- Options: `required`, `maxlength`, `placeholder`, `validate` (typically `exists isemail`), `class_control`, `attrs_control`.
- Common sample:
```json
{
  "type": "email",
  "field": "email",
  "label": "Email",
  "required": true
}
```
- Full sample with validation hints:
```json
{
  "type": "email",
  "field": "contact_email",
  "label": "Contact Email",
  "maxlength": 128,
  "validate": "exists isemail",
  "class_control": "on-refresh",
  "help_text": "Unique per record; validated server-side"
}
```

#### type: number
- Template: `/common/form/showform/number.html`.
- Options: `min`, `max`, `step`, `maxlength`, `placeholder`, `required`, `validate` (`isfloat`), `class_control`, `attrs_control`.
- Common sample:
```json
{
  "type": "number",
  "field": "qty",
  "label": "Quantity",
  "min": 0,
  "step": 1
}
```
- Full sample with validation:
```json
{
  "type": "number",
  "field": "rating",
  "label": "Rating",
  "min": 0,
  "max": 10,
  "step": 0.5,
  "validate": "isfloat",
  "placeholder": "0 - 10",
  "class_control": "w-25"
}
```

#### type: password
- Template: `/common/form/showform/password.html`.
- Options: `maxlength`, `placeholder`, `required`, `class_control`, `attrs_control`.
- Common sample:
```json
{
  "type": "password",
  "field": "pass",
  "label": "Password"
}
```
- Full sample with custom autocomplete handling:
```json
{
  "type": "password",
  "field": "new_pass",
  "label": "New Password",
  "maxlength": 128,
  "placeholder": "leave blank to keep current",
  "attrs_control": "autocomplete=\"new-password\""
}
```

#### type: currency
- Template: `/common/form/showform/currency.html`.
- Options: `currency_symbol`, `maxlength`, `placeholder`, `required`, `class_control`, `attrs_control`.
- Common sample:
```json
{
  "type": "currency",
  "field": "price",
  "label": "Price",
  "currency_symbol": "$"
}
```
- Full sample with helper text:
```json
{
  "type": "currency",
  "field": "budget",
  "label": "Budget",
  "currency_symbol": "€",
  "maxlength": 12,
  "placeholder": "0.00",
  "class_control": "text-end",
  "help_text": "Displayed and posted as plain number with currency symbol"
}
```

#### type: autocomplete
- Template: `/common/form/showform/autocomplete.html`.
- Options: `autocomplete_url` (required), `lookup_model`/`lookup_field`, `lookup_by_value` (store text instead of id), `admin_url`, `required`, `maxlength`, `placeholder`, `class_control`, `attrs_control`, `prepend`/`append`.
- Common sample:
```json
{
  "type": "autocomplete",
  "field": "dict_link_auto_id",
  "label": "DemoDicts Autocomplete",
  "autocomplete_url": "/Admin/DemoDicts/(Autocomplete)?q=",
  "lookup_model": "DemoDicts",
  "lookup_field": "iname"
}
```
- Full sample storing typed value and adding a helper button:
```json
{
  "type": "autocomplete",
  "field": "city",
  "label": "City",
  "autocomplete_url": "/Admin/Cities/(Autocomplete)?q=",
  "lookup_model": "Cities",
  "lookup_field": "iname",
  "lookup_by_value": true,
  "placeholder": "Type to search or enter custom city",
  "append": [
    {
      "label": "Manage",
      "class": "btn-outline-secondary",
      "icon": "bi bi-box-arrow-up-right",
      "url": "/Admin/Cities"
    }
  ]
}
```

#### type: multicb
- Template: `/common/form/showform/multi.html`.
- Options: EITHER `lookup_model` (stores comma-separated ids in the same table field) OR `model` (uses a junction model). Add `is_by_linked` for junctions where the main id is stored on the linked side; `lookup_params` can tune lookup queries.
- Common sample storing ids in the same table field:
```json
{
  "type": "multicb",
  "field": "dict_link_multi",
  "label": "DemoDicts Multi",
  "lookup_model": "DemoDicts"
}
```
- Full sample using a junction model:
```json
{
  "type": "multicb",
  "field": "demo_dicts_link",
  "label": "DemoDicts via Junction Table",
  "model": "DemosDemoDicts",
  "is_by_linked": false,
  "help_text": "Uses DemosDemoDicts.updateJunction... methods to save"
}
```

#### type: multicb_prio
- Template: `/common/form/showform/multi_prio.html`.
- Options: `model` (required junction model with `_link[prio]`), `is_by_linked` to flip main/linked behaviour.
- Common sample:
```json
{
  "type": "multicb_prio",
  "field": "demo_dicts_prio",
  "label": "Prioritized Categories",
  "model": "DemosDemoDicts"
}
```
- Full sample with linked-id mode:
```json
{
  "type": "multicb_prio",
  "field": "user_roles",
  "label": "User Roles (ordered)",
  "model": "UsersRoles",
  "is_by_linked": true,
  "help_text": "Order is saved in _link[prio] from the junction model"
}
```

#### type: radio
- Template: `/common/form/showform/radio.html`.
- Options: data via `lookup_model`, `lookup_tpl`, or `options`; `is_inline` for horizontal layout; `class_control` and `attrs_control` pass through to inputs.
- Common sample:
```json
{
  "type": "radio",
  "field": "status",
  "label": "Status",
  "lookup_tpl": "/common/sel/status.sel",
  "is_inline": true
}
```
- Full sample using lookup_model:
```json
{
  "type": "radio",
  "field": "access_level",
  "label": "Access Level",
  "lookup_model": "AccessLevels",
  "lookup_field": "iname",
  "class_contents": "col-md-6",
  "help_text": "Inline radios sourced from AccessLevels model"
}
```

#### type: yesno
- Template: `/common/form/showform/yesno.html`.
- Options: `is_inline`, plus layout keys.
- Common sample:
```json
{
  "type": "yesno",
  "field": "is_active",
  "label": "Active",
  "is_inline": true
}
```
- Full sample with hint:
```json
{
  "type": "yesno",
  "field": "is_archived",
  "label": "Archived?",
  "is_inline": false,
  "help_text": "Stored as 0/1"
}
```

#### type: cb
- Template: `/common/form/showform/cb.html`.
- Options: layout keys plus `attrs_control` (for `data-*`), default checked when `value` is truthy.
- Common sample:
```json
{
  "type": "cb",
  "field": "is_active",
  "label": "Active"
}
```
- Full sample with helper text:
```json
{
  "type": "cb",
  "field": "is_checkbox",
  "label": "Email Opt-in",
  "attrs_control": "data-noautosave=\"true\"",
  "help_text": "Posted as 1 when checked"
}
```

#### type: date_popup
- Template: `/common/form/showform/date_popup.html`.
- Options: `required`, `class_control`, `attrs_control` (pass-through to input), plus layout keys.
- Common sample:
```json
{
  "type": "date_popup",
  "field": "due_date",
  "label": "Due Date"
}
```
- Full sample with custom class and hint:
```json
{
  "type": "date_popup",
  "field": "expires_on",
  "label": "Expires On",
  "class_control": "on-refresh",
  "attrs_control": "data-noautosave=\"true\"",
  "help_text": "Uses bootstrap-datepicker"
}
```

#### type: date_combo
- Template: `/common/form/showform/date_combo.html`.
- Options: `class_control` (applies to all three selects), plus layout keys.
- Common sample:
```json
{
  "type": "date_combo",
  "field": "dob",
  "label": "Birth Date"
}
```
- Full sample with helper text:
```json
{
  "type": "date_combo",
  "field": "start_on",
  "label": "Start On",
  "class_control": "form-select-sm",
  "help_text": "Converts to a single date on save"
}
```

#### type: datetime_popup
- Template: `/common/form/showform/datetime_popup.html`.
- Options: `default_time` (preselects time part), `class_control`, `attrs_control`, plus layout keys.
- Common sample:
```json
{
  "type": "datetime_popup",
  "field": "start_time",
  "label": "Start",
  "default_time": "09:00"
}
```
- Full sample with placeholder control:
```json
{
  "type": "datetime_popup",
  "field": "scheduled_at",
  "label": "Scheduled At",
  "default_time": "now",
  "class_control": "on-refresh",
  "attrs_control": "data-noautosave=\"true\"",
  "help_text": "Stores combined date and time"
}
```

#### type: time
- Template: `/common/form/showform/time.html`.
- Options: `min`, `max`, `step`, `required`, `class_control`, `attrs_control`. On save, values are converted to seconds; set `conv: "time_from_seconds"` in the Show configuration to display seconds as `HH:mm:ss`.
- Common sample:
```json
{
  "type": "time",
  "field": "start_at",
  "label": "Start Time"
}
```
- Full sample for second-based storage:
```json
{
  "type": "time",
  "field": "duration",
  "label": "Duration",
  "min": "00:00",
  "max": "12:00",
  "step": "00:05",
  "help_text": "Saved as seconds; pair with conv:\"time_from_seconds\" on Show"
}
```

#### type: att_edit
- Template: `/common/form/showform/att.html`.
- Options: `att_category` (modal filter, defaults to `general`), `att_post_prefix` (hidden input prefix, defaults to field name), `attrs_control`, plus layout keys.
- Common sample:
```json
{
  "type": "att_edit",
  "field": "photo",
  "label": "Photo",
  "att_category": "general"
}
```
- Full sample with custom prefix:
```json
{
  "type": "att_edit",
  "field": "avatar_id",
  "label": "Avatar",
  "att_category": "photos",
  "att_post_prefix": "att_photo",
  "help_text": "Uses Att modal to select/upload"
}
```

#### type: att_links_edit
- Template: `/common/form/showform/att_links.html`.
- Options: `att_category` (defaults to `general`), `att_post_prefix` (defaults to `att`), plus layout keys.
- Common sample:
```json
{
  "type": "att_links_edit",
  "field": "_att_links",
  "label": "Documents"
}
```
- Full sample with prefixed inputs:
```json
{
  "type": "att_links_edit",
  "field": "_att_links_images",
  "label": "Images",
  "att_category": "images",
  "att_post_prefix": "att_images",
  "help_text": "Selected ids are posted under att_images[...]"
}
```

#### type: att_files_edit
- Template: `/common/form/showform/att_files.html`.
- Options: `att_category` (filter uploads/listing), `att_post_prefix` (defaults to field name), `att_upload_url` (defaults to `/Controller/(SaveAttFiles)/{id}`), `multiple`, `fwentity` (entity code to auto-create in Att), plus layout keys.
- Common sample:
```json
{
  "type": "att_files_edit",
  "field": "_att_files",
  "label": "Files",
  "att_category": "general",
  "multiple": true
}
```
- Full sample with custom endpoint and prefix:
```json
{
  "type": "att_files_edit",
  "field": "_att_files_docs",
  "label": "Documents",
  "att_category": "docs",
  "att_post_prefix": "att_docs",
  "att_upload_url": "/Admin/Demos/(SaveAttFiles)/{id}",
  "fwentity": "demos",
  "multiple": true,
  "help_text": "Skips PATCH updates when the post prefix is absent"
}
```

#### type: subtable_edit
- Template: `/common/form/showform/subtable.html`.
- Options: `model` (required), `save_fields` (space-separated fields persisted per row), `save_fields_checkboxes` (checkbox defaults such as `is_active|0`), `required_fields` (per-row validation), plus any model-specific keys like `related_field_name` or `lookup_params`.
- Common sample:
```json
{
  "type": "subtable_edit",
  "field": "demos_items",
  "label": "Subtable",
  "model": "DemosItems",
  "save_fields": "iname qty",
  "save_fields_checkboxes": "is_checkbox|0"
}
```
- Full sample with validation and main-id override:
```json
{
  "type": "subtable_edit",
  "field": "line_items",
  "label": "Line Items",
  "model": "OrdersItems",
  "related_field_name": "orders_id",
  "save_fields": "product_id qty price",
  "save_fields_checkboxes": "is_taxable|0 is_gift|0",
  "required_fields": "product_id qty price",
  "help_text": "Rows are validated and saved via OrdersItems model"
}
```

### form_tabs

`form_tabs` allows organising large forms into multiple tabs. When more than one tab is present, the template `/common/form/tabs.html` renders the navigation.

Configuration example:
```json
{
  "form_tabs": [
    {
      "tab": "",
      "label": "Default"
    },
    {
      "tab": "general",
      "label": "General"
    },
    {
      "tab": "advanced",
      "label": "Advanced"
    }
  ],
  "showform_fields": [
    // default tab form fields
  ],
  "showform_fields_general": [
    {
      "field": "iname",
      "type": "input",
      "label": "Title"
    }
  ],
  "showform_fields_advanced": [
    {
      "field": "idesc",
      "type": "textarea",
      "label": "Description"
    }
  ]
}
```

Each entry defines the tab code (`tab`) and the text shown on the tab (`label`).
Fields for a tab should be placed in `show_fields_TAB` and `showform_fields_TAB` arrays where `TAB` is the value from `form_tabs`. If only one tab is defined the tab bar is hidden.
Active tab is set by `tab` parameter in the URL, e.g. `/Admin/DemosDynamic/123?tab=advanced`. If no tab is specified, the default tab is active.
