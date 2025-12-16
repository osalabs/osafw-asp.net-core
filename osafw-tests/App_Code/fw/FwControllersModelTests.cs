using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;

namespace osafw.Tests;

[TestClass]
public class FwControllersModelTests
{
    private class FakeDb : DB
    {
        public FwDict? LastWhere;
        public string? LastOrderBy;

        public FakeDb() : base("", DB.DBTYPE_SQLSRV) { }

        public override DBList array(string table, FwDict where, string order_by = "", ICollection? aselect_fields = null)
        {
            LastWhere = where;
            LastOrderBy = order_by;
            return new DBList { new DBRow(new FwDict { ["id"] = "1" }) };
        }
    }

    private class TestFwControllers : FwControllers
    {
        public TestFwControllers(FW fw, FakeDb db)
        {
            init(fw);
            this.db = db;
        }
    }

    [TestMethod]
    public void ListGrouped_FiltersByStatusAndAccessLevel()
    {
        var db = new FakeDb();
        var fw = TestHelpers.CreateFw();
        fw.db = db;
        fw.Session("access_level", Users.ACL_MANAGER.toStr());

        var model = new TestFwControllers(fw, db);

        var rows = model.listGrouped();

        Assert.AreEqual("igroup, iname", db.LastOrderBy);
        Assert.IsNotNull(db.LastWhere);
        Assert.AreEqual(DBOps.NOT, ((DBOperation)db.LastWhere!["status"]).op);
        Assert.AreEqual(Users.ACL_MANAGER, ((DBOperation)db.LastWhere!["access_level"]).value);
        Assert.AreEqual(1, rows.Count);
    }
}
