using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace osafw.Tests;

[TestClass]
public class FwModelLookupTests
{
    private class FakeDb : DB
    {
        public readonly List<string> SqlCalls = [];
        public readonly List<FwDict> ParamCalls = [];
        public readonly Queue<DBList> Results = [];

        public FakeDb() : base("", DB.DBTYPE_SQLSRV) { }

        public override DBList arrayp(string sql, FwDict? @params = null)
        {
            SqlCalls.Add(sql);
            ParamCalls.Add(@params == null ? [] : new FwDict(@params));
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

    private class NoStatusLookupModel : FwModel
    {
        public NoStatusLookupModel(FW fw)
            : base(fw)
        {
            table_name = "lookup_rows";
            field_status = "";
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

        public override FwList listSelectOptions(FwDict? def = null, object? selected_id = null, bool valueFromIname = false, FwDict? baseWhere = null, string? inameSql = null) => rows;
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
        Assert.HasCount(1, db.SqlCalls);
        StringAssert.Contains(db.SqlCalls[0], "[status] = @status_active");
        StringAssert.Contains(db.SqlCalls[0], "[status] AS status");
        Assert.AreEqual(0, db.ParamCalls[0]["status_active"]);
    }

    [TestMethod]
    public void ListSelectOptions_ReturnsActiveStatusAliasWhenModelHasNoStatusField()
    {
        var db = new FakeDb();
        db.Results.Enqueue(new DBList
        {
            new DBRow(new FwDict { ["id"] = "1", ["iname"] = "Active" }),
        });
        var fw = TestHelpers.CreateFw();
        fw.db = db;
        var model = new NoStatusLookupModel(fw);

        var rows = model.listSelectOptions();

        Assert.HasCount(1, rows);
        Assert.HasCount(1, db.SqlCalls);
        StringAssert.Contains(db.SqlCalls[0], "0 AS status");
        Assert.IsFalse(db.SqlCalls[0].Contains("[status] = @status_active"));
        Assert.IsFalse(db.ParamCalls[0].ContainsKey("status_active"));
    }

    [TestMethod]
    public void ListSelectOptions_IncludesOnlySelectedInactiveOnEdit()
    {
        var db = new FakeDb();
        db.Results.Enqueue(new DBList
        {
            new DBRow(new FwDict { ["id"] = "1", ["iname"] = "Active", ["status"] = "0" }),
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
        Assert.HasCount(1, db.SqlCalls);
        StringAssert.Contains(db.SqlCalls[0], "[status] <> @status_deleted");
        StringAssert.Contains(db.SqlCalls[0], "[id] = @selected_id");
        Assert.AreEqual(2, db.ParamCalls[0]["selected_id"]);
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
        Assert.HasCount(1, db.SqlCalls);
        Assert.IsFalse(db.ParamCalls[0].ContainsKey("selected_id"));
    }

    [TestMethod]
    public void ListSelectedLookupIds_NormalizesEnumerableAndDistinctIds()
    {
        var model = BuildModel(new FakeDb());
        var ids = model.listSelectedLookupIds(null, new object?[] { 2, "3", 2, 0, "bad" });

        CollectionAssert.AreEqual(new[] { 2, 3 }, ids);
    }

    [TestMethod]
    public void ListSelectOptions_UsesInForEnumerableSelectedIds()
    {
        var db = new FakeDb();
        db.Results.Enqueue(new DBList
        {
            new DBRow(new FwDict { ["id"] = "2", ["iname"] = "Inactive", ["status"] = "10" }),
        });
        var model = BuildModel(db);

        model.listSelectOptions(null, new[] { 2 });

        StringAssert.Contains(db.SqlCalls[0], "[id] IN (2)");
        Assert.IsFalse(db.ParamCalls[0].ContainsKey("selected_id"));
    }

    [TestMethod]
    public void ListSelectOptionsName_IncludesSelectedInactiveNameOnEdit()
    {
        var db = new FakeDb();
        db.Results.Enqueue(new DBList
        {
            new DBRow(new FwDict { ["id"] = "Active", ["iname"] = "Active", ["status"] = "0" }),
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

        Assert.HasCount(1, db.SqlCalls);
        StringAssert.Contains(db.SqlCalls[0], "[iname] = @selected_value");
        Assert.AreEqual("Inactive Name", db.ParamCalls[0]["selected_value"]);
    }

    [TestMethod]
    public void UsersListSelectOptions_UsesFullNameExpressionWithBaseInactiveRule()
    {
        var db = new FakeDb();
        db.Results.Enqueue(new DBList
        {
            new DBRow(new FwDict { ["id"] = "2", ["iname"] = "Inactive User", ["status"] = "10" }),
        });
        var fw = TestHelpers.CreateFw();
        fw.db = db;
        var users = new Users();
        users.init(fw);
        var def = new FwDict
        {
            ["record_id"] = 10,
            ["field"] = "users_id",
            ["i"] = new FwDict { ["id"] = 10, ["users_id"] = 2 },
        };

        users.listSelectOptions(def);

        Assert.HasCount(1, db.SqlCalls);
        StringAssert.Contains(db.SqlCalls[0], "CONCAT(fname, ' ', lname) AS iname");
        StringAssert.Contains(db.SqlCalls[0], "[status] <> @status_deleted");
        StringAssert.Contains(db.SqlCalls[0], "[id] = @selected_id");
        Assert.AreEqual(2, db.ParamCalls[0]["selected_id"]);
    }

    [TestMethod]
    public void DemosParentListSelectOptions_ForwardsOptionalArguments()
    {
        var db = new FakeDb();
        db.Results.Enqueue(new DBList
        {
            new DBRow(new FwDict { ["id"] = "Inactive Demo", ["iname"] = "Inactive Demo", ["status"] = "10" }),
        });
        var fw = TestHelpers.CreateFw();
        fw.db = db;
        var demos = new Demos();
        demos.init(fw);
        var def = new FwDict
        {
            ["lookup_params"] = "parent",
            ["record_id"] = 10,
            ["field"] = "demo_name",
            ["i"] = new FwDict { ["id"] = 10, ["demo_name"] = "Inactive Demo" },
        };

        demos.listSelectOptions(def, "Inactive Demo", valueFromIname: true, baseWhere: DB.h("demo_dicts_id", 7), inameSql: "UPPER(iname)");

        Assert.HasCount(1, db.SqlCalls);
        StringAssert.Contains(db.SqlCalls[0], "UPPER(iname) AS iname");
        StringAssert.Contains(db.SqlCalls[0], "[demo_dicts_id] = @lookup_filter_0");
        StringAssert.Contains(db.SqlCalls[0], "[parent_id] = @lookup_filter_1");
        StringAssert.Contains(db.SqlCalls[0], "[iname] = @selected_value");
        Assert.AreEqual(7, db.ParamCalls[0]["lookup_filter_0"]);
        Assert.AreEqual(0, db.ParamCalls[0]["lookup_filter_1"]);
        Assert.AreEqual("Inactive Demo", db.ParamCalls[0]["selected_value"]);
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
