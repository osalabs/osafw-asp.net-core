# Spages CMS

Spages (static pages) is an optional CMS for small public websites, intranets, and application content. It shares the framework's users, permissions, attachments, routing, themes, and ParsePage renderer. Applications can extend the initial tools and layouts without adopting a separate publishing platform.

## Enable and upgrade

SQL Server and SQLite support the block CMS. Existing installations need the additive `upd2026-09-05-spages-cms.sql` update for their provider. See [the upgrade guide](spages-upgrade.md). Fresh schemas include the CMS tables. MySQL keeps the legacy editor; its explicit no-op update prevents SQL Server SQL from being used through provider fallback.

Configuration lives under `appSettings`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `SPAGES_ENABLED` | `true` | Enables page administration and CMS public consumers. |
| `SPAGES_HOME_ENABLED` | `false` | Opts into CMS rendering of `/`; the application's home controller otherwise retains ownership. |
| `SPAGES_AUTHOR_LEVEL` | `80` | Minimum signed-in access level for editing and previewing. |
| `SPAGES_PUBLISHER_LEVEL` | `80` | Minimum publishing access; never lower than the author threshold. |
| `SPAGES_PUBLIC_ORIGIN` | empty | Deployment origin, such as `https://www.example.org`, for canonical links and XML sitemap URLs. No path, credentials, query, or fragment. |

Default managers can edit and publish directly. To require review for ordinary editors, use a lower author threshold and a higher publisher threshold, for example 80 and 90. Existing role/resource permissions, when compiled in, remain an additional restriction. Read-only users cannot author or publish. Site Admin remains required for conversion, demo installation, and executable page customization.

## Editing and publication

Open **Pages** from the manager menu. Create a page or snippet, select a layout, and write structured content. Article and landing layouts use the main region; sidebar and three-column layouts reveal their additional regions. Switching layouts retains hidden content and prevents publication until that content is moved or removed.

Draft changes autosave after a short pause. Autosaves update one working draft; **Save draft** also creates an immutable revision. A version token protects against two editors overwriting each other. A conflict stops saving and preserves the current editor input; reload the latest draft before deliberately reapplying copied changes.

Authors send a draft for review. Publishers can request changes with a review note or publish directly. Publication approves a snapshot of the content and settings, not a mutable working draft. The date picker uses the editor's browser-local time and sends UTC. Blank means now. Publication boundaries have whole-second UTC precision.

A future publication keeps the earlier eligible revision visible. Requests select the latest approved snapshot whose effective time has arrived; no scheduler is needed. A later publish replaces a pending scheduled revision. Cancel withdraws the pending revision, while Unpublish records an explicit withdrawal so an older publication cannot reappear. The framework home row cannot be withdrawn; disable CMS homepage ownership instead.

Restoring a revision creates a new draft. Review or publish it to perform a rollback. History keeps snapshot content, editor identity, timestamps, review notes, and cancelled publication records. Pages retain stable IDs. The initial implementation does not purge history automatically.

Preview uses the same public renderer. Draft and revision previews require author authorization and send `private, no-store` and `noindex` responses with a visible preview banner. They are not anonymous share links. A historical page preview uses the snippet revisions recorded with that snapshot; ordinary draft previews use currently published snippets.

## Content and accessibility

The pinned, self-hosted Editor.js 2.31.6 core is restored through `libman.json`. The framework owns the client tools, document schema, server validation, and HTML generation; stored blocks never run as ParsePage templates. See [Editor.js output data](https://editorjs.io/saving-data/).

Initial tools: paragraphs, H2–H6 headings, flat lists, quotes, images, files, tables, code, dividers, callouts, cards, buttons, reusable snippets, and a legacy Markdown block. The page title supplies H1. Headings may declare an anchor for local links. Images need alternative text or an explicit decorative choice; tables need a caption and column headings to publish. Buttons and file/card links require labels. The editor gives link-label guidance, while the publisher remains responsible for content quality and sensible heading order.

Upload through a page's editor to create page-owned files. Restricted content requires page-owned attachments. Shared unbound library files are public and must not contain private material. Attachment serving also checks the page's current publication, ancestry, audience, and actual file references; withdrawing a page removes public access to its owned files. Image blocks require decoded image uploads. Existing attachment active-content and download policies still apply.

Inline HTML and Markdown pass through server sanitizers. Raw Markdown HTML is disabled. Unsupported Markdown survives conversion in an editable Markdown block rather than being silently discarded. Custom head, CSS, and JavaScript remain a trusted Site Admin capability.

The public templates use Bootstrap/framework tokens for reading width, spacing, responsive columns, cards, images, navigation, and light/dark presentation. The CMS does not replace the application's header, footer, brand, or navigation policy.

## Snippets

A snippet is a Spages row with `is_snippet=1` and a stable lowercase `snippet_key`. It shares drafts, revisions, publication dates, access restrictions, and rollback. A parent associates it with a page and applies that page's ancestor restrictions; a top-level snippet is shared. Snippets have no public page route and do not appear in page navigation or search as separate results.

Insert a named snippet with the editor block or the layout's after-content slot. Snippets use the Article layout's main region and cannot contain snippets. Publication rejects missing references and snippets whose effective access is stricter than the consuming page. The editor lists pages affected by publishing a snippet, including scheduled uses. A required published snippet cannot be withdrawn until its consumers are changed. Publishing a snippet updates its references; page rollback and snippet rollback are separate operations.

Application controllers can supply a snippet to a trusted template:

```csharp
ps["snippet_html"] = fw.model<Spages>().renderSnippet("contact-help");
```

```html
<~/common/spages/snippet>
```

The one-argument helper returns sanitized/rendered content for the current audience, or an empty string for missing or restricted snippets. The overload accepting a page context additionally enforces compatibility with that page's access.

## Public reads, search, and URLs

Use `fw.model<Spages>().published(id)` or `publishedByPath(path)` as the effective-publication authority. They return an empty dictionary for missing, withdrawn, future-only, or inaccessible content. `publishedPages()` lists eligible pages for the current audience. `publishedText(page)` includes current snippet content; pass a page already authorized for the intended consumer. Internal indexing may explicitly resolve an audience, but must reauthorize on retrieval.

Access level 0 is public; level 1 requires login; higher values require at least that framework level. Every ancestor must also be eligible. An inaccessible page returns HTTP 404 and is absent from public discovery. Preview is a separate editor-authorized path.

`/Search?s=words` performs paginated text search over effective content without an external index. All words must match. Redirect and noindex pages are excluded. `/Sitemap` remains an HTML listing; `/sitemap.xml` includes only anonymous, indexable canonical pages. Configure `SPAGES_PUBLIC_ORIGIN` to populate XML URLs. An opt-out CMS home row is excluded from navigation, search, and sitemap listings.

Published URL changes create permanent aliases, including descendant paths. Scheduled URL changes take effect with the publication. Alias targets follow stable page IDs and current access. The manual redirect editor accepts app-local paths only. Publication checks collisions, reserved framework/controller paths, and redirect loops across current and scheduled boundaries. Native application routes keep precedence. Existing pages backing native routes, such as Contact, may retain those routes through the compatibility accessor.

An application that adds custom route rewrites must reserve its additional paths consistently with `Spages.isReservedPath`/publication validation before allowing CMS authors to claim them.

## Compatibility and extension

`one`, `oneField`, `oneByUrl`, `oneByFullUrl`, `isPublished`, and `listChildrenPublished` adapt to the effective publication after schema installation. Editing code uses `draft(id)` explicitly. Legacy `idesc`, `idesc_left`, and `idesc_right` values returned by compatibility reads are `SpagesContent.RenderedHtml` values, convertible with `.toStr()`, so existing `<~page[idesc] markdown noescape>` templates render current server-generated content. ParsePage does not Markdown-parse these rendered values again and still honors ordinary escaping without `noescape`. Code requiring a string cast or serialized DTO must explicitly use `.toStr()` or the block document. Draft/original snapshots retain source Markdown as strings.

Raw table columns, generic SQL/tree queries, and direct database consumers are not publication APIs. They cannot reliably represent scheduled revisions, access ancestry, or reused content. Adapt custom navigation, feeds, exports, caches, and background consumers to the effective accessors. Do not use the mutable working draft or legacy columns as a public source.

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

Applications register trusted renderers during startup, before requests:

```csharp
SpagesContent.registerTool("notice", (data, context) =>
    "<aside>" + SpagesContent.escape(SpagesContent.text(data, "text")) + "</aside>");
SpagesContent.registerLayout("resource-page",
    new SpagesContent.Layout("Resources", ["main", "right"], ["after_content"]));
```

Register the corresponding Editor.js client tool through `window.SpagesEditor.tools` before `spages-editor.js` executes. Custom renderers must validate their data, escape output, and use the supplied attachment/snippet callbacks. Unknown tools remain in draft JSON but cannot publish without a server renderer; the editor prevents silent removal of unavailable client tools. Maximum document size is 1,000,000 characters, 300 blocks, and JSON depth 32.

`pageState(page)` supplies `page[html_main]`, `html_left`, `html_right`, `html_after_content`, and `html_slot_<name>` for declared layout slots. Customize trusted ParsePage templates and CSS to present application-specific layouts. No nested/general page builder is included.

Publication resolution is request-scoped. A longer-lived application cache must include audience and snippet dependencies and expire at the next publication boundary. Optional RAG retrieval verifies current content hashes, URLs, and audience before admitting stored chunks; changed or scheduled content may be absent until reindexed but stale text is excluded.

## Demonstrations

Site Admins can independently install the services or department demo from Pages. Each choice adds that page and the shared help snippet if missing. Existing matching content is skipped; the application's homepage is never overwritten. Publish the help snippet first, then the desired pages. These are editable starting points; replace the sample text and links before using them in an application.

## Deliberate limits

This initial CMS targets small page collections. Search and publication validation materialize the relevant content in the request rather than introducing another search service. It has no anonymous preview links, nested snippets, drag-and-drop page builder, multistage approvals, translation workflow, history retention service, or scheduled job dependency. Applications can add these when their needs justify the additional state and UI.
