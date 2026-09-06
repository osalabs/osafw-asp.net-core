# Spages CMS

Spages is the framework's block CMS for small public websites, intranets, and application content. It uses framework users, permissions, attachments, routing, themes, and ParsePage templates. Authors edit a working draft; public requests read immutable published revisions.

Existing applications must complete [the Spages upgrade](spages-upgrade.md) before using the editor. Fresh database schemas already contain the required fields and revision table.

## Access and routing

`Spages.AUTHOR_LEVEL` and `Spages.PUBLISHER_LEVEL` both default to `80` (`Users.ACL_MANAGER`). Read-only users cannot author or publish. Site Admin access is required for the one-time content migration and for custom head, CSS, and JavaScript.

`HomeController.NotFoundAction` sends unmatched local paths to `Spages.showCmsPage(path)`. Native controllers and application routes therefore keep precedence. To let a CMS page own `/`, uncomment the existing lines in `HomeController.IndexAction`:

```csharp
fw.model<Spages>().showCmsPage("/");
return null!;
```

Canonical URLs use the existing `ROOT_DOMAIN` setting and the published page path. Keep `ROOT_DOMAIN` as an absolute HTTP or HTTPS base. `ROOT_URL` remains the application base for local links.

## Authoring

Open **Pages** from the manager menu to create a page or reusable snippet. A page has a title, URL segment, optional parent, navigation and search metadata, layout, content regions, optional after-content snippet, and publication controls. Secondary page settings stay collapsed while writing.

The bundled Editor.js core is pinned in `libman.json`. Built-in blocks include paragraphs, H2-H6 headings, flat lists, quotes, images, files, tables, code, dividers, callouts, cards, buttons, Markdown, and reusable snippets. The page title supplies H1. Headings may define a link anchor.

Content images require alternative text or an explicit decorative choice. The page header image is rendered with an empty alternative because it appears beside the page title. Tables require a caption and column headings before publication. File, button, and card links need useful labels.

Draft changes autosave after a short pause. Autosaves update the working draft without adding history; **Save draft** appends a saved revision. Saves use latest-write-wins behavior and preserve the current input when a request fails.

An editor control that is missing for a stored tool displays the original block as editable JSON. Valid original JSON is restored on save. Invalid JSON stops saving with a recoverable error, so unavailable blocks are never silently discarded.

## Workflow and revisions

Authors can submit drafts for review. Publishers can request changes, publish immediately, schedule a future publication, cancel a scheduled publication, or unpublish content.

| Workflow | Code |
| --- | ---: |
| Draft | 0 |
| In review | 10 |
| Changes requested | 20 |
| Published | 30 |
| Scheduled | 40 |

| Revision kind | Code |
| --- | ---: |
| Saved | 0 |
| Submitted | 10 |
| Changes requested | 20 |
| Published | 30 |
| Withdrawn | 40 |
| Original content | 50 |

Publication approves a snapshot of content and settings. A future publication leaves the currently eligible revision visible until its effective UTC time. A newer scheduled release replaces an earlier pending release. Cancellation marks the pending release ineligible; unpublishing records a withdrawal so an older revision cannot reappear.

Restoring a revision copies it into a new draft. Review and publish that draft to complete a rollback. Page rollback and snippet rollback are separate operations.

Preview uses the public renderer but requires author access. Preview responses are private, excluded from indexing, and display a preview banner. A historical page preview uses the snippet revisions recorded with that page revision.

## Pages, access, and discovery

Each page and every published ancestor must be active and available to the current audience. Access level 0 is public, level 1 requires a signed-in user, and higher values require at least that framework access level. Missing or inaccessible pages return HTTP 404 and are excluded from public navigation, search, and sitemap results.

`is_nav_visible` controls inclusion in the published navigation. `is_noindex` excludes a page from indexable results and adds search-engine metadata. Redirect pages are also excluded from indexing.

`/Search?s=words` searches effective published content. `/Sitemap` renders the HTML page list, while `/sitemap.xml` lists anonymous, indexable canonical pages.

Page settings accept manual aliases in `url_aliases`, one app-local old path per line. The publication timeline also retains actual previously published paths automatically. That history follows page IDs across page moves, descendant path changes, and scheduled releases. A scheduled path becomes historical only when its release takes effect; cancelling it contributes no alias.

Aliases resolve only after native routes miss and always target the page's current published path. Publication rejects path collisions, reserved framework or controller paths, and redirect loops across current and scheduled boundaries.

## Reusable snippets

A snippet is a Spages row with `is_snippet=1`. Its `url` value is the stable lowercase snippet key, such as `contact-help`; no prefix is used. Snippets have no public page route and do not appear as separate navigation or search results.

Insert a snippet with the reusable snippet block or a layout slot. Snippets use the Article layout's main region and cannot contain other snippets. A consuming page cannot publish until its referenced snippet is published and available at an equal or less restrictive access level. A published or scheduled consumer prevents withdrawal of a required snippet.

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
| `onePublished(id, audience)` | Return one eligible published page or snippet, or an empty dictionary. |
| `listPublished(audience)` | List eligible published pages for an audience. |
| `listIndexable(audience)` | List published pages eligible for search and sitemap indexing. |
| `listPublicationsByDate(at)` | Resolve publication state at one instant, omitting withdrawn items. |
| `oneDraftOrFail(id)` | Read the mutable working draft for the editor. |
| `saveDraft(id, input, isAutosave)` | Create or update a draft; explicit saves append history. |
| `updateWorkflow(id, action, note, publishAt)` | Submit, request changes, publish now or at `publishAt`, cancel, or unpublish. |
| `restoreRevision(id, revisionId)` | Copy a stored revision into a new draft. |
| `buildPageState(item, isPreview, isHistorical)` | Produce the rendered ParsePage state for public pages and previews. |
| `renderSnippet(key)` | Render an eligible named snippet for the current audience. |

`listPublicationsByDate` resolves revision timing but does not itself apply an audience. Pass its results through the visibility-aware public methods or enforce the intended audience before exposing content.

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

Each provider's `demo.sql` seeds three unpublished drafts: one reusable help snippet and two example pages. The seeds do not replace or claim the application homepage.

Spages is intended for small page collections. It has one review step, no anonymous preview links, no nested snippets, no translation workflow, and no background scheduler requirement.
