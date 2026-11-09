using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace osafw.Tests;

[TestClass, TestCategory("Browser")]
public class CardLayoutBrowserTests
{
    [TestMethod]
    public async Task Cards_KeepOuterPaddingWithoutDuplicatingTerminalFormSpacing()
    {
        var root = TestHelpers.RepoRoot();
        var assets = Environment.GetEnvironmentVariable("FW_BROWSER_ASSETS_ROOT") ?? Path.Combine(root, "osafw-app/wwwroot/assets");
        var parser = new ParsePage(new ParsePageOptions { TemplatesRoot = TestHelpers.TemplateRoot() });
        var actions = parser.parse_page("", "common/form/actions_std_responsive.html", new FwDict());
        var filters = parser.parse_page("", "admin/reports/common/filter_buttons.html", new FwDict());
        var row = "<div class='row form-row'><div class='col'><input class='form-control'></div></div>";
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        var bootstrap = File.ReadAllText(Path.Combine(assets, "lib/bootstrap/css/bootstrap.min.css"));
        var css = File.ReadAllText(Path.Combine(root, "osafw-app/wwwroot/assets/css/site.css"));

        foreach (var theme in new[] { 0, 10, 20, 30 })
        {
            var themeCss = theme == 0 ? "" : File.ReadAllText(Path.Combine(root, $"osafw-app/wwwroot/assets/css/theme{theme}.css"));
            foreach (var width in new[] { 390, 1533 })
            {
                await page.SetViewportSizeAsync(width, 1000);
                await page.SetContentAsync($"""
                    <style>{bootstrap}{css}{themeCss}</style>
                    <div class="fw-card" id="plain"><button class="btn">Action</button></div>
                    <form class="fw-card" id="flat"><fieldset>{row}{row}</fieldset></form>
                    <form class="fw-card" id="columns"><fieldset><div class="row"><div class="col-lg-6">{row}{row}</div><div class="col-lg-6">{row}</div></div>{actions}</fieldset></form>
                    <form class="fw-card" id="stacked"><fieldset><div class="row"><div class="col-lg-6">{row}</div><div class="col-lg-6">{row}</div></div></fieldset></form>
                    <form class="fw-card" id="vue"><fieldset>{row}{row}</fieldset>{actions}</form>
                    <div class="fw-card" id="sections"><div><div class="form-row">First section</div></div><p>Second section</p></div>
                    <form class="fw-card" id="filters">{filters}</form>
                    <div class="fw-card" id="table"><table class="table"><tbody><tr><td>Result</td></tr></tbody></table></div>
                    <div class="fw-list-card" id="list">List</div>
                    """);
                var failures = await page.EvaluateAsync<string[]>("""
                    () => {
                        const failures = [];
                        const number = (e, property) => parseFloat(getComputedStyle(e)[property]);
                        const near = (actual, expected, name) => {
                            if (Math.abs(actual - expected) > 1) failures.push(name + ': ' + actual + ' != ' + expected);
                        };
                        for (const id of ['plain', 'flat', 'columns', 'stacked', 'vue', 'filters', 'table']) {
                            const card = document.getElementById(id);
                            const inset = number(card, 'paddingBottom');
                            if (inset < 16) failures.push(id + ': missing inset');
                            const children = [...card.querySelectorAll('input,button,a.btn,table')].filter(e => e.checkVisibility());
                            const bottom = Math.max(...children.map(e => e.getBoundingClientRect().bottom));
                            near(card.getBoundingClientRect().bottom - number(card, 'borderBottomWidth') - bottom, inset, id);
                        }
                        near(number(document.querySelector('#flat .form-row'), 'paddingBottom'), 16, 'inter-row spacing');
                        near(number(document.querySelector('#stacked .col-lg-6:first-child > .form-row:last-child'), 'paddingBottom'), innerWidth < 992 ? 16 : 0, 'stacked-column spacing');
                        near(number(document.querySelector('#sections .form-row'), 'paddingBottom'), 16, 'inter-section spacing');
                        near(number(document.querySelector('#table table'), 'marginBottom'), 0, 'table margin');
                        return failures;
                    }
                    """);
                var listPadding = await page.Locator("#list").EvaluateAsync<float>("e => parseFloat(getComputedStyle(e).paddingBottom)");
                Assert.AreEqual(theme == 0 ? 0 : theme == 30 ? 4 : 8, listPadding, "List table spacing must stay unchanged.");
                Assert.IsEmpty(failures, $"Theme {theme}, width {width}: {string.Join("; ", failures)}");
                await page.Locator("#emailto").EvaluateAsync("e => e.style.display = 'flex'");
                Assert.IsTrue(await page.Locator("#emailto").IsVisibleAsync());
                var emailPadding = await page.Locator("#emailto").EvaluateAsync<float>("e => parseFloat(getComputedStyle(e).paddingBottom)");
                Assert.AreEqual(0, emailPadding, "Expanded email row must not duplicate the card inset.");
            }
        }
    }

    [TestMethod]
    public async Task SpecializedTemplates_KeepTheirCardInsetAndReportPrintRemainsUnpadded()
    {
        var root = TestHelpers.RepoRoot();
        var assets = Environment.GetEnvironmentVariable("FW_BROWSER_ASSETS_ROOT") ?? Path.Combine(root, "osafw-app/wwwroot/assets");
        var parser = new ParsePage(new ParsePageOptions { TemplatesRoot = TestHelpers.TemplateRoot() });
        var state = new FwDict
        {
            ["fields"] = new FwList
            {
                new() { ["field"] = "iname", ["type"] = "input", ["label"] = "Title", ["value"] = "Example" },
                new() { ["field"] = "roles_resources_permissions", ["is_custom"] = true }
            }
        };
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        var css = File.ReadAllText(Path.Combine(assets, "lib/bootstrap/css/bootstrap.min.css"))
            + File.ReadAllText(Path.Combine(root, "osafw-app/wwwroot/assets/css/site.css"));
        foreach (var template in new[] { "my/mfa/showform/main.html", "signup/showform/form.html", "admin/roles/showform/main.html", "admin/roles/show/main.html", "admin/kbarticles/show/main.html", "admin/ragchunks/show/main.html" })
        {
            foreach (var width in new[] { 390, 1533 })
            {
                await page.SetViewportSizeAsync(width, 1000);
                await page.SetContentAsync("<style>" + css + "</style>" + parser.parse_page("/" + template[..template.LastIndexOf('/')], Path.GetFileName(template), state));
                var inset = await page.Locator(".fw-card").EvaluateAsync<float>("e => parseFloat(getComputedStyle(e).paddingBottom)");
                Assert.AreEqual(16, inset, template);
                var fieldsetMargin = await page.Locator(".fw-card > fieldset").EvaluateAsync<float>("e => parseFloat(getComputedStyle(e).marginBottom)");
                Assert.AreEqual(0, fieldsetMargin, template);
                if (template.StartsWith("admin/roles/") && (width >= 992 || template.Contains("/show/")))
                {
                    Assert.AreEqual(0, await page.Locator(".fw-card .border.rounded").EvaluateAsync<float>("e => parseFloat(getComputedStyle(e).marginBottom)"), template);
                }
                if (template.StartsWith("my/") || template.StartsWith("signup/"))
                {
                    var gap = await page.Locator(".fw-card").EvaluateAsync<float>("e => e.getBoundingClientRect().bottom - parseFloat(getComputedStyle(e).borderBottomWidth) - [...e.querySelectorAll('button')].filter(b => b.checkVisibility()).at(-1).getBoundingClientRect().bottom");
                    Assert.AreEqual(16, gap, 1, template);
                }
            }
        }
        await page.SetContentAsync("<style>" + css + "</style>" + parser.parse_page("", "admin/reports/common/result_table.html", new FwDict()));
        await page.AddStyleTagAsync(new() { Content = File.ReadAllText(Path.Combine(TestHelpers.TemplateRoot(), "admin/reports/common/head.css")) });
        await page.EmulateMediaAsync(new() { Media = Media.Print });
        Assert.AreEqual("0px", await page.Locator(".fw-card").EvaluateAsync<string>("e => getComputedStyle(e).padding"));
    }
}
