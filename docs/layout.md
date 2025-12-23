# Layout, theming, and CRUD header

This project uses ParsePage templates with a small set of reusable primitives that make the admin UI themeable without large structural rewrites.

## Layout primitives
- `.fw-app` wraps the whole page; `.fw-sidebar`, `.fw-main`, and `.fw-content` mark the primary regions.
- `.fw-pane` / `.fw-card` apply the shared surface treatment (background, radius, padding, shadow tokens).
- Optional `.fw-topbar-slot` appears when a template provides `topbar`; the `<body>` gains `has-topbar` so themes can offset content.
- Branding lives in `layout/brand_inner`, wrapped by `brand_navbar` and `brand_sidebar`; environment badges use `layout/beta.html`.

## CRUD page header contract
Each CRUD page should render the standard header DOM:
```
<header class="fw-page-header" data-page="crud">
  <div class="fw-page-header-row fw-page-header-row-1">
    <div class="fw-page-breadcrumbs">…</div>
    <div class="fw-record-nav ms-auto">Prev/Next links…</div>
  </div>
  <div class="fw-page-header-row fw-page-header-row-2">
    <div class="fw-page-title-wrap">
      <h1 class="fw-page-title">Title</h1>
      <span class="badge badge-outline-secondary rounded-pill">count</span>
    </div>
    <div class="fw-page-status ms-auto">
      <div class="fw-autosave-status" role="status" aria-live="polite"></div>
    </div>
  </div>
  <div class="fw-page-header-row fw-page-header-row-3">
    <div class="fw-page-actions d-print-none">Action buttons…</div>
  </div>
</header>
```
Row 1 holds breadcrumbs and prev/next navigation; row 2 holds the title, optional record count, and any statuses (autosave lives here); row 3 is for primary/secondary actions.

## Form sections and "On this page"
- Dynamic controllers can define `sections` in `config.json` with `id`, `title`, `collapsible`, `collapsedByDefault`, and `fields`.
- ShowForm pages render each section inside `<section class="fw-form-section" id="section-{id}">` with an optional collapse toggle.
- The “On this page” nav is generated from sections; it is hidden by default in `site.css` but structured for themes to surface or pin it.

## Tokens and theming
Start by overriding CSS variables instead of duplicating component rules. Core tokens include:
- Surface: `--fw-pane-bg`, `--fw-radius-card`, `--fw-shadow-card`, `--fw-card-padding-x/y`
- Layout: `--fw-page-header-gap`, `--fw-page-header-padding`, `--fw-page-header-bg/border`, `--fw-sidebar-width`, `--fw-sidebar-bg`, `--fw-sidebar-link-color`, `--fw-sidebar-section-title-*`, `--fw-topbar-bg`
- Components: `--fw-dashboard-card-shadow/radius/padding-*`, `--fw-prose-font-size/line-height/heading-color`, `--fw-form-section-gap/border/radius`, `--fw-brand-logo-height`
- Accents: `--fw-btn-default-*`, `--fw-hiliter`

Theme 20/30 now focus on overriding these variables plus a handful of targeted selectors (e.g., `.fw-nav-section-title`) to keep their identity with less duplication.

## Extension points
- Topbar slot (`topbar` template) for future Falcon-like layouts.
- Page header actions/status slots for module-specific buttons and autosave messaging.
- Form sections and On-this-page nav for long forms (hidden by default but present in markup).
- Dashboard cards use `dashboard-card` + chart containers with shared padding/shadow tokens.
