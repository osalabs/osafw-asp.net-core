using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Collections.Generic;

namespace osafw.Tests;

[TestClass]
public class DiagnosticSecurityTests
{
    private sealed class DiagnosticUsers : Users
    {
        internal bool IsReadOnly { get; set; }
        public override bool isReadOnly(int id = -1) => IsReadOnly;
        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
        public override DBRow one(int id) => new(DB.h("id", id, "status", STATUS_ACTIVE, "access_level", ACL_SITEADMIN, "pwd", hashPwd("Console regression12!")));
    }

    [TestMethod]
    public void SqlConsoleRejectsQueryGetAndWrongPasswordBeforeDatabaseAccess()
    {
        using var scope = new FwTestScope(_ => new RejectingDb(), context: TestHelpers.CreateHttpContext());
        var fw = scope.Fw;
        fw.Session("user_id", "1");
        fw.Session("access_level", Users.ACL_SITEADMIN.toStr());
        fw.Session("XSS", "token");
        fw.FORM["XSS"] = "token";
        fw.FORM["sql"] = "select 1";
        var users = new DiagnosticUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        var controller = new AdminDBController();
        controller.init(fw);
        fw.route.method = "GET";
        fw.request.Method = "GET";
        Assert.ThrowsExactly<AuthException>(() => controller.IndexAction());
        fw.route.method = "POST";
        fw.request.Method = "POST";
        Assert.ThrowsExactly<AuthException>(() => controller.IndexAction());
        users.IsReadOnly = true;
        fw.FORM["pwd"] = "Console regression12!";
        Assert.ThrowsExactly<AuthException>(() => controller.IndexAction());
        users.IsReadOnly = false;
        fw.FORM["pwd"] = "wrong-password";
        Assert.ThrowsExactly<AuthException>(() => controller.IndexAction());
    }

    [TestMethod]
    public void SqlTableLinkPrefillsFormWithoutExecutingOnGet()
    {
        using var scope = new FwTestScope(_ => new RejectingDb(), context: TestHelpers.CreateHttpContext());
        var fw = scope.Fw;
        fw.Session("user_id", "1");
        fw.Session("access_level", Users.ACL_SITEADMIN.toStr());
        fw.config()["template"] = TestHelpers.TemplateRoot();
        fw.config()["is_lang_update"] = false;
        var link = fw.parsePage("/admin/db/index", "select_link.html", DB.h("value", "sample_table", "db", "main"));
        StringAssert.Contains(link, "?prefill_sql=");
        Assert.IsFalse(link.Contains("?sql=", StringComparison.Ordinal));
        fw.FORM["prefill_sql"] = "select top 50 * from [sample_table]";
        fw.FORM["db"] = "";
        fw.route.method = "GET";
        fw.request.Method = "GET";
        var users = new DiagnosticUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        var controller = new AdminDBController();
        controller.init(fw);
        var state = controller.IndexAction();
        Assert.AreEqual("select top 50 * from [sample_table]", state["sql"]);
        Assert.AreEqual(0, state["sql_ctr"].toInt());
    }

    [TestMethod]
    public void AssistantDebugLogsRedactUserArgumentsUnlessPiiLoggingIsEnabled()
    {
        var path = Path.Combine(Path.GetTempPath(), "assistant-diagnostics-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            using var scope = new FwTestScope(_ => new RejectingDb(), new Dictionary<string,string?>
            {
                ["appSettings:log"] = path, ["appSettings:log_level"] = "DEBUG", ["appSettings:log_pii"] = "false"
            }, TestHelpers.CreateHttpContext());
            var fw = scope.Fw;
            var runtime = new AssistantToolRuntime(fw, 0, 0, 0);
            runtime.LogToolCall("search", new { query = "private-query-marker" });
            runtime.LogToolResult("search", "private-result-marker");
            var content = readLog(path);
            Assert.IsFalse(content.Contains("private-query-marker", StringComparison.Ordinal));
            Assert.IsFalse(content.Contains("private-result-marker", StringComparison.Ordinal));
            fw.config()["log_pii"] = true;
            runtime.LogToolCall("search", new { query = "explicit-debug-marker" });
            StringAssert.Contains(readLog(path), "explicit-debug-marker");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string readLog(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(file);
        return reader.ReadToEnd();
    }

#if isSQLite
    [TestMethod]
    public void SqlConsoleUnlocksForSessionAndKeepsMutationGuards()
    {
        using var database = new SqliteFile();
        using var scope = new FwTestScope(database.CreateDb, context: TestHelpers.CreateHttpContext());
        var fw = scope.Fw;
        fw.Session("user_id", "1");
        fw.Session("access_level", Users.ACL_SITEADMIN.toStr());
        fw.Session("XSS", "token");
        fw.FORM["XSS"] = "token";
        fw.FORM["sql"] = "select 42 as answer";
        fw.FORM["db"] = "";
        fw.route.method = "POST";
        fw.request.Method = "POST";
        fw.FORM["pwd"] = "Console regression12!";
        var users = new DiagnosticUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        var controller = new AdminDBController();
        controller.init(fw);
        var ps = controller.IndexAction();
        Assert.AreEqual(1, ps["sql_ctr"].toInt());
        var rows = (FwList)ps["rows"]!;
        var fields = (FwList)rows[0]["fields"]!;
        Assert.AreEqual(42, fields[0]["value"].toInt());

        fw.FORM.Remove("pwd");
        Assert.AreEqual(1, controller.IndexAction()["sql_ctr"].toInt());
        users.IsReadOnly = true;
        Assert.ThrowsExactly<AuthException>(() => controller.IndexAction());
        users.IsReadOnly = false;
        fw.route.method = "GET";
        fw.request.Method = "GET";
        Assert.ThrowsExactly<AuthException>(() => controller.IndexAction());
        fw.route.method = "POST";
        fw.request.Method = "POST";
        fw.FORM["XSS"] = "wrong-token";
        Assert.ThrowsExactly<AuthException>(() => controller.IndexAction());
        fw.FORM["XSS"] = "token";
        fw.SessionBool("admindb_pwd_checked", false);
        Assert.ThrowsExactly<AuthException>(() => controller.IndexAction());
    }
#endif

    [TestMethod]
    public void DeveloperLogDumpEscapesActiveHtml()
    {
        var path = Path.Combine(Path.GetTempPath(), "log-render-" + Guid.NewGuid().ToString("N") + ".log");
        const string attack = "<script>test-marker</script>";
        File.WriteAllText(path, attack);
        try
        {
            using var scope = new FwTestScope(_ => new RejectingDb(), context: TestHelpers.CreateHttpContext());
            var fw = scope.Fw;
            fw.config()["log"] = path;
            fw.response.Body = new MemoryStream();
            fw.FORM["seek"] = new FileInfo(path).Length.toStr();
            var users = new DiagnosticUsers();
            users.init(fw);
            TestHelpers.RegisterModel(fw, (Users)users);
            var controller = new DevManageController();
            controller.init(fw);
            controller.DumpLogAction();
            fw.response.Body.Position = 0;
            var html = new StreamReader(fw.response.Body).ReadToEnd();
            StringAssert.Contains(html, "&lt;script&gt;");
            Assert.IsFalse(html.Contains(attack, StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
