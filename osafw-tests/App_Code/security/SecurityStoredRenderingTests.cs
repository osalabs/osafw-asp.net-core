using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;

namespace osafw.Tests;

[TestClass]
public class SecurityStoredRenderingTests
{
    [TestMethod]
    public void ParsePageMarkdown_DisablesHtmlByDefaultWhileKeepingFormatting()
    {
        var parser = new ParsePage(null!);
        var ps = new FwDict
        {
            ["body"] = "<script>alert(1)</script>\n\n**safe**"
        };

        var html = parser.parse_string("<~body markdown noescape>", ps);

        Assert.IsFalse(html.Contains("<script>", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(html, "&lt;script&gt;alert(1)&lt;/script&gt;");
        StringAssert.Contains(html, "<strong>safe</strong>");
    }

    [TestMethod]
    public void ParsePageMarkdown_TrustedPathAllowsExplicitRawHtml()
    {
        var parser = new ParsePage(null!);
        var ps = new FwDict
        {
            ["body"] = "<span class=\"trusted-fragment\">Trusted</span>"
        };

        var safeHtml = parser.parse_string("<~body markdown noescape>", ps);
        var trustedHtml = parser.parse_string("<~body markdown=\"trusted\">", ps);

        Assert.IsFalse(safeHtml.Contains("<span class=\"trusted-fragment\">", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(trustedHtml, "<span class=\"trusted-fragment\">Trusted</span>");
    }

    [TestMethod]
    public void StaticPageMarkdownContent_RendersStoredScriptPayloadInert()
    {
        var template = readRepoFile("osafw-app", "App_Data", "template", "home", "spage", "one_col.html");
        var parser = new ParsePage(null!);
        var ps = new FwDict
        {
            ["page"] = new FwDict
            {
                ["idesc"] = "<script>alert(1)</script>\n\n**public copy**"
            }
        };

        var html = parser.parse_string(template, ps);

        Assert.IsFalse(html.Contains("<script>", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(html, "&lt;script&gt;alert(1)&lt;/script&gt;");
        StringAssert.Contains(html, "<strong>public copy</strong>");
    }

    [TestMethod]
    public void ClassicDynamicMarkdown_UsesTrustedOptionOnly()
    {
        var template = readRepoFile("osafw-app", "App_Data", "template", "common", "form", "show", "markdown.html");
        var parser = new ParsePage(null!);
        var safeHtml = parser.parse_string(template, new FwDict
        {
            ["value"] = "<span class=\"raw-fragment\">Raw</span>\n\n**safe**"
        });
        var trustedHtml = parser.parse_string(template, new FwDict
        {
            ["value"] = "<span class=\"raw-fragment\">Raw</span>",
            ["trusted"] = true
        });

        Assert.IsFalse(safeHtml.Contains("<span class=\"raw-fragment\">", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(safeHtml, "&lt;span class=&quot;raw-fragment&quot;&gt;Raw&lt;/span&gt;");
        StringAssert.Contains(safeHtml, "<strong>safe</strong>");
        StringAssert.Contains(trustedHtml, "<span class=\"raw-fragment\">Raw</span>");
    }

    [TestMethod]
    public void ServerControlledMarkdownTemplates_UseTrustedOptIn()
    {
        var markdownHelp = readRepoFile("osafw-app", "App_Data", "template", "home", "markdownhelp", "main.html");
        var devDocsTabs = readRepoFile("osafw-app", "App_Data", "template", "dev", "manage", "docs", "main_tabs.html");
        var devDocsPrint = readRepoFile("osafw-app", "App_Data", "template", "dev", "manage", "docs", "main_print.html");
        var entityBuilder = readRepoFile("osafw-app", "App_Data", "template", "dev", "manage", "entitybuilder", "main.html");
        var staticPage = readRepoFile("osafw-app", "App_Data", "template", "home", "spage", "one_col.html");
        var activityLog = readRepoFile("osafw-app", "App_Data", "template", "common", "activitylogs", "main.html");

        StringAssert.Contains(markdownHelp, "markdown=\"trusted\"");
        Assert.IsFalse(markdownHelp.Contains(" markdown></div>", StringComparison.Ordinal));
        StringAssert.Contains(devDocsTabs, "markdown=\"trusted\"");
        StringAssert.Contains(devDocsPrint, "markdown=\"trusted\"");
        StringAssert.Contains(entityBuilder, "README.md markdown=\"trusted\" nolang");
        Assert.IsFalse(staticPage.Contains("markdown=\"trusted\"", StringComparison.Ordinal));
        Assert.IsFalse(activityLog.Contains("markdown=\"trusted\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DevCodeGenMarkdown_PreservesTrustedOptionForExtractedViews()
    {
        var fields = new FwList
        {
            new FwDict { ["field"] = "body", ["type"] = "markdown" },
            new FwDict { ["field"] = "trusted_body", ["type"] = "markdown", ["trusted"] = true }
        };

        var codeGenType = typeof(FW).Assembly.GetType("osafw.DevCodeGen")
            ?? throw new InvalidOperationException("DevCodeGen type was not found.");
        var method = codeGenType.GetMethod("makeValueTags", BindingFlags.Static | BindingFlags.Public)
            ?? throw new InvalidOperationException("DevCodeGen.makeValueTags was not found.");

        method.Invoke(null, [fields]);

        Assert.AreEqual("<~i[body] markdown>", ((FwDict)fields[0])["value"]);
        Assert.AreEqual("<~i[trusted_body] markdown=\"trusted\">", ((FwDict)fields[1])["value"]);
    }

    [TestMethod]
    public void StaticPageExecutableFields_AreReservedForSiteadminsInFormAndSavePolicy()
    {
        var controllerSource = readRepoFile("osafw-app", "App_Code", "controllers", "AdminSpages.cs");
        var formTemplate = readRepoFile("osafw-app", "App_Data", "template", "admin", "spages", "showform", "page_content.html");

        StringAssert.Contains(controllerSource, "ps[\"is_site_admin\"] = fw.model<Users>().isAccessLevel(Users.ACL_SITEADMIN);");
        StringAssert.Contains(controllerSource, "if (!fw.model<Users>().isAccessLevel(Users.ACL_SITEADMIN))");
        StringAssert.Contains(controllerSource, "foreach (string field in Utils.qw(\"custom_head custom_css custom_js\"))");
        Assert.IsFalse(controllerSource.Contains("canSaveStaticPageExecutableFields", StringComparison.Ordinal));
        Assert.IsFalse(controllerSource.Contains("removeStaticPageExecutableFields", StringComparison.Ordinal));
        StringAssert.Contains(formTemplate, "<~trusted_executable_fields if=\"is_site_admin\" inline>");
        StringAssert.Contains(formTemplate, "name=\"item[custom_head]\"");
        StringAssert.Contains(formTemplate, "name=\"item[custom_css]\"");
        StringAssert.Contains(formTemplate, "name=\"item[custom_js]\"");
    }

    [TestMethod]
    public void VueMarkdownUsesTrustedOptionAndNoescapeRendersRawTrustedContent()
    {
        var formTemplate = readRepoFile("osafw-app", "App_Data", "template", "common", "vue", "form-one-control.html");
        var activityTemplate = readRepoFile("osafw-app", "App_Data", "template", "common", "vue", "activity-logs.html");

        StringAssert.Contains(formTemplate, "renderMarkdown(value, isTrustedMarkdown)");
        StringAssert.Contains(formTemplate, "def.type=='noescape'\" class=\"form-control-plaintext\" v-html=\"value\"");
        StringAssert.Contains(formTemplate, "this.def.trusted === true");
        StringAssert.Contains(formTemplate, "html: isTrustedMarkdown");
        StringAssert.Contains(formTemplate, "AppUtils.htmlescape(text ?? '')");
        Assert.IsFalse(formTemplate.Contains("is_trusted_renderer", StringComparison.Ordinal));
        Assert.IsFalse(formTemplate.Contains("isTrustedRenderer", StringComparison.Ordinal));
        Assert.IsFalse(formTemplate.Contains("html: true", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(activityTemplate, "html: false");
        StringAssert.Contains(activityTemplate, "AppUtils.htmlescape(text ?? '')");
    }

    [TestMethod]
    public void MarkdownEditorPreview_DisablesHtmlAndAttributeExtensionsByDefault()
    {
        var editorTemplate = readRepoFile("osafw-app", "App_Data", "template", "common", "markdown_editor.html");

        StringAssert.Contains(editorTemplate, "html: isTrustedMarkdown");
        StringAssert.Contains(editorTemplate, "isTrustedMarkdown && window.markdownitContainer");
        StringAssert.Contains(editorTemplate, "isTrustedMarkdown && window.markdownItAttrs");
        StringAssert.Contains(editorTemplate, "markdown-trusted");
        StringAssert.Contains(editorTemplate, "markdownTrusted");
        Assert.IsFalse(editorTemplate.Contains("html: true", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void VueCustomListCells_EscapeByDefaultAndUseExplicitTrustedMap()
    {
        var rowTemplate = readRepoFile("osafw-app", "App_Data", "template", "common", "vue", "list-table-row.html");
        var storeTemplate = readRepoFile("osafw-app", "App_Data", "template", "common", "vue", "store.js");
        var controllerSource = readRepoFile("osafw-app", "App_Code", "fw", "FwController.cs");
        var vueControllerSource = readRepoFile("osafw-app", "App_Code", "fw", "FwVueController.cs");

        StringAssert.Contains(rowTemplate, "fwStore.isTrustedListRenderer(header)\" v-html=\"fwStore.cellFormatter(row, header)\"");
        StringAssert.Contains(rowTemplate, "{{ fwStore.cellFormatter(row, header) }}");
        Assert.IsFalse(rowTemplate.Contains("v-html=\"fwStore.cellFormatter(row, header)\"</div>", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(storeTemplate, "view_list_custom_trusted");
        StringAssert.Contains(storeTemplate, "isTrustedListRenderer(header)");
        StringAssert.Contains(controllerSource, "protected string view_list_custom_trusted");
        StringAssert.Contains(controllerSource, "view_list_custom_trusted = config[\"view_list_custom_trusted\"].toStr();");
        StringAssert.Contains(controllerSource, "{\"is_custom_trusted\",hcustomTrusted.ContainsKey(fieldname)}");
        StringAssert.Contains(vueControllerSource, "Utils.qh(this.view_list_custom_trusted, \"1\")");
    }

    private static string readRepoFile(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(repoRoot(), Path.Combine(parts)));
    }

    private static string repoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "osafw-asp.net-core.sln")))
                return dir;

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
