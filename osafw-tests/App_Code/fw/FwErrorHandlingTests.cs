using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class FwErrorHandlingTests
{
    private const string GenericServerError = "Server Error. Please, contact site administrator!";

    [TestMethod]
    public void ErrMsg_MasksServerExceptionJsonWhenNotDev()
    {
        const string sensitiveMessage = "Invalid object name 'customer_secret_table'.";

        var (fw, json, body) = renderErrorJson(false, sensitiveMessage, new ApplicationException(sensitiveMessage));
        var error = json["error"] as FwDict ?? [];

        Assert.AreEqual(StatusCodes.Status500InternalServerError, fw.response.StatusCode);
        Assert.AreEqual(GenericServerError, json["message"]);
        Assert.AreEqual(GenericServerError, json["err_msg"]);
        Assert.AreEqual(GenericServerError, error["message"]);
        Assert.AreEqual(500, error["code"].toInt());
        Assert.IsFalse(json["success"].toBool());
        Assert.IsFalse(json.ContainsKey("DUMP_STACK"));
        Assert.IsFalse(json.ContainsKey("DUMP_SQL"));
        Assert.IsFalse(body.Contains(sensitiveMessage, StringComparison.Ordinal));
        Assert.IsFalse(body.Contains("customer_secret_table", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ErrMsg_ShowsServerExceptionJsonInDev()
    {
        const string detailMessage = "Invalid object name 'missing_table'.";

        var (fw, json, body) = renderErrorJson(true, detailMessage, new ApplicationException(detailMessage));
        var error = json["error"] as FwDict ?? [];

        Assert.AreEqual(StatusCodes.Status500InternalServerError, fw.response.StatusCode);
        Assert.AreEqual(detailMessage, json["message"]);
        Assert.AreEqual(detailMessage, error["message"]);
        Assert.IsTrue(json.ContainsKey("DUMP_STACK"));
        Assert.IsTrue(json.ContainsKey("DUMP_SQL"));
        Assert.IsTrue(body.Contains("missing_table", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ErrMsg_PreservesUserFacingExceptionJsonWhenNotDev()
    {
        const string userMessage = "Uploaded image is too large";

        var (fw, json, body) = renderErrorJson(false, userMessage, new UserException(userMessage));
        var error = json["error"] as FwDict ?? [];

        Assert.AreEqual(StatusCodes.Status400BadRequest, fw.response.StatusCode);
        Assert.AreEqual(userMessage, json["message"]);
        Assert.AreEqual(userMessage, error["message"]);
        Assert.IsFalse(json.ContainsKey("DUMP_STACK"));
        Assert.IsTrue(body.Contains(userMessage, StringComparison.Ordinal));
    }

    private static (FW fw, FwDict json, string body) renderErrorJson(bool isDev, string message, Exception ex)
    {
        var fw = TestHelpers.CreateFw(new Dictionary<string, string?>
        {
            ["appSettings:IS_DEV"] = isDev ? "true" : "false",
        });
        fw.request.Headers.Accept = "application/json";
        fw.response.Body = new MemoryStream();

        fw.errMsg(message, ex);

        fw.response.Body.Position = 0;
        var body = new StreamReader(fw.response.Body).ReadToEnd();
        var json = Utils.jsonDecode(body) as FwDict ?? [];
        return (fw, json, body);
    }
}
