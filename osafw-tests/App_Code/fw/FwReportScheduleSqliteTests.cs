#if isSQLite
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace osafw.Tests;

[TestClass]
public class FwReportScheduleSqliteTests
{
    [TestMethod]
    public void ReportRoutes_CreateIndependentSchedulesAndReplaceReviewedFiltersOnly()
    {
        using var file = new SqliteFile();
        var request = TestHelpers.CreateHttpContext("");
        var appPath = Path.Combine(TestHelpers.RepoRoot(), "osafw-app");
        using var scope = new FwTestScope(file.CreateDb, new Dictionary<string, string?>
        {
            ["appSettings:site_root"] = appPath,
            ["appSettings:template"] = TestHelpers.TemplateRoot(),
            ["appSettings:route_prefixes:/Admin"] = "true",
        }, request);
        var fw = scope.Fw;
        var db = fw.db;
        var sqlPath = Path.Combine(appPath, "App_Data", "sql", "sqlite");
        db.execMultipleSQL(File.ReadAllText(Path.Combine(sqlPath, "fwdatabase.sql")));
        db.execMultipleSQL(File.ReadAllText(Path.Combine(sqlPath, "lookups.sql")));

        const string reportCode = "Case_Report";
        var reportId = db.insert("fwreports", DB.h(
            "icode", reportCode, "iname", "Case Report", "access_level", Users.ACL_SITEADMIN,
            "sql_template", "SELECT @selected_date AS selected_date, @account_id AS account_id",
            "params_json", """{"selected_date":{"type":"date","required":true},"account_id":{"type":"int","label":"Account <number>"}}"""));
        fw.model<Users>().reloadSession(1);
        fw.Session("XSS", "schedule-test-token");
        fw.G["date_format"] = DateUtils.DATE_FORMAT_DMY;

        void call(string method, string action)
        {
            request.Request.Method = method;
            request.Request.Path = $"/Admin/Reports/({action})/{reportCode}";
            fw.setRoute(fw.getRoute());
            fw.callRoute();
        }

        void prepare(string date, string format, string recipient, string cron)
        {
            fw.FORM.Clear();
            fw.FORM["XSS"] = "schedule-test-token";
            fw.FORM["item"] = DB.h("iname", "Independent schedule", "format", format, "recipients", recipient,
                "cron", cron, "timezone", "UTC", "status", "0");
            fw.FORM["f"] = DB.h("selected_date", date, "account_id", "0",
                "pagesize", "5000", "to_emails", "unrelated@example.test", "arbitrary", "ignore");
        }

        TFwCron[] tasks() => db.array<TFwCron>("fwcron", [], "id").ToArray();

        prepare("1/2/2026", "html", "first@example.test", "0 7 * * *");
        Assert.ThrowsExactly<AuthException>(() => call("GET", "ScheduleEmail"));
        fw.FORM["XSS"] = "wrong-token";
        Assert.ThrowsExactly<AuthException>(() => call("POST", "ScheduleEmail"));
        fw.FORM["XSS"] = "schedule-test-token";
        fw.Session("access_level", Users.ACL_ADMIN.ToString());
        Assert.ThrowsExactly<AuthException>(() => call("POST", "ScheduleEmail"));
        Assert.HasCount(0, tasks());

        fw.Session("access_level", Users.ACL_SITEADMIN.ToString());
        call("POST", "ScheduleEmail");
        Assert.AreEqual(302, fw.response.StatusCode);
        Assert.AreEqual("/Admin/Reports/" + reportCode, fw.response.Headers.Location.ToString());
        prepare("3/4/2026", "pdf", "second@example.test", "0 10 * * *");
        call("POST", "ScheduleEmail");

        var records = tasks();
        Assert.HasCount(2, records);
        Assert.AreNotEqual(records[0].icode, records[1].icode);
        Assert.AreEqual(reportCode, FwCron.reportCode(records[0].icode));
        Assert.AreEqual("0 7 * * *", records[0].cron);
        Assert.AreEqual("0 10 * * *", records[1].cron);
        Assert.IsTrue(records.All(job => job.next_run_utc > DateTime.UtcNow && job.last_result == null));

        var scheduler = fw.model<FwScheduledReports>();
        var first = scheduler.payload(records[0].icode, records[0].payload!);
        var second = scheduler.payload(records[1].icode, records[1].payload!);
        Assert.AreEqual("first@example.test", first["recipients"]);
        Assert.AreEqual("html", first["format"]);
        Assert.AreEqual("second@example.test", second["recipients"]);
        Assert.AreEqual("pdf", second["format"]);
        Assert.HasCount(2, (FwList)first["params"]!);
        var report = FwReportsBase.createInstance(fw, reportCode, new FwDict());
        var descriptions = report.describeScheduledFilters((FwList)first["params"]!);
        Assert.AreEqual("Account <number>", descriptions.Single(row => row["name"].toStr() == "account_id")["label"]);
        Assert.AreEqual("0", descriptions.Single(row => row["name"].toStr() == "account_id")["value"]);
        var sample = FwReportsBase.createInstance(fw, "Sample", new FwDict());
        var loginType = fw.model<FwLogTypes>().oneByIcode(FwLogTypes.ICODE_USERS_LOGIN);
        var labels = sample.describeScheduledFilters(new FwList { new FwDict { ["name"] = "events_id", ["type"] = "int", ["value"] = loginType["id"].toStr() } });
        Assert.AreEqual("Event", labels[0]["label"]);
        Assert.AreEqual(loginType["iname"], labels[0]["value"]);

        Assert.AreEqual("1/2/2026", ((FwList)first["params"]!)[0]["value"]);
        Assert.AreEqual("3/4/2026", ((FwList)second["params"]!)[0]["value"]);

        db.update("fwreports", DB.h(
            "sql_template", "SELECT @selected_date AS selected_date, @currency AS currency",
            "params_json", """{"selected_date":{"type":"date","required":true},"currency":{"type":"text","required":true}}"""),
            DB.h("id", reportId));
        fw.FORM.Clear();
        fw.FORM["XSS"] = "schedule-test-token";
        fw.FORM["schedule_id"] = records[0].id.ToString();
        fw.FORM["f"] = DB.h("selected_date", "7/8/2026", "currency", "EUR");
        call("POST", "UpdateScheduleFilters");
        Assert.AreEqual(302, fw.response.StatusCode);
        Assert.AreEqual($"/Admin/Reports/{reportCode}?schedule_id={records[0].id}", fw.response.Headers.Location.ToString());

        var reviewed = tasks();
        var updated = scheduler.payload(reviewed[0].icode, reviewed[0].payload!);
        var filters = (FwList)updated["params"]!;
        Assert.AreEqual("7/8/2026", filters.Single(row => row["name"].toStr() == "selected_date")["value"]);
        Assert.AreEqual("EUR", filters.Single(row => row["name"].toStr() == "currency")["value"]);
        Assert.AreEqual(first["run_as_users_id"], updated["run_as_users_id"]);
        Assert.AreEqual(first["format"], updated["format"]);
        Assert.AreEqual(first["recipients"], updated["recipients"]);
        Assert.AreEqual(records[0].next_run_utc, reviewed[0].next_run_utc);
        Assert.AreEqual(records[1].payload, reviewed[1].payload);

        db.update("fwreports", DB.h("status", FwModel.STATUS_INACTIVE), DB.h("id", reportId));
        fw.FORM.Clear();
        request.Request.Method = "GET";
        var cron = new AdminCronController();
        cron.init(fw);
        var edit = cron.ShowFormAction(records[0].id)!;
        Assert.IsTrue(edit["is_report_unavailable"].toBool());
        Assert.AreEqual("first@example.test", edit["recipients"]);
        Assert.AreEqual("html", edit["delivery_format"]);
        Assert.IsNotNull(edit["run_as_users"]);
        Assert.HasCount(2, (FwList)edit["report_params"]!);
        db.update("fwreports", DB.h("status", FwModel.STATUS_ACTIVE, "params_json",
            """{"selected_date":{"type":"date"},"currency":{"type":"lookup_sql","source":"SELECT id, iname FROM missing_lookup_table"}}"""), DB.h("id", reportId));
        var brokenLookup = cron.ShowFormAction(records[0].id)!;
        Assert.IsTrue(brokenLookup["is_report_unavailable"].toBool());
        Assert.AreEqual("first@example.test", brokenLookup["recipients"]);


    }
}
#endif
