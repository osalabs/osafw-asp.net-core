# Spages CMS

Spages is the framework's block CMS for small public websites, intranets, and application content. It uses framework users, permissions, attachments, routing, themes, and ParsePage templates. Authors edit a working draft; public requests read immutable published revisions.

Existing applications must complete [the Spages upgrade](spages-upgrade.md) before using the editor. Fresh database schemas contain the required fields and revision table; the separate provider `spages.sql` script installs the starter pages during initialization.

## Access and routing

`Spages.AUTHOR_LEVEL` and `Spages.PUBLISHER_LEVEL` both default to `80` (`Users.ACL_MANAGER`). Read-only users cannot author or publish. Site Admin access is required for the one-time content migration and for custom head, CSS, and JavaScript.

`HomeController.NotFoundAction` sends unmatched local paths to `Spages.showCmsPage(path)`. Native controllers and application routes therefore keep precedence. To let a CMS page own `/`, uncomment the existing lines in `HomeController.IndexAction`:

```csharp
fw.model<Spages>().showCmsPage("/");
return null!;
```

Canonical URLs use the existing `ROOT_DOMAIN` setting and the published page path. Keep `ROOT_DOMAIN` as an absolute HTTP or HTTPS base. `ROOT_URL` remains the application base for local links.

## Authoring

Open **Pages** from the manager menu to create a page or snippet. A page has a title, URL segment, optional parent, navigation and search metadata, layout, content regions, optional after-content snippet, and publication controls. The standard dynamic list supports filtering, sortable and customizable columns, checkboxes, bulk actions, and pagination. Content kind is available through Customize Columns but is not shown by default; snippet rows have a Snippet badge beneath the title. The status and content-kind filters use the standard `- all -` option, with `[Deleted]` in the status filter. Sorting by Title groups child pages below their parents, with siblings following the selected direction. Other sorts remain flat. Filters run in SQL before hierarchy ordering and pagination; matching children remain visible when their parent is filtered out. Title sorting reads the filtered list columns to arrange the hierarchy, without loading content documents. The URL/key and View action are always linked: accessible published pages open their public URL; unpublished pages, inaccessible public versions, and snippets open an authorized draft preview. Link titles distinguish the two destinations. List and editor show a green Published badge whenever a current approved version exists, alongside Draft, In review, Changes requested, or Scheduled when applicable. A clean published page shows only Published. Publication badges describe the approved version; audience and ancestor restrictions still govern access to it. Both screens use the same status colors.

The editor keeps Content, Navigation and Search, Image, Custom, and Revisions in top tabs; executable customization remains restricted to Site Admins. The header includes the standard record search and Previous/Next links, current status, View page, Save draft, Preview, and a green Publish Now action for authorized publishers. Search includes non-deleted working titles, and Previous/Next follows the current list filters and ordering. Breadcrumbs link to each parent’s edit screen, including when adding a subpage. Header Publish Now saves pending edits and publishes immediately, regardless of the date in the scheduling panel. Use the Content tab’s publication panel to schedule a future release. View page opens the currently accessible published URL in a new tab without saving; it stays available when a working draft or scheduled revision exists. It is disabled when no accessible publication exists, with a message distinguishing unpublished content from a higher access requirement, and omitted for snippets.

Navigation and Search groups URL/access, navigation, search engines, and redirects. The parent selector distinguishes the top level and site root. The URL prefix follows the selected parent’s working-draft path. Navigation visibility and the request to prevent search-engine indexing use checkboxes; unchecked values are saved explicitly. Settings use standard horizontal framework form rows. URL and access occupies three quarters of the settings row on wide screens, with a compact Navigation section beside it. These sections stretch to equal height on wide screens; Show in navigation is a single checkbox label. Access-level and order controls fit their values; Search engines and Redirects each occupy a full-width section. These groups use collapsible `fw-fieldset` sections. Image and Custom use plain form rows with compact label columns and no redundant section legend. Custom Head HTML, Custom CSS, and Custom JavaScript provide placeholder examples; CSS and JavaScript omit their surrounding HTML tags. Labels and columns stack on small screens. CMS controls inherit the framework’s theme styles.

The Image tab uses the standard Select / Upload modal for the page banner, with a recommended 1110 × 300 px image. It opens with the Page banners category selected; the editor may change the category, and new uploads retain that selection. Its library is limited to this page’s images and unbound public images; uploads are attached to the current page. Removing the selected page image clears the draft reference and does not delete the uploaded file.

The bundled Editor.js core is pinned in `libman.json`. Built-in blocks include paragraphs, H2-H6 headings, flat lists, quotes, images, files, tables, code, dividers, callouts, cards, buttons, Markdown, and snippets. The page title supplies H1. Headings may define a link anchor.

Content images require alternative text or an explicit decorative choice. The page header image is rendered with an empty alternative because it appears beside the page title. Tables require a caption and column headings before publication. File, button, and card links need useful labels.

Draft changes autosave after a short pause. Autosaves update the working draft without adding history; **Save draft** appends a saved revision. Saves use latest-write-wins behavior and preserve the current input when a request fails.

For published content, the **Save draft** split menu includes **Discard draft**. After confirmation, it replaces working changes with the current published version without changing the live publication. The saved working copy is retained in Revisions with a “Draft discarded” note; edits still unsaved in the browser are discarded. Any save already in progress finishes before the discard request. Cancel a pending scheduled publication first. This action requires editing permission and is unavailable for never-published, withdrawn, or deleted content.

An editor control that is missing for a stored tool displays the original block as editable JSON. Valid original JSON is restored on save. Invalid JSON stops saving with a recoverable error, so unavailable blocks are never silently discarded.

## Workflow and revisions

Authors can submit drafts for review. Publishers can request changes, publish immediately, schedule a future publication, cancel a scheduled publication, or unpublish content.

| Page status | Code |
| --- | ---: |
| Published | 0 |
| Draft | 10 |
| In review | 20 |
| Changes requested | 30 |
| Scheduled | 40 |
| Deleted | 127 |

The standard `spages.status` column holds the authoring state. Saving changes to a published page makes a draft while its approved revision remains public. An elapsed schedule displays and filters as Published without a background update. Public eligibility is determined from revisions, not the draft row status.

| Revision kind | Code |
| --- | ---: |
| Saved | 0 |
| Submitted | 10 |
| Changes requested | 20 |
| Published | 30 |
| Withdrawn | 40 |
| Original content | 50 |

Publication approves a snapshot of content and settings. A future publication leaves the currently eligible revision visible until its effective UTC time. A newer scheduled release replaces an earlier pending release. Cancellation marks the pending release ineligible; unpublishing records a withdrawal so an older revision cannot reappear.

The Revisions tab displays saved timestamps through ParsePage using the current user timezone and date/time preferences. Restoring a revision copies it into a new draft. Review and publish that draft to complete a rollback. Page rollback and snippet rollback are separate operations. Bulk actions can submit, unpublish, move pages to trash, or restore drafts. Restoring from trash does not republish the page; deletion retains revision history. Required published or scheduled snippet dependencies prevent removal.

Preview uses the public renderer and requires a signed-in user at AUTHOR_LEVEL or above, with the CMS View permission (or Site Admin access). Authorized read-only users can preview content and its attachments without gaining permission to save or publish. Preview responses are private and excluded from indexing. The public page has one slash-separated breadcrumb row, with an Edit action for authors. Preview uses that same row with Bootstrap warning colors and an Unpublished preview label. Preview ancestor links open authorized previews; ordinary page breadcrumbs use published ancestor titles and URLs. The default page template does not add a second flat page-navigation row; applications can use the published `pages` state in their site navigation. A historical page preview uses the snippet revisions recorded with that page revision.

## Pages, access, and discovery

Each page and every published ancestor must be active and available to the current audience. Access level 0 is public, level 1 requires a signed-in user, and higher values require at least that framework access level. Missing or inaccessible pages return HTTP 404 and are excluded from public navigation, search, and sitemap results.

`is_nav_visible` controls inclusion in the published navigation. `is_noindex` excludes a page from indexable results and adds search-engine metadata. Redirect pages are also excluded from indexing.

`/Search?s=words` searches effective published content. `/Sitemap` renders the HTML page list, while `/sitemap.xml` lists anonymous, indexable canonical pages.

Page settings accept manual aliases in `url_aliases`, one app-local old path per line. The publication timeline also retains actual previously published paths automatically. That history follows page IDs across page moves, descendant path changes, and scheduled releases. A scheduled path becomes historical only when its release takes effect; cancelling it contributes no alias.

The **Redirect this page to** setting accepts an app-local path or a full HTTP/HTTPS URL, such as `https://www.google.com/`. External destinations are approved through the normal CMS publication workflow; draft or future changes do not replace the current public behavior. Redirect pages retain page and ancestor access restrictions and are excluded from search/sitemaps. Absolute URLs back into the configured application also participate in redirect-loop checks. Old local paths remain app-local aliases.

Aliases resolve only after native routes miss and always target the page's current published path. Publication rejects path collisions, reserved framework or controller paths, and redirect loops across current and scheduled boundaries.

## Snippets

A snippet is a Spages row with `is_snippet=1`. Its `url` value is the stable lowercase snippet key, such as `contact-help`; no prefix is used. Snippets have no public page route and do not appear as separate navigation or search results. Use Add Snippet on an existing page to start a snippet with that parent selected. Alternatively, choose a Parent page in Navigation and Search to associate a snippet with a page, or Top level for shared content. Keys remain site-wide and other eligible pages can reuse an associated snippet; the parent’s publication and access restrictions apply. The editor explains snippet use below Publishing and disables page-only navigation, search-engine, redirect, page-image, and executable customization settings. Content images and links remain available through blocks.

Insert a snippet with the Snippet block or a layout slot. Snippets use the Article layout's main region and cannot contain other snippets. A consuming page cannot publish until its referenced snippet is published and available at an equal or less restrictive access level. A published or scheduled consumer prevents withdrawal of a required snippet.

Publishing a snippet updates its consumers. Historical page revisions record the snippet revisions they used, while current drafts use current published snippets.

Application code can render an eligible snippet for the current audience:

```csharp
ps["snippet_html"] = fw.model<Spages>().renderSnippet("contact-help");
```

`renderSnippet(key)` returns an empty string when the snippet is missing or restricted. The page-context overload also enforces compatibility with the consuming page.

## Model API

Use the publication API for public output and the draft API for editor integrations:

| Method | Purpose |
| --- | --- |
| `listSelectOptionsAutocomplete(q)` | Editor-authorized lookup over non-deleted working titles; public selectors must use publication APIs. |
| `listDraftParents(parentId)` | Working ancestor breadcrumb rows, from the topmost page through the supplied parent. |
| `onePublished(id, audience)` | Return one eligible published page or snippet, or an empty dictionary. |
| `listPublished(audience)` | List eligible published pages for an audience. |
| `listIndexable(audience)` | List published pages eligible for search and sitemap indexing. |
| `listPublicationsByDate(at)` | Resolve publication state at one instant, omitting withdrawn items. |
| `oneDraftOrFail(id)` | Read the mutable working draft for the editor. |
| `saveDraft(id, input, isAutosave)` | Create or update a draft; explicit saves append history. |
| `updateWorkflow(id, action, note, publishAt)` | Submit, request changes, publish now or at `publishAt`, cancel, or unpublish. |
| `isPublished(id)` | Editor-only indicator of a current approved version, independent of working status and audience restrictions. Do not use it to authorize public reads. |
| `updateDiscardDraft(id)` | Return working content to the current published snapshot and retain the discarded draft in history; pending schedules must be cancelled first. |
| `restoreRevision(id, revisionId)` | Copy a stored revision into a new draft. |
| `buildPageState(item, isPreview, isHistorical)` | Produce the rendered ParsePage state for public pages and previews. |
| `renderSnippet(key)` | Render an eligible named snippet for the current audience. |

`listPublicationsByDate` resolves revision timing but does not itself apply an audience. Pass its results through the visibility-aware public methods or enforce the intended audience before exposing content.

`oneByUrl` and `listChildren` filter eligible revisions in the database. Single-page reads and URL construction share request-scoped resolution, including ancestor checks. The existing `tree`, `getPagesTree`, `getPagesTreeList`, and `getPagesTreeSelectHtml` names remain as deprecated wrappers. Select-option labels are prepared as data and escaped by ParsePage.

`SpagesContent` owns versioned block JSON, validation, sanitized HTML rendering, Markdown conversion, and developer tool/layout registration. `Spages` owns persistence, publication, access, URLs, and dependencies. ParsePage consumes the sanitized output through its standard `noescape` option.

`buildPageState` supplies `page[html_main]`, `html_left`, `html_right`, `html_after_content`, and declared `html_slot_<name>` values. These values are sanitized server-rendered HTML for the Spages public templates.

## Extending blocks and layouts

The version-one document shape is:

```json
{
  "schemaVersion": 1,
  "regions": {
    "main": {"blocks": [{"type": "paragraph", "data": {"text": "Welcome"}}]},
    "left": {"blocks": []},
    "right": {"blocks": []}
  },
  "slots": {"after_content": "contact-help"}
}
```

Register trusted server renderers and layouts during startup:

```csharp
SpagesContent.registerTool("notice", (data, context) =>
    "<aside>" + SpagesContent.escape(SpagesContent.text(data, "text")) + "</aside>");

SpagesContent.registerLayout("resource-page",
    new SpagesContent.Layout("Resources", ["main", "right"], ["after_content"]));
```

Register the matching Editor.js tool through `window.SpagesEditor.tools` before `spages-editor.js` executes. Server renderers must validate data, escape output, and use the supplied attachment and snippet callbacks. Unknown server tools cannot publish until a renderer is registered.

Inline HTML and Markdown pass through server sanitizers, and raw Markdown HTML is disabled. Custom head, CSS, and JavaScript are trusted Site Admin fields.

## Attachments and sample drafts

Files uploaded in the editor belong to that page. Restricted content requires page-owned attachments. Shared unbound library files are public and should contain only public material. Attachment access follows the current publication, ancestor access, and actual references; image blocks accept decoded image files.

Each provider's `demo.sql` seeds five unpublished example pages and the `demo-help` snippet. Together the pages cover all five layouts and every built-in block. They contain illustrative workplace content that developers can adapt; they do not replace or claim the application homepage.

| Page | Layout | Blocks demonstrated |
| --- | --- | --- |
| `/demo-services` — Good work starts with a clear plan | Landing page | Paragraph, heading, button, cards, quote, snippet |
| `/demo-start-here` — Your first week, made simpler | Left sidebar | Paragraph, headings with anchors, ordered/unordered lists, callout, file, after-content snippet slot |
| `/demo-department` — People, resources, and a place to start | Right sidebar | Paragraph, heading, cards, table with caption/headers, callout, snippet |
| `/demo-bulletin` — Around the workplace | Three columns | Paragraph, heading, lists, quote, cards, divider, callout, snippet |
| `/demo-field-notes` — A practical guide to a calmer project kickoff | Article | Paragraph, heading, image with alt text/caption, quote, ordered list, code, Markdown, divider, button, after-content snippet slot |

The SQL seeds create the two bundled attachment records as Inactive with no file size. Development database initialization copies their files from `App_Data/demo` into normal storage, generates image thumbnails, and activates each record only after its files are installed. The files use normal attachment URLs and access checks; no external image or download service is required. Publish `demo-help` first so page previews can include it, then preview or publish the pages individually.

To add these examples to an existing development app, run only the **CMS demonstration drafts** section of the selected provider's `demo.sql`; the earlier demo-table setup is destructive. This section skips existing URLs, working-draft URLs, and attachment codes. In Manage Uploads, open the seeded Planning workshop and First-week checklist records and upload their matching bundled files. Existing content and stored files are not overwritten by seeding. If initialization encounters an occupied original or thumbnail path, media setup stops and the pending record stays Inactive. Finish that record through Manage Uploads with the matching bundled file; do not rerun destructive database initialization to repair media.

Spages is intended for small page collections. It has one review step, no anonymous preview links, no nested snippets, no translation workflow, and no background scheduler requirement.
