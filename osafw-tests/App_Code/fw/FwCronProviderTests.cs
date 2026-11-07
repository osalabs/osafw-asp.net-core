using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace osafw.Tests;

internal static class FwCronProviderChecks
{
    private sealed class TestCron(Func<TFwCron, FwDict?> run) : FwCron
    {
        protected override FwDict? runJobAction(TFwCron job) => run(job);
    }

    private sealed class CollisionDb(string connection, string provider) : DB(connection, provider)
    {
        public int Attempts { get; private set; }
        public bool IsForceOtherError { get; set; }

        public override int insert(string table, FwDict fields)
        {
            if (table == "fwcron" && ++Attempts == 1)
            {
                if (IsForceOtherError)
                    return base.insert(table, DB.h("icode", fields["icode"]));

                var code = fields["icode"].toStr();
                if (dbtype == DBTYPE_SQLSRV && valuep("SELECT CASE WHEN 'Abc' = 'ABC' THEN 1 ELSE 0 END").toInt() == 1)
                    code = code.ToUpperInvariant();
                base.insert(table, DB.h("icode", code, "cron", "0 0 * * *"));
            }
            return base.insert(table, fields);
        }
    }

    private static FW context(string connection, string provider, string timezone = "UTC", DB? db = null) =>
        new(null, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["appSettings:site_root"] = Path.Combine(TestHelpers.RepoRoot(), "osafw-app"),
            ["appSettings:log_level"] = "0"
        }).Build(), _ => db ?? new DB(DB.h(
            "connection_string", connection, "type", provider, "timezone", timezone), "main"));

    private static TestCron model(FW fw, Func<TFwCron, FwDict?> run)
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
        var id = firstCron.add(DB.h("icode", "claim_test", "cron", "0 0 * * *", "next_run", due));
        var stale = secondCron.listDueJobs().Single(task => task.id == id);
        Assert.AreEqual(due, stale.next_run, "Existing datetime fields use the DB timezone on read and write.");
        Assert.IsTrue(firstCron.saveDefinition(id, stale, DB.h("payload", "current payload")));
        Assert.IsTrue(firstCron.requestRun(id));
        var firstRun = Task.Run(() => firstCron.runJob(stale));
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(20)), "The worker did not enter the handler.");
            var held = secondCron.getTask(id)!;
            Assert.IsNotNull(held.run_token);
            Assert.IsNull(held.run_requested_utc, "Manual and scheduled work coalesce in the same claim.");
            Assert.AreEqual(DateTimeKind.Utc, held.last_started_utc!.Value.Kind);
            Assert.IsTrue((DateTime.UtcNow - held.last_started_utc.Value).Duration() < TimeSpan.FromSeconds(30));
            Assert.IsFalse(secondCron.requestRun(id));
            Assert.IsFalse(secondCron.saveDefinition(id, held, DB.h("payload", "obsolete payload")));
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
        var finished = secondCron.getTask(id)!;
        Assert.IsNull(finished.run_token);
        Assert.IsNotNull(finished.last_finished_utc);
        Assert.IsTrue(finished.next_run > DateTime.UtcNow);
        var result = FwCron.parseResult(finished.last_result);
        Assert.AreEqual(FwCron.RESULT_SUCCEEDED, result["status"]);
        Assert.AreEqual(3, ((FwDict)result["data"]!)["processed_count"].toInt());
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
        var id = cron.add(DB.h("icode", "manual_test", "cron", "0 0 * * *", "next_run", next, "start_date", next));
        Assert.IsTrue(cron.requestRun(id));
        Assert.IsFalse(admin.requestRun(id));
        cron.runJob(cron.getTask(id)!);
        var paused = cron.getTask(id)!;
        Assert.AreEqual(FwModel.STATUS_INACTIVE, paused.status, "Finishing does not undo a concurrent pause.");
        Assert.AreEqual(next, paused.next_run, "An early manual run preserves the next occurrence.");
        var result = FwCron.parseResult(paused.last_result);
        Assert.AreEqual(FwCron.RESULT_SUCCEEDED, result["status"]);
        Assert.IsFalse(result.ContainsKey("data"));

        first.db.update("fwcron", DB.h("run_token", "AbcHeld"), DB.h("id", id));
        var held = cron.getTask(id)!;
        cron.runJob(held);
        Assert.AreEqual(1, calls);
        Assert.IsFalse(admin.recover(id, "abcHeld"), "Recovery compares the exact observed token even under SQL Server collation.");
        Assert.IsTrue(admin.recover(id, "AbcHeld"));
        var recovered = cron.getTask(id)!;
        Assert.AreEqual(FwCron.RESULT_UNCERTAIN, FwCron.parseResult(recovered.last_result)["status"]);
        Assert.AreEqual(FwModel.STATUS_INACTIVE, recovered.status);
        Assert.IsNull(recovered.run_token);
        Assert.IsTrue(admin.requestRun(id));
        Assert.IsTrue(admin.pause(id), "Pause can cancel a manual request queued on an already-paused task.");
        Assert.IsNull(admin.getTask(id)!.run_requested_utc);

        var failingId = cron.add(DB.h("icode", "failing_test", "cron", "0 0 * * *", "next_run", next.AddDays(-3)));
        var failing = cron.getTask(failingId)!;
        cron.runJob(failing);
        cron.runJob(failing);
        Assert.AreEqual(2, calls, "The failed occurrence advances without per-minute retries.");
        var failed = cron.getTask(failingId)!;
        Assert.IsTrue(failed.next_run > DateTime.UtcNow);
        Assert.AreEqual(FwCron.RESULT_FAILED, FwCron.parseResult(failed.last_result)["status"]);
        Assert.IsFalse(failed.last_result!.Contains("sensitive test message", StringComparison.Ordinal));
    }

    public static void ReportIdentityAndSkippedWork(string connection, string provider)
    {
        using var scope = FwConfig.beginScope();
        using var first = context(connection, provider);
        var cron = first.model<FwCron>();
        var stale = DateTime.UtcNow.AddHours(-1);
        var fields = DB.h("cron", "0 0 * * *", "next_run", stale, "run_requested_utc", stale);
        var id = cron.addReport("Sample_Archive", fields);
        cron.addReport("Sample", fields);
        cron.addReport("SampleXArchive", fields);

        var parameters = new FwDict();
        var predicate = cron.reportFilterSql("Sample_Archive", parameters);
        var matched = first.db.arrayp<TFwCron>($"SELECT * FROM {cron.qTable()} WHERE {predicate}", parameters);
        Assert.AreEqual(id, matched.Single().id);

        var candidate = cron.getTask(id)!;
        Assert.AreEqual("Sample_Archive", FwCron.reportCode(candidate.icode));
        cron.runJob(candidate);
        var skipped = cron.getTask(id)!;
        Assert.IsTrue(skipped.next_run > DateTime.UtcNow);
        Assert.IsNull(skipped.run_requested_utc);
        Assert.IsNull(skipped.last_started_utc);
        Assert.IsNull(skipped.last_result);
        var events = first.model<FwActivityLogs>().listByEntity(FwEntities.ICODE_CRON, id);
        Assert.IsTrue(events.Any(row => row["idesc"].toStr().Contains("expired", StringComparison.Ordinal)));
        Assert.IsTrue(events.Any(row => row["idesc"].toStr().Contains("Skipped", StringComparison.Ordinal)));
    }

    public static void DatabaseFoldGuard(string connection, string provider)
    {
        using var scope = FwConfig.beginScope();
        using var first = context(connection, provider, "Eastern Standard Time");
        var calls = 0;
        var cron = model(first, _ =>
        {
            calls++;
            return null;
        });

        // Eastern's clock reads 01:30 twice. The generic DB contract reads the same local value as standard time.
        var firstFold = new DateTime(2026, 11, 1, 5, 30, 0, DateTimeKind.Utc);
        var secondFold = firstFold.AddHours(1);
        var beforeFold = firstFold.AddHours(-1);
        var afterFold = secondFold.AddHours(1);
        Assert.ThrowsExactly<UserException>(() => cron.add(DB.h("icode", "fold_rejected", "cron", "0 0 * * *",
            "next_run", firstFold)));

        var safe = cron.add(DB.h("icode", "fold_adjacent", "cron", "0 0 * * *",
            "start_date", beforeFold, "next_run", afterFold));
        var observed = cron.getTask(safe)!;
        Assert.AreEqual(beforeFold, observed.start_date);
        Assert.AreEqual(afterFold, observed.next_run);
        Assert.ThrowsExactly<UserException>(() => cron.saveDefinition(safe, observed, DB.h("end_date", secondFold)));
        Assert.ThrowsExactly<UserException>(() => cron.update(safe, DB.h("next_run", firstFold)));
        Assert.IsTrue(cron.requestRun(safe));
        cron.runJob(observed);
        Assert.AreEqual(1, calls, "Non-ambiguous neighboring instants still support manual execution.");

        // Raw insertion simulates a pre-upgrade write whose original UTC offset was lost on storage.
        var existing = first.db.insert("fwcron", DB.h("icode", "fold_existing", "cron", "0 0 * * *",
            "start_date", firstFold, "next_run", afterFold));
        var legacy = cron.getTask(existing)!;
        Assert.AreEqual(secondFold, legacy.start_date);
        Assert.IsTrue(cron.requestRun(existing));
        cron.runJob(legacy);
        var paused = cron.getTask(existing)!;
        Assert.AreEqual(1, calls, "The worker must not use an ambiguous legacy bound for delivery.");
        Assert.AreEqual(FwModel.STATUS_INACTIVE, paused.status);
        Assert.IsNull(paused.run_token);
        Assert.IsNull(paused.run_requested_utc);
        Assert.IsNull(paused.last_started_utc);
        Assert.IsNull(paused.last_result);
        Assert.ThrowsExactly<UserException>(() => cron.resume(paused));
        Assert.IsTrue(first.model<FwActivityLogs>().listByEntity(FwEntities.ICODE_CRON, existing)
            .Any(row => row["idesc"].toStr().Contains("repeated local hour", StringComparison.Ordinal)));

        // This safe due instant can run. Its next recurrence is always in the repeated Eastern hour.
        var due = new DateTime(2025, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        var nextFold = cron.add(DB.h("icode", "fold_computed", "cron", "30 1 * 11 SUN#1",
            "timezone", "Eastern Standard Time", "next_run", due));
        cron.runJob(cron.getTask(nextFold)!);
        var finished = cron.getTask(nextFold)!;
        Assert.AreEqual(2, calls);
        Assert.AreEqual(FwModel.STATUS_INACTIVE, finished.status);
        Assert.IsNull(finished.next_run);
        Assert.IsNull(finished.run_token);
        Assert.AreEqual(FwCron.RESULT_SUCCEEDED, FwCron.parseResult(finished.last_result)["status"]);
    }

    public static void ReportIdentityUsesProviderCaseRules(string connection, string provider)
    {
        using var scope = FwConfig.beginScope();
        using var first = context(connection, provider);
        var cron = first.model<FwCron>();
        var fields = DB.h("cron", "0 0 * * *");
        var upper = cron.addReport("Case_Report", fields);
        var lower = cron.addReport("case_report", fields);
        cron.addReport("Case_Report_Archive", fields);
        cron.addReport("CaseXReport", fields);

        int[] matchingIds(string code, string source)
        {
            var parameters = new FwDict();
            var predicate = cron.reportFilterSql(code, parameters);
            return first.db.arrayp<TFwCron>($"SELECT id FROM {source} WHERE {predicate}", parameters)
                .Select(row => row.id).ToArray();
        }

        var table = cron.qTable();
        if (provider == DB.DBTYPE_SQLITE)
        {
            CollectionAssert.AreEqual(new[] { upper }, matchingIds("Case_Report", table));
            CollectionAssert.AreEqual(new[] { lower }, matchingIds("case_report", table));
            return;
        }

        // A SQL Server task code inherits the column collation. Exercise both supported case rules
        // without changing the isolated server/database collation.
        var caseSensitive = $"(SELECT id, icode COLLATE Latin1_General_100_CS_AS AS icode FROM {table}) AS tasks";
        CollectionAssert.AreEqual(new[] { upper }, matchingIds("Case_Report", caseSensitive));
        CollectionAssert.AreEqual(new[] { lower }, matchingIds("case_report", caseSensitive));

        var caseInsensitive = $"(SELECT id, icode COLLATE Latin1_General_100_CI_AS AS icode FROM {table}) AS tasks";
        CollectionAssert.AreEquivalent(new[] { upper, lower }, matchingIds("Case_Report", caseInsensitive));
        CollectionAssert.AreEquivalent(new[] { upper, lower }, matchingIds("case_report", caseInsensitive));
    }

    public static void ExistingLegacyHandler(string connection, string provider)
    {
        using var scope = FwConfig.beginScope();
        using var first = context(connection, provider);
        var cron = first.model<FwCron>();
        var id = cron.add(DB.h("icode", FwCron.ICODE_EXAMPLE, "cron", "0 0 * * *"));

        cron.runJob(cron.listDueJobs().Single(task => task.id == id));
        var initialized = cron.getTask(id)!;
        Assert.IsTrue(initialized.next_run > DateTime.UtcNow);
        Assert.IsNull(initialized.last_result);

        first.db.update("fwcron", DB.h("next_run", DateTime.UtcNow.AddHours(-1), "start_date", DateTime.UtcNow.AddDays(-1)), DB.h("id", id));
        cron.runJob(cron.listDueJobs().Single(task => task.id == id));
        var finished = cron.getTask(id)!;
        Assert.AreEqual(FwCron.RESULT_SUCCEEDED, FwCron.parseResult(finished.last_result)["status"]);
        Assert.IsTrue(finished.next_run > DateTime.UtcNow);
    }

    public static void ExistingTaskUpgrade(string connection, string provider)
    {
        using var scope = FwConfig.beginScope();
        using var first = context(connection, provider);
        var db = first.db;
        var cron = first.model<FwCron>();
        var updates = first.model<FwUpdates>();

        if (provider == DB.DBTYPE_SQLSRV)
            db.exec($"ALTER TABLE {cron.qTable()} DROP CONSTRAINT DF_fwcron_timezone");

        // Recreate the pre-feature fwcron shape on the disposable provider fixture.
        foreach (var column in new[] { "timezone", "payload", "run_requested_utc", "run_token", "last_started_utc", "last_finished_utc", "last_result" })
            db.exec($"ALTER TABLE {cron.qTable()} DROP COLUMN {db.qid(column)}");
        db.del("fwentities", DB.h("icode", FwEntities.ICODE_CRON));
        db.del("log_types", DB.h("icode", FwLogTypes.ICODE_CRON_EVENT));

        var due = new DateTime(2099, 11, 3, 12, 34, 0, DateTimeKind.Utc);
        var id = cron.add(DB.h("icode", FwCron.ICODE_EXAMPLE, "iname", "Original task", "cron", "0 0 * * *",
            "next_run", due, "start_date", due.AddDays(-7), "end_date", due.AddDays(7), "status", FwModel.STATUS_INACTIVE));
        updates.loadUpdates();
        var update = updates.listPending().Single(row => row["iname"].toStr() == "upd2026-11-07-scheduled-tasks.sql");
        updates.applyOne(update["id"].toInt());

        var task = cron.getTask(id)!;
        Assert.AreEqual(FwCron.ICODE_EXAMPLE, task.icode);
        Assert.AreEqual("Original task", task.iname);
        Assert.AreEqual("0 0 * * *", task.cron);
        Assert.AreEqual(due, task.next_run);
        Assert.AreEqual(due.AddDays(-7), task.start_date);
        Assert.AreEqual(due.AddDays(7), task.end_date);
        Assert.AreEqual(FwModel.STATUS_INACTIVE, task.status);
        Assert.AreEqual("UTC", task.timezone);
        Assert.IsNull(task.payload);
        Assert.IsNull(task.last_started_utc);
        Assert.IsNull(task.last_result);

        Assert.IsTrue(cron.requestRun(id));
        Assert.IsTrue(first.model<FwActivityLogs>().listByEntity(FwEntities.ICODE_CRON, id)
            .Any(row => row["idesc"].toStr().StartsWith("Manual run requested", StringComparison.Ordinal)));
    }

    public static void GeneratedCodeRetriesOnlyUniqueCodeConflicts(string connection, string provider)
    {
        using var scope = FwConfig.beginScope();
        using var collision = new CollisionDb(connection, provider);
        using var first = context(connection, provider, db: collision);
        var cron = first.model<FwCron>();
        var id = cron.addReport("Collision", DB.h("cron", "0 0 * * *"));
        Assert.AreEqual(2, collision.Attempts);
        Assert.AreEqual("Collision", FwCron.reportCode(cron.getTask(id)!.icode));

        using var broken = new CollisionDb(connection, provider) { IsForceOtherError = true };
        using var second = context(connection, provider, db: broken);
        try
        {
            second.model<FwCron>().addReport("ConstraintFailure", DB.h("cron", "0 0 * * *"));
            Assert.Fail("An unrelated constraint failure must be surfaced.");
        }
        catch (DbException)
        {
            Assert.AreEqual(1, broken.Attempts);
        }
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
    public void DatabaseFoldGuard() => FwCronProviderChecks.DatabaseFoldGuard(file.ConnectionString, DB.DBTYPE_SQLITE);

    [TestMethod]
    public void ManualRecoveryAndFailure() => FwCronProviderChecks.ManualRecoveryAndFailure(file.ConnectionString, DB.DBTYPE_SQLITE);

    [TestMethod]
    public void ReportIdentityAndSkippedWork() => FwCronProviderChecks.ReportIdentityAndSkippedWork(file.ConnectionString, DB.DBTYPE_SQLITE);

    [TestMethod]
    public void ReportIdentityUsesProviderCaseRules() =>
        FwCronProviderChecks.ReportIdentityUsesProviderCaseRules(file.ConnectionString, DB.DBTYPE_SQLITE);

    [TestMethod]
    public void GeneratedCodeRetriesOnlyUniqueCodeConflicts() =>
        FwCronProviderChecks.GeneratedCodeRetriesOnlyUniqueCodeConflicts(file.ConnectionString, DB.DBTYPE_SQLITE);
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
    public void ManualRecoveryAndFailure() => FwCronProviderChecks.ManualRecoveryAndFailure(database!.ConnectionString, DB.DBTYPE_SQLSRV);

    [TestMethod]
    public void DatabaseFoldGuard() => FwCronProviderChecks.DatabaseFoldGuard(database!.ConnectionString, DB.DBTYPE_SQLSRV);

    [TestMethod]
    public void ReportIdentityAndSkippedWork() => FwCronProviderChecks.ReportIdentityAndSkippedWork(database!.ConnectionString, DB.DBTYPE_SQLSRV);

    [TestMethod]
    public void ReportIdentityUsesProviderCaseRules() =>
        FwCronProviderChecks.ReportIdentityUsesProviderCaseRules(database!.ConnectionString, DB.DBTYPE_SQLSRV);

    [TestMethod]
    public void GeneratedCodeRetriesOnlyUniqueCodeConflicts() =>
        FwCronProviderChecks.GeneratedCodeRetriesOnlyUniqueCodeConflicts(database!.ConnectionString, DB.DBTYPE_SQLSRV);
}
