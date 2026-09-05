using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class FwLoginRedirectTests
{
    [TestMethod]
    [DataRow("", "/Admin/DemosDynamic", "")]
    [DataRow("", "/Admin/DemosDynamic/", "?dofilter=1&f[title]=A%26B&gourl=https%3A%2F%2Fgoogle.com")]
    [DataRow("/portal", "/Admin/DemosDynamic", "?page=2")]
    public void Dispatch_AnonymousPageGoesToLoginWithOriginalUrl(string pathBase, string path, string query)
    {
        using var fw = createFw(pathBase, path, query);

        fw.dispatch();

        Assert.AreEqual(302, fw.response.StatusCode);
        var location = fw.response.Headers.Location.ToString();
        StringAssert.StartsWith(location, pathBase + "/Login?");
        var parameters = QueryHelpers.ParseQuery(location[(location.IndexOf('?') + 1)..]);
        Assert.AreEqual(path + query, parameters["gourl"].ToString());
        Assert.HasCount(1, parameters);
        Assert.AreEqual("AdminDemosDynamic", fw.route.controller);
    }

    [TestMethod]
    public void Dispatch_ConfigAccessRuleAlsoPreservesOriginalUrl()
    {
        using var fw = createFw();
        fw.config()["access_levels"] = new FwDict { ["/AdminDemosDynamic"] = Users.ACL_MANAGER };

        fw.dispatch();

        Assert.AreEqual("/Login?gourl=%2fAdmin%2fDemosDynamic", fw.response.Headers.Location.ToString());
    }

    [TestMethod]
    [DataRow("POST", "", "", "")]
    [DataRow("GET", "application/json", "", "")]
    [DataRow("GET", "", "XMLHttpRequest", "")]
    [DataRow("GET", "", "", "POST")]
    public void Dispatch_NonPageRequestsKeepExistingRedirect(string method, string accept, string ajax, string methodOverride)
    {
        using var fw = createFw();
        fw.request.Method = method;
        fw.request.Headers.Accept = accept;
        fw.request.Headers.XRequestedWith = ajax;
        if (methodOverride.Length > 0)
            fw.FORM["_method"] = methodOverride;

        fw.dispatch();

        Assert.AreEqual(302, fw.response.StatusCode);
        Assert.AreEqual("/", fw.response.Headers.Location.ToString());
    }

    [TestMethod]
    public void Dispatch_LoggedInWithoutPermissionKeepsAccessError()
    {
        using var fw = createFw();
        fw.Session("user_id", "9");
        fw.Session("access_level", Users.ACL_MEMBER.toStr());
        fw.request.Headers.Accept = "application/json";

        fw.dispatch();

        Assert.AreEqual(403, fw.response.StatusCode);
        Assert.IsFalse(fw.response.Headers.ContainsKey("Location"));
        fw.response.Body.Position = 0;
        var json = Utils.jsonDecode(new StreamReader(fw.response.Body).ReadToEnd()) as FwDict;
        Assert.IsNotNull(json);
        Assert.IsFalse(json["success"].toBool());
    }

    private static FW createFw(string pathBase = "", string path = "/Admin/DemosDynamic", string query = "")
    {
        var context = TestHelpers.CreateHttpContext("app.example.test");
        context.Request.Method = "GET";
        context.Request.PathBase = pathBase;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        context.Response.Body = new MemoryStream();
        var settings = new Dictionary<string, string?>
        {
            ["appSettings:ROOT_DOMAIN"] = "https://app.example.test",
            ["appSettings:UNLOGGED_DEFAULT_URL"] = "/",
            ["appSettings:route_prefixes:/Admin"] = "True",
        };
        return new FW(context, new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }
}
