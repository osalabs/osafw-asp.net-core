using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class FwReportsTests
{
    [TestMethod]
    public void CleanupRepcode_StripsUnsafeCharacters()
    {
        var cleaned = FwReportsBase.cleanupRepcode("Sales Report#1/2025");

        Assert.AreEqual("SalesReport12025", cleaned);
    }

    [TestMethod]
    public void FilterSessionKey_UsesControllerAction()
    {
        var fw = TestHelpers.CreateFw();
        fw.G["controller.action"] = "AdminReports.Index";

        var sessionKey = FwReportsBase.filterSessionKey(fw, "sales");

        Assert.AreEqual("_filter_AdminReports.Index.sales", sessionKey);
    }

    [TestMethod]
    public void Format2Ext_ReturnsJsonExtension()
    {
        Assert.AreEqual(".json", FwReportsBase.format2ext("json"));
        Assert.AreEqual(".json", FwReportsBase.format2ext("JSON"));
        Assert.AreEqual(".html", FwReportsBase.format2ext(null!));
    }

    [TestMethod]
    public void ValidateSqlTemplate_AllowsSelectAndCte()
    {
        FwReports.validateSqlTemplate("select id, iname from users where id=@users_id");
        FwReports.validateSqlTemplate("with active_users as (select id from users) select * from active_users");
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
        Assert.ThrowsExactly<UserException>(() => FwReports.validateSqlTemplate(sql));
    }

    [TestMethod]
    [DataRow("update users set iname='x'")]
    [DataRow("select * into report_tmp from users")]
    [DataRow("select * from users; select * from settings")]
    public void CustomReportRuntime_RejectsUnsafeSqlBeforeExecution(string sql)
    {
        var fw = TestHelpers.CreateFw();
        fw.Session("access_level", Users.ACL_SITEADMIN.ToString());
        var report = new FwCustomReport(new FwDict
        {
            ["icode"] = "unsafe",
            ["iname"] = "Unsafe",
            ["access_level"] = Users.ACL_SITEADMIN,
            ["sql_template"] = sql,
            ["params_json"] = "",
            ["render_options_json"] = ""
        });
        report.init(fw, "unsafe", new FwDict { ["is_preview"] = true });

        var ex = Assert.ThrowsExactly<UserException>(() => report.getData());

        StringAssert.Contains(ex.Message, "Report SQL");
    }

    [TestMethod]
    public void ParseParamDefinitions_AddsDefaultsForMissingMetadata()
    {
        var defs = FwReports.parseParamDefinitions(
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
        Assert.ThrowsExactly<UserException>(() => FwReports.parseParamDefinitions(
            "select * from users where id=@users_id",
            """{"missing":{"type":"text"}}"""));
    }

    [TestMethod]
    public void ParseParamDefinitions_PreservesModelLookupSource()
    {
        var defs = FwReports.parseParamDefinitions(
            "select * from users where id=@users_id",
            """[{"name":"users_id","label":"User","type":"lookup","source":"model:Users"}]""");

        Assert.AreEqual(1, defs.Count);
        Assert.AreEqual("lookup", defs[0]["type"]);
        Assert.AreEqual("model:Users", defs[0]["source"]);
    }

    [TestMethod]
    public void ParseParamDefinitions_InfersDatetimeBeforeDate()
    {
        var defs = FwReports.parseParamDefinitions(
            "select * from users where add_time <= @to_datetime",
            "");

        Assert.AreEqual(1, defs.Count);
        Assert.AreEqual("datetime", defs[0]["type"]);
    }

    [TestMethod]
    public void ParseParamDefinitions_AllowsSplitLookupTypes()
    {
        var defs = FwReports.parseParamDefinitions(
            "select * from users where demo_dicts_id=@demo_dicts_id and id=@users_id and status=@status and is_active=@is_active",
            """
            [
              {"name":"demo_dicts_id","type":"lookup_table","source":"demo_dicts"},
              {"name":"users_id","type":"lookup_model","source":"Users"},
              {"name":"status","type":"lookup_sql","source":"SELECT id, iname FROM statuses"},
              {"name":"is_active","type":"lookup_tpl","source":"/common/sel/yn.sel"}
            ]
            """);

        Assert.AreEqual("lookup_table", defs[0]["type"]);
        Assert.AreEqual("lookup_model", defs[1]["type"]);
        Assert.AreEqual("lookup_sql", defs[2]["type"]);
        Assert.AreEqual("lookup_tpl", defs[3]["type"]);
        Assert.IsTrue(FwReports.isLookupParamType(defs[3]["type"].toStr()));
    }

    [TestMethod]
    public void ListIndexState_MarksNoParamReportsForAutorun()
    {
        var model = new FwReports();
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

        model.withIndexDisplayState(rows);

        Assert.AreEqual("1", rows[0]["is_autorun"]);
        Assert.AreEqual("", rows[1]["is_autorun"]);
    }

    [TestMethod]
    public void CleanupIcon_RemovesBootstrapPrefixAndUnsafeCharacters()
    {
        Assert.AreEqual("currency-dollar", FwReports.cleanupIcon("bi bi-currency-dollar"));
        Assert.AreEqual("graph-up", FwReports.cleanupIcon("Graph Up!"));
    }

    [TestMethod]
    public void CreateInstance_UsesHardcodedReportBeforeCustomLookup()
    {
        var fw = TestHelpers.CreateFw();
        fw.Session("access_level", Users.ACL_SITEADMIN.ToString());

        var report = FwReportsBase.createInstance(fw, "Sample", []);

        Assert.IsInstanceOfType(report, typeof(SampleReport));
    }

    [TestMethod]
    public void CustomReportAccess_RequiresConfiguredAccessLevel()
    {
        var fw = TestHelpers.CreateFw();
        fw.Session("access_level", Users.ACL_MEMBER.ToString());
        var model = new FwReports();
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

        report.buildResultTable();

        var headers = report.ps["result_headers"] as FwList ?? throw new AssertFailedException("Missing result headers");
        var totals = report.ps["result_totals"] as FwList ?? throw new AssertFailedException("Missing result totals");

        Assert.AreEqual("text-end", headers[0]["align_class"]);
        Assert.AreEqual("text-end", headers[1]["align_class"]);
        Assert.AreEqual("", headers[3]["align_class"]);
        Assert.IsTrue(totals[0]["is_first_cell"].toBool());
        Assert.AreEqual("", totals[0]["display_value"]);
        Assert.AreEqual("", totals[0]["align_class"]);
        Assert.AreEqual("12.5", totals[1]["display_value"]);
        Assert.AreEqual("", totals[2]["display_value"]);
        Assert.IsTrue(report.ps["has_result_totals"].toBool());
        Assert.IsTrue(report.ps["is_result_sortable"].toBool());
    }

    [TestMethod]
    public void CustomReportSqlParams_AcceptsDateOnlyValueForDatetime()
    {
        var model = new FwReports();
        var defs = FwReports.parseParamDefinitions(
            "select * from users where add_time <= @to_datetime",
            """[{"name":"to_datetime","type":"datetime"}]""");

        var values = model.buildSqlParams(
            defs,
            new FwDict { ["to_datetime"] = "06/17/2026" },
            DateUtils.DATE_FORMAT_MDY,
            DateUtils.TIME_FORMAT_12);

        Assert.AreEqual(new DateTime(2026, 6, 17, 0, 0, 0), values["@to_datetime"]);
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

        report.sortResultRows();

        Assert.AreEqual("Beta", report.list_rows[0]["name"]);
        Assert.AreEqual("Alpha", report.list_rows[1]["name"]);
        Assert.AreEqual("Gamma", report.list_rows[2]["name"]);
        Assert.AreEqual("amount", report.f["sortby"]);
        Assert.AreEqual("desc", report.f["sortdir"]);
    }

    [TestMethod]
    public void CustomReportExecutionError_ShowsDetailOnlyWhenAllowed()
    {
        var fw = TestHelpers.CreateFw();
        var report = new FwCustomReport(new FwDict { ["icode"] = "bad", ["iname"] = "Bad Report" });
        report.init(fw, "bad", []);

        report.setExecutionError(new ApplicationException("Invalid object name 'missing_table'."), true);

        Assert.IsTrue(report.ps["has_report_error"].toBool());
        Assert.IsFalse(report.ps["is_report_results_visible"].toBool());
        Assert.IsTrue(report.ps["has_run_context"].toBool());
        Assert.AreEqual("Row limit: 1000", report.ps["row_limit_context"]);
        Assert.AreEqual("Invalid object name 'missing_table'.", report.ps["report_error_message"]);
        Assert.AreEqual("Bad Report", report.ps["title"]);

        report.setExecutionError(new ApplicationException("Sensitive database details"), false);

        Assert.IsTrue(report.ps["has_report_error"].toBool());
        Assert.IsFalse(report.ps["is_report_results_visible"].toBool());
        Assert.AreEqual("Report doesn't work. Contact Site Administrator.", report.ps["report_error_message"]);
    }

    [TestMethod]
    public void AdminReportsController_AllowsLoggedUsersForCustomReportGate()
    {
        Assert.AreEqual(Users.ACL_MEMBER, AdminReportsController.access_level);
    }

    [TestMethod]
    public void AdminReportsController_RejectsCustomCodeThatMatchesHardcodedReport()
    {
        var fw = TestHelpers.CreateFw();
        var controller = new AdminReportsController();
        controller.init(fw);
        var item = new FwDict
        {
            ["icode"] = "Sample",
            ["iname"] = "Sample Custom",
            ["sql_template"] = "select 1 as id"
        };
        Assert.ThrowsExactly<ValidationException>(() => controller.validateCustomReport(0, item));
        Assert.AreEqual("HARDCODED", fw.FormErrors["icode"]);
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

    [TestMethod]
    public void Init_DefaultReportUsesFrameworkDb()
    {
        var fw = TestHelpers.CreateFw();
        var report = new DbExposedReportForTest();

        report.init(fw, "sample", []);

        Assert.AreSame(fw.db, report.RuntimeDb);
    }

    [TestMethod]
    public void Init_ConfiguredReportUsesNamedDb()
    {
        var fw = TestHelpers.CreateFw();
        addReadonlyDbConfig(fw);
        var report = new DbExposedReportForTest("readonly");

        report.init(fw, "sample", []);

        Assert.AreNotSame(fw.db, report.RuntimeDb);
        Assert.AreEqual("readonly", report.RuntimeDb.db_name);
        Assert.AreEqual(DB.DBTYPE_SQLITE, report.RuntimeDb.dbtype);
    }

    [TestMethod]
    public void CustomReport_ConfiguredDbKeepsMetadataOnFrameworkDb()
    {
        var fw = TestHelpers.CreateFw();
        addReadonlyDbConfig(fw);
        var report = new DbConfiguredCustomReportForTest(new FwDict
        {
            ["icode"] = "custom",
            ["iname"] = "Custom",
            ["access_level"] = Users.ACL_SITEADMIN,
            ["sql_template"] = "select 1 as id",
            ["params_json"] = "",
            ["render_options_json"] = ""
        });

        report.init(fw, "custom", []);

        Assert.AreEqual("readonly", report.RuntimeDb.db_name);
        Assert.AreEqual(DB.DBTYPE_SQLITE, report.RuntimeDb.dbtype);
        Assert.AreSame(fw.db, report.MetadataModel.getDB());
    }

    [TestMethod]
    public void ListParamOptions_UsesProvidedDbForSqlLookup()
    {
        var fw = TestHelpers.CreateFw();
        var model = new FwReports();
        model.init(fw);
        var lookupDb = new TrackingDb("reportdb");
        var def = new FwDict
        {
            ["type"] = "lookup_sql",
            ["source"] = "select id, iname from report_values"
        };

        var options = model.listParamOptions(def, lookupDb);

        Assert.AreEqual(1, lookupDb.ArraypLimitCalls);
        StringAssert.Contains(lookupDb.LastSql, "report_values");
        Assert.AreEqual("reportdb", lookupDb.db_name);
        Assert.AreEqual(1, options.Count);
        Assert.AreEqual("Report Value", options[0]["iname"]);
        Assert.AreSame(fw.db, model.getDB());
    }

    private static void addReadonlyDbConfig(FW fw)
    {
        var dbConfig = fw.config("db") as FwDict ?? [];
        dbConfig["readonly"] = new FwDict
        {
            ["type"] = DB.DBTYPE_SQLITE,
            ["connection_string"] = "Data Source=:memory:"
        };
        fw.config()["db"] = dbConfig;
    }

    private sealed class DbExposedReportForTest : FwReportsBase
    {
        public DbExposedReportForTest(string dbConfig = "")
        {
            db_config = dbConfig;
        }

        public DB RuntimeDb => db;
    }

    private sealed class DbConfiguredCustomReportForTest : FwCustomReport
    {
        public DbConfiguredCustomReportForTest(FwDict report) : base(report)
        {
            db_config = "readonly";
        }

        public DB RuntimeDb => db;

        public FwReports MetadataModel
        {
            get
            {
                return ReportModel;
            }
        }
    }

    private sealed class TrackingDb(string dbName) : DB("", DB.DBTYPE_SQLSRV, dbName)
    {
        public int ArraypLimitCalls { get; private set; }
        public string LastSql { get; private set; } = string.Empty;

        public override DBList arrayp(string sql, FwDict? @params, int limit)
        {
            ArraypLimitCalls++;
            LastSql = sql;
            return
            [
                new DBRow
                {
                    ["id"] = "1",
                    ["iname"] = "Report Value"
                }
            ];
        }
    }

    private sealed class JsonReportForTest : FwReportsBase
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
