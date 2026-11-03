using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace osafw.Tests;

[TestClass, TestCategory("SqlServer")]
public class ObjectPermissionsSqlServerTests
{
    [TestMethod]
    public void NativeSqlAutocompleteAndAttachmentMutationPreserveObjectBoundaries()
    {
        if (Environment.GetEnvironmentVariable("FW_TEST_SQLSERVER") != "1")
            Assert.Inconclusive("Run the SqlServer profile.");
        using var database = new SqlServerTestDatabase();
        using var scope = new FwTestScope(name => new DB(database.ConnectionString, DB.DBTYPE_SQLSRV, name),
            context: TestHelpers.CreateHttpContext());
        var fw = scope.Fw;
        fw.Session("user_id", "1");
        fw.Session("access_level", Users.ACL_MANAGER.toStr());
        foreach (var table in new[] { "user_views", "user_filters", "user_lists" })
        {
            var owner = DB.h("iname", "Native Z mine", "add_users_id", 1);
            var other = DB.h("iname", "Native A private", "add_users_id", 2);
            owner[table == "user_lists" ? "entity" : "icode"] = "native-test";
            other[table == "user_lists" ? "entity" : "icode"] = "native-test";
            fw.db.insert(table, owner);
            fw.db.insert(table, other);
        }
        foreach (var model in new FwModel[] { fw.model<UserViews>(), fw.model<UserFilters>(), fw.model<UserLists>() })
        {
            var rows = model.listSelectOptionsAutocomplete("Native", limit: 1);
            Assert.HasCount(1, rows);
            Assert.AreEqual("Native Z mine", rows[0]["iname"]);
        }

        var entity = fw.model<FwEntities>().idByIcodeOrAdd("demos");
        var privateId = fw.db.insert("att", DB.h("icode", "native-private", "iname", "Native private file",
            "fwentities_id", entity, "item_id", 1, "fsize", 10));
        fw.db.insert("att", DB.h("icode", "native-library", "iname", "Native library file", "fsize", 10));
        var controller = new AdminAttController();
        controller.init(fw);
        fw.FORM["q"] = "Native";
        var items = (StrList)controller.QuickSearchAction()["_json"]!;
        Assert.HasCount(1, items);
        Assert.IsTrue(items[0].Contains("library", StringComparison.Ordinal));
        fw.FORM["item"] = DB.h("iname", "Forged", "fwentities_id", 0, "item_id", 0);
        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction(privateId));
        Assert.AreEqual(entity, fw.db.value("att", DB.h("id", privateId), "fwentities_id").toInt());
    }
}
