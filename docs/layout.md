# Screen Layout

This document describes the structural layout used by dashboard, list, view, and edit screens. For visual rules, theme tokens, colors, spacing, and component examples, use [design_system.html](design_system.html).

## Layout Primitives

The framework organizes pages with a small set of reusable primitives so dashboard, CRUD, and generated screens stay predictable.

- **App shell**
  - `layout.html`, `layout_public.html`, and `layout_vue.html` set the document shell, `data-fw-theme`, `data-bs-theme`, sidebar, and main content region.
  - Shared shell fragments live under `osafw-app/App_Data/template/layout/`.
- **Page header**
  - Standard area for breadcrumbs, title, record navigation, metadata, and actions.
  - CRUD headers are built from shared partials under `osafw-app/App_Data/template/common/form/` and `osafw-app/App_Data/template/common/list/`.
  - Vue screens mirror the same structure through `osafw-app/App_Data/template/common/vue/*-header.html`.
- **Content region**
  - Dashboard screens use panes/cards from `osafw-app/App_Data/template/main/index/`.
  - List screens use filters plus `.fw-list-card` table wrappers, especially `common/list/filter_compact.html` and `common/list/form_list.html`.
  - View/edit screens use `.fw-card`, form rows, fieldsets, tabs, and shared form fragments under `common/form/show/` and `common/form/showform/`.
- **Panels and cards**
  - Use `.fw-card` for forms, filters, and detail panels.
  - Use `.fw-list-card` for list-table wrappers.
  - Dashboard cards use Bootstrap `.card` plus `.dashboard-card`.

## Screen Guidance

- **Dashboard screens**
  - Put overview metrics above supporting detail.
  - Use the dashboard pane templates and visual rules in [design_system.html](design_system.html#components).
- **List screens**
  - Lead with filters, search, and bulk/list actions.
  - Keep tables inside the primary content region so sticky headers and horizontal scrolling helpers work.
- **View screens**
  - Put identity fields, status, and ownership near the top.
  - Use panels for related records, attachments, or activity.
- **Edit screens**
  - Keep save/cancel actions in shared header/action slots.
  - Put advanced or secondary fields in fieldsets or tabs instead of adding local spacing one-offs.

## CRUD Page Header

CRUD screens share a standard header structure to make actions and navigation consistent.

1. **Breadcrumbs**
   - Show the navigation path for list to view/edit transitions.
   - Use `common/form/breadcrumbs` and the `page_header_breadcrumbs` slot.
2. **Page title and record context**
   - Use one primary page title.
   - Put counts, record status, or other compact metadata beside the title or in the metadata slot.
3. **Primary actions**
   - Keep Save, Add New, Edit, and similar primary actions in the shared action row.
4. **Secondary actions**
   - Put Delete, Archive, export, or custom workflow actions in the secondary/right action area or in menus.

When adding custom actions, keep them inside shared header slots such as `page_header_actions`, `page_header_actions_right`, or `page_header_custom.html` so screens remain aligned.

## CSS And Theming

Active global styles and theme files live under:

- `osafw-app/wwwroot/assets/css/site.css`
- `osafw-app/wwwroot/assets/css/theme10.css`
- `osafw-app/wwwroot/assets/css/theme20.css`
- `osafw-app/wwwroot/assets/css/theme30.css`

The load order is Bootstrap CSS, Bootstrap Icons, `site.css`, and then the optional `themeXX.css`. Layout templates include the optional theme through `layout/theme_link.html` when `GLOBAL[ui_theme]` is set.

Static template icons should be rendered through `common/icons/*` partials. Each icon partial includes its own `me-1` spacing, so layout and screen templates should place labels immediately after the include instead of adding manual whitespace.

Use [design_system.html](design_system.html#tokens) for token categories and [design_system.html](design_system.html#customizing) for customization rules. In short:

- Prefer Bootstrap utilities for local layout adjustments.
- Prefer framework CSS variables for shared color, spacing, sizing, and component behavior.
- Prefer theme files for branded visual changes.
- Add selector-specific CSS only when the token layer cannot express the needed behavior.

## Extension Points

- **Custom header actions**
  - Add module actions through shared header slots rather than local header markup.
- **Dashboard widgets**
  - Add `type_NAME.html` under `osafw-app/App_Data/template/main/index/` and register it in `std_pane.html`.
- **List/table enhancements**
  - Start from `common/list/filter_compact.html`, `common/list/filter_std.html`, and `common/list/form_list.html`.
- **Detail view panels**
  - Use `.fw-card`, shared form fragments, fieldsets, and tabs before creating local layout structures.
- **Theme overrides**
  - Override Bootstrap and framework custom properties in a theme file while keeping shared markup intact.
