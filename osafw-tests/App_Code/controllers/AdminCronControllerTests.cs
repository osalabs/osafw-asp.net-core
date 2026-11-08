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
    private sealed class TestUsers : Users
    {
        public override bool isReadOnly(int id = -1) => false;
        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    private static (FW fw, AdminCronController controller) Setup(string method, int access, string token)
    {
        var fw = new FW(TestHelpers.CreateHttpContext(), new ConfigurationBuilder().Build(), _ => new RejectingDb());
        fw.config()["template"] = TestHelpers.TemplateRoot();
        fw.Session("access_level", access.ToString());
        fw.G["timezone"] = "UTC";
        fw.Session("XSS", "valid");
        fw.request.Method = method;
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
    public void MutatingActions_RejectInvalidMethodAndToken(string method, int access, string token)
    {
        using var config = FwConfig.beginScope();
        var (_, controller) = Setup(method, access, token);

        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction(5));
        Assert.ThrowsExactly<AuthException>(() => controller.DeleteAction(5));
        Assert.ThrowsExactly<AuthException>(() => controller.RestoreDeletedAction(5));
        Assert.ThrowsExactly<AuthException>(() => controller.RunNowAction(5));
        Assert.ThrowsExactly<AuthException>(() => controller.PauseAction(5));
        Assert.ThrowsExactly<AuthException>(() => controller.ResumeAction(5));
        Assert.ThrowsExactly<AuthException>(() => controller.RecoverAction(5));
    }

    [TestMethod]
    public void Preview_RequiresSiteAdminAndUsesStandardCronosExpressions()
    {
        using var config = FwConfig.beginScope();
        var (lowerAccess, _) = Setup("GET", Users.ACL_ADMIN, "valid");
        lowerAccess.route.controller = "AdminCron";
        lowerAccess.route.action = "Preview";
        Assert.ThrowsExactly<AuthException>(() => lowerAccess.callRoute());

        var (fw, controller) = Setup("GET", Users.ACL_SITEADMIN, "valid");
        controller.PreviewAction();
        using var response = JsonDocument.Parse(((MemoryStream)fw.response.Body).ToArray());
        Assert.AreEqual(3, response.RootElement.GetProperty("occurrences").GetArrayLength());
        var utcOccurrence = response.RootElement.GetProperty("occurrences")[0];
        Assert.AreEqual(utcOccurrence.GetProperty("recurrence").GetString(), utcOccurrence.GetProperty("local").GetString(),
            "All three columns remain populated even when the task and viewer share UTC.");
        StringAssert.Contains(utcOccurrence.GetProperty("recurrence_title").GetString()!, "UTC (+00:00)");
        StringAssert.EndsWith(utcOccurrence.GetProperty("utc").GetString()!, " UTC");

        var (otherViewer, otherController) = Setup("GET", Users.ACL_SITEADMIN, "valid");
        otherViewer.G["timezone"] = "America/Chicago";
        otherController.PreviewAction();
        using var otherResponse = JsonDocument.Parse(((MemoryStream)otherViewer.response.Body).ToArray());
        var otherOccurrence = otherResponse.RootElement.GetProperty("occurrences")[0];
        Assert.AreNotEqual(otherOccurrence.GetProperty("recurrence").GetString(), otherOccurrence.GetProperty("local").GetString());
        StringAssert.Contains(otherOccurrence.GetProperty("local_title").GetString()!, "America/Chicago (");

        var (alias, aliasController) = Setup("GET", Users.ACL_SITEADMIN, "valid");
        ((FwDict)alias.FORM["item"]!)["cron"] = "@weekly";
        aliasController.PreviewAction();
        using var aliasResponse = JsonDocument.Parse(((MemoryStream)alias.response.Body).ToArray());
        Assert.AreEqual(3, aliasResponse.RootElement.GetProperty("occurrences").GetArrayLength());

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
        var settings = new Dictionary<string, string?>
        {
            ["appSettings:site_root"] = app,
            ["appSettings:template"] = TestHelpers.TemplateRoot(),
            ["appSettings:route_prefixes:/Admin"] = "true"
        };
        using var scope = new FwTestScope(file.CreateDb, settings, context);
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
            using var requestScope = new FwTestScope(file.CreateDb, settings, context);
            var requestFw = requestScope.Fw;
            requestFw.G["timezone"] = fw.G["timezone"];
            Utils.mergeHash(requestFw.FORM, fw.FORM);
            requestFw.setRoute(requestFw.getRoute());
            try
            {
                requestFw.callRoute();
            }
            finally
            {
                fw.model<FwCron>().removeCacheAll();
            }
        }

        var taskName = "<img src=x onerror=alert(1)>" + new string('x', 255);
        fw.FORM["XSS"] = "valid";
        fw.FORM["item"] = new FwDict
        {
            ["icode"] = FwCron.ICODE_EXAMPLE,
            ["iname"] = taskName,
            ["cron"] = "@daily",
            ["timezone"] = "Central Standard Time",
            ["payload"] = "{\"forged\":true}",
            ["run_token"] = "forged",
            ["run_requested_utc"] = DateTime.UtcNow,
            ["next_run_utc"] = DateTime.UtcNow.AddDays(-1),
            ["last_result"] = "{\"forged\":true}"
        };
        context.Request.Headers.Accept = "application/json";
        Assert.ThrowsExactly<NotFoundException>(() => Call("POST", "/Admin/Cron/-1"));
        Assert.AreEqual(0, fw.db.value("fwcron", DB.h("icode", FwCron.ICODE_EXAMPLE), "id").toInt());
        context.Request.Headers.Remove("Accept");
        Call("POST", "/Admin/Cron");
        Assert.AreEqual(302, context.Response.StatusCode);

        var id = fw.db.value("fwcron", DB.h("icode", FwCron.ICODE_EXAMPLE), "id").toInt();
        var task = fw.model<FwCron>().oneT(id);
        Assert.IsNotNull(task);
        Assert.AreEqual(taskName[..255], task.iname);
        Assert.AreEqual("@daily", task.cron);
        Assert.IsNull(task.payload);
        Assert.IsNull(task.run_token);
        Assert.IsNull(task.run_requested_utc);
        Assert.IsNull(task.last_result);
        Assert.IsTrue(task.next_run_utc > DateTime.UtcNow);

        fw.FORM.Clear();
        Call("GET", "/Admin/Cron");
        var html = Encoding.UTF8.GetString(body.ToArray());
        StringAssert.Contains(html, "&lt;img src=x onerror=alert(1)&gt;");
        StringAssert.Contains(html, "Customize Columns");
        StringAssert.Contains(html, "My List");
        StringAssert.Contains(html, "Central Standard Time");
        var scheduleTime = TimeZoneInfo.ConvertTimeFromUtc(task.next_run_utc!.Value,
            TimeZoneInfo.FindSystemTimeZoneById(task.timezone));
        StringAssert.Contains(html, scheduleTime.ToString(DateUtils.mapDateFormat(fw.userDateFormat)
            + " " + DateUtils.mapTimeFormat(fw.userTimeFormat), System.Globalization.CultureInfo.InvariantCulture));
        StringAssert.Contains(html, " UTC");
        Assert.IsFalse(html.Contains("/Admin/Cron/(Pause)/" + id), "Pause belongs to the task view, not each list row.");

        var invalidZoneId = fw.model<FwCron>().add(DB.h("icode", "legacy_bad_zone", "iname", "Legacy zone",
            "cron", "@weekly", "timezone", "Not/AZone", "next_run_utc", task.next_run_utc));
        var utcDisplay = task.next_run_utc.Value.ToString("yyyy-MM-dd HH:mm 'UTC'");
        Call("GET", "/Admin/Cron");
        StringAssert.Contains(Encoding.UTF8.GetString(body.ToArray()), "title=\"UTC\">" + utcDisplay);
        Call("GET", "/Admin/Cron/" + invalidZoneId);
        StringAssert.Contains(Encoding.UTF8.GetString(body.ToArray()), utcDisplay);

        fw.FORM["XSS"] = "valid";
        context.Request.Headers.Accept = "application/json";
        fw.FORM["item"] = DB.h("iname", "Legacy zone", "cron", "@weekly", "timezone", "Not/AZone");
        Assert.ThrowsExactly<UserException>(() => Call("POST", "/Admin/Cron/" + invalidZoneId));
        fw.FORM["item"] = DB.h("iname", "Legacy zone", "cron", "0 0 7 * * *", "timezone", "UTC");
        Assert.ThrowsExactly<UserException>(() => Call("POST", "/Admin/Cron/" + invalidZoneId));
        Assert.AreEqual("@weekly", fw.model<FwCron>().oneT(invalidZoneId)!.cron);
        fw.FORM.Clear();
        context.Request.Headers.Remove("Accept");

        Call("GET", "/Admin/Cron/" + id + "/edit");
        var edit = Encoding.UTF8.GetString(body.ToArray());
        StringAssert.Contains(edit, "id=\"task-cron-expression\"");
        StringAssert.Contains(edit, "name=\"item[timezone]\"");
        StringAssert.Contains(edit, "value=\"Central Standard Time\" selected");

        fw.FORM["XSS"] = "valid";
        Call("POST", "/Admin/Cron/(Pause)/" + id);
        Assert.AreEqual(302, context.Response.StatusCode);
        Assert.AreEqual(FwModel.STATUS_INACTIVE, fw.model<FwCron>().oneT(id)!.status);
        Call("POST", "/Admin/Cron/(Pause)/" + id);
        Assert.AreEqual(302, context.Response.StatusCode, "An already paused task uses normal feedback rather than an exception.");
        context.Request.Headers.Accept = "application/json";
        Call("POST", "/Admin/Cron/(Pause)/" + id);
        Assert.AreEqual(400, context.Response.StatusCode);
        var rejected = Utils.jsonDecodeDict(Encoding.UTF8.GetString(body.ToArray()))!;
        Assert.IsFalse(rejected["success"].toBool());
        Assert.IsTrue(((FwDict)rejected["error"]!)["message"].toStr().Length > 0);
        context.Request.Headers.Remove("Accept");

        Assert.AreEqual(FwModel.STATUS_INACTIVE, fw.model<FwCron>().oneT(id)!.status);


        fw.FORM.Clear();
        Call("GET", "/Admin/Cron/" + id);
        var detailsHtml = Encoding.UTF8.GetString(body.ToArray());
        StringAssert.Contains(detailsHtml, "id=\"FView\"");
        StringAssert.Contains(detailsHtml, "page-header-record-nav");
        var history = fw.model<FwActivityLogs>().listByEntityForUI("fwcron", id, FwActivityLogs.TAB_HISTORY);
        Assert.AreEqual(2, history.Count, "Distinct task events must not be merged within ten minutes.");

        fw.FORM["dofilter"] = "1";
        fw.FORM["f"] = new FwDict { ["s"] = "example_sleep", ["status"] = "10" };
        var listController = new AdminCronController();
        listController.init(fw);
        var filtered = listController.IndexAction();
        Assert.AreEqual(1, ((FwList)filtered["list_rows"]!).Count);
        fw.FORM.Clear();

        fw.FORM["XSS"] = "valid";
        fw.FORM["density"] = "table-dense";
        Call("POST", "/Admin/Cron/(SaveUserViews)");
        Assert.AreEqual("table-dense", fw.model<UserViews>().oneByIcode("/Admin/Cron")["density"]);
        fw.FORM.Clear();
        fw.FORM["XSS"] = "valid";
        fw.FORM["fld"] = new FwDict { ["iname"] = "1", ["icode"] = "2" };
        Call("POST", "/Admin/Cron/(SaveUserViews)");
        Assert.AreEqual("iname icode", fw.model<UserViews>().oneByIcode("/Admin/Cron")["fields"]);
        fw.FORM.Clear();

        fw.db.update("fwcron", DB.h("run_token", "test-owned-claim"), DB.h("id", id));
        fw.model<FwCron>().removeCache(id);
        Call("GET", "/Admin/Cron/" + id);
        Assert.IsFalse(Encoding.UTF8.GetString(body.ToArray()).Contains("/Admin/Cron/" + id + "/delete"),
            "The standard header must not offer Delete for a claimed task.");
        fw.FORM["XSS"] = "valid";
        fw.FORM["delete"] = "1";
        fw.FORM["cb"] = new FwDict { [id.ToString()] = "1" };
        fw.FORM["_method"] = "PUT";
        Call("POST", "/Admin/Cron");
        Assert.AreEqual(FwModel.STATUS_INACTIVE, fw.model<FwCron>().oneT(id)!.status,
            "Standard bulk delete must retain a task claimed by a worker.");
        fw.db.update("fwcron", DB.h("run_token", null), DB.h("id", id));
        fw.model<FwCron>().removeCache(id);
        fw.FORM.Clear();

        var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var wallTime = new DateTime(2099, 11, 1, 1, 30, 0, DateTimeKind.Unspecified);
        while (wallTime.DayOfWeek != DayOfWeek.Sunday)
            wallTime = wallTime.AddDays(1);
        var offsets = zone.GetAmbiguousTimeOffsets(wallTime);
        var firstOffset = offsets[0] > offsets[1] ? offsets[0] : offsets[1];
        var start = new DateTimeOffset(wallTime, firstOffset).UtcDateTime.AddMilliseconds(123);
        fw.db.update("fwcron", DB.h("start_date_utc", start), DB.h("id", id));
        fw.model<FwCron>().removeCache(id);
        fw.FORM.Clear();
        fw.FORM["fwcron_id"] = id.ToString();
        fw.FORM["item"] = DB.h("cron", "*/30 1 * * *", "timezone", zone.Id,
            "start_date_utc", wallTime.ToString("yyyy-MM-dd'T'HH:mm:ss"));
        Call("GET", "/Admin/Cron/(Preview)");
        Assert.AreEqual(200, context.Response.StatusCode);
        var expected = FwCron.calculateNextRun("*/30 1 * * *", DateTime.UtcNow, null, zone, start);
        using var preview = JsonDocument.Parse(body.ToArray());
        Assert.AreEqual(expected!.Value.ToString("yyyy-MM-dd HH:mm 'UTC'"),
            preview.RootElement.GetProperty("occurrences")[0].GetProperty("utc").GetString(),
            "Preview must retain the existing bound's fold and fractional precision, just like Save.");

        fw.FORM.Clear();
        fw.FORM["_method"] = "PUT";
        fw.FORM["XSS"] = "invalid";
        fw.FORM["delete"] = "1";
        fw.FORM["cb"] = new FwDict { [id.ToString()] = "1" };
        Assert.ThrowsExactly<AuthException>(() => Call("POST", "/Admin/Cron"));
        Assert.AreEqual(FwModel.STATUS_INACTIVE, fw.model<FwCron>().oneT(id)!.status);
        fw.FORM["XSS"] = "valid";
        fw.FORM["_method"] = "PUT";
        context.Request.Headers.Accept = "application/json";
        Assert.ThrowsExactly<AuthException>(() => Call("GET", "/Admin/Cron"));
        context.Request.Headers.Remove("Accept");
        Assert.AreEqual(FwModel.STATUS_INACTIVE, fw.model<FwCron>().oneT(id)!.status,
            "A query-string method override must not allow a GET mutation.");
        Call("POST", "/Admin/Cron");
        Assert.AreEqual(302, context.Response.StatusCode);
        Assert.AreEqual(FwModel.STATUS_DELETED, fw.model<FwCron>().oneT(id)!.status);

        fw.FORM.Clear();
        fw.FORM["dofilter"] = "1";
        fw.FORM["f"] = new FwDict { ["status"] = "127" };
        Call("GET", "/Admin/Cron");
        var trash = Encoding.UTF8.GetString(body.ToArray());
        StringAssert.Contains(trash, "data-url=\"/Admin/Cron/" + id + "\"");
        Assert.IsFalse(trash.Contains("/Admin/Cron/" + id + "/edit", StringComparison.Ordinal));

        fw.FORM.Clear();
        var restoreRoute = "/Admin/Cron/(RestoreDeleted)/" + id;
        Call("GET", "/Admin/Cron/" + id);
        var deleted = Encoding.UTF8.GetString(body.ToArray());
        StringAssert.Contains(deleted, "method=\"post\" action=\"" + restoreRoute + "\"");
        Assert.IsFalse(deleted.Contains("/Admin/Cron/(Resume)/" + id, StringComparison.Ordinal));
        Assert.IsFalse(deleted.Contains("/Admin/Cron/" + id + "/delete", StringComparison.Ordinal));

        fw.FORM["XSS"] = "valid";
        context.Request.Headers.Accept = "application/json";
        Assert.ThrowsExactly<AuthException>(() => Call("GET", restoreRoute));
        fw.FORM["XSS"] = "invalid";
        Assert.ThrowsExactly<AuthException>(() => Call("POST", restoreRoute));
        fw.FORM["XSS"] = "valid";
        fw.db.update("users", DB.h("is_readonly", 1), DB.h("id", 1));
        fw.model<Users>().removeCache(1);
        Assert.ThrowsExactly<AuthException>(() => Call("POST", restoreRoute));
        Assert.AreEqual(FwModel.STATUS_DELETED, fw.model<FwCron>().oneT(id)!.status);
        fw.db.update("users", DB.h("is_readonly", 0), DB.h("id", 1));
        fw.model<Users>().removeCache(1);
        context.Request.Headers.Remove("Accept");

        Call("POST", restoreRoute);
        Assert.AreEqual(302, context.Response.StatusCode);
        Assert.AreEqual("/Admin/Cron/" + id, context.Response.Headers.Location.ToString());
        Assert.AreEqual(FwModel.STATUS_INACTIVE, fw.model<FwCron>().oneT(id)!.status);
        Assert.IsNull(fw.model<FwCron>().oneT(id)!.run_requested_utc);

        var applicationUrl = "/Admin/Cron/" + id;
        fw.FORM["return_url"] = applicationUrl;
        fw.FORM["_method"] = "DELETE";
        Call("POST", applicationUrl);
        Assert.AreEqual(302, context.Response.StatusCode);
        Assert.AreEqual(applicationUrl, context.Response.Headers.Location.ToString());
        Assert.AreEqual(FwModel.STATUS_DELETED, fw.model<FwCron>().oneT(id)!.status);

        foreach (var transport in new[] { "PATCH", "POST" })
        {
            var patchId = fw.model<FwCron>().add(new FwDict
            {
                ["icode"] = "patch_delete_" + transport,
                ["iname"] = "Patch delete",
                ["cron"] = "0 7 * * *"
            });
            fw.FORM.Clear();
            fw.FORM["XSS"] = "valid";
            fw.FORM["delete"] = "1";
            fw.FORM["cb"] = new FwDict { [patchId.ToString()] = "1" };
            if (transport == "POST")
                fw.FORM["_method"] = "PATCH";
            Call(transport, "/Admin/Cron");
            Assert.AreEqual(FwModel.STATUS_DELETED, fw.model<FwCron>().oneT(patchId)!.status);
        }
        fw.FORM.Clear();

        var singleId = fw.model<FwReports>().addSchedule("Sample", DB.h("iname", "Single report delete", "cron", "0 7 * * *"));
        var reportUrl = "/Admin/Cron/" + singleId;
        fw.FORM["return_url"] = reportUrl;
        Call("GET", reportUrl + "/delete");
        var confirmation = Encoding.UTF8.GetString(body.ToArray());
        StringAssert.Contains(confirmation, "id=\"FDel\"");
        StringAssert.Contains(confirmation, "name=\"_method\" value=\"DELETE\"");
        StringAssert.Contains(confirmation, "name=\"return_url\" value=\"" + reportUrl + "\"");

        fw.FORM["XSS"] = "valid";
        fw.FORM["_method"] = "DELETE";
        Call("POST", reportUrl);
        Assert.AreEqual(302, context.Response.StatusCode);
        Assert.AreEqual("/Admin/Cron", context.Response.Headers.Location.ToString());
        Assert.IsNull(fw.model<FwCron>().oneT(singleId));

        fw.FORM.Remove("return_url");
        var bulkReportId = fw.model<FwReports>().addSchedule("Sample", DB.h("iname", "Bulk report delete", "cron", "@daily"));
        fw.FORM["_method"] = "PUT";
        fw.FORM["delete"] = "1";
        fw.FORM["cb"] = new FwDict { [bulkReportId.ToString()] = "1" };
        Call("POST", "/Admin/Cron");
        Assert.AreEqual(302, context.Response.StatusCode);
        Assert.IsNull(fw.model<FwCron>().oneT(bulkReportId));
    }

#endif
    [TestMethod]
    public void PreparingUnchangedSchedule_PreservesExistingSlotAndRejectsAutoZone()
    {
        var fw = new FW(TestHelpers.CreateHttpContext(), new ConfigurationBuilder().Build(), _ => new RejectingDb());
        fw.G["timezone"] = "UTC";
        var nextRun = DateTime.UtcNow.AddDays(2);
        var existing = new FwCron.Row { cron = "0 7 * * *", timezone = "UTC", next_run_utc = nextRun };
        var item = new FwDict { ["cron"] = existing.cron, ["timezone"] = existing.timezone };

        fw.model<FwCron>().prepareSchedule(item, existing);

        Assert.AreEqual(nextRun, item["next_run_utc"]);
        fw.G["timezone"] = "Central Standard Time";
        existing.start_date_utc = new DateTime(2026, 11, 1, 6, 30, 0, DateTimeKind.Utc).AddMilliseconds(123);
        item["start_date_utc"] = "2026-11-01T01:30:00";
        fw.model<FwCron>().prepareSchedule(item, existing);
        Assert.AreEqual(existing.start_date_utc, item["start_date_utc"], "An unchanged displayed bound retains its original fold and precision.");

        item["timezone"] = "Auto";
        Assert.ThrowsExactly<UserException>(() => fw.model<FwCron>().prepareSchedule(item, existing));
    }
}
