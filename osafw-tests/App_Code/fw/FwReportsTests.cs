using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Reflection;

namespace osafw.Tests;

[TestClass]
public class FwReportsTests
{
    [TestMethod]
    public void CleanupRepcode_StripsUnsafeCharacters()
    {
        var cleaned = FwReports.cleanupRepcode("Sales Report#1/2025");

        Assert.AreEqual("SalesReport12025", cleaned);
    }

    [TestMethod]
    public void FilterSessionKey_UsesControllerAction()
    {
        var fw = TestHelpers.CreateFw();
        fw.G["controller.action"] = "AdminReports.Index";

        var sessionKey = FwReports.filterSessionKey(fw, "sales");

        Assert.AreEqual("_filter_AdminReports.Index.sales", sessionKey);
    }

    [TestMethod]
    public void Format2Ext_ReturnsJsonExtension()
    {
        Assert.AreEqual(".json", FwReports.format2ext("json"));
        Assert.AreEqual(".json", FwReports.format2ext("JSON"));
        Assert.AreEqual(".html", FwReports.format2ext(null!));
    }

    [TestMethod]
    public void ValidateSqlTemplate_AllowsSelectAndCte()
    {
        FwReportsModel.validateSqlTemplate("select id, iname from users where id=@users_id");
        FwReportsModel.validateSqlTemplate("with active_users as (select id from users) select * from active_users");
    }

    [TestMethod]
    [DataRow("update users set iname='x'")]
    [DataRow("select * into report_tmp from users")]
    [DataRow("select * from users; select * from settings")]
    [DataRow("select * from xp_cmdshell")]
    [DataRow("exec dbo.ReportProc")]
    [DataRow("drop table users")]
    public void ValidateSqlTemplate_RejectsUnsafeSql(string sql)
    {
        Assert.ThrowsExactly<UserException>(() => FwReportsModel.validateSqlTemplate(sql));
    }

    [TestMethod]
    public void ParseParamDefinitions_AddsDefaultsForMissingMetadata()
    {
        var defs = FwReportsModel.parseParamDefinitions(
            "select * from users where add_time>=@from_date and id=@users_id and email like @s",
            """
            {
              "from_date": {"label":"From","type":"date","default":"-30d"}
            }
            """);

        Assert.AreEqual(3, defs.Count);
        Assert.AreEqual("from_date", defs[0]["name"]);
        Assert.AreEqual("date", defs[0]["type"]);
        Assert.AreEqual("users_id", defs[1]["name"]);
        Assert.AreEqual("int", defs[1]["type"]);
        Assert.AreEqual("s", defs[2]["name"]);
        Assert.AreEqual("text", defs[2]["type"]);
    }

    [TestMethod]
    public void ParseParamDefinitions_RejectsUnknownMetadataParam()
    {
        Assert.ThrowsExactly<UserException>(() => FwReportsModel.parseParamDefinitions(
            "select * from users where id=@users_id",
            """{"missing":{"type":"text"}}"""));
    }

    [TestMethod]
    public void ParseParamDefinitions_PreservesModelLookupSource()
    {
        var defs = FwReportsModel.parseParamDefinitions(
            "select * from users where id=@users_id",
            """[{"name":"users_id","label":"User","type":"lookup","source":"model:Users"}]""");

        Assert.AreEqual(1, defs.Count);
        Assert.AreEqual("lookup", defs[0]["type"]);
        Assert.AreEqual("model:Users", defs[0]["source"]);
    }

    [TestMethod]
    public void ListIndexState_MarksNoParamReportsForAutorun()
    {
        var model = new FwReportsModel();
        var rows = new DBList
        {
            new()
            {
                ["sql_template"] = "select 1 as score",
                ["params_json"] = ""
            },
            new()
            {
                ["sql_template"] = "select * from users where email like @s",
                ["params_json"] = """[{"name":"s","type":"text"}]"""
            }
        };

        typeof(FwReportsModel)
            .GetMethod("withIndexDisplayState", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(model, [rows]);

        Assert.AreEqual("1", rows[0]["is_autorun"]);
        Assert.AreEqual("", rows[1]["is_autorun"]);
    }

    [TestMethod]
    public void CleanupIcon_RemovesBootstrapPrefixAndUnsafeCharacters()
    {
        Assert.AreEqual("currency-dollar", FwReportsModel.cleanupIcon("bi bi-currency-dollar"));
        Assert.AreEqual("graph-up", FwReportsModel.cleanupIcon("Graph Up!"));
    }

    [TestMethod]
    public void CreateInstance_UsesHardcodedReportBeforeCustomLookup()
    {
        var fw = TestHelpers.CreateFw();
        fw.Session("access_level", Users.ACL_SITEADMIN.ToString());

        var report = FwReports.createInstance(fw, "Sample", []);

        Assert.IsInstanceOfType(report, typeof(SampleReport));
    }

    [TestMethod]
    public void CustomReportAccess_RequiresConfiguredAccessLevel()
    {
        var fw = TestHelpers.CreateFw();
        fw.Session("access_level", Users.ACL_MEMBER.ToString());
        var model = new FwReportsModel();
        model.init(fw);
        var report = new FwDict
        {
            ["icode"] = "sales",
            ["status"] = FwModel.STATUS_ACTIVE,
            ["access_level"] = Users.ACL_MANAGER
        };

        Assert.IsFalse(model.isAccessible(report, FW.ACTION_SHOW));

        fw.Session("access_level", Users.ACL_MANAGER.ToString());

        Assert.IsTrue(model.isAccessible(report, FW.ACTION_SHOW));
    }

    [TestMethod]
    public void CustomReportResultTable_RightAlignsNumericColumnsAndTotalsMetrics()
    {
        var report = new FwCustomReport([]);
        report.list_rows =
        [
            new FwDict { ["id"] = "1", ["amount"] = "10.50", ["status"] = "0", ["name"] = "Alpha" },
            new FwDict { ["id"] = "2", ["amount"] = "2", ["status"] = "0", ["name"] = "Beta" }
        ];

        typeof(FwCustomReport)
            .GetMethod("buildResultTable", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(report, null);

        var headers = report.ps["result_headers"] as FwList ?? throw new AssertFailedException("Missing result headers");
        var totals = report.ps["result_totals"] as FwList ?? throw new AssertFailedException("Missing result totals");

        Assert.AreEqual("text-end", headers[0]["align_class"]);
        Assert.AreEqual("text-end", headers[1]["align_class"]);
        Assert.AreEqual("", headers[3]["align_class"]);
        Assert.AreEqual("Total", totals[0]["display_value"]);
        Assert.AreEqual("", totals[0]["align_class"]);
        Assert.AreEqual("12.5", totals[1]["display_value"]);
        Assert.AreEqual("", totals[2]["display_value"]);
        Assert.IsTrue(report.ps["has_result_totals"].toBool());
    }

    [TestMethod]
    public void CustomReportResultRows_SortsByRequestedResultColumn()
    {
        var report = new FwCustomReport([]);
        report.f = new FwDict { ["sortby"] = "amount", ["sortdir"] = "desc" };
        report.list_rows =
        [
            new FwDict { ["name"] = "Alpha", ["amount"] = "10.50" },
            new FwDict { ["name"] = "Beta", ["amount"] = "20" },
            new FwDict { ["name"] = "Gamma", ["amount"] = "2" }
        ];

        typeof(FwCustomReport)
            .GetMethod("sortResultRows", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(report, null);

        Assert.AreEqual("Beta", report.list_rows[0]["name"]);
        Assert.AreEqual("Alpha", report.list_rows[1]["name"]);
        Assert.AreEqual("Gamma", report.list_rows[2]["name"]);
        Assert.AreEqual("amount", report.f["sortby"]);
        Assert.AreEqual("desc", report.f["sortdir"]);
    }

    [TestMethod]
    public void AdminReportsController_AllowsLoggedUsersForCustomReportGate()
    {
        Assert.AreEqual(Users.ACL_MEMBER, AdminReportsController.access_level);
    }

    [TestMethod]
    public void Render_Json_WritesReportPayloadToResponse()
    {
        var fw = TestHelpers.CreateFw();
        fw.response.Body = new MemoryStream();

        var report = new JsonReportForTest();
        report.init(fw, "sample", new FwDict { ["format"] = "json" });
        report.setFilters();
        report.getData();

        report.render();

        fw.response.Body.Position = 0;
        var body = new StreamReader(fw.response.Body).ReadToEnd();
        var decoded = Utils.jsonDecode(body) as FwDict;
        var rows = decoded?["list_rows"] as ObjList;
        var firstRow = rows?[0] as FwDict;
        var filter = decoded?["filter"] as FwDict;

        Assert.AreEqual("application/json; charset=utf-8", fw.response.ContentType);
        Assert.IsNotNull(decoded);
        Assert.AreEqual("sample", decoded["report_code"]);
        Assert.AreEqual(1, decoded["count"].toInt());
        Assert.AreEqual(1, decoded["total"].toInt());
        Assert.IsNotNull(rows);
        Assert.AreEqual(1, rows.Count);
        Assert.IsNotNull(firstRow);
        Assert.AreEqual("alpha", firstRow["name"]);
        Assert.IsNotNull(filter);
        Assert.AreEqual("ready", filter["status"]);
    }

    private sealed class JsonReportForTest : FwReports
    {
        public override void setFilters()
        {
            base.setFilters();

            f_data["status"] = "ready";
        }

        public override void getData()
        {
            base.getData();

            list_rows =
            [
                new FwDict { ["id"] = 1, ["name"] = "alpha" }
            ];
            list_count = list_rows.Count;
            ps["total"] = list_count;
        }
    }
}
