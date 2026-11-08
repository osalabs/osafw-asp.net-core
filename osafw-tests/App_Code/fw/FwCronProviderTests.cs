using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace osafw.Tests;

internal static class FwCronProviderChecks
{
    private sealed class TestCron(Func<FwCron.Row, FwDict?> run) : FwCron
    {
        protected override FwDict? runJobAction(FwCron.Row job) => run(job);

        public bool updateIf(int id, FwDict fields, FwDict conditions) => update(id, fields, conditions);
    }

    private static FW context(string connection, string provider, string timezone = "UTC") =>
        new(null, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["appSettings:site_root"] = Path.Combine(TestHelpers.RepoRoot(), "osafw-app"),
            ["appSettings:log_level"] = "0"
        }).Build(), _ => new DB(DB.h(
            "connection_string", connection, "type", provider, "timezone", timezone), "main"));

    private static TestCron model(FW fw, Func<FwCron.Row, FwDict?> run)
    {
        var model = new TestCron(run);
        model.init(fw);
        return model;
    }

    public static async Task OneClaim(string connection, string provider)
    {
        using var scope = FwConfig.beginScope();
        using var first = context(connection, provider, "Eastern Standard Time");
        using var second = context(connection, provider, "Eastern Standard Time");
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var firstCalls = 0;
        var secondCalls = 0;
        var firstCron = model(first, snapshot =>
        {
            Assert.AreEqual("current payload", snapshot.payload);
            firstCalls++;
            entered.Set();
            Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(20)), "The test did not release the handler.");
            return new() { ["data"] = DB.h("processed_count", 3) };
        });
        var secondCron = model(second, _ =>
        {
            secondCalls++;
            return null;
        });
        var reference = DateTime.UtcNow.AddMinutes(-1);
        var due = reference.Date.AddHours(reference.Hour).AddMinutes(reference.Minute);
        var id = firstCron.add(DB.h("icode", "claim_test", "cron", "0 0 * * *", "next_run_utc", due,
            "payload", "Current payload", "upd_time", due.AddDays(-1)));
        var stale = secondCron.listDueJobs().Single(task => task.id == id);
        Assert.AreEqual(due, stale.next_run_utc, "Schedule instants remain UTC even when the database timezone is Eastern.");
        Assert.IsTrue(firstCron.updateDefinition(id, stale, DB.h("payload", "current payload")));
        Assert.IsFalse(secondCron.updateDefinition(id, stale, DB.h("payload", "stale overwrite")),
            "A concurrent case-only edit invalidates the observed definition on case-insensitive databases.");
        Assert.IsTrue(firstCron.requestRun(id));
        var firstRun = Task.Run(() => firstCron.runJob(stale));
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(20)), "The worker did not enter the handler.");
            var held = secondCron.oneT(id)!;
            Assert.IsNotNull(held.run_token);
            Assert.IsNull(held.run_requested_utc, "Manual and scheduled work coalesce in the same claim.");
            Assert.AreEqual(DateTimeKind.Utc, held.last_started_utc!.Value.Kind);
            Assert.IsTrue((DateTime.UtcNow - held.last_started_utc.Value).Duration() < TimeSpan.FromSeconds(30));
            Assert.IsFalse(secondCron.requestRun(id));
            Assert.IsFalse(secondCron.updateDefinition(id, held, DB.h("payload", "obsolete payload")));
            Assert.IsFalse(secondCron.deleteTask(id));
            secondCron.runJob(stale);
            Assert.AreEqual(0, secondCalls);
        }
        finally
        {
            release.Set();
            await firstRun;
        }

        secondCron.runJob(stale);
        Assert.AreEqual(1, firstCalls);
        Assert.AreEqual(0, secondCalls, "The earlier due-row read cannot start a second run after the claim was released.");
        secondCron.removeCache(id); // The worker finished in a separate request context.
        var finished = secondCron.oneT(id)!;
        Assert.IsNull(finished.run_token);
        Assert.IsNotNull(finished.last_finished_utc);
        Assert.IsTrue(finished.next_run_utc > DateTime.UtcNow);
        var result = FwCron.parseResult(finished.last_result);
        Assert.AreEqual(FwCron.RESULT_SUCCEEDED, result["status"]);
        Assert.AreEqual(3, ((FwDict)result["data"]!)["processed_count"].toInt());
        var events = second.model<FwActivityLogs>().listByEntity(FwEntities.ICODE_CRON, id,
            new[] { FwLogTypes.ICODE_CRON_EVENT });
        Assert.HasCount(1, events, "Only the administrator's manual run request enters activity history.");
    }

    public static void ConditionalModelUpdate(string connection, string provider)
    {
        using var scope = FwConfig.beginScope();
        using var fw = context(connection, provider);
        var cron = model(fw, _ => null);
        var before = DateTime.UtcNow.AddDays(-1);
        var id = cron.add(DB.h("icode", "conditional_update", "iname", "Before", "cron", "@daily", "upd_time", before));
        var cached = cron.oneT(id)!;
        cron.one(id);
        cron.oneTByIcode(cached.icode);

        Assert.IsFalse(cron.updateIf(id, DB.h("iname", "Missed"), DB.h("run_token", "held")));
        Assert.AreEqual("Before", fw.db.value("fwcron", DB.h("id", id), "iname"));
        fw.db.update("fwcron", DB.h("iname", "External change"), DB.h("id", id));
        Assert.AreEqual("Before", cron.one(id)["iname"], "A rejected update leaves the request cache intact.");
        Assert.AreEqual("Before", cron.oneT(id)!.iname);
        Assert.AreEqual("Before", cron.oneTByIcode(cached.icode)!.iname);
        cron.oneT(id)!.iname = "Unsaved edit";
        Assert.AreEqual("Before", cron.oneT(id)!.iname, "Returned rows must not mutate the cached snapshot.");
        cron.removeCache(id);
        Assert.AreEqual("External change", cron.oneTByIcode(cached.icode)!.iname);
        Assert.AreEqual("External change", cron.oneT(id)!.iname);
        Assert.IsEmpty(fw.model<FwActivityLogs>().listByEntity("fwcron", id, new[] { FwLogTypes.ICODE_UPDATED }));

        var conditions = DB.h("id", id + 1, "run_token", null);
        Assert.IsTrue(cron.updateIf(id, DB.h("iname", "After"), conditions));
        Assert.AreEqual(id + 1, conditions["id"], "The supplied conditions are not mutated.");
        Assert.AreEqual("After", cron.oneT(id)!.iname);
        Assert.AreEqual("After", cron.one(id)["iname"]);
        Assert.AreEqual("After", cron.oneTByIcode(cached.icode)!.iname);
        Assert.IsTrue(cron.oneT(id)!.upd_time > before);
        Assert.HasCount(1, fw.model<FwActivityLogs>().listByEntity("fwcron", id, new[] { FwLogTypes.ICODE_UPDATED }));

        var day = DateTime.UtcNow.Date.AddDays(1);
        var clockId = cron.add(DB.h("icode", "clock_update", "cron", "0 7 * * *",
            "start_date_utc", day.AddHours(6), "next_run_utc", day.AddHours(7)));
        var observed = cron.oneT(clockId)!;
        Assert.IsTrue(cron.updateDefinition(clockId, observed, DB.h("start_date_utc", day.AddHours(6.5))));
        Assert.AreEqual(day.AddHours(6.5), cron.oneT(clockId)!.start_date_utc);
        Assert.AreEqual(observed.next_run_utc, cron.oneT(clockId)!.next_run_utc);
        Assert.HasCount(1, fw.model<FwActivityLogs>().listByEntity("fwcron", clockId, new[] { FwLogTypes.ICODE_UPDATED }),
            "A time-only definition change must be audited even if the next recurrence is unchanged.");
        Assert.IsFalse(cron.updateDefinition(clockId, observed, DB.h("start_date_utc", day.AddHours(6.75))));
        Assert.HasCount(1, fw.model<FwActivityLogs>().listByEntity("fwcron", clockId, new[] { FwLogTypes.ICODE_UPDATED }));
    }

    public static void FailureDiagnostics(string connection, string provider)
    {
        using var scope = FwConfig.beginScope();
        foreach (var isLogPii in new[] { false, true })
        {
            var logPath = Path.GetTempFileName();
            try
            {
                using var fw = context(connection, provider);
                fw.config()["log_pii"] = isLogPii;
                fw.flogger.log_level = LogLevel.ERROR;
                fw.flogger.log_file = logPath;
                var cron = model(fw, _ => throw new InvalidOperationException("private failure detail"));
                var id = cron.add(DB.h("icode", "diagnostics_" + isLogPii, "cron", "@daily", "next_run_utc", DateTime.UtcNow.AddMinutes(-1)));
                cron.runJob(cron.oneT(id)!);

                fw.flogger.Dispose();
                var log = File.ReadAllText(logPath);
                StringAssert.Contains(log, "Scheduled task failed. Task ID:");
                StringAssert.Contains(log, id.ToString());
                StringAssert.Contains(log, nameof(InvalidOperationException));
                Assert.AreEqual(isLogPii, log.Contains("private failure detail", StringComparison.Ordinal));
                Assert.AreEqual("CRON_HANDLER_FAILED", FwCron.parseResult(cron.oneT(id)!.last_result)["message"]);
                Assert.IsNull(cron.oneT(id)!.run_token);
            }
            finally
            {
                File.Delete(logPath);
            }
        }
    }

    public static void ManualRecoveryAndFailure(string connection, string provider)
    {
        using var scope = FwConfig.beginScope();
        using var first = context(connection, provider);
        using var second = context(connection, provider);
        var admin = second.model<FwCron>();
        var calls = 0;
        var cron = model(first, job =>
        {
            calls++;
            if (job.icode == "manual_test")
            {
                Assert.IsTrue(admin.pause(job.id));
                return new() { ["data"] = new string('a', 18000) };
            }

            throw new InvalidOperationException("sensitive test message");
        });

        var next = DateTime.UtcNow.Date.AddDays(2);
        var id = cron.add(DB.h("icode", "manual_test", "cron", "0 0 * * *", "next_run_utc", next, "start_date_utc", next));
        Assert.IsTrue(cron.requestRun(id));
        Assert.IsFalse(admin.requestRun(id));
        cron.runJob(cron.oneT(id)!);
        var paused = cron.oneT(id)!;
        Assert.AreEqual(FwModel.STATUS_INACTIVE, paused.status, "Finishing does not undo a concurrent pause.");
        Assert.AreEqual(next, paused.next_run_utc, "An early manual run preserves the next occurrence.");
        var result = FwCron.parseResult(paused.last_result);
        Assert.AreEqual(FwCron.RESULT_SUCCEEDED, result["status"]);
        Assert.AreEqual(new string('a', 18000), result["data"]);

        first.db.update("fwcron", DB.h("run_token", "AbcHeld"), DB.h("id", id));
        cron.removeCache(id);
        var held = cron.oneT(id)!;
        cron.runJob(held);
        Assert.AreEqual(1, calls);
        Assert.IsFalse(admin.recover(id, "abcHeld"), "Recovery compares the exact observed token even under SQL Server collation.");
        Assert.IsTrue(admin.recover(id, "AbcHeld"));
        cron.removeCache(id); // Recovery used the separate admin context.
        var recovered = cron.oneT(id)!;
        Assert.AreEqual(FwCron.RESULT_UNCERTAIN, FwCron.parseResult(recovered.last_result)["status"]);
        Assert.AreEqual(FwModel.STATUS_INACTIVE, recovered.status);
        Assert.IsNull(recovered.run_token);
        Assert.IsTrue(admin.requestRun(id));
        Assert.IsTrue(admin.pause(id), "Pause can cancel a manual request queued on an already-paused task.");
        Assert.IsNull(admin.oneT(id)!.run_requested_utc);

        var failingId = cron.add(DB.h("icode", "failing_test", "cron", "0 0 * * *", "next_run_utc", next.AddDays(-3)));
        var failing = cron.oneT(failingId)!;
        cron.runJob(failing);
        cron.runJob(failing);
        Assert.AreEqual(2, calls, "The failed occurrence advances without per-minute retries.");
        var failed = cron.oneT(failingId)!;
        Assert.IsTrue(failed.next_run_utc > DateTime.UtcNow);
        Assert.AreEqual(FwCron.RESULT_FAILED, FwCron.parseResult(failed.last_result)["status"]);
        Assert.IsFalse(failed.last_result!.Contains("sensitive test message", StringComparison.Ordinal));
        Assert.HasCount(1, first.model<FwActivityLogs>().listByEntity(FwEntities.ICODE_CRON, failingId,
            new[] { FwLogTypes.ICODE_CRON_EVENT }), "Failed runs remain in activity history.");
    }

    public static void ReportIdentityAndSkippedWork(string connection, string provider)
    {
        using var scope = FwConfig.beginScope();
        using var first = context(connection, provider);
        var legacyCalls = 0;
        var cron = model(first, _ =>
        {
            legacyCalls++;
            return null;
        });
        var stale = DateTime.UtcNow.AddHours(-1);
        var fields = DB.h("cron", "0 0 * * *", "next_run_utc", stale, "run_requested_utc", stale);
        var id = first.model<FwReports>().addSchedule("Sample_Archive", fields);
        first.model<FwReports>().addSchedule("Sample", fields);
        first.model<FwReports>().addSchedule("SampleXArchive", fields);

        var candidate = cron.oneT(id)!;
        Assert.AreEqual("Sample_Archive", FwReports.reportCode(candidate.icode));
        cron.runJob(candidate);
        var skipped = cron.oneT(id)!;
        Assert.IsTrue(skipped.next_run_utc > DateTime.UtcNow);
        Assert.IsNull(skipped.run_requested_utc);
        Assert.IsNull(skipped.last_started_utc);
        Assert.IsNull(skipped.last_result);
        var events = first.model<FwActivityLogs>().listByEntity(FwEntities.ICODE_CRON, id);
        Assert.IsTrue(events.Any(row => row["idesc"].toStr().Contains("expired", StringComparison.Ordinal)));
        Assert.IsTrue(events.Any(row => row["idesc"].toStr().Contains("Skipped", StringComparison.Ordinal)));
        Assert.AreEqual(0, legacyCalls, "Generated report schedules retain their report-specific overdue policy.");

        var legacyId = cron.add(DB.h("icode", "report_weekly_dispatch", "cron", "@daily", "next_run_utc", stale));
        cron.runJob(cron.oneT(legacyId)!);
        Assert.AreEqual(1, legacyCalls, "A prefix alone must not redirect a fixed application handler to the report runner.");
        Assert.AreEqual(FwCron.RESULT_SUCCEEDED, FwCron.parseResult(cron.oneT(legacyId)!.last_result)["status"]);
    }

    public static void DeleteAndRestoreRespectTaskTypeAndClaim(string connection, string provider)
    {
        using var scope = FwConfig.beginScope();
        using var fw = context(connection, provider);
        var cron = fw.model<FwCron>();
        var now = DateTime.UtcNow;
        var reportId = fw.model<FwReports>().addSchedule("Sample", DB.h("cron", "@daily", "run_requested_utc", now));
        var report = cron.oneT(reportId)!;
        cron.oneByIcode(report.icode);

        fw.db.update("fwcron", DB.h("run_token", "report-claim"), DB.h("id", reportId));
        Assert.IsFalse(cron.deleteTask(reportId), "A claim obtained after the observed read must block permanent deletion.");
        fw.db.update("fwcron", DB.h("run_token", null), DB.h("id", reportId));

        Assert.IsTrue(cron.deleteTask(reportId));
        Assert.IsNull(cron.oneT(reportId));
        Assert.IsEmpty(cron.oneByIcode(report.icode));
        Assert.IsFalse(cron.restoreTask(reportId));
        Assert.HasCount(1, fw.model<FwActivityLogs>().listByEntity("fwcron", reportId, new[] { FwLogTypes.ICODE_DELETED }));
        var oldReportId = fw.model<FwReports>().addSchedule("Sample", DB.h("cron", "@daily", "status", FwModel.STATUS_DELETED));
        Assert.IsFalse(cron.restoreTask(oldReportId), "Previously soft-deleted report schedules cannot be restored.");
        Assert.IsTrue(cron.deleteTask(oldReportId), "Previously soft-deleted report schedules can be purged.");

        var taskId = cron.add(DB.h("icode", "report_weekly", "cron", "@weekly", "run_requested_utc", now));
        Assert.IsFalse(cron.restoreTask(taskId));
        Assert.IsTrue(cron.deleteTask(taskId));
        Assert.IsFalse(cron.deleteTask(taskId), "A fixed job code remains reserved while deleted.");
        Assert.AreEqual(FwModel.STATUS_DELETED, cron.oneT(taskId)!.status);
        Assert.AreEqual(FwModel.STATUS_DELETED, cron.oneTByIcode("report_weekly")!.status);
        Assert.IsNull(cron.oneT(taskId)!.run_requested_utc);

        fw.db.update("fwcron", DB.h("run_token", "application-claim", "run_requested_utc", now), DB.h("id", taskId));
        Assert.IsFalse(cron.restoreTask(taskId), "A held claim must block restoration even after an idle cached read.");
        fw.db.update("fwcron", DB.h("run_token", null), DB.h("id", taskId));
        Assert.IsTrue(cron.restoreTask(taskId));
        Assert.IsFalse(cron.restoreTask(taskId));
        Assert.AreEqual(FwModel.STATUS_INACTIVE, cron.oneT(taskId)!.status);
        Assert.AreEqual(FwModel.STATUS_INACTIVE, cron.oneTByIcode("report_weekly")!.status);
        Assert.IsNull(cron.oneT(taskId)!.run_requested_utc);
        Assert.IsFalse(cron.listDueJobs().Any(row => row.id == taskId));
        Assert.HasCount(1, fw.model<FwActivityLogs>().listByEntity("fwcron", taskId, new[] { FwLogTypes.ICODE_UPDATED }));
    }

    public static void UtcDatesSurviveDatabaseFold(string connection, string provider)
    {
        using var scope = FwConfig.beginScope();
        using var first = context(connection, provider, "Eastern Standard Time");
        var calls = 0;
        var cron = model(first, _ =>
        {
            calls++;
            return null;
        });

        // These different instants both display as 01:30 in Eastern, but remain distinct in storage.
        var firstFold = new DateTime(2026, 11, 1, 5, 30, 0, DateTimeKind.Utc);
        var secondFold = firstFold.AddHours(1);
        var id = cron.add(DB.h("icode", "fold_utc", "cron", "0 0 * * *", "status", FwModel.STATUS_INACTIVE,
            "start_date_utc", firstFold, "next_run_utc", secondFold));
        var observed = cron.oneT(id)!;
        Assert.AreEqual(firstFold, observed.start_date_utc);
        Assert.AreEqual(secondFold, observed.next_run_utc);
        Assert.AreEqual(DateTimeKind.Utc, observed.next_run_utc!.Value.Kind);
        Assert.AreEqual(1, first.db.valuep($"SELECT COUNT(*) FROM {cron.qTable()} WHERE id = @id AND start_date_utc = '2026-11-01 05:30:00' AND next_run_utc = '2026-11-01 06:30:00'", DB.h("id", id)).toInt(),
            "Raw storage must contain UTC, without a database-local conversion on either write or read.");

        Assert.IsTrue(cron.updateDefinition(id, observed, DB.h("end_date_utc", secondFold)));
        Assert.AreEqual(secondFold, cron.oneT(id)!.end_date_utc);
        cron.update(id, DB.h("end_date_utc", null));
        Assert.IsTrue(cron.resume(cron.oneT(id)!));
        Assert.IsTrue(cron.requestRun(id));
        cron.runJob(cron.oneT(id)!);
        Assert.AreEqual(1, calls, "A bound in a repeated local hour must not pause or block the task.");
        Assert.AreEqual(FwModel.STATUS_ACTIVE, cron.oneT(id)!.status);

        // The next recurrence itself falls in the repeated Eastern hour and must remain scheduled.
        var due = new DateTime(2025, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        var nextFold = cron.add(DB.h("icode", "fold_computed", "cron", "30 1 * 11 SUN#1",
            "timezone", "Eastern Standard Time", "next_run_utc", due));
        cron.runJob(cron.listDueJobs().Single(task => task.id == nextFold));
        var finished = cron.oneT(nextFold)!;
        Assert.AreEqual(2, calls);
        Assert.AreEqual(FwModel.STATUS_ACTIVE, finished.status);
        Assert.IsTrue(finished.next_run_utc > DateTime.UtcNow);
        var localNext = TimeZoneInfo.ConvertTimeFromUtc(finished.next_run_utc!.Value, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
        Assert.IsTrue(TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time").IsAmbiguousTime(localNext));
        Assert.IsNull(finished.run_token);
        Assert.AreEqual(FwCron.RESULT_SUCCEEDED, FwCron.parseResult(finished.last_result)["status"]);
    }

    public static void ExistingLegacyHandler(string connection, string provider)
    {
        using var scope = FwConfig.beginScope();
        using var first = context(connection, provider);
        var cron = first.model<FwCron>();
        var id = cron.add(DB.h("icode", FwCron.ICODE_EXAMPLE, "cron", "0 0 * * *"));

        cron.runJob(cron.listDueJobs().Single(task => task.id == id));
        var initialized = cron.oneT(id)!;
        Assert.IsTrue(initialized.next_run_utc > DateTime.UtcNow);
        Assert.IsNull(initialized.last_result);

        first.db.update("fwcron", DB.h("next_run_utc", DateTime.UtcNow.AddHours(-1), "start_date_utc", DateTime.UtcNow.AddDays(-1)), DB.h("id", id));
        cron.runJob(cron.listDueJobs().Single(task => task.id == id));
        var finished = cron.oneT(id)!;
        Assert.AreEqual(FwCron.RESULT_SUCCEEDED, FwCron.parseResult(finished.last_result)["status"]);
        Assert.IsTrue(finished.next_run_utc > DateTime.UtcNow);
    }

    public static void ExistingTaskUpgrade(string connection, string provider)
    {
        using var scope = FwConfig.beginScope();
        using var first = context(connection, provider, "Eastern Standard Time");
        var db = first.db;
        var cron = first.model<FwCron>();
        var updates = first.model<FwUpdates>();

        if (provider == DB.DBTYPE_SQLSRV)
            db.exec($"ALTER TABLE {cron.qTable()} DROP CONSTRAINT DF_fwcron_timezone");

        // Recreate the pre-feature fwcron shape on the disposable provider fixture.
        db.exec(provider == DB.DBTYPE_SQLSRV
            ? $"DROP INDEX IX_fwcron_next_run_utc ON {cron.qTable()}"
            : "DROP INDEX IX_fwcron_next_run_utc");
        foreach (var column in new[] { "next_run_utc", "start_date_utc", "end_date_utc", "timezone", "payload", "run_requested_utc", "run_token", "last_started_utc", "last_finished_utc", "last_result" })
            db.exec($"ALTER TABLE {cron.qTable()} DROP COLUMN {db.qid(column)}");
        foreach (var column in new[] { "next_run", "start_date", "end_date" })
            db.exec($"ALTER TABLE {cron.qTable()} ADD {db.qid(column)} DATETIME NULL");
        db.del("fwentities", DB.h("icode", FwEntities.ICODE_CRON));
        db.del("log_types", DB.h("icode", FwLogTypes.ICODE_CRON_EVENT));

        var due = new DateTime(2099, 11, 3, 12, 34, 0, DateTimeKind.Utc);
        var id = cron.add(DB.h("icode", FwCron.ICODE_EXAMPLE, "iname", "Original task", "cron", "0 0 * * *",
            "next_run", due, "start_date", due.AddDays(-7), "end_date", due.AddDays(7), "status", FwModel.STATUS_INACTIVE));
        updates.loadUpdates();
        var update = updates.listPending().Single(row => row["iname"].toStr() == "upd2026-11-07-scheduled-tasks.sql");
        updates.applyOne(update["id"].toInt());

        var unmigrated = cron.oneT(id)!;
        Assert.IsNull(unmigrated.next_run_utc, "The update must not guess how to convert existing dates.");
        Assert.IsNull(unmigrated.start_date_utc);
        Assert.IsNull(unmigrated.end_date_utc);

        // Application-owned migration: the configured original DB zone converts these unambiguous values to UTC.
        var legacy = db.row("fwcron", DB.h("id", id));
        Assert.AreEqual(due, legacy["next_run"].toDate());
        cron.update(id, DB.h("next_run_utc", legacy["next_run"], "start_date_utc", legacy["start_date"], "end_date_utc", legacy["end_date"]));
        var task = cron.oneT(id)!;
        Assert.AreEqual(FwCron.ICODE_EXAMPLE, task.icode);
        Assert.AreEqual("Original task", task.iname);
        Assert.AreEqual("0 0 * * *", task.cron);
        Assert.AreEqual(due, task.next_run_utc);
        Assert.AreEqual(due.AddDays(-7), task.start_date_utc);
        Assert.AreEqual(due.AddDays(7), task.end_date_utc);
        Assert.AreEqual(FwModel.STATUS_INACTIVE, task.status);
        Assert.AreEqual("UTC", task.timezone);
        Assert.IsNull(task.payload);
        Assert.IsNull(task.last_started_utc);
        Assert.IsNull(task.last_result);

        Assert.IsTrue(cron.requestRun(id));
        Assert.IsTrue(first.model<FwActivityLogs>().listByEntity(FwEntities.ICODE_CRON, id)
            .Any(row => row["idesc"].toStr().StartsWith("Manual run requested", StringComparison.Ordinal)));
    }
}

#if isSQLite
[TestClass]
public class FwCronSqliteTests
{
    private SqliteFile file = null!;

    [TestInitialize]
    public void Setup()
    {
        file = new SqliteFile();
        using var db = file.CreateDb("main");
        var sql = Path.Combine(TestHelpers.RepoRoot(), "osafw-app", "App_Data", "sql", "sqlite");
        db.execMultipleSQL(File.ReadAllText(Path.Combine(sql, "fwdatabase.sql")));
        db.execMultipleSQL(File.ReadAllText(Path.Combine(sql, "lookups.sql")));
    }

    [TestCleanup]
    public void Cleanup() => file?.Dispose();

    [TestMethod]
    public Task OneClaim() => FwCronProviderChecks.OneClaim(file.ConnectionString, DB.DBTYPE_SQLITE);

    [TestMethod]
    public void ExistingTaskUpgrade() => FwCronProviderChecks.ExistingTaskUpgrade(file.ConnectionString, DB.DBTYPE_SQLITE);

    [TestMethod]
    public void ExistingLegacyHandler() => FwCronProviderChecks.ExistingLegacyHandler(file.ConnectionString, DB.DBTYPE_SQLITE);

    [TestMethod]
    public void UtcDatesSurviveDatabaseFold() => FwCronProviderChecks.UtcDatesSurviveDatabaseFold(file.ConnectionString, DB.DBTYPE_SQLITE);

    [TestMethod]
    public void ConditionalModelUpdate() => FwCronProviderChecks.ConditionalModelUpdate(file.ConnectionString, DB.DBTYPE_SQLITE);

    [TestMethod]
    public void FailureDiagnostics() => FwCronProviderChecks.FailureDiagnostics(file.ConnectionString, DB.DBTYPE_SQLITE);

    [TestMethod]
    public void ManualRecoveryAndFailure() => FwCronProviderChecks.ManualRecoveryAndFailure(file.ConnectionString, DB.DBTYPE_SQLITE);

    [TestMethod]
    public void ReportIdentityAndSkippedWork() => FwCronProviderChecks.ReportIdentityAndSkippedWork(file.ConnectionString, DB.DBTYPE_SQLITE);

    [TestMethod]
    public void DeleteAndRestoreRespectTaskTypeAndClaim() => FwCronProviderChecks.DeleteAndRestoreRespectTaskTypeAndClaim(file.ConnectionString, DB.DBTYPE_SQLITE);
}
#endif

[TestClass, TestCategory("SqlServer")]
public class FwCronSqlServerTests
{
    private static SqlServerTestDatabase? database;

    [ClassInitialize]
    public static void Setup(TestContext context)
    {
        if (Environment.GetEnvironmentVariable("FW_TEST_SQLSERVER") != "1")
            Assert.Inconclusive("Use scripts/test.ps1 -Profile SqlServer to provision an isolated database.");
        database = new SqlServerTestDatabase();
    }

    [ClassCleanup]
    public static void Cleanup() => database?.Dispose();

    [TestMethod]
    public Task OneClaim() => FwCronProviderChecks.OneClaim(database!.ConnectionString, DB.DBTYPE_SQLSRV);

    [TestMethod]
    public void ExistingTaskUpgrade()
    {
        using var legacy = new SqlServerTestDatabase();
        FwCronProviderChecks.ExistingTaskUpgrade(legacy.ConnectionString, DB.DBTYPE_SQLSRV);
    }

    [TestMethod]
    public void ConditionalModelUpdate() => FwCronProviderChecks.ConditionalModelUpdate(database!.ConnectionString, DB.DBTYPE_SQLSRV);

    [TestMethod]
    public void FailureDiagnostics() => FwCronProviderChecks.FailureDiagnostics(database!.ConnectionString, DB.DBTYPE_SQLSRV);

    [TestMethod]
    public void ManualRecoveryAndFailure() => FwCronProviderChecks.ManualRecoveryAndFailure(database!.ConnectionString, DB.DBTYPE_SQLSRV);

    [TestMethod]
    public void UtcDatesSurviveDatabaseFold() => FwCronProviderChecks.UtcDatesSurviveDatabaseFold(database!.ConnectionString, DB.DBTYPE_SQLSRV);

    [TestMethod]
    public void ReportIdentityAndSkippedWork() => FwCronProviderChecks.ReportIdentityAndSkippedWork(database!.ConnectionString, DB.DBTYPE_SQLSRV);

    [TestMethod]
    public void DeleteAndRestoreRespectTaskTypeAndClaim() => FwCronProviderChecks.DeleteAndRestoreRespectTaskTypeAndClaim(database!.ConnectionString, DB.DBTYPE_SQLSRV);
}
