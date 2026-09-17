using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace osafw.Tests;

[TestClass]
public class CalculatedDynamicListTests
{
    private sealed class ListDb : RejectingDb
    {
        internal string Projection = "";
        public override FwList loadTableSchemaFull(string table) => [];
        public override object? valuep(string sql, FwDict? parameters = null) => 1;
        public override DBList arrayp(string sql, FwDict? parameters = null)
        {
            if (sql.Contains("[user_filters]", StringComparison.Ordinal)) return [];
            Projection = sql.Split(" FROM ", StringSplitOptions.None)[0];
            var row = new DBRow { ["id"] = "7", ["title"] = "Visible", ["first_name"] = "Ada" };
            foreach (var field in row.Keys.ToArray())
                if (!Projection.Contains($"[{field}]", StringComparison.Ordinal)) row.Remove(field);
            return [row];
        }
    }

    private sealed class ListModel : FwModel
    {
        internal ListModel() { table_name = "test_records"; field_status = ""; }
        public override void filterForJson(FwDict item) => Assert.Fail("Classic Dynamic must use the controller calculation hook.");
    }

    private sealed class ListController : FwDynamicController
    {
        internal void Configure(FW fw, bool withDependency)
        {
            init(fw);
            base_url = "/CalculatedDynamic";
            model0 = new ListModel();
            model0.init(fw);
            loadControllerConfig(new FwDict
            {
                ["is_dynamic_index"] = true,
                ["view_list_defaults"] = "title calculated_name",
                ["view_list_map"] = new FwDict { ["title"] = "Title", ["calculated_name"] = "Calculated name" },
                ["list_calculated_fields"] = new FwDict { ["calculated_name"] = withDependency ? new StrList { "first_name" } : new StrList() },
                ["list_sortmap"] = new FwDict { ["title"] = "title" },
                ["list_sortdef"] = "title asc",
            });
        }
        protected override FwDict getListUserView() => [];
        public override FwDict initFilter(string? session_key = null) => list_filter = new FwDict { ["pagenum"] = 0, ["pagesize"] = 25 };
        public override void getListRows()
        {
            base.getListRows();
            foreach (var row in list_rows)
                row["calculated_name"] = row.ContainsKey("first_name") ? "Hello " + row["first_name"] : "Calculated";
        }
        internal string Export()
        {
            export_format = "csv";
            fw.response.Body = new MemoryStream();
            IndexAction();
            exportList();
            fw.response.Body.Position = 0;
            return new StreamReader(fw.response.Body).ReadToEnd();
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void IndexAndExportProjectDependenciesAndUseControllerCalculation(bool withDependency)
    {
        var db = new ListDb();
        using var scope = new FwTestScope(_ => db);
        var controller = new ListController();
        controller.Configure(scope.Fw, withDependency);

        var page = controller.IndexAction();

        StringAssert.Contains(db.Projection, "[id]");
        StringAssert.Contains(db.Projection, "[title]");
        Assert.AreEqual(withDependency, db.Projection.Contains("[first_name]", StringComparison.Ordinal));
        Assert.IsFalse(db.Projection.Contains("[calculated_name]", StringComparison.Ordinal));
        Assert.IsFalse(db.Projection.Contains('*'));
        var expected = withDependency ? "Hello Ada" : "Calculated";
        var row = ((FwList)page["list_rows"]!)[0];
        Assert.AreEqual(expected, row["calculated_name"]);
        var cell = ((FwList)row["cols"]!).Single(col => col["field_name"].toStr() == "calculated_name");
        Assert.AreEqual(expected, cell["data"]);

        var csv = controller.Export();
        StringAssert.Contains(csv, "Title,Calculated name");
        StringAssert.Contains(csv, "Visible," + expected);
        Assert.IsFalse(csv.Contains("first_name", StringComparison.Ordinal));
        Assert.IsFalse(db.Projection.Contains("[calculated_name]", StringComparison.Ordinal));
    }
}
