using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Text.Json.Nodes;

namespace osafw.Tests;
[TestClass]
public class SpagesContentTests
{
    private static JsonObject document(string type, JsonObject data)
    {
        var doc = SpagesContent.empty();
        ((JsonArray)doc["regions"]!["main"]!["blocks"]!).Add(new JsonObject { ["type"] = type, ["data"] = data });
        return doc;
    }

    [TestMethod]
    [DataRow("image")]
    [DataRow("file")]
    public void UnavailableMediaIsOmittedDuringReadsAndRejectedAtPublication(string type)
    {
        var doc = document(type, new JsonObject
        {
            ["att_id"] = 1,
            ["alt"] = "Useful diagram",
            ["title"] = "Useful file"
        });
        ((JsonArray)doc["regions"]!["main"]!["blocks"]!).Add(new JsonObject { ["type"] = "paragraph", ["data"] = new JsonObject { ["text"] = "Keep the following text" } });
        string active = SpagesContent.renderRegion(doc, "main", new(Attachment: _ => "/Att/active"));
        StringAssert.Contains(active, "/Att/active");
        StringAssert.Contains(active, "Keep the following text");
        SpagesContent.validate(doc, "article", new(Attachment: _ => "/Att/active", IsPublishing: true), false);
        string unavailable = SpagesContent.renderRegion(doc, "main", new(Attachment: _ => ""));
        Assert.AreEqual("<p>Keep the following text</p>", unavailable);
        Assert.ThrowsExactly<UserException>(() => SpagesContent.validate(doc, "article", new(Attachment: _ => "", IsPublishing: true), false));
    }

    [TestMethod]
    public void StoredMarkupIsSanitizedAndUnsafeUrlsCannotPublish()
    {
        var doc = SpagesContent.parse(document("paragraph", new() { ["text"] = "<b>Safe</b><img src=x onerror=alert(1)><a href=javascript:alert(1)>Bad</a>" }).ToJsonString());
        string html = SpagesContent.renderRegion(doc, "main", new());
        StringAssert.Contains(html, "<b>Safe</b>");
        Assert.IsFalse(html.Contains("onerror") || html.Contains("javascript:") || html.Contains("<img"));
        foreach (string url in new[]
        {
            "javascript:alert(1)",
            "//outside.test",
            "/\\outside.test",
            "data:text/html,hi",
            "&#106;avascript:alert(1)"
        }

        )
        {
            Assert.ThrowsExactly<UserException>(() => SpagesContent.safeUrl(url));
        }
    }

    [TestMethod]
    public void MarkdownConversionSupportsCoreBlocksAndKeepsNestedAndReferenceContent()
    {
        const string MARKDOWN = "## Heading\n\nA **paragraph**.\n\n- Alpha\n- Beta\n\n> A quotation\n\n```cs\nvar a = 1;\n```\n\n| Name | Value |\n| --- | --- |\n| A | B |\n\n---\n\n- Parent\n  - Child";
        var doc = SpagesContent.fromMarkdown(new FwDict { ["idesc"] = MARKDOWN });
        var types = ((JsonArray)doc["regions"]!["main"]!["blocks"]!).Select(x => x!["type"]!.ToString()).ToList();
        foreach (var type in new[]
        {
            "header",
            "paragraph",
            "list",
            "quote",
            "code",
            "table",
            "delimiter",
            "legacyMarkdown"
        }

        )
        {
            CollectionAssert.Contains(types, type);
        }

        var references = SpagesContent.fromMarkdown(new FwDict { ["idesc"] = "Read [the guide][guide].\n\n[guide]: https://example.org/guide" });
        StringAssert.Contains(SpagesContent.renderRegion(references, "main", new()), "href=\"https://example.org/guide\"");
        StringAssert.Contains(references.ToJsonString(), "[guide]:");
        StringAssert.Contains(SpagesContent.renderRegion(doc, "main", new()), "Child");
    }

    [TestMethod]
    public void ImagesAndFilesCanBeMigratedWithoutLosingTheirLabels()
    {
        var image = SpagesContent.fromMarkdown(new FwDict { ["idesc"] = "![A useful diagram](/Att/abc)" }, url => url == "/Att/abc" ? 42 : 0);
        var block = image["regions"]!["main"]!["blocks"]![0]!;
        Assert.AreEqual("image", block["type"]!.ToString());
        Assert.AreEqual("A useful diagram", block["data"]!["alt"]!.ToString());
        var file = SpagesContent.fromMarkdown(new FwDict { ["idesc"] = "[Annual report](/Att/report)" }, _ => 43);
        Assert.AreEqual("file", file["regions"]!["main"]!["blocks"]![0]!["type"]!.ToString());
    }

    [TestMethod]
    public void AccessibilityChecksRequireAltTextTableCaptionsAndStructuredHeadings()
    {
        var image = document("image", new() { ["att_id"] = 1 });
        Assert.ThrowsExactly<UserException>(() => SpagesContent.validate(image, "article", new(Attachment: _ => "/Att/image", IsPublishing: true), false));
        image["regions"]!["main"]!["blocks"]![0]!["data"]!["alt"] = "Useful diagram";
        SpagesContent.validate(image, "article", new(Attachment: _ => "/Att/image", IsPublishing: true), false);
        StringAssert.Contains(SpagesContent.renderRegion(image, "main", new(Attachment: _ => "/Att/image")), "alt=\"Useful diagram\"");
        image["regions"]!["main"]!["blocks"]![0]!["data"]!["decorative"] = true;
        SpagesContent.validate(image, "article", new(Attachment: _ => "/Att/image", IsPublishing: true), false);
        StringAssert.Contains(SpagesContent.renderRegion(image, "main", new(Attachment: _ => "/Att/image")), "alt=\"\"");
        var heading = document("header", new() { ["text"] = "Invalid main heading", ["level"] = 1 });
        Assert.ThrowsExactly<UserException>(() => SpagesContent.validate(heading, "article", new(IsPublishing: true), false));
        var table = document("table", new() { ["content"] = new JsonArray(new JsonArray("A", "B")), ["withHeadings"] = true });
        Assert.ThrowsExactly<UserException>(() => SpagesContent.validate(table, "article", new(IsPublishing: true), false));
        table["regions"]!["main"]!["blocks"]![0]!["data"]!["caption"] = "Useful values";
        SpagesContent.validate(table, "article", new(IsPublishing: true), false);
        heading["regions"]!["main"]!["blocks"]![0]!["data"]!["level"] = 2;
        SpagesContent.validate(heading, "article", new(IsPublishing: true), false);
    }

    [TestMethod]
    public void HiddenRegionsAndUnknownToolsArePreservedButCannotBePublished()
    {
        var doc = SpagesContent.fromMarkdown(new FwDict { ["idesc"] = "Main", ["idesc_right"] = "Keep this sidebar" });
        Assert.ThrowsExactly<UserException>(() => SpagesContent.validate(doc, "article", new(IsPublishing: true), false));
        Assert.ThrowsExactly<UserException>(() => SpagesContent.validate(doc, "sidebar-right", new(IsPublishing: true), true));
        Assert.ThrowsExactly<UserException>(() => SpagesContent.validate(doc, "article", new(IsPublishing: true), true));
        StringAssert.Contains(doc.ToJsonString(), "Keep this sidebar");
        SpagesContent.validate(doc, "sidebar-right", new(IsPublishing: true), false);
        var unknown = document("applicationToolNotInstalled", new() { ["content"] = "Keep this block" });
        var parsed = SpagesContent.parse(unknown.ToJsonString());
        Assert.ThrowsExactly<UserException>(() => SpagesContent.validate(parsed, "article", new(IsPublishing: true), false));
        StringAssert.Contains(parsed.ToJsonString(), "Keep this block");
    }

}