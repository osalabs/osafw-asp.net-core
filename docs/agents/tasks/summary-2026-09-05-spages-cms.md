# Spages CMS first version

## Objective / acceptance

Provide an optional, extensible CMS for small public websites and application pages using the framework's routing, ParsePage, permissions, attachments, and themes. Deliver the authorized `codex/spages-cms` branch and one review PR against `master`. Keep homepage ownership opt-in and retain lean extension points instead of introducing a general page builder or separate search service.

## What changed

- Self-hosted Editor.js 2.31.6 with framework-owned structured tools, server sanitization/rendering, accessible authoring controls, curated page layouts, breadcrumbs, autosave, explicit saves, and conflict preservation.
- Working drafts, immutable history, configurable author/publisher thresholds, review notes, authenticated private previews, date-based approved revisions, cancellation, withdrawal, and restore-to-draft rollback.
- Named shared or page-associated snippets, affected-page lists, dependency/access validation, historical preview pins, and independent snippet rollback. Snippets use one main region and cannot nest.
- Effective-publication accessors shared by public routes, navigation, discovery, attachments, and optional RAG. Ancestor restrictions and stale-index exclusion apply at retrieval.
- Search, canonical metadata, HTML/XML sitemaps, manual aliases, and automatic descendant redirects at publication boundaries. New public templates use framework tokens for responsive layouts and all default themes.
- Matching SQL Server/SQLite fresh and additive schemas, explicit MySQL skip, recoverable/resumable all-row Markdown conversion, and separate demo drafts with shared help content.
- Canonical contracts and migration guidance are in [Spages CMS](../../spages.md) and [the upgrade guide](../../spages-upgrade.md); the dated changelog records compatibility changes.

## Requirements / decisions

The public [FPF specification](https://github.com/ailev/FPF/blob/main/FPF-Spec.md), September 2026, informed the snippets decision through B.5.2 and C.17. The ignored cache was refreshed after accommodating the upstream Table of Contents heading change. SHA-256: `8c71c4a22c1e0b2bff154a2953010e591ef517e052407289164e885ab72657f5`.

The comparison is qualitative, based on code inspection and the requested use cases; implementation verification is separate evidence.

| Alternative | Editor simplicity / reuse / compatibility / cost | Decision |
| --- | --- | --- |
| Copy content between pages | Simple editing and little implementation cost, but shared changes drift and require repeated maintenance. | Insufficient for requested reuse. |
| Named reusable snippets | One familiar content editor, stable keys, shared revision machinery, and bounded dependency checks. | Include. |
| Nested slots and general builder | Flexible composition, but substantially more editor, dependency, access, and migration complexity. | Defer. |

Publication resolves eligible snapshots per request at whole-second UTC boundaries. A future revision preserves the earlier release; withdrawal does not fall back. Raw table queries are not an authoritative publication API. Compatibility reads wrap rendered regions for existing Markdown ParsePage consumers; string/DTO consumers must explicitly convert the wrapper. Unsupported Markdown and unavailable editor tools retain their source rather than disappearing.

## Scope reviewed

CMS model/controller/content tools; legacy Spages accessors and ParsePage Markdown behavior; Home/Search/Sitemap routes; attachment serving and RAG indexing/retrieval; frontend templates/assets; provider schemas/update discovery; configuration defaults; samples and canonical docs.

Two bounded `implementation_sol_high` packets supplied frontend polish and focused access/RAG tests with disjoint file ownership. The primary integrated them and performed browser/provider verification. A fresh `reviewer_astra_xhigh` reviewed without implementation history because publication, access, file serving, migrations, and source-copy compatibility interact. Security-boundary and state-integrity overlays were selected. Its initial six findings concerned review-note autosave, attachment RBAC, pre-conversion editing, mixed-case redirect loops, base-path links, and conversion recovery visibility. Re-review extended the conversion guard to every workflow transition. All six received code fixes and targeted regression checks. The reviewer also inspected the single-region snippet rule, independent demo choices, and active summary; it reported no supplemental summary/privacy findings. Final adjudication: No blocking findings. Review loop can stop.

## Commands used / verification

- Rebased onto `master` commit `d1c1490d` before delivery. Its runtime-type model lookup introduced eight tests; the only merge conflict was the task index, resolved by retaining both entries. Range comparison confirmed no CMS code changed during rebase.
- `dotnet test osafw-tests/osafw-tests.csproj --nologo`: 764 passed on the rebased branch, no warnings.
- `dotnet test osafw-tests/osafw-tests.csproj --nologo -p:DefineConstants=isSQLite --filter 'FullyQualifiedName~SpagesCmsTests|FullyQualifiedName~SpagesContentTests'`: 23 passed on fresh SQLite and 23 with the prior-master SQLite baseline. The final rebased full SQLite run (`--no-build --no-restore -p:DefineConstants=isSQLite`) passed 790 tests. The preceding run had 789 passes and one existing `DBTests.sqlNOWTest` failure when its two operations crossed a one-second boundary; one bounded full rerun passed without changing that test.
- `dotnet test osafw-tests/osafw-tests.csproj --nologo --no-build --no-restore -p:DefineConstants=isSQLite --filter FullyQualifiedName~SpagesCmsTests`: 18 passed with the approved SQL Server connection, and 18 with that connection plus the prior-master SQL Server baseline. Upgrade fixtures discover the provider script with `FwUpdates`, apply only this CMS update, and verify the ledger after reloading. Content conversion repeatability, deleted-row recovery, original/column preservation, and pre-conversion workflow rejection are covered.
- `dotnet test osafw-tests/osafw-tests.csproj --nologo '-p:DefineConstants=isSQLite%3BisRoles' --filter FullyQualifiedName~DraftAttachmentRequiresPagePreviewPermissionInAdditionToAttView`: one passed on SQLite and one on SQL Server. A real manager with Att/view but no AdminSpages/view cannot fetch a draft attachment; granting page-view permission allows it. Anonymous referenced published files remain accessible.
- `node --check osafw-app/wwwroot/assets/js/spages-editor.js` and the corresponding `spages-redirects.js` command passed. Production search and HTML sitemap templates rendered `/portal` base-path links correctly in the integration tests.
- All 19 isolated HTTP checks passed on the final runtime for draft/preview authorization and headers, POST/XSS protection, multipart page-owned upload, public/restricted file serving, current search/sitemap, withdrawal, genuine 404, and scheduled content/URL transitions without a background worker.
- Chrome desktop captures covered public pages and the editor in Default, Pink, Shadows, and Blue, each in light and dark. Product Design assessment drove reading-width, spacing, contrast, focus, navigation, and publishing-panel refinements. Mobile public/editor checks at 390 by 844 showed stacked layouts without horizontal overflow. Keyboard checks covered title-to-content focus and heading controls with visible focus indication.
- Chrome round trips covered demo installation/publication, independent demo install buttons preserving existing content, unavailable-block JSON retention and invalid-JSON preservation, concurrent-tab conflict preservation, and request-changes after entering a review note. The final snippet editor exposed only Article/main. File upload was exercised through actual multipart HTTP; native browser file chooser automation and an exhaustive keyboard/screen-reader audit were not performed.

## Testing instructions

Use a task-owned SQLite file or an explicitly approved disposable SQL Server database. The integration fixture rejects SQL Server catalogs other than its named verification database. Set `SPAGES_CMS_TEST_SQLSERVER` privately for SQL Server, and optionally `SPAGES_CMS_TEST_BASELINE` to a local prior-schema file for upgrade tests; never put connection details or database files in Git. Run the `SpagesCmsTests` filter with `isSQLite` compiled for either provider. The default build does not exercise SQLite-only CMS integration tests.

For a copied app, review the additive script, convert existing rows explicitly as Site Admin, inspect unsupported content, configure the public origin, and test its custom routes/templates and access levels. Demo installation is separate and creates drafts only; publish the help snippet before its pages.

## Risks / follow-ups

- The configured development database and original Visual Studio app were not modified. Runtime and uploads used a separate worktree instance and disposable SQLite database. The explicitly authorized SQL Server verification database was removed after the final passing runs, and the task-owned preview process was stopped. Small local runtime evidence remains ignored.
- The existing optional SQLite dependency reports NU1903 for SQLitePCLRaw.lib.e_sqlite3 2.1.11 (GHSA-2m69-gcr7-jv3q). This dependency warning predates the CMS change. No MySQL CMS parity, production IIS deployment, or Linux runtime verification is claimed.
- Search/publication validation intentionally targets small collections and materializes relevant content per request. Custom caches and direct SQL/RAG consumers must follow the documented effective-publication contract. History retention, anonymous previews, multistage approvals, nested builders, and separate indexing infrastructure remain application extensions.

## Reflection

The fresh review caught interacting workflow/access failures that happy-path checks did not expose. Real browser editing and provider-backed entry-point tests supplied different evidence: editor events and ParsePage includes need runtime checks, while permission combinations, publication boundaries, and interrupted conversion need explicit negative controls. Keep these targeted checks alongside the canonical contracts rather than treating full-suite counts as evidence for untested UI behavior.
