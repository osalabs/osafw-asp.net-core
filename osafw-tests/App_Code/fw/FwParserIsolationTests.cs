using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace osafw.Tests;

[TestClass]
[DoNotParallelize]
public class FwParserIsolationTests
{
    private sealed class StubSettings : Settings
    {
        public override DBRow oneByIcode(string icode) => [];
    }

    private readonly IConfiguration configuration = new ConfigurationBuilder().Build();
    private string templateRoot = "";
    private string host = "";
    private IDisposable settingsScope = null!;

    [TestInitialize]
    public void initialize()
    {
        settingsScope = FwConfig.beginScope();
        host = $"parser-{Guid.NewGuid():N}.test";
        templateRoot = Path.Combine(Path.GetTempPath(), $"fw-parser-{Guid.NewGuid():N}");
        Directory.CreateDirectory(templateRoot);
        File.WriteAllText(Path.Combine(templateRoot, "full.html"), "<html>FULL|<~title>|<~_layout></html>");
        File.WriteAllText(Path.Combine(templateRoot, "partial.html"), "<script>PARTIAL|<~title>|<~_layout></script>");
        File.WriteAllText(Path.Combine(templateRoot, "modal.html"), "MODAL|<~title>|<~_layout>");
        File.WriteAllText(Path.Combine(templateRoot, "custom.html"), "CUSTOM|<~title>|<~_layout>");

        FwConfig.init(null, configuration, host);
        var settings = FwConfig.GetCurrentSettings();
        settings["template"] = templateRoot;
        settings["log"] = "";
        settings["log_level"] = (int)LogLevel.OFF;
        settings["IS_DEV"] = false;
        settings["db"] = new FwDict();
        settings["access_levels"] = new FwDict();
        settings["ROOT_URL"] = "";
        settings["UNLOGGED_DEFAULT_URL"] = "/";
        settings["PAGE_LAYOUT"] = "/full.html";
        settings["PAGE_LAYOUT_PUBLIC"] = "/full.html";
        settings["PAGE_LAYOUT_PJAX"] = "/partial.html";
        settings["PAGE_LAYOUT_MODAL"] = "/modal.html";
    }

    [TestCleanup]
    public void cleanup()
    {
        settingsScope.Dispose();
        Directory.Delete(templateRoot, true);
    }

    private FW createRequest(string path = "/", string query = "", string requestedWith = "", string format = "html")
    {
        var context = new DefaultHttpContext { Session = new TestHelpers.FakeSession() };
        context.Request.Host = new HostString(host);
        context.Request.Method = "GET";
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        context.Request.Headers.Accept = "text/html";
        if (requestedWith.Length > 0)
            context.Request.Headers.XRequestedWith = requestedWith;
        context.Response.Body = new MemoryStream();

        var fw = new FW(context, configuration, name => new RejectingDb(name));
        TestHelpers.RegisterModel(fw, (Settings)new StubSettings());
        fw.route = new FwRoute
        {
            controller = "Home", controller_path = "/Home", action = "Index", method = "GET", format = format,
        };
        return fw;
    }

    private static string responseBody(FW fw)
    {
        return Encoding.UTF8.GetString(((MemoryStream)fw.response.Body).ToArray());
    }

    private string render(FwDict data, string query = "", string requestedWith = "", string format = "html")
    {
        using var fw = createRequest(query: query, requestedWith: requestedWith, format: format);
        fw.parser(data);
        return responseBody(fw);
    }

    [TestMethod]
    [DataRow("header")]
    [DataRow("route")]
    [DataRow("query")]
    public void shared_data_renders_full_page_after_partial_request(string source)
    {
        var cacheKey = $"parser-page-{Guid.NewGuid():N}";
        var original = new FwDict { ["title"] = "Login" };
        FwCache.setValue(cacheKey, original);
        try
        {
            FwDict cached() => (FwDict)FwCache.getValue(cacheKey)!;
            Assert.AreEqual("<html>FULL|Login|</html>", render(cached()));
            Assert.AreEqual("<script>PARTIAL|Login|pjax</script>", render(cached(),
                query: source == "query" ? "?_layout=pjax" : "",
                requestedWith: source == "header" ? "XMLHttpRequest" : "",
                format: source == "route" ? "pjax" : "html"));
            // Each render has a separate FW/context/session, while the cached dictionary is identical.
            Assert.AreEqual("<html>FULL|Login|</html>", render(cached()));
            Assert.AreEqual("<html>FULL|Login|</html>", render(cached()));
            Assert.AreSame(original, cached());
            Assert.IsFalse(original.ContainsKey("_layout"));
            Assert.AreEqual(1, original.Count);
        }
        finally
        {
            FwCache.remove(cacheKey);
        }
    }

    [TestMethod]
    public void layout_precedence_is_preserved_without_changing_controller_data()
    {
        var data = new FwDict { ["title"] = "Login", ["_layout"] = "/custom.html" };
        Assert.AreEqual("CUSTOM|Login|/custom.html", render(data));
        Assert.AreEqual("<script>PARTIAL|Login|pjax</script>", render(data, requestedWith: "XMLHttpRequest"));
        Assert.AreEqual("MODAL|Login|modal", render(data, query: "?_layout=modal", requestedWith: "XMLHttpRequest"));
        Assert.AreEqual("CUSTOM|Login|/custom.html", render(data));
        Assert.AreEqual("/custom.html", data["_layout"]);
    }

    [TestMethod]
    public void explicit_layout_overload_does_not_change_shared_data()
    {
        var data = new FwDict { ["title"] = "Login" };
        using (var fw = createRequest())
        {
            fw.parser("/home/index", "/custom.html", data);
            Assert.AreEqual("CUSTOM|Login|/custom.html", responseBody(fw));
        }
        Assert.IsFalse(data.ContainsKey("_layout"));
        Assert.AreEqual("<html>FULL|Login|</html>", render(data));

        using var partial = createRequest(requestedWith: "XMLHttpRequest");
        partial.parser("/home/index", "/custom.html", data);
        Assert.AreEqual("<script>PARTIAL|Login|pjax</script>", responseBody(partial));
        Assert.IsFalse(data.ContainsKey("_layout"));
    }

    [TestMethod]
    public void form_layout_is_request_local_and_query_still_takes_precedence()
    {
        var data = new FwDict { ["title"] = "Login" };
        using (var fw = createRequest())
        {
            fw.request.ContentType = "application/x-www-form-urlencoded";
            fw.request.Form = new FormCollection(new() { ["_layout"] = "modal" });
            fw.parser("/home/index", data);
            Assert.AreEqual("MODAL|Login|modal", responseBody(fw));
        }
        using (var fw = createRequest(query: "?_layout=pjax"))
        {
            fw.request.ContentType = "application/x-www-form-urlencoded";
            fw.request.Form = new FormCollection(new() { ["_layout"] = "modal" });
            fw.parser("/home/index", data);
            Assert.AreEqual("<script>PARTIAL|Login|pjax</script>", responseBody(fw));
        }
        Assert.IsFalse(data.ContainsKey("_layout"));
        Assert.AreEqual("<html>FULL|Login|</html>", render(data));
    }

    [TestMethod]
    public void json_flag_survives_repeated_requests_and_preserves_comparer()
    {
        var data = new FwDict(StringComparer.OrdinalIgnoreCase) { ["_JSON"] = true, ["title"] = "Login" };
        for (int i = 0; i < 2; i++)
        {
            using var response = JsonDocument.Parse(render(data, format: "json"));
            Assert.AreEqual("Login", response.RootElement.GetProperty("title").GetString());
            Assert.IsFalse(response.RootElement.TryGetProperty("_JSON", out _));
        }
        Assert.AreEqual(true, data["_json"]);
        Assert.AreEqual(2, data.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void validation_details_do_not_leak_into_shared_data(bool is_existing_error)
    {
        var data = new FwDict { ["_json"] = true };
        var error = new FwDict { ["message"] = "Existing message" };
        if (is_existing_error)
            data["error"] = error;

        using (var fw = createRequest(format: "json"))
        {
            fw.FormIssues.Add(new FwDict { ["severity"] = FW.ISSUE_ERROR, ["field"] = "field", ["message"] = "Required" });
            fw.parser(data);
            using var response = JsonDocument.Parse(responseBody(fw));
            Assert.AreEqual("Required", response.RootElement.GetProperty("error").GetProperty("details").GetProperty("field").GetString());
        }
        Assert.IsFalse(data.ContainsKey("form_issues"));
        Assert.IsFalse(error.ContainsKey("details"));
        Assert.AreEqual(is_existing_error, data.ContainsKey("error"));
        if (is_existing_error)
            Assert.AreSame(error, data["error"]);

        using var nextResponse = JsonDocument.Parse(render(data, format: "json"));
        Assert.IsFalse(nextResponse.RootElement.TryGetProperty("form_issues", out _));
        if (is_existing_error)
            Assert.IsFalse(nextResponse.RootElement.GetProperty("error").TryGetProperty("details", out _));
        else
            Assert.IsFalse(nextResponse.RootElement.TryGetProperty("error", out _));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void controller_validation_details_are_preserved(bool is_ignore_case)
    {
        var details = new FwDict { ["field"] = "Controller error" };
        var detailsKey = is_ignore_case ? "DETAILS" : "details";
        var error = new FwDict(is_ignore_case ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal) { [detailsKey] = details };
        var data = new FwDict { ["_json"] = true, ["error"] = error };
        using var fw = createRequest(format: "json");
        fw.FormIssues.Add(new FwDict { ["severity"] = FW.ISSUE_ERROR, ["field"] = "field", ["message"] = "Request error" });
        fw.parser(data);
        using var response = JsonDocument.Parse(responseBody(fw));
        Assert.AreEqual("Controller error", response.RootElement.GetProperty("error").GetProperty(detailsKey).GetProperty("field").GetString());
        Assert.AreSame(details, error["details"]);
    }

    [TestMethod]
    [DataRow("?_layout=pjax", "modal", "<script>PARTIAL|Login|pjax</script>")]
    [DataRow("?_layout=", "modal", "MODAL|Login|modal")]
    [DataRow("", "modal", "MODAL|Login|modal")]
    public void request_layout_overrides_explicit_layout(string query, string formLayout, string expected)
    {
        var data = new FwDict { ["title"] = "Login", ["_layout"] = "/full.html" };
        using var fw = createRequest(query: query, requestedWith: "XMLHttpRequest");
        fw.request.Method = "POST";
        fw.request.ContentType = "application/x-www-form-urlencoded";
        fw.request.Form = new FormCollection(new() { ["_layout"] = formLayout });
        fw.parser("/home/index", "/custom.html", data);
        Assert.AreEqual(expected, responseBody(fw));
        Assert.AreEqual("/full.html", data["_layout"]);
    }

    [TestMethod]
    public void explicit_layout_replaces_controller_layout_and_empty_layout_stays_empty()
    {
        var data = new FwDict { ["title"] = "Login", ["_layout"] = "/full.html" };
        using (var fw = createRequest())
        {
            fw.parser("/home/index", "/custom.html", data);
            Assert.AreEqual("CUSTOM|Login|/custom.html", responseBody(fw));
        }

        using (var fw = createRequest())
        {
            fw.parser("/home/index", "", data);
            Assert.AreEqual("", responseBody(fw));
        }

        Assert.AreEqual("/full.html", data["_layout"]);
    }

    [TestMethod]
    public void json_payload_and_controller_form_issues_are_preserved()
    {
        var rows = new FwList { new FwDict { ["title"] = "Shared row" } };
        var data = new FwDict { ["_json"] = rows };
        for (int i = 0; i < 2; i++)
        {
            using var fw = createRequest();
            fw.request.Headers.Accept = "application/json";
            fw.parser(data);
            using var response = JsonDocument.Parse(responseBody(fw));
            Assert.AreEqual("Shared row", response.RootElement[0].GetProperty("title").GetString());
        }

        Assert.AreSame(rows, data["_json"]);
        var issues = new FwList { new FwDict { ["message"] = "Controller warning" } };
        data = new FwDict { ["_json"] = true, ["form_issues"] = issues };
        using var request = createRequest(format: "json");
        request.FormIssues.Add(new FwDict { ["severity"] = FW.ISSUE_WARNING, ["message"] = "Request warning" });
        request.parser(data);
        using var result = JsonDocument.Parse(responseBody(request));
        Assert.AreEqual("Controller warning", result.RootElement.GetProperty("form_issues")[0].GetProperty("message").GetString());
        Assert.IsFalse(result.RootElement.TryGetProperty("error", out _));
        Assert.AreSame(issues, data["form_issues"]);
    }

    [TestMethod]
    [DataRow("html", LogLevel.DEBUG, "/full.html")]
    [DataRow("pjax", LogLevel.DEBUG, "/partial.html")]
    [DataRow("json", LogLevel.DEBUG, "none")]
    [DataRow("html", LogLevel.INFO, "/full.html")]
    [DataRow("json", LogLevel.INFO, "none")]
    public void rendering_diagnostics_are_gated_and_exclude_page_bodies(string format, LogLevel level, string layout)
    {
        var logPath = Path.Combine(templateRoot, "render.log");
        string body;
        using (var fw = createRequest(format: format))
        {
            fw.flogger.log_level = level;
            fw.flogger.log_file = logPath;
            fw.parser(new FwDict { ["title"] = "Private page marker", ["_json"] = true });
            body = responseBody(fw);
        }

        var log = File.Exists(logPath) ? File.ReadAllText(logPath) : "";
        if (level >= LogLevel.DEBUG)
        {
            StringAssert.Contains(log, $"Rendered page: format={format}, layout={layout}, chars={body.Length}");
        }
        else
        {
            Assert.IsFalse(log.Contains("Rendered page:"));
        }

        Assert.IsFalse(log.Contains("Private page marker"));
    }

    [TestMethod]
    public void manual_unauthorized_navigation_redirects_and_home_refresh_stays_full()
    {
        var previousHome = FwCache.getValue("home_page");
        var sharedHome = new FwDict { ["title"] = "Home" };
        FwCache.setValue("home_page", sharedHome);
        try
        {
            using (var home = createRequest())
            {
                home.dispatch();
                StringAssert.StartsWith(responseBody(home), "<html>FULL");
            }

            using (var partial = createRequest(requestedWith: "XMLHttpRequest"))
            {
                partial.dispatch();
                StringAssert.StartsWith(responseBody(partial), "<script>PARTIAL");
            }

            using (var protectedRequest = createRequest("/Main"))
            {
                protectedRequest.dispatch();
                Assert.AreEqual(302, protectedRequest.response.StatusCode);
                Assert.AreEqual("/Login?gourl=%2fMain", protectedRequest.response.Headers.Location.ToString());
                Assert.AreEqual("", responseBody(protectedRequest));
            }

            using (var login = createRequest("/Login", "?gourl=%2fMain"))
            {
                login.dispatch();
                Assert.AreEqual(200, login.response.StatusCode);
                StringAssert.StartsWith(responseBody(login), "<html>FULL");
            }

            for (int i = 0; i < 2; i++)
            {
                using var home = createRequest();
                home.dispatch();
                Assert.AreEqual(200, home.response.StatusCode);
                StringAssert.StartsWith(responseBody(home), "<html>FULL");
            }

            Assert.AreSame(sharedHome, FwCache.getValue("home_page"));
            Assert.IsFalse(sharedHome.ContainsKey("_layout"));
        }
        finally
        {
            if (previousHome == null)
            {
                FwCache.remove("home_page");
            }
            else
            {
                FwCache.setValue("home_page", previousHome);
            }
        }
    }
}
