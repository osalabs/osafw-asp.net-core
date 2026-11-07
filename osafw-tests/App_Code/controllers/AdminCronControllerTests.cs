using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace osafw.Tests;

[TestClass]
public class AdminCronControllerTests
{
    private sealed class UtcDb : RejectingDb
    {
        public UtcDb() => conf["timezone"] = "UTC";
    }

    private sealed class TestUsers : Users
    {
        public override bool isReadOnly(int id = -1) => false;
        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    private static (FW fw, AdminCronController controller) Setup(string method, int access, string token)
    {
        var fw = new FW(TestHelpers.CreateHttpContext(), new ConfigurationBuilder().Build(), _ => new UtcDb());
        fw.config()["template"] = TestHelpers.TemplateRoot();
        fw.Session("access_level", access.ToString());
        fw.G["timezone"] = "UTC";
        fw.Session("XSS", "valid");
        fw.route.method = method;
        fw.FORM["XSS"] = token;
        fw.FORM["item"] = new FwDict
        {
            ["cron"] = "0 7 * * *",
            ["timezone"] = "UTC"
        };
        fw.response.Body = new MemoryStream();
        var users = new TestUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        var controller = new AdminCronController();
        controller.init(fw);
        return (fw, controller);
    }

    [TestMethod]
    [DataRow("GET", Users.ACL_SITEADMIN, "valid")]
    [DataRow("POST", Users.ACL_SITEADMIN, "wrong")]
    [DataRow("POST", Users.ACL_ADMIN, "valid")]
    public void MutatingActions_RejectInvalidMethodTokenAndAccess(string method, int access, string token)
    {
        using var config = FwConfig.beginScope();
        var (_, controller) = Setup(method, access, token);

        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction(5));
        Assert.ThrowsExactly<AuthException>(() => controller.DeleteAction(5));
        Assert.ThrowsExactly<AuthException>(() => controller.RunNowAction(5));
        Assert.ThrowsExactly<AuthException>(() => controller.PauseAction(5));
        Assert.ThrowsExactly<AuthException>(() => controller.ResumeAction(5));
        Assert.ThrowsExactly<AuthException>(() => controller.RecoverAction(5));
    }

    [TestMethod]
    public void Preview_RequiresSiteAdminAndUsesFiveFieldServerCalculation()
    {
        using var config = FwConfig.beginScope();
        var (_, lowerAccess) = Setup("GET", Users.ACL_ADMIN, "valid");
        Assert.ThrowsExactly<AuthException>(() => lowerAccess.PreviewAction());

        var (fw, controller) = Setup("GET", Users.ACL_SITEADMIN, "valid");
        controller.PreviewAction();
        using var response = JsonDocument.Parse(((MemoryStream)fw.response.Body).ToArray());
        Assert.AreEqual(3, response.RootElement.GetProperty("occurrences").GetArrayLength());

        var (invalid, invalidController) = Setup("GET", Users.ACL_SITEADMIN, "valid");
        ((FwDict)invalid.FORM["item"]!)["cron"] = "0 0 7 * * *";
        invalidController.PreviewAction();
        Assert.AreEqual(400, invalid.response.StatusCode);
        StringAssert.Contains(Encoding.UTF8.GetString(((MemoryStream)invalid.response.Body).ToArray()), "five-field");
    }

#if isSQLite
    [TestMethod]
    public void TaskRoutes_SaveAllowlistedDefinitionAndRenderThePersistedListAndEdit()
    {
        using var file = new SqliteFile();
        var context = TestHelpers.CreateHttpContext("");
        using var body = new MemoryStream();
        context.Response.Body = body;
        var app = Path.Combine(TestHelpers.RepoRoot(), "osafw-app");
        using var scope = new FwTestScope(file.CreateDb, new Dictionary<string, string?>
        {
            ["appSettings:site_root"] = app,
            ["appSettings:template"] = TestHelpers.TemplateRoot(),
            ["appSettings:route_prefixes:/Admin"] = "true"
        }, context);
        var fw = scope.Fw;
        var scripts = Path.Combine(app, "App_Data", "sql", "sqlite");
        fw.db.execMultipleSQL(File.ReadAllText(Path.Combine(scripts, "fwdatabase.sql")));
        fw.db.execMultipleSQL(File.ReadAllText(Path.Combine(scripts, "lookups.sql")));
        fw.model<Users>().reloadSession(1);
        fw.G["timezone"] = "Eastern Standard Time";
        fw.Session("XSS", "valid");

        void Call(string method, string path)
        {
            body.SetLength(0);
            context.Response.StatusCode = 200;
            context.Request.Method = method;
            context.Request.Path = path;
            fw.setRoute(fw.getRoute());
            fw.callRoute();
        }

        const string taskName = "<img src=x onerror=alert(1)>";
        fw.FORM["XSS"] = "valid";
        fw.FORM["item"] = new FwDict
        {
            ["icode"] = FwCron.ICODE_EXAMPLE,
            ["iname"] = taskName,
            ["cron"] = "0 7 * * *",
            ["timezone"] = "Central Standard Time",
            ["payload"] = "{\"forged\":true}",
            ["run_token"] = "forged",
            ["run_requested_utc"] = DateTime.UtcNow,
            ["next_run"] = DateTime.UtcNow.AddDays(-1),
            ["last_result"] = "{\"forged\":true}"
        };
        Call("POST", "/Admin/Cron");
        Assert.AreEqual(302, context.Response.StatusCode);

        var id = fw.db.value("fwcron", DB.h("icode", FwCron.ICODE_EXAMPLE), "id").toInt();
        var task = fw.model<FwCron>().getTask(id);
        Assert.IsNotNull(task);
        Assert.IsNull(task.payload);
        Assert.IsNull(task.run_token);
        Assert.IsNull(task.run_requested_utc);
        Assert.IsNull(task.last_result);
        Assert.IsTrue(task.next_run > DateTime.UtcNow);

        fw.FORM.Clear();
        Call("GET", "/Admin/Cron");
        var html = Encoding.UTF8.GetString(body.ToArray());
        StringAssert.Contains(html, "&lt;img src=x onerror=alert(1)&gt;");
        StringAssert.Contains(html, "Central Standard Time");
        StringAssert.Contains(html, "Eastern Standard Time (");
        StringAssert.Contains(html, " UTC");
        StringAssert.Contains(html, "/Admin/Cron/(Pause)/" + id);

        Call("GET", "/Admin/Cron/" + id + "/edit");
        var edit = Encoding.UTF8.GetString(body.ToArray());
        StringAssert.Contains(edit, "id=\"task-cron-expression\"");
        StringAssert.Contains(edit, "name=\"item[timezone]\"");
        StringAssert.Contains(edit, "value=\"Central Standard Time\" selected");

        fw.FORM["XSS"] = "valid";
        Call("POST", "/Admin/Cron/(Pause)/" + id);
        Assert.AreEqual(302, context.Response.StatusCode);
        Assert.AreEqual(FwModel.STATUS_INACTIVE, fw.model<FwCron>().getTask(id)!.status);

        var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var wallTime = new DateTime(2099, 11, 1, 1, 30, 0, DateTimeKind.Unspecified);
        while (wallTime.DayOfWeek != DayOfWeek.Sunday)
            wallTime = wallTime.AddDays(1);
        var offsets = zone.GetAmbiguousTimeOffsets(wallTime);
        var firstOffset = offsets[0] > offsets[1] ? offsets[0] : offsets[1];
        var start = new DateTimeOffset(wallTime, firstOffset).UtcDateTime.AddMilliseconds(123);
        fw.db.update("fwcron", DB.h("start_date", start), DB.h("id", id));
        fw.FORM.Clear();
        fw.FORM["task_id"] = id.ToString();
        fw.FORM["item"] = DB.h("cron", "*/30 1 * * *", "timezone", zone.Id,
            "start_date", wallTime.ToString("yyyy-MM-dd'T'HH:mm:ss"));
        Call("GET", "/Admin/Cron/(Preview)");
        Assert.AreEqual(200, context.Response.StatusCode);
        var expected = FwCron.calculateNextRun("*/30 1 * * *", DateTime.UtcNow, null, zone, start);
        using var preview = JsonDocument.Parse(body.ToArray());
        Assert.AreEqual(expected!.Value.ToString("yyyy-MM-dd HH:mm 'UTC'"),
            preview.RootElement.GetProperty("occurrences")[0].GetProperty("utc").GetString(),
            "Preview must retain the existing bound's fold and fractional precision, just like Save.");
    }

#endif
    [TestMethod]
    public void PreparingUnchangedSchedule_PreservesExistingSlotAndRejectsAutoZone()
    {
        var fw = new FW(TestHelpers.CreateHttpContext(), new ConfigurationBuilder().Build(), _ => new UtcDb());
        fw.G["timezone"] = "UTC";
        var nextRun = DateTime.UtcNow.AddDays(2);
        var existing = new TFwCron { cron = "0 7 * * *", timezone = "UTC", next_run = nextRun };
        var item = new FwDict { ["cron"] = existing.cron, ["timezone"] = existing.timezone };

        AdminCronController.prepareSchedule(fw, item, existing);

        Assert.AreEqual(nextRun, item["next_run"]);
        fw.G["timezone"] = "Central Standard Time";
        existing.start_date = new DateTime(2026, 11, 1, 6, 30, 0, DateTimeKind.Utc).AddMilliseconds(123);
        item["start_date"] = "2026-11-01T01:30:00";
        AdminCronController.prepareSchedule(fw, item, existing);
        Assert.AreEqual(existing.start_date, item["start_date"], "An unchanged displayed bound retains its original fold and precision.");

        item["timezone"] = "Auto";
        Assert.ThrowsExactly<UserException>(() => AdminCronController.prepareSchedule(fw, item, existing));
    }
}
