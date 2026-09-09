using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class ErrorPageGuidanceTests
{
    [TestMethod]
    [DataRow(400, false, "Check your request", "Review the message below")]
    [DataRow(403, false, "Access denied", "sign in before accessing this page")]
    [DataRow(403, true, "Access denied", "does not have permission to access this page")]
    [DataRow(404, false, "Page not found", "may have moved, been removed")]
    [DataRow(500, false, "Something went wrong", "Try again later")]
    public void HtmlAndJsonKeepStatusAndSafeMessagesWithStatusSpecificGuidance(
        int code,
        bool loggedIn,
        string expectedTitle,
        string expectedDescriptionFragment)
    {
        var message = code == 500 ? "INTERNAL_DIAGNOSTIC_SENTINEL" : "Correct <script>unsafe()</script> input";
        Exception ex = code switch
        {
            400 => new UserException(message),
            403 => new AuthException(message),
            404 => new NotFoundException(message),
            _ => new ApplicationException(message)
        };
        foreach (var json in new[] { true, false })
        {
            var context = TestHelpers.CreateHttpContext("");
            context.Request.PathBase = "/portal";
            context.Request.Path = "/Admin/Demos";
            context.Request.QueryString = new Microsoft.AspNetCore.Http.QueryString("?page=2");
            context.Request.Headers.Accept = json ? "application/json" : "text/html";
            using var scope = new FwTestScope(_ => new RejectingDb(), new Dictionary<string, string?>
            {
                ["appSettings:IS_DEV"] = "false",
                ["appSettings:ROOT_URL"] = "/portal",
                ["appSettings:ROOT_DOMAIN"] = "https://example.test",
                ["appSettings:PAGE_LAYOUT"] = "main.html",
                ["appSettings:template"] = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../osafw-app/App_Data/template")),
            }, context);
            var fw = scope.Fw;
            if (loggedIn) fw.Session("user_id", "7");
            fw.FormErrors["iname"] = "REQUIRED";
            fw.response.Body = new MemoryStream();
            fw.errMsg(message, ex);
            Assert.AreEqual(code, fw.response.StatusCode);
            fw.response.Body.Position = 0;
            var body = new StreamReader(fw.response.Body).ReadToEnd();
            Assert.DoesNotContain("INTERNAL_DIAGNOSTIC_SENTINEL", body);
            if (json)
            {
                var result = (FwDict)Utils.jsonDecode(body)!;
                var error = (FwDict)result["error"]!;
                Assert.AreEqual(code, error["code"].toInt());
                Assert.IsFalse(error.ContainsKey("display_message"));
                Assert.IsFalse(error.ContainsKey("title"));
                Assert.IsFalse(error.ContainsKey("description"));
                Assert.IsFalse(error.ContainsKey("show_login"));
                Assert.AreEqual("REQUIRED", ((FwDict)error["details"]!)["iname"]);
                var shouldOfferLogin = code == 403 && !loggedIn;
                Assert.AreEqual(shouldOfferLogin, error.ContainsKey("login_url"));
                if (shouldOfferLogin)
                {
                    var url = error["login_url"].toStr();
                    StringAssert.StartsWith(url, "/portal/Login?gourl=");
                    StringAssert.Contains(Uri.UnescapeDataString(url), "/Admin/Demos?page=2");
                }
            }
            else
            {
                StringAssert.Contains(body, $"<h1 id=\"error-title\">{expectedTitle}</h1>");
                StringAssert.Contains(body, expectedDescriptionFragment);
                Assert.DoesNotContain("<script>unsafe()", body);
                Assert.AreEqual(code == 403 && !loggedIn, body.Contains(">Sign in</a>", StringComparison.Ordinal));
                StringAssert.Contains(body, "/portal/");
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NotFoundHtmlUsesCustomStatusTemplateAndParsePageTranslation(bool legacyOverride)
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), $"error-page-guidance-{Guid.NewGuid():N}");
        var language = $"error{Guid.NewGuid():N}";
        Directory.CreateDirectory(Path.Combine(templateRoot, "error", "404"));
        Directory.CreateDirectory(Path.Combine(templateRoot, "lang"));
        Directory.CreateDirectory(Path.Combine(templateRoot, "error", "4xx"));
        try
        {
            File.WriteAllText(
                Path.Combine(templateRoot, "error", "404", "main.html"),
                "<h1>`CUSTOM_NOT_FOUND_TITLE`</h1><p><~error[message]></p>");
            File.WriteAllText(
                Path.Combine(templateRoot, "lang", language + ".txt"),
                "CUSTOM_NOT_FOUND_TITLE === Translated custom not found");

            File.WriteAllText(
                Path.Combine(templateRoot, "error", "4xx", "main.html"),
                legacyOverride ? "<h1>`CUSTOM_NOT_FOUND_TITLE`</h1><p><~error[message]></p>" : "<~/error/404/main>");

            var context = TestHelpers.CreateHttpContext("");
            context.Request.Headers.Accept = "text/html";
            using var scope = new FwTestScope(_ => new RejectingDb(), new Dictionary<string, string?>
            {
                ["appSettings:IS_DEV"] = "false",
                ["appSettings:PAGE_LAYOUT"] = "main.html",
                ["appSettings:template"] = templateRoot,
                ["appSettings:lang"] = language,
                ["appSettings:is_lang_update"] = "false",
            }, context);
            scope.Fw.response.Body = new MemoryStream();

            scope.Fw.errMsg("Missing <record>", new NotFoundException("Missing <record>"));

            Assert.AreEqual(404, scope.Fw.response.StatusCode);
            scope.Fw.response.Body.Position = 0;
            var body = new StreamReader(scope.Fw.response.Body).ReadToEnd();
            Assert.AreEqual("<h1>Translated custom not found</h1><p>Missing &lt;record&gt;</p>", body);
        }
        finally
        {
            if (Directory.Exists(templateRoot))
                Directory.Delete(templateRoot, true);
        }
    }
}
