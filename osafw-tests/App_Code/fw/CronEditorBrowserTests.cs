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
            var state = AdminCronController.cronEditor(fw, new FwDict { ["cron"] = cron, ["timezone"] = "UTC" }, prefix);
            var html = parser.parse_page("", "common/cron_editor.html", state);
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
            Body = """{"occurrences":[{"recurrence":"07:00 UTC","local":"","utc":""}]}"""
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
        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-cron-preview] li').length === 2");
        Assert.AreEqual(2, await page.EvaluateAsync<int>("previewRequests"));
        Assert.AreEqual("weekdays", await page.Locator("#task-cron-mode").InputValueAsync());
        Assert.AreEqual("advanced", await page.Locator("#report-cron-mode").InputValueAsync());
        Assert.AreEqual("0 7 L * *", await page.Locator("#report-cron-expression").InputValueAsync());

        await page.Locator("#task-cron-mode").SelectOptionAsync("monthly");
        await page.Locator("#task-cron-day").FillAsync("31");
        Assert.AreEqual("0 7 31 * *", await page.Locator("#task-cron-expression").InputValueAsync());
        Assert.AreEqual("0 7 L * *", await page.Locator("#report-cron-expression").InputValueAsync());
        var values = await page.EvaluateAsync<string[]>("Array.from(new FormData(document.querySelector('#task')).keys())");
        CollectionAssert.AreEquivalent(new[] { "item[cron]", "item[timezone]" }, values);
        Assert.IsEmpty(errors, string.Join(", ", errors));
    }
}
