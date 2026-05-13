using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace osafw.Tests;

[TestClass]
public class FwModelLookupTests
{
    private class FakeDb : DB
    {
        public readonly List<FwDict> WhereCalls = [];
        public readonly Queue<DBList> Results = [];

        public FakeDb() : base("", DB.DBTYPE_SQLSRV) { }

        public override DBList array(string table, FwDict where, string order_by = "", ICollection? aselect_fields = null)
        {
            WhereCalls.Add(new FwDict(where));
            return Results.Count > 0 ? Results.Dequeue() : [];
        }
    }

    private class LookupModel : FwModel
    {
        public LookupModel(FW fw)
            : base(fw)
        {
            table_name = "lookup_rows";
        }
    }

    private class CustomIdLookupModel : FwModel
    {
        private readonly FwList rows;

        public CustomIdLookupModel(FW fw, FwList rows)
            : base(fw)
        {
            table_name = "custom_lookup_rows";
            field_id = "lookup_key";
            this.rows = rows;
        }

        public override FwList listSelectOptions(FwDict? def = null, object? selected_id = null) => rows;
    }

    private class JunctionModel : FwModel
    {
        private readonly DBList links;

        public JunctionModel(FW fw, FwModel linkedModel, DBList links)
            : base(fw)
        {
            table_name = "main_lookup_rows";
            junction_model_linked = linkedModel;
            junction_field_main_id = "main_id";
            junction_field_linked_id = "lookup_key";
            this.links = links;
        }

        public override DBList listByMainId(int main_id, FwDict? def = null) => links;
    }

    private static LookupModel BuildModel(FakeDb db)
    {
        var fw = TestHelpers.CreateFw();
        fw.db = db;
        return new LookupModel(fw);
    }

    [TestMethod]
    public void ListSelectOptions_ReturnsActiveRowsByDefault()
    {
        var db = new FakeDb();
        db.Results.Enqueue(new DBList
        {
            new DBRow(new FwDict { ["id"] = "1", ["iname"] = "Active", ["status"] = "0" }),
        });
        var model = BuildModel(db);

        var rows = model.listSelectOptions();

        Assert.HasCount(1, rows);
        Assert.AreEqual(0, db.WhereCalls[0]["status"]);
    }

    [TestMethod]
    public void ListSelectOptions_IncludesOnlySelectedInactiveOnEdit()
    {
        var db = new FakeDb();
        db.Results.Enqueue(new DBList
        {
            new DBRow(new FwDict { ["id"] = "1", ["iname"] = "Active", ["status"] = "0" }),
        });
        db.Results.Enqueue(new DBList
        {
            new DBRow(new FwDict { ["id"] = "2", ["iname"] = "Inactive", ["status"] = "10" }),
        });
        var model = BuildModel(db);
        var def = new FwDict
        {
            ["record_id"] = 10,
            ["field"] = "lookup_id",
            ["i"] = new FwDict { ["id"] = 10, ["lookup_id"] = 2 },
        };

        var rows = model.listSelectOptions(def);

        Assert.HasCount(2, rows);
        Assert.AreEqual("Inactive" + FwModel.LOOKUP_INACTIVE_SUFFIX, rows[1]["iname"]);
        Assert.AreEqual("text-muted", rows[1]["class"]);

        var selectedWhere = db.WhereCalls[1];
        Assert.AreEqual(DBOps.NOT, ((DBOperation)selectedWhere["status"]!).op);
        Assert.AreEqual(DBOps.IN, ((DBOperation)selectedWhere["id"]!).op);
    }

    [TestMethod]
    public void ListSelectOptions_IgnoresInactiveDefaultsOnAdd()
    {
        var db = new FakeDb();
        db.Results.Enqueue(new DBList
        {
            new DBRow(new FwDict { ["id"] = "1", ["iname"] = "Active", ["status"] = "0" }),
        });
        var model = BuildModel(db);
        var def = new FwDict
        {
            ["record_id"] = 0,
            ["field"] = "lookup_id",
            ["i"] = new FwDict { ["id"] = 0, ["lookup_id"] = 2 },
        };

        var rows = model.listSelectOptions(def);

        Assert.HasCount(1, rows);
        Assert.HasCount(1, db.WhereCalls);
    }

    [TestMethod]
    public void ListSelectedLookupIds_NormalizesCommaSeparatedAndDistinctIds()
    {
        var model = BuildModel(new FakeDb());
        var ids = model.listSelectedLookupIds(null, "2, 3,2,0,bad");

        CollectionAssert.AreEqual(new[] { 2, 3 }, ids);
    }

    [TestMethod]
    public void ListSelectOptionsName_IncludesSelectedInactiveNameOnEdit()
    {
        var db = new FakeDb();
        db.Results.Enqueue(new DBList
        {
            new DBRow(new FwDict { ["id"] = "Active", ["iname"] = "Active", ["status"] = "0" }),
        });
        db.Results.Enqueue(new DBList
        {
            new DBRow(new FwDict { ["id"] = "Inactive Name", ["iname"] = "Inactive Name", ["status"] = "10" }),
        });
        var model = BuildModel(db);
        var def = new FwDict
        {
            ["record_id"] = 10,
            ["field"] = "lookup_name",
            ["i"] = new FwDict { ["id"] = 10, ["lookup_name"] = "Inactive Name" },
        };

        var rows = model.listSelectOptionsName(def);

        Assert.HasCount(2, rows);
        Assert.AreEqual("Inactive Name" + FwModel.LOOKUP_INACTIVE_SUFFIX, rows[1]["iname"]);

        var selectedWhere = db.WhereCalls[1];
        Assert.AreEqual(DBOps.IN, ((DBOperation)selectedWhere["iname"]!).op);
    }

    [TestMethod]
    public void ListLinkedByMainId_UsesNormalizedOptionIdForCustomLookupKey()
    {
        var fw = TestHelpers.CreateFw();
        var selectedKey = new string(new[] { 'B', '2' });
        var lookup = new CustomIdLookupModel(fw,
        [
            new FwDict { ["id"] = "A1", ["iname"] = "Alpha" },
            new FwDict { ["id"] = "B2", ["iname"] = "Beta" },
        ]);
        var junction = new JunctionModel(fw, lookup,
        [
            new DBRow(new FwDict { ["main_id"] = "10", ["lookup_key"] = selectedKey }),
        ]);

        var rows = junction.listLinkedByMainId(10);

        var alpha = rows.First(row => row["id"].toStr() == "A1");
        var beta = rows.First(row => row["id"].toStr() == "B2");
        Assert.IsFalse(alpha["is_checked"].toBool());
        Assert.IsTrue(beta["is_checked"].toBool());
    }
}
