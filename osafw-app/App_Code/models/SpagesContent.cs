using System;
using System.Linq;
using Ganss.Xss;
using AngleSharp.Html.Parser;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace osafw;

/// <summary>
/// Version-one CMS documents. Only developer-registered tools render HTML; stored blocks never execute ParsePage.
/// Unknown tools survive draft storage, but prevent publication until their server renderer is installed.
/// </summary>
public static class SpagesContent
{
    public const int MaxCharacters = 1_000_000;
    public const int MaxBlocks = 300;
    public static readonly string[] Regions = ["main", "left", "right"];
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder().UsePipeTables().UseAutoLinks().UseTaskLists().DisableHtml().Build();
    private static readonly HtmlSanitizer InlineSanitizer = makeSanitizer(true);
    private static readonly HtmlSanitizer MarkdownSanitizer = makeSanitizer(false);
    private static readonly ConcurrentDictionary<string, Func<JsonObject, RenderContext, string>> Tools = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Layout> Layouts = new(StringComparer.Ordinal);

    public sealed record Layout(string Title, string[] Regions, string[] Slots);
    public sealed record RenderContext(Func<string, string>? Snippet = null, Func<int, string>? Attachment = null, bool Publishing = false, Func<int, string>? ImageAttachment = null);

    /// <summary>Server-rendered region used by legacy ParsePage Markdown fields. Only CMS rendering constructs this value.</summary>
    public sealed class RenderedHtml
    {
        private readonly string html;
        internal RenderedHtml(string html) => this.html = html;
        public override string ToString() => html;
    }

    static SpagesContent()
    {
        registerLayout("article", new("Article", ["main"], ["after_content"]));
        registerLayout("landing", new("Landing page", ["main"], ["after_content"]));
        registerLayout("sidebar-left", new("Left sidebar", ["main", "left"], ["after_content"]));
        registerLayout("sidebar-right", new("Right sidebar", ["main", "right"], ["after_content"]));
        registerLayout("three-column", new("Three columns", Regions, ["after_content"]));
        registerTool("paragraph", (d, c) => "<p>" + inline(text(d, "text")) + "</p>");
        registerTool("header", renderHeader);
        registerTool("list", renderList);
        registerTool("quote", (d, c) => "<blockquote><p>" + inline(text(d, "text")) + "</p><footer>" + inline(text(d, "caption")) + "</footer></blockquote>");
        registerTool("code", (d, c) => "<pre><code>" + escape(text(d, "code")) + "</code></pre>");
        registerTool("delimiter", (d, c) => "<hr>");
        registerTool("legacyMarkdown", (d, c) => markdown(text(d, "text")));
        registerTool("image", renderImage);
        registerTool("file", renderFile);
        registerTool("table", renderTable);
        registerTool("callout", (d, c) => "<aside class=\"spage-callout\"><strong>" + escape(text(d, "title")) + "</strong><p>" + inline(text(d, "text")) + "</p></aside>");
        registerTool("button", (d, c) => "<p class=\"spage-cta\"><a class=\"btn btn-primary\" href=\"" + escape(safeUrl(text(d, "url"))) + "\">" + escape(required(d, "text", "Button label")) + "</a></p>");
        registerTool("cards", renderCards);
        registerTool("snippet", (d, c) => c.Snippet?.Invoke(required(d, "key", "Snippet key")) ?? "");
    }

    /// <summary>Register trusted application code at startup. Renderers must validate their shape and sanitize any HTML they return.</summary>
    public static void registerTool(string name, Func<JsonObject, RenderContext, string> renderer) => Tools[name] = renderer;
    /// <summary>Declare allowed editable regions and named snippet slots. Layout names never become file paths.</summary>
    public static void registerLayout(string name, Layout layout) => Layouts[name] = layout;
    public static IReadOnlyDictionary<string, Layout> layouts() => Layouts;
    public static Layout layout(string name) => Layouts.TryGetValue(name, out var value) ? value : throw new UserException("Select an installed page layout.");

    public static JsonObject empty() => new() { ["schemaVersion"] = 1, ["regions"] = new JsonObject { ["main"] = new JsonObject { ["blocks"] = new JsonArray() } }, ["slots"] = new JsonObject() };

    public static JsonObject parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return empty();
        if (json.Length > MaxCharacters) throw new UserException("Page content is too large.");
        JsonObject doc;
        try { doc = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 32 }) as JsonObject ?? throw new JsonException(); }
        catch (JsonException) { throw new UserException("Invalid page content. Your unsaved changes have been kept in the editor."); }
        if (number(doc, "schemaVersion") != 1 || doc["regions"] is not JsonObject regions)
            throw new UserException("Unsupported page document version or missing regions.");
        int count = 0;
        foreach (var region in regions)
        {
            if (!Regions.Contains(region.Key) || region.Value is not JsonObject obj || obj["blocks"] is not JsonArray blocks)
                throw new UserException("Invalid content region.");
            count += blocks.Count;
            foreach (var block in blocks)
                if (block is not JsonObject b || b["type"] is not JsonValue || b["data"] is not JsonObject)
                    throw new UserException("Invalid content block.");
                else if (text(b, "type") is "paragraph" or "header" or "quote" or "callout")
                {
                    var data = (JsonObject)b["data"]!;
                    if (data["text"] != null) data["text"] = inline(text(data, "text"));
                }
        }
        if (count > MaxBlocks) throw new UserException("A page can contain at most 300 blocks.");
        if (doc["slots"] != null && doc["slots"] is not JsonObject) throw new UserException("Invalid snippet slots.");
        return doc;
    }

    public static string renderRegion(JsonObject document, string region, RenderContext context)
    {
        var html = new StringBuilder();
        if (document["regions"]?[region]?["blocks"] is not JsonArray blocks) return "";
        foreach (var block in blocks.OfType<JsonObject>())
        {
            string type = text(block, "type");
            if (!Tools.TryGetValue(type, out var render)) throw new UserException($"The '{type}' block needs an installed server renderer before it can be previewed or published.");
            html.Append(render((JsonObject)block["data"]!, context));
        }
        return html.ToString();
    }

    public static void validate(JsonObject document, string layoutName, RenderContext context, bool isSnippet)
    {
        var definition = layout(layoutName);
        if (isSnippet && layoutName != "article") throw new UserException("Snippets use the main content region of the Article layout.");
        foreach (var region in Regions)
        {
            if (!definition.Regions.Contains(region) && document["regions"]?[region]?["blocks"] is JsonArray a && a.Count > 0)
                throw new UserException("This layout would hide existing content. Move that content before changing the layout.");
            renderRegion(document, region, context);
        }
        if (isSnippet && snippetKeys(document).Count > 0) throw new UserException("Snippets cannot contain other snippets.");
        if (document["slots"] is JsonObject slots)
            foreach (var slot in slots)
            {
                if (!definition.Slots.Contains(slot.Key)) throw new UserException("This layout does not declare the selected snippet slot.");
                if (!string.IsNullOrEmpty(slot.Value?.ToString())) context.Snippet?.Invoke(slot.Value!.ToString());
            }
    }

    public static HashSet<string> snippetKeys(JsonObject document)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var region in Regions)
            if (document["regions"]?[region]?["blocks"] is JsonArray blocks)
                foreach (var block in blocks.OfType<JsonObject>().Where(x => text(x, "type") == "snippet"))
                    keys.Add(text((JsonObject)block["data"]!, "key"));
        if (document["slots"] is JsonObject slots)
            foreach (var slot in slots) if (!string.IsNullOrEmpty(slot.Value?.ToString())) keys.Add(slot.Value!.ToString());
        return keys;
    }

    public static HashSet<int> attachmentIds(JsonObject document)
    {
        var ids = new HashSet<int>();
        foreach (var region in Regions)
            if (document["regions"]?[region]?["blocks"] is JsonArray blocks)
                foreach (var block in blocks.OfType<JsonObject>().Where(x => text(x, "type") is "image" or "file"))
                    ids.Add(number((JsonObject)block["data"]!, "att_id"));
        ids.Remove(0);
        return ids;
    }

    /// <summary>Convert recognized Markdown blocks without dropping unsupported syntax. Originals remain in migration snapshots.</summary>
    public static JsonObject fromMarkdown(FwDict page, Func<string, int>? resolveAttachment = null)
    {
        var doc = empty();
        var regions = (JsonObject)doc["regions"]!;
        foreach (var pair in new[] { ("main", "idesc"), ("left", "idesc_left"), ("right", "idesc_right") })
        {
            var source = page[pair.Item2].toStr();
            var blocks = new JsonArray();
            var parsed = Markdown.Parse(source, MarkdownPipeline);
            // Reference definitions and footnotes can span blocks. Keep their whole region editable together.
            bool keepRegion = Regex.IsMatch(source, @"(?m)^\s{0,3}\[[^\]]+\]:") || parsed.Any(x => x.Span.Start < 0 || x.Span.End >= source.Length);
            if (keepRegion) blocks.Add(new JsonObject { ["type"] = "legacyMarkdown", ["data"] = new JsonObject { ["text"] = source } });
            foreach (var node in keepRegion ? Enumerable.Empty<Block>() : parsed)
            {
                string original = source.Substring(node.Span.Start, node.Span.Length);
                string type = "legacyMarkdown";
                JsonObject data = new() { ["text"] = original };
                if (node is HeadingBlock heading && heading.Level >= 2)
                {
                    type = "header";
                    data = new() { ["text"] = inline(markdown(Regex.Replace(original, @"^\s{0,3}#{1,6}\s+|\s+#+\s*$", ""))), ["level"] = heading.Level };
                }
                else if (node is ParagraphBlock paragraph && paragraph.Inline != null && !paragraph.Inline.Descendants<LinkInline>().Any(x => x.IsImage))
                {
                    type = "paragraph";
                    data["text"] = inline(markdown(original));
                }
                if (node is ParagraphBlock media && media.Inline?.FirstChild is LinkInline link && link.NextSibling == null && resolveAttachment?.Invoke(link.Url ?? "") is int attachmentId && attachmentId > 0)
                {
                    var html = new HtmlParser().ParseDocument(markdown(original));
                    type = link.IsImage ? "image" : "file";
                    data = link.IsImage
                        ? new() { ["att_id"] = attachmentId, ["alt"] = html.QuerySelector("img")?.GetAttribute("alt") ?? "", ["decorative"] = false, ["caption"] = "" }
                        : new() { ["att_id"] = attachmentId, ["title"] = html.QuerySelector("a")?.TextContent ?? "Download file" };
                }
                else if (node is FencedCodeBlock code)
                {
                    type = "code";
                    data = new() { ["code"] = code.Lines.ToString() };
                }
                else if (node is ListBlock list && (!list.IsOrdered || list.OrderedStart == "1") && !source.Contains("[ ]") && !source.Contains("[x]", StringComparison.OrdinalIgnoreCase) && list.All(x => x is ListItemBlock li && li.Count == 1 && li[0] is ParagraphBlock))
                {
                    type = "list";
                    var items = new JsonArray();
                    foreach (var listItem in list.OfType<ListItemBlock>())
                    {
                        var paragraphNode = listItem[0];
                        items.Add(inline(markdown(source.Substring(paragraphNode.Span.Start, paragraphNode.Span.Length))));
                    }
                    data = new() { ["style"] = list.IsOrdered ? "ordered" : "unordered", ["items"] = items };
                }
                else if (node is QuoteBlock quote && quote.Count == 1 && quote[0] is ParagraphBlock)
                {
                    var html = new HtmlParser().ParseDocument(markdown(original));
                    type = "quote";
                    data = new() { ["text"] = html.QuerySelector("blockquote p")?.InnerHtml ?? original, ["caption"] = "" };
                }
                else if (node is Markdig.Extensions.Tables.Table)
                {
                    var html = new HtmlParser().ParseDocument(markdown(original));
                    var rows = new JsonArray();
                    foreach (var row in html.QuerySelectorAll("tr")) rows.Add(new JsonArray(row.Children.Select(cell => JsonValue.Create(cell.InnerHtml)).ToArray()));
                    type = "table";
                    data = new() { ["content"] = rows, ["withHeadings"] = true, ["caption"] = "" };
                }
                else if (node is ThematicBreakBlock) { type = "delimiter"; data = new(); }
                blocks.Add(new JsonObject { ["id"] = Guid.NewGuid().ToString("N"), ["type"] = type, ["data"] = data });
            }
            regions[pair.Item1] = new JsonObject { ["blocks"] = blocks };
        }
        return doc;
    }

    public static string plainText(string html) => WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]*>", " ")).Trim();
    public static string escape(string value) => WebUtility.HtmlEncode(value);
    public static string inline(string value) => InlineSanitizer.Sanitize(value);
    public static string markdown(string value) => MarkdownSanitizer.Sanitize(Markdown.ToHtml(value, MarkdownPipeline));
    public static string text(JsonObject data, string key) => data[key]?.ToString() ?? "";
    public static int number(JsonObject data, string key, int fallback = 0) => int.TryParse(text(data, key), out int result) ? result : fallback;

    public static string safeUrl(string value)
    {
        value = WebUtility.HtmlDecode(value).Trim();
        if (value.Length == 0 || value.Any(char.IsControl) || value.Contains('\\') || value.StartsWith("//")) throw new UserException("Enter a safe link URL.");
        if (value.StartsWith('/') || value.StartsWith('#')) return value;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto" or "tel") return value;
        throw new UserException("Links must be app-local paths, https/http, mailto, or tel URLs.");
    }

    private static string required(JsonObject data, string key, string label)
    {
        var value = text(data, key).Trim();
        return value.Length > 0 ? value : throw new UserException(label + " is required.");
    }

    private static HtmlSanitizer makeSanitizer(bool inlineOnly)
    {
        var s = new HtmlSanitizer { KeepChildNodes = true };
        s.AllowedTags.Clear();
        foreach (var tag in (inlineOnly ? "b strong i em a code mark br s u sub sup" : "p h1 h2 h3 h4 h5 h6 b strong i em a code pre mark br s u sub sup ul ol li blockquote hr table thead tbody tr th td img figure figcaption input").Split(' ')) s.AllowedTags.Add(tag);
        s.AllowedAttributes.Clear();
        foreach (var attr in "href title alt src colspan rowspan scope type checked disabled".Split(' ')) s.AllowedAttributes.Add(attr);
        s.AllowedSchemes.Clear();
        foreach (var scheme in "http https mailto tel".Split(' ')) s.AllowedSchemes.Add(scheme);
        return s;
    }

    private static string renderList(JsonObject data, RenderContext context)
    {
        string tag = text(data, "style") == "ordered" ? "ol" : "ul";
        if (data["items"] is not JsonArray items) throw new UserException("A list needs items.");
        if (items.Any(x => x is not JsonValue)) throw new UserException("Version-one lists contain text items. Use Markdown for nested lists.");
        return $"<{tag}>" + string.Join("", items.Select(x => "<li>" + inline(x is JsonObject obj ? text(obj, "content") : x?.ToString() ?? "") + "</li>")) + $"</{tag}>";
    }

    private static string renderHeader(JsonObject data, RenderContext context)
    {
        int level = number(data, "level", 2);
        if (level < 2 || level > 6) throw new UserException("Content headings must be H2–H6; the page title is H1.");
        string anchor = text(data, "anchor");
        if (anchor.Length > 0 && !Regex.IsMatch(anchor, @"^[a-zA-Z][a-zA-Z0-9_-]{0,63}$")) throw new UserException("Heading anchors start with a letter and contain letters, numbers, hyphens, or underscores.");
        return $"<h{level}" + (anchor.Length > 0 ? " id=\"" + escape(anchor) + "\"" : "") + ">" + inline(text(data, "text")) + $"</h{level}>";
    }

    private static string renderImage(JsonObject data, RenderContext context)
    {
        bool decorative = text(data, "decorative") == "true";
        string alt = text(data, "alt");
        if (context.Publishing && !decorative && string.IsNullOrWhiteSpace(alt)) throw new UserException("Add image alternative text or mark the image as decorative.");
        string url = (context.ImageAttachment ?? context.Attachment)?.Invoke(number(data, "att_id")) ?? "";
        if (url.Length == 0) throw new UserException("Select an available image attachment.");
        return "<figure><img loading=\"lazy\" class=\"img-fluid\" src=\"" + escape(url) + "\" alt=\"" + escape(decorative ? "" : alt) + "\"><figcaption>" + inline(text(data, "caption")) + "</figcaption></figure>";
    }

    private static string renderFile(JsonObject data, RenderContext context)
    {
        string url = context.Attachment?.Invoke(number(data, "att_id")) ?? "";
        if (url.Length == 0) throw new UserException("Select an available file attachment.");
        return "<p><a href=\"" + escape(url) + "\">" + escape(required(data, "title", "File link label")) + "</a></p>";
    }

    private static string renderTable(JsonObject data, RenderContext context)
    {
        if (data["content"] is not JsonArray rows) throw new UserException("A table needs rows.");
        if (context.Publishing && (text(data, "withHeadings") != "true" || string.IsNullOrWhiteSpace(text(data, "caption")))) throw new UserException("Add a table caption and column headings.");
        var html = new StringBuilder("<div class=\"table-responsive\"><table class=\"table\"><caption>").Append(escape(text(data, "caption"))).Append("</caption>");
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] is not JsonArray cells) throw new UserException("Invalid table row.");
            bool header = i == 0 && text(data, "withHeadings") == "true";
            html.Append("<tr>");
            foreach (var cell in cells) html.Append(header ? "<th scope=\"col\">" : "<td>").Append(inline(cell?.ToString() ?? "")).Append(header ? "</th>" : "</td>");
            html.Append("</tr>");
        }
        return html.Append("</table></div>").ToString();
    }

    private static string renderCards(JsonObject data, RenderContext context)
    {
        if (data["items"] is not JsonArray items || items.Count > 12) throw new UserException("Use at most 12 cards in a group.");
        var html = new StringBuilder("<div class=\"spage-cards\">");
        foreach (var item in items.OfType<JsonObject>())
        {
            html.Append("<section class=\"spage-card\"><h2>").Append(escape(text(item, "title"))).Append("</h2><p>").Append(inline(text(item, "text"))).Append("</p>");
            if (text(item, "url").Length > 0) html.Append("<a href=\"").Append(escape(safeUrl(text(item, "url")))).Append("\">").Append(escape(required(item, "label", "Card link label"))).Append("</a>");
            html.Append("</section>");
        }
        return html.Append("</div>").ToString();
    }
}
