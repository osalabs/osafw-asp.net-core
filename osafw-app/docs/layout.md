# Screen layout

This document describes the overall screen layout used by major dashboard, list, view, and edit screens, along with theming guidance and the standard CRUD page header conventions.

## Layout primitives

The framework organizes pages using a small set of reusable layout primitives. These primitives appear across the major screen types (dashboard, list, view, edit) to keep information density consistent and predictable.

- **App shell**
  - Global container that wraps the main navigation, page header, and content area.
  - Used by all primary screens to ensure navigation and theming are consistent.
  - Page-level content is typically produced by ParsePage templates under `App_Data/template/<area>/<controller>/` or shared includes under `App_Data/template/common/`.
- **Page header**
  - Standardized area for the title, context actions, and breadcrumbs.
  - CRUD headers are built from the shared partials in:
    - `App_Data/template/common/form/page_header.html`
    - `App_Data/template/common/form/page_header_edit.html`
    - `App_Data/template/common/form/page_header_custom.html`
    - `App_Data/template/common/list/page_header.html`
    - `App_Data/template/common/list/page_header_compact.html`
  - The breadcrumb slot is rendered via `<~/common/form/breadcrumbs>` and `page_header_breadcrumbs` includes.
  - Vue screens mirror this via `App_Data/template/common/vue/*-header.html`.
- **Content region**
  - Main scrollable area for each screen.
  - Dashboard screens typically divide this region into panels/cards (example: dashboard cards in `App_Data/template/main/index/std_pane.html`).
  - List screens emphasize data tables and filters (example: list tables wrap in `.fw-list-card` via `App_Data/template/common/list/form_list.html`).
  - View/edit screens emphasize details, form fields, and secondary panels (example: `.fw-card` forms in `App_Data/template/**/showform/form.html`).
- **Panels / cards**
  - Optional blocks that group related data or controls.
  - Use `.fw-card` for form and detail panels; list tables use `.fw-list-card`.
  - Dashboard cards frequently use Bootstrap `.card` styling (see `App_Data/template/main/index/std_pane.html`).
- **Utility rails**
  - Optional sidebars for filters, inline help, or related actions.
  - Keep secondary tasks available without breaking primary reading flow.
  - List filters are typically rendered via `App_Data/template/common/list/filter_std.html` or `filter_compact.html` and use `.fw-card`.

**Major screen layout guidance**

- **Dashboard screens**
  - Use cards or panels to display KPIs, charts, and quick actions.
  - Place overview metrics above supporting detail panels for fast scanning.
- **List screens**
  - Lead with filters, search, and bulk actions.
  - Table or list content should stay within the primary content region to keep headers sticky.
- **View screens**
  - Present key identity fields (title, status, ownership) at the top.
  - Use panels for related records or recent activity.
- **Edit screens**
  - Keep the header and primary form fields immediately visible.
  - Secondary or advanced fields can move into collapsed panels or tabs.

## CRUD page header

CRUD screens share a standard header structure to make actions and navigation consistent. The shared header templates live under `App_Data/template/common/form/` and `App_Data/template/common/list/`, with Vue equivalents under `App_Data/template/common/vue/`.

**Recommended structure**

1. **Breadcrumbs**
   - Show the navigation path for list → view/edit transitions.
   - Uses `<nav class=\"page-header-breadcrumbs\">` markup in `common/form/page_header*.html` and `common/list/page_header*.html`.
2. **Page title + record context**
   - Primary title for the entity.
   - Optional subtitle for identifiers or status.
3. **Primary actions**
   - Save, Create, or Update actions aligned to the right.
   - Common buttons are injected by shared header includes; additional buttons go into the header action slots.
4. **Secondary actions**
   - Delete, Archive, or custom workflow actions grouped separately.
   - Use the custom header slots in `page_header_custom.html` when needed.

**Behavior guidelines**

- Keep primary actions visually dominant.
- Use a consistent placement so users can predict where to save or edit.
- Avoid overcrowding the header; move rarely used actions into menus.
- When adding custom actions, keep them inside the shared header templates so list/view/edit screens stay aligned.

## CSS tokens and theming

Theming uses CSS tokens to keep layout and color consistent across screens. Use these tokens to avoid hard-coded values and ensure dark/light mode readiness.

**Where tokens live**

- Global styles and tokens are defined in `App_Data/template/common/head.css` and theme-specific CSS under `App_Data/template/common/theme*/`.
- UI primitives such as `.fw-card`, `.fw-list-card`, and `.page-header-breadcrumbs` are styled in these shared stylesheets and reused across templates.

**Common token categories**

- **Spacing**: consistent padding, margins, and gaps for layout primitives.
- **Typography**: font families, sizes, weights for headers vs. body.
- **Color**: primary accents, neutral backgrounds, borders, and status colors.
- **Elevation**: shadows or borders for panels and cards.

**Theming guidance**

- Prefer tokens for colors and spacing when styling components.
- Ensure sufficient contrast for headers, buttons, and data tables.
- Keep dashboard and CRUD screens visually aligned through shared spacing and typography tokens.

## Extension points

Use these extension points to adapt the layout to new modules or custom features.

- **Custom header actions**
  - Add module-specific actions to the CRUD header secondary actions area.
  - Use the shared slot templates (`common/form/page_header_custom.html`) so the base header remains consistent.
- **Dashboard widgets**
  - Insert new panels/cards into the dashboard content region.
  - Follow the `.card` markup in `App_Data/template/main/index/std_pane.html` for consistent visuals.
- **List/table enhancements**
  - Add filters, bulk actions, or column toggles in the list screen utility rail.
  - Use `common/list/filter_std.html`, `common/list/filter_compact.html`, and `common/list/form_list.html` as the starting point.
- **Detail view panels**
  - Expand view/edit screens with related-record panels or activity feeds.
  - Use `.fw-card` panels and the shared view/edit form templates (`**/showform/form.html`) to keep spacing consistent.
- **Theme overrides**
  - Swap token values for branded themes while keeping layout primitives intact.
