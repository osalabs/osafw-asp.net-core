# Spages CMS first version

## Objective / acceptance

Provide a lean, extensible CMS for small public websites and application pages using framework routing, ParsePage, users, attachments, and themes. Deliver branch `codex/spages-cms` and one PR against `master`. CMS ownership of `/` remains an application developer's explicit choice. The September 6 feedback supersedes the initial plan's configuration switches, edit-version tokens, separate redirect table, runtime compatibility projections, and sample installer.

## What changed

- Self-hosted Editor.js 2.31.6 with framework-owned validation and rendering, curated layouts, image-block accessibility controls, breadcrumbs, draft autosave, and immutable explicit saves. Latest save wins; failed saves retain the editor's input.
- One Spages model and one AdminSpages controller. Author/publisher levels are model constants, both manager by default. No CMS enable/origin settings; canonical URLs use `ROOT_DOMAIN`, and Home contains a commented CMS integration example.
- Review, publication dates, cancellation, withdrawal, private authenticated previews, and restore-to-draft rollback. Public reads resolve eligible approved revisions per request without a scheduler.
- Snippets use `is_snippet` and the existing `url` as their stable key. Reuse shares the revision workflow, validates dependencies/access, pins historical previews, and keeps snippet rollback separate. Nested snippets are excluded.
- Manual aliases are newline-separated `url_aliases` on the page. Actual prior paths, including descendant moves and scheduled transitions, are derived from publication history. Redirects cannot be stored by clients after later publication changes.
- Public navigation, search, HTML/XML sitemaps, attachment access, and optional RAG use effective publications and ancestor restrictions. `listIndexable` owns the repeated indexability filter. Missing or inaccessible media is omitted during reads; approval remains strict.
- Matching fresh/additive schemas and demo seeds for SQL Server, SQLite, and MySQL. New boolean fields use `is_`; workflow and revision kind are numeric. Added columns have comments, and indexes follow provider/table conventions. Page-level image accessibility columns and the redirect table were removed.
- One-time `AdminSpagesController.MigrateAction` converts every unconverted row, retains full original snapshots and source columns, preserves unsupported Markdown in editable blocks, and commits per row for resumability. Public requests never convert data or fall back to old Markdown storage.
- Demo SQL provides two page drafts and one shared help snippet without replacing the application homepage. Current documentation is in [Spages CMS](../../spages.md); conversion instructions are in [the upgrade guide](../../spages-upgrade.md). The changelog contains breaking upgrade contracts only.

## Requirements / decisions

The public [FPF specification](https://github.com/ailev/FPF/blob/main/FPF-Spec.md), September 2026, informed the snippets decision through B.5.2 and C.17. The ignored cache was refreshed after accommodating the upstream Table of Contents heading change. SHA-256: `8c71c4a22c1e0b2bff154a2953010e591ef517e052407289164e885ab72657f5`.

The comparison is qualitative, based on code inspection and the requested use cases; implementation checks establish separate reliability evidence.

| Alternative | Simplicity, reuse, compatibility, and cost | Decision |
| --- | --- | --- |
| Copy content between pages | Easy editing and little implementation cost, but shared changes drift. | Insufficient for requested reuse. |
| Named reusable snippets | Familiar editor, stable keys, shared revision machinery, bounded dependency checks. | Include. |
| Nested slots and general builder | Flexible composition with substantially more authoring and dependency complexity. | Defer. |

The framework owns publication/access rules and sanitized HTML. ParsePage has no Spages-specific code; CMS templates use its existing `noescape` option explicitly. Public consumers use `onePublished`, `listPublished`, or `listIndexable`; unfiltered revision resolution and direct table queries are not public authorization boundaries. Publication timing uses whole-second UTC application values and normal provider timezone conversion.

## Scope reviewed / delegation

Reviewed model/controller/renderers, established public entrypoints, Home/Contact/Search/Sitemap templates, attachment serving, RAG indexing/retrieval, frontend assets, provider schemas/update discovery, migration recovery, and documentation.

The initial frontend polish and access/RAG test packets used `gpt-5.6-sol` with high reasoning. The primary wrote/integrated the core CMS and owned final acceptance. The feedback pass reused the frontend agent for editor/docs simplification and used `implementation_astra_xhigh` (`gpt-6-astra`, xhigh) for schema/provider regressions and bounded recovery. File leases separated their changes from primary integration. Test formatting and dictionary cleanup were syntax-aware; unrelated work was preserved.

A fresh `reviewer_astra_xhigh` (`gpt-6-astra`, xhigh) reviewed the diff against `origin/master` `d1c1490d` before reading this summary. Security-boundary and state-integrity overlays were selected because publication, attachment access, migration, and copied-app contracts interact. Its four findings were unavailable-media reader failures, cacheable redirects, parent moves exceeding descendant path/depth limits, and navigation-hidden children appearing in related links. All four received fixes and negative/positive regressions; the reviewer confirmed the fixes. Chrome exposed two further issues: a ParsePage variable/include name collision in the search summary and weak editor keyboard focus. Both were fixed and rechecked. The supplemental summary audit found no factual or privacy findings. Final adjudication: **No blocking findings. Review loop can stop.**

## Commands used / verification

All final builds use absolute `OutDir` paths under ignored `artifacts/assistant_spages_feedback/`. TRX evidence is under its `tests/results/` directory. Earlier counts in this task are superseded by the following final runs.

| Check | Command / result |
| --- | --- |
| Default suite | `dotnet test osafw-tests/osafw-tests.csproj -p:OutDir=<task>/tests-default-final/ --logger trx`: **766 passed**, no warnings. |
| Full SQLite suite | Same command with `-p:DefineConstants=isSQLite` and `tests-final/`: **811 passed**. |
| SQL Server fresh + upgrade | `dotnet vstest <task>/tests-final/osafw-tests.dll /TestCaseFilter:"FullyQualifiedName~SpagesCmsTests\|FullyQualifiedName~SpagesContentTests\|FullyQualifiedName~SpagesCmsAccessTests"` with the guarded disposable connection: **44 passed** fresh and **44 passed** from the prior schema plus provider update. |
| SQLite upgrade | Same focused assembly/filter with an isolated SQLite prior-schema baseline: **44 passed**. |
| Role permissions | Focused recovery suite with `-p:DefineConstants=isSQLite%3BisRoles`: **44 passed** before the later independent search-summary test was added. |
| MySQL compile | `dotnet build osafw-app/osafw-app.csproj -p:DefineConstants=isMySQL -p:OutDir=<task>/build-mysql-final/`: **0 errors**, two existing DB nullable warnings. Fresh/update/demo SQL reviewed statically; no MySQL server execution claimed. |
| Frontend / repository | `node --check osafw-app/wwwroot/assets/js/spages-editor.js`; retired-contract scans, documentation links, strict UTF-8/no BOM/CRLF, and `git diff --check`. |

The integration tests cover draft isolation, permissions, previews, historical snippets, rollback, latest-save behavior, immediate/scheduled URL boundaries, cancellation/withdrawal, reserved/colliding paths, ancestor access and depth, navigation/search/sitemap/RAG, attachment readers/ownership, and migration fidelity/repeatability. Regressions for the independent review findings failed before their fixes. SQL NULL publication timestamps exposed a migration issue during initial provider checks; the final parser handles null/empty values explicitly.

Chrome verification used a separate loopback host, port, SQLite database, uploads, and process in the original checkout. Both demo pages were published after their shared snippet. Screenshots of the public page and initialized editor were captured and inspected in Default, Pink, Shadows, and Blue, each in light and dark. At 390 by 844, public and editor content stacked without horizontal overflow. Product Design assessment used the current captures and framework tokens; final focus inspection reported a visible 2px outline. The revised search summary displayed two matching consumer pages for shared snippet text.

Chrome also verified status filtering, manual aliases, explicit draft saves, autosave persistence across reload, public isolation from changed drafts, authenticated preview banners, and restoring a published revision into a new draft. The existing test page retained its route and bottom footer. Native file-chooser automation, exhaustive keyboard/screen-reader evaluation, and every mobile theme combination were not repeated in this feedback pass; model/controller attachment behavior and all eight desktop combinations were checked.

## Testing instructions

Use an isolated SQLite file or an explicitly approved disposable SQL Server database. `SPAGES_CMS_TEST_SQLSERVER` selects the guarded SQL Server fixture; `SPAGES_CMS_TEST_BASELINE` optionally supplies a prior provider schema for additive-upgrade checks. Keep connection details and database files outside Git. CMS provider integration tests require `isSQLite` even when their runtime provider is SQL Server; the default suite alone does not execute them.

For a copied app, review its provider update, run the Site Admin conversion action, inspect unsupported content and custom templates, then verify public/restricted routes and attachments. The migration action can be removed from that application after conversion. Customize model constants and the commented Home integration in source. Publish the help snippet before its demo consumers.

## Risks / cleanup

- The configured development database was repaired and converted after explicit authorization, as recorded below; no Visual Studio restart was needed. The original checkout remains on `codex/spages-cms`; no extra CMS worktree is needed. The task-owned SQL Server database was dropped after final passes, and the recorded isolated runtime process was stopped. Ignored build/runtime/TRX evidence remains under `artifacts/assistant_spages_feedback/`.
- SQLite reports the pre-existing NU1903 warning for SQLitePCLRaw.lib.e_sqlite3 2.1.11 (GHSA-2m69-gcr7-jv3q). MySQL runtime, production IIS deployment, and Linux runtime verification remain unperformed.
- Resolution/validation intentionally materializes small page collections per request. Application caches must consider publication dates, audience, and snippet dependencies. History pruning, translations, anonymous previews, multistage approvals, nested builders, and separate search infrastructure remain application extensions.

## September 6 development-schema repair

After the feedback pass, restarting the existing app exposed SQL error 207 in `Spages.listPublicationsByDate`: its revision table still used `cancelled` and textual `kind`. Read-only schema inspection confirmed that the initial CMS preview script was already applied. `FwUpdates.loadUpdates` retains both the filename and the SQL first loaded into its ledger, so replacing that migration file could not update this database. Earlier upgrade verification started from the pre-CMS framework schema and missed the already-applied preview path.

A bounded, one-time repair and verification harness are retained under ignored `artifacts/assistant_spages_schema_repair/`. They are deliberately outside normal application updates: the preview was unpublished, and the developer requested no permanent compatibility layer. The repair maps the revised columns and snapshot metadata, preserves content and revision identities, converts revision creation timestamps from the preview's UTC default to the database timezone, and removes the empty retired redirects table. It accepts only draft pages and saved revisions, rejects populated redirects or retired image metadata, locks affected tables, and rolls back on failure. The original update ledger is preserved.

The SQL Server harness reproduced the exact admin failure from the pre-CMS schema plus the first preview script. After repair it checked the current 34/10-column table shapes, long Unicode block content, saved revisions, timestamp instants, snippet keys, navigation/indexing flags, admin listing, draft isolation, draft save, and publication through current model/controller entrypoints. Wrong targets and changed state were rejected; an injected failure after DDL demonstrated rollback. `dotnet build artifacts/assistant_spages_schema_repair/verify.csproj -p:OutDir=<task>/bin-verify/` completed with zero warnings/errors; `dotnet <task>/bin-verify/verify.dll` passed all assertions. The SQL Server database used was the same explicitly authorized disposable resource. No current runtime source or provider release schema changed, so other provider suites and theme captures were not repeated.

A fresh `reviewer_astra_high` (`gpt-6-astra`, high) performed independent state-integrity review before reading this summary. Line-ending observations were corrected and an explicit timestamp assertion was added. The reviewer inspected the application runner, final evidence, and entire active summary; final adjudication was **No blocking findings. Review loop can stop.** The disposable database was removed after verification. The upgrade guide now explains the stored-script behavior and why resetting the ledger or replaying the CMS schema is inappropriate. There is no additional breaking change to record in the changelog.

The user subsequently authorized local development database changes as needed during development. The reviewed script hash was checked before executing `apply.ps1`; the repair preserved all six pages and three saved revisions. The existing Site Admin conversion action then converted the two remaining original pages, leaving zero unconverted rows and adding two Original content and two Published revisions. Chrome verified the admin list, initialized editor, and public page; an anonymous request to `/test-page` returned HTTP 200 without a SQL exception. No restart or runtime-source change was necessary. The only browser console error observed was the unrelated Visual Studio Browser Link endpoint returning 404.

## Reflection

The initial implementation exceeded the requested framework scope and was insufficiently readable. High reasoning profiles did not replace primary responsibility for a lean design and final integration. The feedback pass removed configuration/projection/concurrency ceremony, aligned provider schemas, and added checks at real reader boundaries. Browser observations and provider-backed regressions caught different failures; full-suite counts alone did not establish usability or public-read reliability. A concrete improvement is to check proposed framework fields and public helpers against the lean source-customization boundary before implementation delegation.
