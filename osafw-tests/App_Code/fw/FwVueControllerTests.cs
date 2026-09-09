using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace osafw.Tests;

[TestClass]
public class FwVueControllerTests
{
    private class StubUsers : Users
    {
        public override void checkReadOnly(int id = -1) { }
        public override bool isReadOnly(int id = -1) => false;
        public override bool isAccessLevel(int min_acl) => true;
        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    private class StubModel : FwModel
    {
        public StubModel() : base() => table_name = "stub";
    }

    private sealed class RecordingDb : RejectingDb
    {
        public string CountSql { get; private set; } = "";
        public string SelectSql { get; private set; } = "";

        public override object? valuep(string sql, FwDict? @params = null)
        {
            CountSql = sql;
            return 1;
        }

        public override DBList arrayp(string sql, FwDict? @params = null)
        {
            SelectSql = sql;
            var projection = sql.Split(" FROM ", StringSplitOptions.None)[0];
            var row = new DBRow(new FwDict
            {
                ["id"] = 7,
                ["title"] = "Visible",
                ["first_name"] = "Ada",
                ["last_name"] = "Lovelace",
            });
            foreach (var field in new[] { "id", "title", "first_name", "last_name" })
                if (!projection.Contains($"[{field}]", StringComparison.Ordinal))
                    row.Remove(field);

            return new DBList
            {
                row,
            };
        }
    }

    private sealed class CalculatedModel : FwModel
    {
        public CalculatedModel()
        {
            table_name = "stub";
            field_status = "";
            field_add_users_id = "";
            field_upd_users_id = "";
        }

        public override void filterForJson(FwDict item) { }
    }

    private sealed class CalculatedVueController : FwVueController
    {
        public FwList Rows => list_rows;
        public FwList Headers => list_headers;
        public FwDict Params => list_where_params;

        public void Configure(
            FW fw,
            object? calculatedFields,
            string visibleFields,
            string searchFields = "title calculated_name",
            FwDict? sortMap = null,
            string sortDefault = "title asc", bool legacyStore = false)
        {
            init(fw);
            base_url = "/Calculated";
            model0 = new CalculatedModel();
            model0.init(fw);
            db = fw.db;
            var definition = new FwDict
            {
                ["is_dynamic_index"] = true,
                ["view_list_defaults"] = visibleFields,
                ["view_list_map"] = new FwDict
                {
                    ["title"] = "Title",
                    ["calculated_name"] = "Calculated name",
                },
                ["list_calculated_fields"] = calculatedFields,
                ["search_fields"] = searchFields,
                ["list_sortdef"] = sortDefault,
                ["list_sortmap"] = sortMap ?? new FwDict { ["title"] = "title" },
            };
            if (legacyStore || calculatedFields == null) definition.Remove("list_calculated_fields");
            if (legacyStore) definition["store"] = new FwDict { ["list_calculated_fields"] = calculatedFields };
            loadControllerConfig(definition);
        }

        public FwDict InitState() { FwDict ps = []; setScopeInitial(ps); return ps; }
        protected override FwDict getListUserView() => [];

        public FwDict RunList(string keyword = "", FwDict? columnSearch = null, string sortBy = "title")
        {
            list_filter = new FwDict
            {
                ["pagenum"] = 0,
                ["pagesize"] = 25,
                ["sortby"] = sortBy,
                ["sortdir"] = "asc",
                ["s"] = keyword,
            };
            list_filter_search = columnSearch ?? [];
            var ps = new FwDict();
            setScopeListRows(ps);
            return ps;
        }

        public string RunCsvExport()
        {
            export_format = "csv";
            fw.response.Body = new MemoryStream();
            RunList();
            exportList();
            fw.response.Body.Position = 0;
            return new StreamReader(fw.response.Body).ReadToEnd();
        }

        public override void getListRows()
        {
            base.getListRows();
            foreach (FwDict row in list_rows)
                row["calculated_name"] = $"{row["first_name"]} {row["last_name"]}";
        }
    }

    private class TestVueController : FwVueController
    {
        public string? LastListFields => list_fields;

        public void Configure(FW fw, FwModel model)
        {
            init(fw);
            model0 = model;
            db = fw.db;
        }

        public void SetupHeaders(params string[] fieldNames)
        {
            list_headers = [];
            foreach (var field in fieldNames)
            {
                list_headers.Add(new FwDict { ["field_name"] = field });
            }
        }

        public void InvokeSetListFields() => setListFields();
    }

    [TestMethod]
    public void SetListFields_AppendsIdWhenMissing()
    {
        var fw = TestHelpers.CreateFw();
        TestHelpers.RegisterModel(fw, (Users)new StubUsers());
        var controller = new TestVueController();
        controller.Configure(fw, new StubModel());
        controller.SetupHeaders("title", "status");

        controller.InvokeSetListFields();

        Assert.IsNotNull(controller.LastListFields);
        StringAssert.Contains(controller.LastListFields!, "title");
        StringAssert.Contains(controller.LastListFields!, "id");
    }

    [TestMethod]
    public void ListRows_MixedViewLoadsDependenciesAndPrunesThemFromJson()
    {
        var db = new RecordingDb();
        using var scope = new FwTestScope(_ => db);
        TestHelpers.RegisterModel(scope.Fw, (Users)new StubUsers());
        var controller = new CalculatedVueController();
        controller.Configure(scope.Fw, new ObjList
        {
            new FwDict
            {
                ["field"] = "calculated_name",
                ["dependencies"] = new ObjList { "first_name", "last_name" },
            },
        }, "title calculated_name");

        var ps = controller.RunList();

        StringAssert.Contains(db.SelectSql, "[title]");
        StringAssert.Contains(db.SelectSql, "[first_name]");
        StringAssert.Contains(db.SelectSql, "[last_name]");
        StringAssert.Contains(db.SelectSql, "[id]");
        Assert.IsFalse(db.SelectSql.Contains("[calculated_name]", StringComparison.Ordinal));
        var row = ((FwList)ps["list_rows"]!)[0];
        Assert.AreEqual("Visible", row["title"]);
        Assert.AreEqual("Ada Lovelace", row["calculated_name"]);
        Assert.IsFalse(row.ContainsKey("first_name"));
        Assert.IsFalse(row.ContainsKey("last_name"));
    }

    [TestMethod]
    public void ListRows_CalculatedOnlyViewStillSelectsIdAndDeclaredDependencies()
    {
        var db = new RecordingDb();
        using var scope = new FwTestScope(_ => db);
        TestHelpers.RegisterModel(scope.Fw, (Users)new StubUsers());
        var controller = new CalculatedVueController();
        controller.Configure(scope.Fw, new FwDict
        {
            ["calculated_name"] = "first_name last_name",
        }, "calculated_name");

        var ps = controller.RunList();

        var projection = db.SelectSql.Split(" FROM ", StringSplitOptions.None)[0];
        Assert.IsFalse(projection.Contains("[title]", StringComparison.Ordinal));
        StringAssert.Contains(db.SelectSql, "[first_name]");
        StringAssert.Contains(db.SelectSql, "[last_name]");
        StringAssert.Contains(db.SelectSql, "[id]");
        var row = ((FwList)ps["list_rows"]!)[0];
        CollectionAssert.AreEquivalent(new[] { "id", "calculated_name" }, new System.Collections.ArrayList(row.Keys));
    }

    [TestMethod]
    public void ListRows_UnselectedCalculatedFieldDoesNotLoadDependencies()
    {
        var db = new RecordingDb();
        using var scope = new FwTestScope(_ => db);
        TestHelpers.RegisterModel(scope.Fw, (Users)new StubUsers());
        var controller = new CalculatedVueController();
        controller.Configure(scope.Fw, new FwDict
        {
            ["calculated_name"] = "first_name last_name",
        }, "title");

        controller.RunList();

        var projection = db.SelectSql.Split(" FROM ", StringSplitOptions.None)[0];
        Assert.IsFalse(projection.Contains("[first_name]", StringComparison.Ordinal));
        Assert.IsFalse(projection.Contains("[last_name]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SearchAndColumnFilter_IgnoreCalculatedFieldsAtSqlBoundary()
    {
        var db = new RecordingDb();
        using var scope = new FwTestScope(_ => db);
        TestHelpers.RegisterModel(scope.Fw, (Users)new StubUsers());
        var controller = new CalculatedVueController();
        controller.Configure(scope.Fw, new FwDict
        {
            ["calculated_name"] = "first_name last_name",
        }, "title calculated_name");

        controller.RunList("Ada", new FwDict
        {
            ["title"] = "^Vis",
            ["calculated_name"] = "^Ada",
        });

        StringAssert.Contains(db.CountSql, "[title] LIKE @list_search_0_0");
        StringAssert.Contains(db.CountSql, "LIKE 'Vis%'");
        Assert.IsFalse(db.CountSql.Contains("calculated_name", StringComparison.Ordinal));
        Assert.IsFalse(controller.Params.Keys.Any(key => key.Contains("calculated", StringComparison.Ordinal)));
        Assert.IsFalse(controller.Headers.Single(header => header["field_name"].toStr() == "calculated_name")["is_sortable"].toBool());
    }

    [TestMethod]
    public void ExplicitSortMapping_AllowsCalculatedHeaderToUseDatabaseOrdering()
    {
        var db = new RecordingDb();
        using var scope = new FwTestScope(_ => db);
        TestHelpers.RegisterModel(scope.Fw, (Users)new StubUsers());
        var controller = new CalculatedVueController();
        controller.Configure(
            scope.Fw,
            new FwDict { ["calculated_name"] = "first_name last_name" },
            "calculated_name",
            sortMap: new FwDict { ["calculated_name"] = "last_name" },
            sortDefault: "calculated_name asc");

        controller.RunList(sortBy: "calculated_name");

        StringAssert.Contains(db.SelectSql, "ORDER BY [last_name]");
        Assert.IsTrue(controller.Headers.Single()["is_sortable"].toBool());
    }

    [TestMethod]
    public void CsvExport_IncludesCalculatedValueWithoutHiddenDependencies()
    {
        var db = new RecordingDb();
        using var scope = new FwTestScope(_ => db);
        TestHelpers.RegisterModel(scope.Fw, (Users)new StubUsers());
        var controller = new CalculatedVueController();
        controller.Configure(scope.Fw, new FwDict
        {
            ["calculated_name"] = new ObjList { "first_name", "last_name" },
        }, "title calculated_name");

        var csv = controller.RunCsvExport();

        StringAssert.Contains(csv, "Title,Calculated name");
        StringAssert.Contains(csv, "Visible,Ada Lovelace");
        Assert.IsFalse(csv.Contains("first_name", StringComparison.Ordinal));
        Assert.IsFalse(csv.Contains("last_name", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MaliciousAndUnknownMetadata_NeverEntersSelectProjection()
    {
        var db = new RecordingDb();
        using var scope = new FwTestScope(_ => db);
        TestHelpers.RegisterModel(scope.Fw, (Users)new StubUsers());
        var controller = new CalculatedVueController();
        controller.Configure(scope.Fw, new FwDict
        {
            ["calculated_name"] = new ObjList { "first_name", "last_name]; DROP TABLE users;--" },
            ["unknown_calculated"] = "secret",
            ["calcuated_name"] = "title",
            ["bad]; DROP TABLE users;--"] = "title",
        }, "title calculated_name");

        controller.RunList();

        StringAssert.Contains(db.SelectSql, "[first_name]");
        Assert.IsFalse(db.SelectSql.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(db.SelectSql.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(db.SelectSql.Contains("unknown_calculated", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(db.SelectSql.Contains("calcuated_name", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(db.SelectSql, "[title]");
    }

    [TestMethod]
    public void LegacyStoreMetadataIsAdoptedAndAbsentMetadataDoesNotOverwriteClientState()
    {
        var db = new RecordingDb();
        using var scope = new FwTestScope(_ => db);
        TestHelpers.RegisterModel(scope.Fw, (Users)new StubUsers());
        var controller = new CalculatedVueController();
        controller.Configure(scope.Fw, new FwDict { ["first_name"] = "calculated_name" }, "title calculated_name", legacyStore: true);
        var initial = controller.InitState();
        CollectionAssert.AreEqual(new[] { "calculated_name" }, ((StrList)initial["list_calculated_fields"]!).ToArray());
        controller.RunList();
        StringAssert.Contains(db.SelectSql, "[first_name]");
        Assert.IsFalse(db.SelectSql.Split(" FROM ")[0].Contains("[calculated_name]"));
        controller.Configure(scope.Fw, null, "title");
        Assert.IsFalse(controller.InitState().ContainsKey("list_calculated_fields"));
    }
}
