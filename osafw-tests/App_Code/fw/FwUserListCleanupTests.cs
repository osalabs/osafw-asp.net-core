using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class FwUserListCleanupTests
{
#if isSQLite
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void StandardDelete_RemovesLinksOnlyAfterPhysicalDeletion(bool is_dynamic, bool is_bulk)
    {
        using var file = new SqliteFile();
        using var scope = new FwTestScope(file.CreateDb, context: TestHelpers.CreateHttpContext());
        var fw = prepareSqlite(scope.Fw);
        loadScript(fw, "demo.sql");

        var classic = new AdminDemosController();
        var dynamic_controller = new AdminDemosDynamicController();
        FwController controller = is_dynamic ? dynamic_controller : classic;
        controller.init(fw);

        var record = fw.db.insert("demos", DB.h("email", "list-cleanup@example.test"));
        var first_list = addList(fw, controller.base_url, 1, record);
        var second_list = addList(fw, controller.base_url, 2, record);
        var other_entity_list = addList(fw, "/Admin/Other", 1, record);
        var demos = fw.model<Demos>();
        var lists = fw.model<UserLists>();

        void delete()
        {
            if (is_bulk)
                selectForDelete(fw, record);

            if (is_dynamic)
            {
                if (is_bulk)
                    dynamic_controller.SaveMultiAction();
                else
                    dynamic_controller.DeleteAction(record);
            }
            else if (is_bulk)
            {
                classic.SaveMultiAction();
            }
            else
            {
                classic.DeleteAction(record);
            }
        }

        delete();
        Assert.AreEqual(FwModel.STATUS_DELETED, demos.one(record)["status"].toInt());
        Assert.AreEqual(1, lists.countItems(first_list));
        Assert.AreEqual(1, lists.countItems(second_list));

        delete();
        Assert.IsEmpty(demos.one(record));
        Assert.AreEqual(0, lists.countItems(first_list));
        Assert.AreEqual(0, lists.countItems(second_list));
        Assert.AreEqual(1, lists.countItems(other_entity_list));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ScheduledTaskDelete_PurgesReportLinksAndPreservesHeldAndRestorableLinks(bool is_bulk)
    {
        using var file = new SqliteFile();
        using var scope = new FwTestScope(file.CreateDb, context: TestHelpers.CreateHttpContext());
        scheduledTaskDelete(prepareSqlite(scope.Fw), is_bulk);
    }

    private static FW prepareSqlite(FW fw)
    {
        loadScript(fw, "fwdatabase.sql");
        loadScript(fw, "lookups.sql");
        return prepare(fw);
    }

    private static void loadScript(FW fw, string file_name)
    {
        var script = Path.Combine(TestHelpers.RepoRoot(), "osafw-app", "App_Data", "sql", "sqlite", file_name);
        fw.db.execMultipleSQL(File.ReadAllText(script));
    }
#endif

    [TestMethod, TestCategory("SqlServer")]
    public void SqlServer_ScheduledTaskBulkDelete_RemovesOnlyPurgedReportLinks()
    {
        if (Environment.GetEnvironmentVariable("FW_TEST_SQLSERVER") != "1")
            Assert.Inconclusive("Use scripts/test.ps1 -Profile SqlServer to provision an isolated database.");

        using var database = new SqlServerTestDatabase();
        using var scope = new FwTestScope(
            name => new DB(database.ConnectionString, DB.DBTYPE_SQLSRV, name),
            context: TestHelpers.CreateHttpContext());
        scheduledTaskDelete(prepare(scope.Fw), is_bulk: true);
    }

    private static void scheduledTaskDelete(FW fw, bool is_bulk)
    {
        using var response = new MemoryStream();
        fw.response.Body = response;

        var controller = new AdminCronController();
        controller.init(fw);
        var report = fw.db.insert("fwcron", DB.h("icode", "report_cleanup:0123456789", "cron", "0 7 * * *"));
        var held = fw.db.insert("fwcron", DB.h("icode", "report_held:0123456789", "cron", "0 7 * * *", "run_token", "held"));
        var application = fw.db.insert("fwcron", DB.h("icode", "application_cleanup", "cron", "0 7 * * *"));
        var report_list = addList(fw, controller.base_url, 1, report);
        var other_owner_list = addList(fw, controller.base_url, 2, report);
        var held_list = addList(fw, controller.base_url, 1, held);
        var application_list = addList(fw, controller.base_url, 1, application);
        var other_entity_list = addList(fw, "/Admin/Other", 1, report);
        var cron = fw.model<FwCron>();
        var lists = fw.model<UserLists>();

        if (is_bulk)
        {
            selectForDelete(fw, report, application, held);
            var result = controller.SaveMultiAction();
            Assert.AreEqual(2, ((FwDict)result!["_json"]!)["ctr"].toInt());
        }
        else
        {
            controller.DeleteAction(report);
            controller.DeleteAction(application);
            controller.DeleteAction(held);
            Assert.AreEqual(400, fw.response.StatusCode);
        }

        Assert.IsNull(cron.oneT(report));
        Assert.AreEqual("held", cron.oneT(held)?.run_token);
        Assert.AreEqual(FwModel.STATUS_DELETED, cron.oneT(application)?.status);
        Assert.AreEqual(0, lists.countItems(report_list));
        Assert.AreEqual(0, lists.countItems(other_owner_list));
        Assert.AreEqual(1, lists.countItems(held_list));
        Assert.AreEqual(1, lists.countItems(application_list));
        Assert.AreEqual(1, lists.countItems(other_entity_list));
    }

    private static FW prepare(FW fw)
    {
        fw.config()["template"] = TestHelpers.TemplateRoot();
        fw.model<Users>().reloadSession(1);
        fw.request.Method = "POST";
        fw.route.method = "POST";
        fw.request.Headers.Accept = "application/json";
        fw.Session("XSS", "valid");
        fw.FORM["XSS"] = "valid";
        return fw;
    }

    private static int addList(FW fw, string entity, int owner, int record)
    {
        var list = fw.db.insert("user_lists", DB.h("entity", entity, "add_users_id", owner, "iname", "Cleanup test"));
        fw.db.insert("user_lists_items", DB.h("user_lists_id", list, "item_id", record, "add_users_id", owner));
        return list;
    }

    private static void selectForDelete(FW fw, params int[] ids)
    {
        FwDict selected = [];
        foreach (var id in ids)
            selected[id.toStr()] = "1";

        fw.FORM["cb"] = selected;
        fw.FORM["delete"] = "1";
    }
}
