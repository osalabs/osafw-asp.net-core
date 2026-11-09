#if isSQLite
using Microsoft.AspNetCore.Http;
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
    [DataRow("Sample", "recipients", "Enter at least one valid recipient address.")]
    [DataRow("Feedback_Report", "recipients", "Enter at least one valid recipient address.")]
    [DataRow("Sample", "cron", "Enter a valid five-field cron expression")]
    [DataRow("Feedback_Report", "cron", "Enter a valid five-field cron expression")]
    [DataRow("Sample", "iname", "Please review and update your input.")]
    [DataRow("Feedback_Report", "iname", "Please review and update your input.")]
    public void ScheduleEmail_RejectedPostRendersFeedbackAndPreservesInput(string code, string invalidField, string message)
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
        var sqlPath = Path.Combine(appPath, "App_Data", "sql", "sqlite");
        fw.db.execMultipleSQL(File.ReadAllText(Path.Combine(sqlPath, "fwdatabase.sql")));
        fw.db.execMultipleSQL(File.ReadAllText(Path.Combine(sqlPath, "lookups.sql")));
        fw.db.insert("fwreports", DB.h("icode", "Feedback_Report", "iname", "Feedback Report",
            "access_level", Users.ACL_SITEADMIN, "sql_template", "SELECT 42 AS amount", "params_json", "{}"));
        fw.model<Users>().reloadSession(1);
        fw.Session("XSS", "feedback-token");
        fw.FORM["XSS"] = "feedback-token";
        var item = DB.h("iname", "Retained schedule name", "format", "html", "recipients", "recipient@example.test",
            "cron", "0 7 * * *", "timezone", "UTC", "status", "10");
        item[invalidField] = invalidField == "iname" ? "" : "invalid-value";
        fw.FORM["item"] = item;
        request.Request.Method = "POST";
        request.Request.Path = $"/Admin/Reports/(ScheduleEmail)/{code}";
        using var page = new MemoryStream();
        request.Response.Body = page;
        fw.setRoute(fw.getRoute());

        fw.callRoute();

        var html = System.Text.Encoding.UTF8.GetString(page.ToArray());
        StringAssert.Contains(html, "role=\"alert\"");
        StringAssert.Contains(html, message);
        Assert.IsFalse(html.Contains("Review the report filters and delivery options.", StringComparison.Ordinal));
        StringAssert.Contains(html, "value=\"" + item["recipients"] + "\"");
        if (invalidField != "iname")
            StringAssert.Contains(html, "Retained schedule name");
        Assert.HasCount(0, fw.db.array("fwcron", []));
    }

    [TestMethod]
    public void ReportRoutes_CreateIndependentSchedulesAndReplaceReviewedFiltersOnly()
    {
        using var file = new SqliteFile();
        var request = TestHelpers.CreateHttpContext("");
        var appPath = Path.Combine(TestHelpers.RepoRoot(), "osafw-app");
        var settings = new Dictionary<string, string?>
        {
            ["appSettings:site_root"] = appPath,
            ["appSettings:template"] = TestHelpers.TemplateRoot(),
            ["appSettings:route_prefixes:/Admin"] = "true",
        };
        using var scope = new FwTestScope(file.CreateDb, settings, request);
        var fw = scope.Fw;
        var db = fw.db;
        var sqlPath = Path.Combine(appPath, "App_Data", "sql", "sqlite");
        db.execMultipleSQL(File.ReadAllText(Path.Combine(sqlPath, "fwdatabase.sql")));
        db.execMultipleSQL(File.ReadAllText(Path.Combine(sqlPath, "lookups.sql")));

        const string reportCode = "Case_Report";
        var reportId = db.insert("fwreports", DB.h(
            "icode", reportCode, "iname", "Case Report", "access_level", Users.ACL_SITEADMIN,
            "sql_template", "SELECT @selected_date, @account_id, @notes, @old_filter",
            "params_json", """{"selected_date":{"type":"date","required":true},"account_id":{"type":"int","label":"Account <number>"},"notes":{"type":"text"},"old_filter":{"type":"text"}}"""));
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
            fw.FORM["f"] = DB.h("selected_date", date, "account_id", "0", "notes", "", "old_filter", "retired-value",
                "pagesize", "5000", "to_emails", "unrelated@example.test", "arbitrary", "ignore");
        }

        FwCron.Row[] tasks() => db.array<FwCron.Row>("fwcron", [], "id").ToArray();

        prepare("1/2/2026", "html", "first@example.test, invalid-address", "0 7 * * *");
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
        Assert.AreEqual(reportCode, FwReports.reportCode(records[0].icode));
        Assert.AreEqual("0 7 * * *", records[0].cron);
        Assert.AreEqual("0 10 * * *", records[1].cron);
        Assert.IsTrue(records.All(job => job.next_run_utc > DateTime.UtcNow && job.last_result == null));

        var scheduler = fw.model<FwReports>();
        var first = scheduler.parseSchedulePayload(records[0].icode, records[0].payload!);
        var second = scheduler.parseSchedulePayload(records[1].icode, records[1].payload!);
        Assert.AreEqual("first@example.test", first["recipients"]);
        Assert.AreEqual("html", first["format"]);
        Assert.AreEqual("second@example.test", second["recipients"]);
        Assert.AreEqual("pdf", second["format"]);
        Assert.HasCount(4, (FwList)first["params"]!);
        var report = FwReportsBase.createInstance(fw, reportCode, new FwDict());
        var descriptions = report.describeScheduledFilters((FwList)first["params"]!);
        Assert.AreEqual("Account <number>", descriptions.Single(row => row["name"].toStr() == "account_id")["label"]);
        Assert.AreEqual("0", descriptions.Single(row => row["name"].toStr() == "account_id")["value"]);
        var sample = FwReportsBase.createScheduledInstance(fw, "Sample", []);
        var loginType = fw.model<FwLogTypes>().oneByIcode(FwLogTypes.ICODE_USERS_LOGIN);
        var sampleInputs = DB.h("from_date", "", "events_id", loginType["id"].toStr());
        var sampleSnapshot = sample.captureScheduledFilters(sampleInputs, is_frozen: false);
        sample.applyScheduledFilters(new FwDict { ["params"] = sampleSnapshot });
        sample.getData();
        var labels = (FwList)sample.ps["applied_params"]!;
        Assert.HasCount(1, labels);
        Assert.AreEqual("Event", labels[0]["label"]);
        Assert.AreEqual(loginType["iname"], labels[0]["value"]);

        Assert.AreEqual("1/2/2026", ((FwList)first["params"]!)[0]["value"]);
        Assert.AreEqual("3/4/2026", ((FwList)second["params"]!)[0]["value"]);

        // Exercise the rendered links and default name from the actual report template.
        fw.FORM.Clear();
        using var page = new MemoryStream();
        fw.response.Body = page;
        call("GET", "Show");
        var html = System.Text.Encoding.UTF8.GetString(page.ToArray());
        StringAssert.Contains(html, $"/Admin/Reports/(ScheduleEmail)/{reportCode}");
        StringAssert.Contains(html, "/Admin/Cron?dofilter=1&amp;f[s]=report_" + reportCode);
        StringAssert.Contains(html, "Case Report - email");

        db.update("fwreports", DB.h(
            "sql_template", "SELECT @selected_date, @account_id, @notes, @currency, @region",
            "params_json", """{"selected_date":{"type":"date","required":true},"account_id":{"type":"text","default":"new account"},"notes":{"type":"text","default":"new notes"},"currency":{"type":"text","default":"USD"},"region":{"type":"text","required":true}}"""),
            DB.h("id", reportId));

        var reviewRequest = TestHelpers.CreateHttpContext("");
        reviewRequest.Request.Method = "GET";
        reviewRequest.Request.Path = $"/Admin/Reports/{reportCode}";
        reviewRequest.Request.QueryString = new QueryString($"?fwcron_id={records[0].id}");
        using var reviewPage = new MemoryStream();
        reviewRequest.Response.Body = reviewPage;
        using (var reviewScope = new FwTestScope(file.CreateDb, settings, reviewRequest))
        {
            var reviewFw = reviewScope.Fw;
            reviewFw.model<Users>().reloadSession(1);
            reviewFw.setRoute(reviewFw.getRoute());
            reviewFw.callRoute();
        }

        Assert.AreEqual(200, reviewRequest.Response.StatusCode);
        var reviewHtml = System.Text.Encoding.UTF8.GetString(reviewPage.ToArray());
        StringAssert.Contains(reviewHtml, "Review the current values before updating the schedule.");
        StringAssert.Contains(reviewHtml, """id="fselected_date" name="f[selected_date]" maxlength="10" value="1/2/2026" """);
        StringAssert.Contains(reviewHtml, """id="fnotes" name="f[notes]" value="" """);
        StringAssert.Contains(reviewHtml, """id="faccount_id" name="f[account_id]" value="new account" """);
        StringAssert.Contains(reviewHtml, """id="fcurrency" name="f[currency]" value="USD" """);
        StringAssert.Contains(reviewHtml, """id="fregion" name="f[region]" value="" type="text" class="form-control" required""");
        Assert.IsFalse(reviewHtml.Contains("retired-value"));
        Assert.AreEqual(records[0].payload, tasks()[0].payload);
        fw.FORM.Clear();
        fw.FORM["XSS"] = "schedule-test-token";
        fw.FORM["fwcron_id"] = records[0].id.ToString();
        fw.FORM["f"] = DB.h("selected_date", "7/8/2026", "account_id", "reviewed account", "notes", "", "currency", "EUR", "region", "EMEA");
        call("POST", "UpdateScheduleFilters");
        Assert.AreEqual(302, fw.response.StatusCode);
        Assert.AreEqual($"/Admin/Reports/{reportCode}?fwcron_id={records[0].id}", fw.response.Headers.Location.ToString());

        var reviewed = tasks();
        var updated = scheduler.parseSchedulePayload(reviewed[0].icode, reviewed[0].payload!);
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
        Assert.HasCount(5, (FwList)edit["report_params"]!);
        db.update("fwreports", DB.h("status", FwModel.STATUS_ACTIVE, "params_json",
            """{"selected_date":{"type":"date"},"currency":{"type":"lookup_sql","source":"SELECT id, iname FROM missing_lookup_table"}}"""), DB.h("id", reportId));
        var brokenLookup = cron.ShowFormAction(records[0].id)!;
        Assert.IsTrue(brokenLookup["is_report_unavailable"].toBool());
        Assert.AreEqual("first@example.test", brokenLookup["recipients"]);

        // A failed update may reroute once to Show, which must report an unreadable saved schedule.
        db.update("fwcron", DB.h("payload", "{}"), DB.h("id", records[0].id));
        fw.model<FwCron>().removeCache(records[0].id);
        fw.FORM["XSS"] = "schedule-test-token";
        fw.FORM["fwcron_id"] = records[0].id.ToString();
        var error = Assert.ThrowsExactly<UserException>(() => call("POST", "UpdateScheduleFilters"));
        Assert.AreNotEqual("REPORT_SCHEDULE_INVALID", error.Message);
        Assert.AreEqual("{}", tasks()[0].payload);
    }
}
#endif
