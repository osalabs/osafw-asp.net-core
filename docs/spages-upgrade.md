# Upgrading Spages to the block CMS

This upgrade adds working drafts and immutable revisions, then converts every existing Spages row through an explicit Site Admin action. There is no runtime fallback to the former Markdown templates or storage shape.

Read [Spages CMS](spages.md) for the resulting authoring, publication, routing, and API contracts.

## Prerequisites

Before changing an application:

1. Back up the application database and confirm its configured database provider.
2. Merge the Spages model and controller, ParsePage templates, assets, `libman.json`, schema files, and additive update for that provider. Preserve application-specific routes, layouts, and templates deliberately.
3. Restore client dependencies and build the application.
4. Review custom public Spages reads. Public consumers must use `onePublished`, `listPublished`, `listIndexable`, or a visibility check over `listPublicationsByDate`. Editor integrations use `oneDraftOrFail`, `saveDraft`, `updateWorkflow`, and `restoreRevision`.

## Apply the schema

Existing databases use the additive script selected by `FwUpdates`:

| Provider | Update file |
| --- | --- |
| SQL Server | `osafw-app/App_Data/sql/updates/upd2026-09-05-spages-cms.sql` |
| SQLite | `osafw-app/App_Data/sql/sqlite/updates/upd2026-09-05-spages-cms.sql` |
| MySQL | `osafw-app/App_Data/sql/mysql/updates/upd2026-09-05-spages-cms.sql` |

Use the application's normal `FwUpdates` flow so the provider-specific script is recorded in the update ledger. In development, pending updates are available through `/Admin/FwUpdates`; the existing `HomeController.IndexAction` check can redirect to the pending-update flow when `IS_DEV` and `is_fwupdates_auto_apply` are enabled.

Do not run a provider's `fwdatabase.sql` against an existing database. Fresh-schema files drop and recreate framework tables.

The additive update adds the block document, working draft, workflow, access, navigation, indexing, alias, and snippet fields to `spages`, plus the `spages_revisions` table. Applying the schema does not convert content.

## Convert existing rows

After the schema update succeeds:

1. Sign in as a Site Admin who can publish Spages content.
2. Open **Pages**.
3. Choose **Convert existing pages to blocks**.

The button sends an authenticated POST to `AdminSpagesController.MigrateAction` at `/admin/spages/(Migrate)`. Automated deployment tooling may send the same POST with an authenticated Site Admin session and the current `XSS` form token.

`MigrateAction` converts every row whose `draft_json` is empty, including inactive, deleted, and future-dated rows. It commits one row at a time, skips completed rows, and can be run again after an interruption.

For each row, migration:

- captures the complete pre-conversion row, including every original column and Markdown field, in an immutable **Original content** revision;
- converts supported Markdown structures and attachment references into the version-one block document;
- keeps unsupported or reference-dependent Markdown in an editable Markdown block;
- selects the matching article, sidebar, or three-column layout from the existing content columns;
- creates the initial saved or published revision with the existing publication status and effective date; and
- writes the working draft only after both revision snapshots are ready.

Unmigrated rows cannot be edited or sent through the new workflow. Public requests never perform migration writes.

## Review the result

Review every converted page before making a new publication:

- inspect unsupported Markdown and attachment references;
- confirm the selected layout and content regions;
- add captions and headings to tables where required;
- add alternative text or mark content images decorative;
- verify custom head, CSS, and JavaScript as trusted Site Admin content;
- check anonymous and restricted reads, navigation, search, sitemap output, and page-owned attachment URLs; and
- verify custom application routes still take precedence over CMS paths.

The page header image is decorative beside the page title and has no page-level alternative-text fields. Image blocks retain their own alternative-text and decorative controls.

Manual old paths belong in the page's `url_aliases` field, one local path per line. Actual published path history is also retained automatically when pages or their ancestors move. Scheduled paths take effect at their release time, and cancelled schedules do not create aliases.

Snippet keys now use the snippet row's stable `url` value without a prefix. Publish required snippets before pages that reference them. Page rollback and snippet rollback remain separate operations.

## Rollback planning

To roll back content after migration, restore the desired original or historical revision into a draft, inspect it, and publish the draft. The Original content revision preserves the complete source row for investigation or application-specific recovery.

Do not roll back only application binaries after authors begin using the new workflow. Database and application rollback requires the coordinated backup and recovery procedure prepared before the upgrade.

Fresh demo data contains three unpublished Spages drafts and does not replace the application homepage. The demo rows are seed data; they are not installed by `MigrateAction`.
