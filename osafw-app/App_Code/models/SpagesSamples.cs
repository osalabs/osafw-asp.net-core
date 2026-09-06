using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace osafw;

public partial class Spages
{
    /// <summary>Install separate demonstration drafts once. Existing pages and the application homepage are never overwritten.</summary>
    public FwList installSamples(string sample = "")
    {
        requireAuthor(true);
        if (fw.userAccessLevel < Users.ACL_SITEADMIN) throw new AuthException();
        if (sample is not ("" or "services" or "department")) throw new UserException("Select an installed sample page.");
        var result = new FwList();
        JsonObject document(params JsonObject[] blocks)
        {
            var doc = SpagesContent.empty();
            doc["regions"]!["main"]!["blocks"] = new JsonArray(blocks);
            return doc;
        }
        JsonObject block(string type, JsonObject data) => new() { ["type"] = type, ["data"] = data };
        void install(string title, string slug, string layout, JsonObject doc, bool snippet = false)
        {
            if (!snippet && sample.Length > 0 && slug != "demo-" + sample) return;
            var existing = db.array(table_name, []).Select(row => draft(row["id"].toInt())).FirstOrDefault(row => snippet ? row["snippet_key"].toStr() == slug : row["url"].toStr() == slug) ?? new FwDict();
            if (existing.Count > 0) { result.Add(DB.h("id", existing["id"], "created", false)); return; }
            int id = saveDraft(0, DB.h("iname", title, "url", slug, "template", layout, "content_json", doc.ToJsonString(), "is_snippet", snippet ? 1 : 0, "snippet_key", snippet ? slug : "", "nav_visible", 1, "nav_title", slug == "demo-services" ? "Our services" : slug == "demo-department" ? "Department resources" : "Help"), 0);
            result.Add(DB.h("id", id, "created", true));
        }
        install("How can we help?", "demo-help", "article", document(
            block("callout", new() { ["title"] = "Let's find the right next step", ["text"] = "Tell us what you need. Our team will connect you with the right person." }),
            block("button", new() { ["text"] = "Contact our team", ["url"] = "/Contact" })), true);
        install("Good work starts with a clear plan", "demo-services", "landing", document(
            block("paragraph", new() { ["text"] = "Practical expertise. Thoughtful service. We help ambitious teams turn complex challenges into useful, lasting improvements." }),
            block("button", new() { ["text"] = "Explore our services", ["url"] = "#services" }),
            block("header", new() { ["text"] = "Support at every stage", ["level"] = 2, ["anchor"] = "services" }),
            block("cards", new() { ["items"] = new JsonArray(
                new JsonObject { ["title"] = "Understand", ["text"] = "Find clarity through focused discovery, research, and a shared view of success." },
                new JsonObject { ["title"] = "Create", ["text"] = "Build practical solutions around the people who will use them every day." },
                new JsonObject { ["title"] = "Improve", ["text"] = "Keep learning, measure what matters, and make the next iteration better." }) }),
            block("quote", new() { ["text"] = "A good partnership makes the next step feel possible.", ["caption"] = "Our approach" }),
            block("snippet", new() { ["key"] = "demo-help" })));
        var department = document(
            block("paragraph", new() { ["text"] = "Your starting point for the people, guidance, and everyday resources that help our department do its best work." }),
            block("header", new() { ["text"] = "Start here", ["level"] = 2 }),
            block("cards", new() { ["items"] = new JsonArray(
                new JsonObject { ["title"] = "New to the team?", ["text"] = "Get oriented with a simple checklist, team introductions, and the tools you need." },
                new JsonObject { ["title"] = "Planning a project", ["text"] = "Use our shared guidance to define the outcome, find support, and prepare your next step." }) }),
            block("table", new() { ["caption"] = "Where to find support", ["withHeadings"] = true, ["content"] = new JsonArray(new JsonArray("Need", "First step"), new JsonArray("Getting access", "Contact the service desk"), new JsonArray("Project advice", "Speak with your team lead")) }),
            block("snippet", new() { ["key"] = "demo-help" }));
        department["regions"]!["right"] = new JsonObject { ["blocks"] = new JsonArray(block("callout", new() { ["title"] = "Keep this page useful", ["text"] = "Found a gap or an outdated resource? Let the department editor know." })) };
        install("People, resources, and a place to start", "demo-department", "sidebar-right", department);
        return result;
    }
}
