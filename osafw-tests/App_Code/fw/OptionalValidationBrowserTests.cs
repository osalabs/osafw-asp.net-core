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
    public async Task DynamicForms_AcceptBothDataErrorsFormatsAndKeepFeedbackScoped()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var assets = Environment.GetEnvironmentVariable("FW_BROWSER_ASSETS_ROOT") ?? Path.Combine(root, "osafw-app/wwwroot/assets");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.SetContentAsync("""
            <form id="structured" data-autosave data-errors='[{"severity":"warning","field":"email","message":"Check before saving."}]'>
                <div class="form-row"><div><input name="item[email]"><p class="err-EMAIL">Invalid email</p></div></div>
                <div class="form-row"><div><input name="item-lines#new-1[title]"></div></div>
            </form>
            <form id="mapped" data-autosave data-errors='{"email":"EMAIL"}'>
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
                fw.process_form_errors();
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
             "form_issues":[{"severity":"error","field":"email","message":"Check <b>this</b> address."}]}
            """;
        Assert.AreEqual("Check before saving.", await page.Locator("#structured .fw-form-issue.text-warning").TextContentAsync());
        Assert.AreEqual(0, await page.Locator("#structured .is-invalid").CountAsync());
        Assert.AreEqual(1, await page.Locator("#mapped .err-EMAIL.invalid-feedback").CountAsync());
        await Save("structured", failure, true);
        Assert.AreEqual(1, await page.Locator("#structured input.is-invalid").CountAsync());
        Assert.AreEqual(1, await page.Locator("#structured .invalid-feedback").CountAsync());
        Assert.AreEqual("Check <b>this</b> address.", await page.Locator("#structured .fw-form-issue").TextContentAsync());
        Assert.AreEqual(0, await page.Locator("#structured .fw-form-issue b").CountAsync());
        Assert.AreEqual(1, await page.Locator("#mapped .err-EMAIL.invalid-feedback").CountAsync());
        Assert.AreEqual(1, await page.EvaluateAsync<int>("toasts.length"));

        await Save("structured", """
            {"error":{"message":"Please review your input","details":{"email":"Check this value: [email]."}},
             "form_issues":[{"severity":"warning","field":"email","message":"Secondary warning."},
                 {"severity":"error","field":"email","message":"Check this value: [email]."}]}
            """);
        Assert.AreEqual(1, await page.Locator("#structured .invalid-feedback").CountAsync());
        Assert.AreEqual(0, await page.Locator("#structured .text-warning").CountAsync());
        Assert.AreEqual("Check this value: [email].", await page.Locator("#structured .fw-form-issue").TextContentAsync());

        await Save("mapped", """{"error":{"message":"Please review your input","details":{"email":"EMAIL"}}}""");
        Assert.AreEqual(1, await page.Locator("#mapped .err-EMAIL.invalid-feedback").CountAsync());
        Assert.AreEqual(0, await page.Locator("#mapped .fw-form-issue").CountAsync());

        await Save("mapped", failure);
        Assert.AreEqual(0, await page.Locator("#mapped .err-EMAIL.invalid-feedback").CountAsync());
        Assert.AreEqual("Check <b>this</b> address.", await page.Locator("#mapped .fw-form-issue").TextContentAsync());

        await Save("mapped", """
            {"error":{"message":"Please review your input","details":{"email":"EMAIL"}},
             "form_issues":[{"severity":"error","field":"email","code":"EMAIL","message":"Generic email message"}]}
            """);
        Assert.AreEqual("Invalid email", await page.Locator("#mapped .invalid-feedback").TextContentAsync());
        Assert.AreEqual(0, await page.Locator("#mapped .fw-form-issue").CountAsync());

        await Save("structured", """
            {"success":true,"form_issues":[{"severity":"warning","field":"item-lines#new-1[title]","message":"Saved; please review."}]}
            """);
        Assert.AreEqual(0, await page.Locator("#structured .is-invalid").CountAsync());
        Assert.AreEqual("Saved; please review.", await page.Locator("#structured .fw-form-issue.text-warning").TextContentAsync());
        Assert.IsFalse(await page.EvaluateAsync<bool>("$('#structured').data('is-changed')"));
        Assert.AreEqual(1, await page.Locator("#mapped .is-invalid").CountAsync());

        await Save("structured", "{\"success\":true}");
        Assert.AreEqual(0, await page.Locator("#structured .fw-form-issue").CountAsync());
        await Save("mapped", """{"error":{"code":500,"message":"Save failed","details":{"email":"EMAIL"}}}""", true);
        Assert.AreEqual(0, await page.Locator("#mapped .is-invalid,#mapped .invalid-feedback").CountAsync(), "Generic server details must not become field validation.");
        Assert.IsEmpty(errors, string.Join("; ", errors));
    }
}
