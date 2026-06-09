using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Collections.Generic;

namespace osafw.Tests;

[TestClass]
public class FwControllersModelTests
{
    private class FakeDb : DB
    {
        public FwDict? LastWhere;
        public string? LastOrderBy;
        public int LastOffset;
        public int LastLimit;
        public int UpdateCount;

        public FakeDb() : base("", DB.DBTYPE_SQLSRV) { }

        public override DBList array(string table, FwDict where, string order_by = "", ICollection? aselect_fields = null, int offset = 0, int limit = -1)
        {
            LastWhere = where;
            LastOrderBy = order_by;
            LastOffset = offset;
            LastLimit = limit;
            return new DBList { new DBRow(new FwDict { ["id"] = "1" }) };
        }

        public override int update(string table, FwDict fields, FwDict where)
        {
            UpdateCount++;
            return 1;
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

    private class CachedFwControllers : TestFwControllers
    {
        private readonly Queue<DBRow> rows = new();

        public int ReadCount { get; private set; }

        public CachedFwControllers(FW fw, FakeDb db) : base(fw, db)
        {
            is_log_changes = false;
        }

        public void EnqueueRow(int id, int accessLevel)
        {
            rows.Enqueue(new DBRow(new FwDict
            {
                ["id"] = id,
                ["icode"] = "Virtual",
                ["access_level"] = accessLevel
            }));
        }

        protected override DBRow oneByIcodeIC(string icode)
        {
            ReadCount++;
            return rows.Count > 0 ? rows.Dequeue() : [];
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
        var lastWhere = db.LastWhere ?? throw new AssertFailedException("Expected where clause to be set");
        var statusOperation = lastWhere["status"] as DBOperation ?? throw new AssertFailedException("Missing status operation");
        var accessLevelOperation = lastWhere["access_level"] as DBOperation ?? throw new AssertFailedException("Missing access level operation");
        Assert.AreEqual(DBOps.NOT, statusOperation.op);
        Assert.AreEqual(Users.ACL_MANAGER, accessLevelOperation.value);
        Assert.HasCount(1, rows);
    }

    [TestMethod]
    public void OneByIcode_CachesCaseInsensitivelyAndUpdateInvalidates()
    {
        var db = new FakeDb();
        var fw = TestHelpers.CreateFw();
        fw.db = db;
        var model = new CachedFwControllers(fw, db);
        model.removeCacheAll();
        try
        {
            model.EnqueueRow(1, Users.ACL_MEMBER);
            model.EnqueueRow(1, Users.ACL_SITEADMIN);

            var first = model.oneByIcode("Virtual");
            var second = model.oneByIcode("virtual");
            model.update(1, new FwDict { ["access_level"] = Users.ACL_SITEADMIN });
            var third = model.oneByIcode("VIRTUAL");

            Assert.AreEqual(1, first["access_level"].toInt());
            Assert.AreEqual(1, second["access_level"].toInt());
            Assert.AreEqual(Users.ACL_SITEADMIN, third["access_level"].toInt());
            Assert.AreEqual(2, model.ReadCount);
            Assert.AreEqual(1, db.UpdateCount);
        }
        finally
        {
            model.removeCacheAll();
        }
    }
}
