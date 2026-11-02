#if isSQLite
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace osafw.Tests;

[TestClass]
public class PreferenceAutocompleteTests
{
    private sealed class SearchUsers : Users
    {
        public override bool isReadOnly(int id = -1) => false;
        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    [TestMethod]
    public void AttachmentQuickSearchDoesNotDiscloseBoundNamesOrIds()
    {
        using var database = new SqliteFile();
        using var scope = new FwTestScope(database.CreateDb, context: TestHelpers.CreateHttpContext());
        var fw = scope.Fw;
        fw.db.exec("CREATE TABLE att (id INTEGER PRIMARY KEY, iname TEXT, status INT, fwentities_id INT)");
        fw.db.exec("INSERT INTO att VALUES (1,'Alpha private',0,7),(2,'Alpha library',0,0),(3,'Alpha null',0,NULL),(4,'Alpha inactive',10,0)");
        var users = new SearchUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        var controller = new AdminAttController();
        controller.init(fw);
        fw.FORM["q"] = "1";
        Assert.IsEmpty((StrList)controller.QuickSearchAction()["_json"]!);
        fw.FORM["q"] = "Alpha";
        var items = (StrList)controller.QuickSearchAction()["_json"]!;
        Assert.HasCount(2, items);
        Assert.IsFalse(items.Any(item => item.Contains("private") || item.Contains("inactive")));
        Assert.IsTrue(items.Any(item => item.Contains("library")));
    }

    [TestMethod]
    public void AutocompleteFiltersOwnersBeforeLimitAndKeepsSystemRows()
    {
        using var database = new SqliteFile();
        using var scope = new FwTestScope(database.CreateDb, context: TestHelpers.CreateHttpContext());
        var fw = scope.Fw;
        fw.Session("user_id", "10");
        foreach (var table in new[] { "user_views", "user_filters", "user_lists" })
        {
            fw.db.exec($"CREATE TABLE {table} (id INTEGER PRIMARY KEY, iname TEXT, status INT, add_users_id INT, is_system INT)");
            fw.db.exec($"INSERT INTO {table} VALUES (1,'Alpha private',0,20,0),(2,'Alpha mine',0,10,0),(3,'Alpha system',0,20,1),(4,'Alpha inactive',1,10,0)");
        }
        FwModel[] models = [fw.model<UserViews>(), fw.model<UserFilters>(), fw.model<UserLists>()];
        foreach (var model in models)
        {
            var ids = model.listSelectOptionsAutocomplete("Alpha", limit: 10).Select(row => row["id"].toInt()).ToArray();
            CollectionAssert.AreEqual(model is UserLists ? new[] { 2 } : new[] { 2, 3 }, ids);
            Assert.AreEqual(2, model.listSelectOptionsAutocomplete("Alpha", limit: 1)[0]["id"].toInt());
            Assert.IsEmpty(model.listSelectOptionsAutocomplete("1"));
        }
    }
}
#endif
