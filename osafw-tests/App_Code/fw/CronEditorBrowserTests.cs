using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace osafw.Tests;

[TestClass, TestCategory("Browser")]
public class CronEditorBrowserTests
{
    [TestMethod]
    public async Task SharedEditor_RendersPresetsAndPreservesAnAdvancedExpressionAcrossInstances()
    {
        var root = TestHelpers.RepoRoot();
        var assets = Environment.GetEnvironmentVariable("FW_BROWSER_ASSETS_ROOT") ?? Path.Combine(root, "osafw-app/wwwroot/assets");
        var parser = new ParsePage(new ParsePageOptions { TemplatesRoot = TestHelpers.TemplateRoot() });
        var fw = TestHelpers.CreateFw();
        fw.Session("timezone", "UTC");

        string Editor(string prefix, string cron)
        {
            var state = AdminCronController.cronEditor(fw, new FwDict { ["cron"] = cron, ["timezone"] = "UTC" }, prefix, isExternalPreview: prefix == "report-cron");
            var html = parser.parse_page("", "common/cron_editor.html", state);
            if (prefix == "report-cron")
                html += parser.parse_page("", "common/cron_preview.html", state);
            return Regex.Replace(html, "<script>.*?</script>", "", RegexOptions.Singleline);
        }

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.RouteAsync("**/Admin/Cron/**", route => route.FulfillAsync(new()
        {
            ContentType = "application/json",
            Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
            Body = """{"occurrences":[{"recurrence":"11/8/2026 07:00","recurrence_title":"11/8/2026 07:00 America/Chicago (-06:00)","local":"11/8/2026 13:00","local_title":"11/8/2026 13:00 UTC (+00:00)","utc":"2026-11-08 13:00 UTC"}]}"""
        }));
        await page.SetContentAsync("<base href=\"https://scheduler.test/\"><form id=\"task\">" + Editor("task-cron", "0 7 * * 1-5")
            + "</form><form id=\"report\">" + Editor("report-cron", "0 7 L * *") + "</form>");
        await page.AddScriptTagAsync(new() { Path = Path.Combine(assets, "lib/jquery/jquery.min.js") });
        await page.AddScriptTagAsync(new() { Path = Path.Combine(root, "osafw-app/wwwroot/assets/js/fw.js") });
        await page.AddScriptTagAsync(new() { Path = Path.Combine(root, "osafw-app/wwwroot/assets/js/fw-cron-editor.js") });
        await page.EvaluateAsync("""
            async () => {
                window.previewRequests = 0;
                const fetchPreview = window.fetch.bind(window);
                window.fetch = (...args) => { previewRequests++; return fetchPreview(...args); };
                const initialize = () => fw.initComponent('cron-editor', {init: scope => fwCronEditor.init(scope)});
                await initialize();
                await initialize();
            }
            """);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-cron-preview] tr').length === 2");
        Assert.AreEqual(2, await page.EvaluateAsync<int>("previewRequests"));
        Assert.AreEqual(1, await page.Locator("#report [data-cron-preview]").CountAsync());
        Assert.AreEqual(3, await page.Locator("#report [data-cron-preview] td").CountAsync());
        Assert.AreEqual("11/8/2026 07:00 America/Chicago (-06:00)", await page.Locator("#report [data-cron-preview] td").First.GetAttributeAsync("title"));
        Assert.AreEqual("11/8/2026 13:00", await page.Locator("#report [data-cron-preview] td").Nth(1).InnerTextAsync());
        Assert.AreEqual(0, await page.Locator("#report [data-cron-editor] [data-cron-preview]").CountAsync());
        Assert.AreEqual("weekdays", await page.Locator("#task-cron-mode").InputValueAsync());
        Assert.AreEqual("true", await page.Locator("#report [data-cron-edit-mode=expression]").GetAttributeAsync("aria-pressed"));
        Assert.IsFalse(await page.Locator("#report [data-cron-presets]").IsVisibleAsync());
        Assert.AreEqual("0 7 L * *", await page.Locator("#report-cron-expression").InputValueAsync());

        await page.Locator("#task-cron-mode").SelectOptionAsync("monthly");
        await page.Locator("#task-cron-day").FillAsync("31");
        Assert.AreEqual("0 7 31 * *", await page.Locator("#task-cron-expression").InputValueAsync());
        Assert.AreEqual("0 7 L * *", await page.Locator("#report-cron-expression").InputValueAsync());
        var values = await page.EvaluateAsync<string[]>("Array.from(new FormData(document.querySelector('#task')).keys())");
        CollectionAssert.AreEquivalent(new[] { "item[cron]", "item[timezone]" }, values);
        await page.Locator("#task [data-cron-edit-mode=expression]").ClickAsync();
        Assert.IsTrue(await page.Locator("#task-cron-expression").IsVisibleAsync());
        await page.Locator("#task-cron-expression").FillAsync("15 8 * * *");
        await page.Locator("#task [data-cron-edit-mode=simple]").ClickAsync();
        Assert.AreEqual("daily", await page.Locator("#task-cron-mode").InputValueAsync());
        Assert.AreEqual("08:15", await page.Locator("#task-cron-time").InputValueAsync());
        Assert.AreEqual("15 8 * * *", await page.Locator("#task-cron-expression").InputValueAsync());
        Assert.IsEmpty(errors, string.Join(", ", errors));
    }
}
