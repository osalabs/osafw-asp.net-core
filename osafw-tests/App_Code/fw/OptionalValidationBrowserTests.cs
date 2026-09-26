using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace osafw.Tests;

[TestClass]
public class OptionalValidationBrowserTests
{
    [TestMethod]
    public async Task DynamicAutosave_OptInFeedbackPreservesLegacyFormsAndClearsAfterSuccess()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var assets = Environment.GetEnvironmentVariable("FW_BROWSER_ASSETS_ROOT") ?? Path.Combine(root, "osafw-app/wwwroot/assets");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.SetContentAsync("""
            <form id="optional" data-autosave data-validation-issues="[]">
                <div class="form-row"><div><input name="item[email]"><p class="err-EMAIL">Invalid email</p></div></div>
                <div class="form-row"><div><input name="item-lines#new-1[title]"></div></div>
            </form>
            <form id="legacy" data-autosave>
                <div class="form-row"><div><input name="item[email]"><p class="err-EMAIL">Invalid email</p></div></div>
            </form>
            """);
        await page.AddScriptTagAsync(new() { Path = Path.Combine(assets, "lib/jquery/jquery.min.js") });
        await page.AddScriptTagAsync(new() { Path = Path.Combine(root, "osafw-app/wwwroot/assets/js/fw.js") });
        await page.EvaluateAsync("""
            () => {
                window.toasts = []; window.completed = 0;
                fw.error = message => toasts.push(message);
                $.fn.ajaxSubmit = function (options) {
                    if (window.isHttpError) options.error({responseJSON: window.response});
                    else options.success(window.response);
                    completed++;
                };
                fw.setup_autosave_form_handlers();
            }
            """);

        async Task Save(string form, string response, bool is_http_error = false)
        {
            var count = await page.EvaluateAsync<int>("completed");
            await page.EvaluateAsync("([form, response, isError]) => { window.response = JSON.parse(response); window.isHttpError = isError; $('#' + form).trigger('autosave'); }",
                new object[] { form, response, is_http_error });
            await page.WaitForFunctionAsync("count => completed > count", count);
        }

        const string failure = """
            {"error":{"message":"Please review your input","details":{"email":"EMAIL"}},
             "validation_issues":[{"severity":"error","field":"email","message":"Check <b>this</b> address."}]}
            """;
        await Save("optional", failure, true);
        Assert.AreEqual(1, await page.Locator("#optional input.is-invalid").CountAsync());
        Assert.AreEqual(1, await page.Locator("#optional .invalid-feedback").CountAsync());
        Assert.AreEqual("Check <b>this</b> address.", await page.Locator("#optional .fw-validation-issue").TextContentAsync());
        Assert.AreEqual(0, await page.Locator("#optional .fw-validation-issue b").CountAsync());
        Assert.AreEqual(0, await page.Locator("#legacy .is-invalid").CountAsync());
        Assert.AreEqual(1, await page.EvaluateAsync<int>("toasts.length"));

        await Save("optional", """
            {"error":{"message":"Please review your input","details":{"email":"Check this value: [email]."}},
             "validation_issues":[{"severity":"error","field":"email","message":"Check this value: [email]."}]}
            """);
        Assert.AreEqual(1, await page.Locator("#optional .invalid-feedback").CountAsync());
        Assert.AreEqual("Check this value: [email].", await page.Locator("#optional .fw-validation-issue").TextContentAsync());

        await Save("legacy", failure);
        Assert.AreEqual(1, await page.Locator("#legacy .err-EMAIL.invalid-feedback").CountAsync());
        Assert.AreEqual(0, await page.Locator("#legacy .fw-validation-issue").CountAsync());

        await Save("optional", """
            {"success":true,"validation_issues":[{"severity":"warning","field":"item-lines#new-1[title]","message":"Saved; please review."}]}
            """);
        Assert.AreEqual(0, await page.Locator("#optional .is-invalid").CountAsync());
        Assert.AreEqual("Saved; please review.", await page.Locator("#optional .fw-validation-issue.text-warning").TextContentAsync());
        Assert.IsFalse(await page.EvaluateAsync<bool>("$('#optional').data('is-changed')"));
        Assert.AreEqual(1, await page.Locator("#legacy .is-invalid").CountAsync());

        await Save("optional", "{\"success\":true}");
        Assert.AreEqual(0, await page.Locator("#optional .fw-validation-issue").CountAsync());
        Assert.IsEmpty(errors, string.Join("; ", errors));
    }
}
