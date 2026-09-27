using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace osafw.Tests;

[TestClass]
public class SecurityGroup9ATests
{
    private sealed class DashboardUsers : Users
    {
        public override bool isReadOnly(int id = -1) => false;

        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    private sealed class DashboardSettings : Settings
    {
        public override DBRow oneByIcode(string icode)
        {
            return new DBRow(DB.h("id", 1, "icode", icode, "ivalue", "0"));
        }
    }

    private sealed record ValueCall(string Table, FwDict Where);

    private sealed record QueryCall(string Sql, FwDict Params);

    private sealed class DashboardDb : DB
    {
        public List<ValueCall> ValueCalls { get; } = [];
        public List<QueryCall> ValuepCalls { get; } = [];
        public List<QueryCall> ArraypCalls { get; } = [];

        public DashboardDb() : base("", DB.DBTYPE_SQLSRV) { }

        public override object? value(string table, FwDict where, string field_name = "", string order_by = "")
        {
            ValueCalls.Add(new ValueCall(table, new FwDict(where)));
            return 1;
        }

        public override object? valuep(string sql, FwDict? @params = null)
        {
            ValuepCalls.Add(new QueryCall(sql, new FwDict(@params)));
            return 1;
        }

        public override DBList arrayp(string sql, FwDict? @params = null)
        {
            ArraypCalls.Add(new QueryCall(sql, new FwDict(@params)));
            if (sql.Contains("access_level", StringComparison.OrdinalIgnoreCase))
            {
                return new DBList
                {
                    new DBRow(new FwDict { ["access_level"] = Users.ACL_MEMBER, ["ivalue"] = 1 })
                };
            }
            if (sql.Contains(" as Event", StringComparison.OrdinalIgnoreCase))
            {
                return new DBList
                {
                    new DBRow(new FwDict { ["On"] = "2026-06-01 12:00:00", ["Event"] = "User login" })
                };
            }

            return new DBList
            {
                new DBRow(new FwDict { ["idate"] = "2026-06-01", ["ivalue"] = 1 })
            };
        }
    }

    [TestMethod]
    public void Auth_MixedCaseActionUsesControllerAuthorizationAfterCsrfGate()
    {
        var fw = TestHelpers.CreateFw();
        fw.Session("access_level", Users.ACL_MEMBER.toStr());
        var route = new FwRoute
        {
            controller = "adminusers",
            action = "sAvE",
            method = "GET"
        };

        var result = fw._auth(route, is_die: false);

        Assert.AreEqual(1, result);
        Assert.AreEqual(FW.ACTION_SAVE, route.action);
    }

    [TestMethod]
    public void Auth_MixedCaseMutatingActionStillRequiresValidXssToken()
    {
        var fw = TestHelpers.CreateFw();
        fw.Session("access_level", Users.ACL_SITEADMIN.toStr());
        fw.Session("XSS", "expected-token");
        fw.FORM["XSS"] = "wrong-token";
        var route = new FwRoute
        {
            controller = "AdminUsers",
            action = "sAvE",
            method = "GET"
        };

        Assert.ThrowsExactly<AuthException>(() => fw._auth(route));
    }

    [TestMethod]
    public void Auth_ConfiguredNoXssPrefixSkipsXssCheck()
    {
        var fw = TestHelpers.CreateFw();
        fw.config()["no_xss_prefixes"] = new FwDict
        {
            ["v1"] = true
        };
        fw.Session("access_level", Users.ACL_SITEADMIN.toStr());
        fw.Session("XSS", "expected-token");
        fw.FORM["XSS"] = "wrong-token";
        var route = new FwRoute
        {
            prefix = "v1",
            controller = "Api",
            action = FW.ACTION_SAVE,
            method = "POST"
        };

        var result = fw._auth(route, is_die: false);

        Assert.AreEqual(1, result);
    }

    [TestMethod]
    public void GetRoute_StandardParenthesizedActionIsCaseInsensitive()
    {
        var fw = TestHelpers.CreateFw();
        fw.request.Method = "GET";

        var route = fw.getRoute("/controller/(sAvE)");

        Assert.AreEqual(FW.ACTION_SAVE, route.action);
    }

    [TestMethod]
    public void Permissions_StandardActionMappingIsCaseInsensitive()
    {
        var permissions = new Permissions();

        Assert.AreEqual(Permissions.PERMISSION_ADD, permissions.mapActionToPermission("showform", "NEW"));
        Assert.AreEqual(Permissions.PERMISSION_EDIT, permissions.mapActionToPermission("savemulti"));
    }

    [TestMethod]
    public void MainDashboard_MemberQueriesAreScopedToCurrentUser()
    {
        var db = new DashboardDb();
        var fw = TestHelpers.CreateFw();
        fw.db = db;
        fw.Session("user_id", "9");
        fw.Session("access_level", Users.ACL_MEMBER.toStr());
        fw.Session("date_format", DateUtils.DATE_FORMAT_DMY.toStr());
        fw.Session("time_format", DateUtils.TIME_FORMAT_24.toStr());
        fw.Session("timezone", DateUtils.TZ_UTC);
        var users = new DashboardUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        var settings = new DashboardSettings();
        settings.init(fw);
        TestHelpers.RegisterModel(fw, (Settings)settings);
        var controller = new MainController();
        controller.init(fw);

        var ps = controller.IndexAction();

        var panes = ps["panes"] as FwDict ?? throw new AssertFailedException("Expected dashboard panes");
        Assert.IsTrue(panes.ContainsKey("plate1"));
        Assert.IsTrue(panes.ContainsKey("plate2"));
        Assert.IsTrue(panes.ContainsKey("plate3"));
        Assert.IsTrue(panes.ContainsKey("plate4"));
        Assert.IsTrue(panes.ContainsKey("barchart"));
        Assert.IsTrue(panes.ContainsKey("piechart"));
        Assert.IsTrue(panes.ContainsKey("tabledata"));
        Assert.IsTrue(panes.ContainsKey("linechart"));
        Assert.IsTrue(panes.ContainsKey("progress"));

        Assert.IsTrue(db.ValueCalls.Where(call => call.Table == "spages").All(call => call.Where["add_users_id"].toInt() == 9));
        Assert.IsTrue(db.ValueCalls.Where(call => call.Table == "att").All(call => call.Where["add_users_id"].toInt() == 9));
        Assert.IsTrue(db.ValueCalls.Where(call => call.Table == "users").All(call => call.Where["id"].toInt() == 9));
        Assert.IsTrue(db.ValuepCalls.All(call => hasUsersIdScope(call.Params)));
        Assert.IsTrue(db.ArraypCalls
            .Where(call => call.Sql.Contains("activity_logs", StringComparison.OrdinalIgnoreCase))
            .All(call => hasUsersIdScope(call.Params)));

        var pagesPane = panes["plate1"] as FwDict ?? [];
        Assert.AreEqual("/Admin/Spages", pagesPane["url"].toStr());
    }

    [TestMethod]
    public void DbLogging_LogsParameterNamesWithoutSensitiveValues()
    {
        var db = new DB("", DB.DBTYPE_SQLSRV);
        var messages = new List<string>();
        db.setLogger((_, args) => messages.Add(string.Join("", args.Select(arg => FwLogger.dumper(arg)))));
        var sql = "update users set pwd=@pwd where pwd_reset=@reset";
        var parameters = new FwDict
        {
            ["@pwd"] = "plaintext-password",
            ["@reset"] = "reset-code"
        };

        db.logQueryAndParams(sql, parameters);

        var log = string.Join("\n", messages);
        Assert.IsTrue(log.Contains("@pwd"));
        Assert.IsTrue(log.Contains("@reset"));
        Assert.IsFalse(log.Contains("plaintext-password"));
        Assert.IsFalse(log.Contains("reset-code"));
    }

    [TestMethod]
    public void DbLogging_LogPiiCanExposeParameterValuesForLocalDebugging()
    {
        var db = new DB("", DB.DBTYPE_SQLSRV)
        {
            is_log_pii = true
        };
        var messages = new List<string>();
        db.setLogger((_, args) => messages.Add(string.Join("", args.Select(arg => FwLogger.dumper(arg)))));
        var sql = "select * from users where pwd=@pwd";
        var parameters = new FwDict
        {
            ["@pwd"] = "plaintext-password"
        };

        db.logQueryAndParams(sql, parameters);

        var log = string.Join("\n", messages);
        Assert.IsTrue(log.Contains("plaintext-password"));
        Assert.IsTrue(log.Contains("{ plaintext-password }"));
        Assert.IsFalse(log.Contains("@pwd=plaintext-password"));
    }

    [TestMethod]
    public void DbLogging_LogPiiUnwrapsHelperParameterMetadata()
    {
        var db = new DB("", DB.DBTYPE_SQLSRV)
        {
            is_log_pii = true
        };
        var messages = new List<string>();
        db.setLogger((_, args) => messages.Add(string.Join("", args.Select(arg => FwLogger.dumper(arg)))));
        var sql = "select * from att_categories where icode=@icode and prio=@prio";
        var parameters = new FwDict
        {
            ["icode"] = DB.paramValue("icode", "varchar", "general"),
            ["prio"] = DB.paramValue("prio", "int", 10)
        };

        db.logQueryAndParams(sql, parameters);

        var log = string.Join("\n", messages);
        Assert.IsTrue(log.Contains("@icode => general"));
        Assert.IsTrue(log.Contains("@prio => 10"));
        Assert.IsFalse(log.Contains("FieldName"));
        Assert.IsFalse(log.Contains("FieldType"));
        Assert.IsFalse(log.Contains("Value"));
    }

    [TestMethod]
    public void SettingsRequestsNeverLogSubmittedValuesWhenPiiLoggingIsEnabled()
    {
        var fw = TestHelpers.CreateFw(new Dictionary<string, string?>
        {
            ["appSettings:log_pii"] = "true",
        });
        fw.route.controller = "AdminSettings";
        fw.FORM["item"] = new FwDict
        {
            ["icode"] = "mail.password",
            ["ivalue"] = "plaintext-secret",
        };

        var logged = fw.requestFormForLog();

        Assert.IsInstanceOfType<StrList>(logged);
        var text = FwLogger.dumper(logged);
        Assert.IsFalse(text.Contains("plaintext-secret", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("mail.password", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SensitiveSettingsMarkerRedactsCustomConsumerRequests()
    {
        var fw = TestHelpers.CreateFw(new Dictionary<string, string?>
        {
            ["appSettings:log_pii"] = "true",
        });
        fw.route.controller = "CustomSettings";
        fw.context.Items["OSAFW.SensitiveSettings"] = true;
        fw.FORM["ivalue"] = "plaintext-secret";

        var logged = fw.requestFormForLog();

        Assert.IsInstanceOfType<StrList>(logged);
        Assert.IsFalse(FwLogger.dumper(logged).Contains("plaintext-secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SensitiveSettingsMarkerRedactsDetailedErrorResponse()
    {
        var fw = TestHelpers.CreateFw(new Dictionary<string, string?>
        {
            ["appSettings:IS_DEV"] = "true",
            ["appSettings:log_pii"] = "true",
        });
        fw.context.Items["OSAFW.SensitiveSettings"] = true;
        fw.request.Headers.Accept = "application/json";
        fw.response.Body = new MemoryStream();
        fw.FORM["ivalue"] = "plaintext-secret";

        fw.errMsg("Settings failure", new ApplicationException("Settings failure"));

        fw.response.Body.Position = 0;
        var body = new StreamReader(fw.response.Body).ReadToEnd();
        Assert.IsFalse(body.Contains("plaintext-secret", StringComparison.Ordinal));
        Assert.IsTrue(body.Contains("DUMP_FORM", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PiiLoggingPreservesOrdinaryFormValuesButRedactsCredentialFields()
    {
        var fw = TestHelpers.CreateFw(new Dictionary<string, string?>
        {
            ["appSettings:log_pii"] = "true",
        });
        fw.route.controller = "Contact";
        fw.FORM["email"] = "person@example.test";
        fw.FORM["password"] = "plaintext-secret";

        var logged = fw.requestFormForLog() as FwDict;

        Assert.IsNotNull(logged);
        Assert.AreEqual("person@example.test", logged["email"]);
        Assert.AreEqual("[REDACTED]", logged["password"]);
    }

    [TestMethod]
    public void Appsettings_SentryDefaultsDoNotSendPiiOrRequestBodies()
    {
        var appsettingsPath = findRepoFile("osafw-app", "appsettings.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(appsettingsPath));
        var sentry = doc.RootElement.GetProperty("Sentry");
        var appSettings = doc.RootElement.GetProperty("appSettings");
        var developmentPath = findRepoFile("osafw-app", "appsettings.Development.json");
        using var developmentDoc = JsonDocument.Parse(File.ReadAllText(developmentPath));
        var developmentSettings = developmentDoc.RootElement.GetProperty("appSettings");

        Assert.IsFalse(sentry.GetProperty("SendDefaultPii").GetBoolean());
        Assert.AreEqual("None", sentry.GetProperty("MaxRequestBodySize").GetString());
        Assert.IsFalse(appSettings.TryGetProperty("log_pii", out _));
        Assert.IsTrue(developmentSettings.GetProperty("log_pii").GetBoolean());
        Assert.IsFalse(appSettings.TryGetProperty("access_levels", out _));
        Assert.IsFalse(appSettings.TryGetProperty("accesss_levels", out _));
    }

    private static bool hasUsersIdScope(FwDict parameters)
    {
        return parameters.ContainsKey("users_id") || parameters.ContainsKey("@users_id");
    }

    private static string findRepoFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not find repo file", Path.Combine(relativeParts));
    }
}
