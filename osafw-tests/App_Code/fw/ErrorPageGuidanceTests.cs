using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class ErrorPageGuidanceTests
{
    [TestMethod]
    [DataRow(400, false)]
    [DataRow(403, false)]
    [DataRow(403, true)]
    [DataRow(404, false)]
    [DataRow(500, false)]
    public void HtmlAndJsonKeepStatusAndSafeMessagesWithStatusSpecificGuidance(int code, bool loggedIn)
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
                Assert.AreEqual(error["message"], error["display_message"]);
                Assert.IsFalse(string.IsNullOrWhiteSpace(error["title"].toStr()));
                Assert.IsFalse(string.IsNullOrWhiteSpace(error["description"].toStr()));
                Assert.AreEqual("REQUIRED", ((FwDict)error["details"]!)["iname"]);
                Assert.AreEqual(code == 403 && !loggedIn, error["show_login"].toBool());
                if (code == 403 && !loggedIn)
                {
                    var url = error["login_url"].toStr();
                    StringAssert.StartsWith(url, "/portal/Login?gourl=");
                    StringAssert.Contains(Uri.UnescapeDataString(url), "/Admin/Demos?page=2");
                }
            }
            else
            {
                StringAssert.Contains(body, "error-title");
                Assert.DoesNotContain("<script>unsafe()", body);
                Assert.AreEqual(code == 403 && !loggedIn, body.Contains(">Sign in</a>", StringComparison.Ordinal));
                StringAssert.Contains(body, "/portal/");
            }
        }
    }
}
