using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace osafw.Tests;

[TestClass, TestCategory("SqlServer")]
public class ReportsModulesSqlServerTests
{
    [TestMethod]
    public void NativeReportExecutionKeepsLookupSqlPrivateAndDisabledModulesUnroutable()
    {
        if (Environment.GetEnvironmentVariable("FW_TEST_SQLSERVER") != "1")
            Assert.Inconclusive("Run the SqlServer profile.");
        using var database = new SqlServerTestDatabase();
        using var scope = new FwTestScope(name => new DB(database.ConnectionString, DB.DBTYPE_SQLSRV, name),
            context: TestHelpers.CreateHttpContext());
        var fw = scope.Fw;
        fw.Session("user_id", "1");
        fw.Session("access_level", Users.ACL_MEMBER.toStr());
        var definitions = Utils.jsonEncode(DB.h("choice", DB.h("type", "lookup_sql", "label", "Choice",
            "source", "SELECT 7 AS id, N'Visible choice' AS iname /* private-lookup-marker */",
            "internal_note", "private-definition-marker")));
        fw.db.insert("fwreports", DB.h("icode", "NativeReport", "iname", "Native report", "access_level", Users.ACL_MEMBER,
            "sql_template", "SELECT @choice AS visible_result", "params_json", definitions));
        fw.response.Body = new MemoryStream();
        var report = FwReportsBase.createInstance(fw, "NativeReport", DB.h("format", "json", "choice", "7"));
        report.setFilters();
        report.getData();
        report.render();
        fw.response.Body.Position = 0;
        var body = new StreamReader(fw.response.Body).ReadToEnd();
        StringAssert.Contains(body, "Visible choice");
        StringAssert.Contains(body, "visible_result");
        Assert.IsFalse(body.Contains("private-lookup-marker", StringComparison.Ordinal));
        Assert.IsFalse(body.Contains("private-definition-marker", StringComparison.Ordinal));

        fw.Session("access_level", Users.ACL_SITEADMIN.toStr());
        var code = "NativeModule" + Guid.NewGuid().ToString("N");
        var config = Utils.jsonEncode(DB.h("model", "Demos", "save_fields", "iname",
            "controller", DB.h("url", "/" + code, "type", "vue")));
        var id = fw.db.insert("fwcontrollers", DB.h("icode", code, "url", "/" + code, "model", "Demos",
            "config", config, "status", FwModel.STATUS_INACTIVE));
        var modules = fw.model<FwControllers>();
        modules.is_log_changes = false;
        try
        {
            Assert.AreEqual(id, modules.oneByIcode(code)["id"].toInt());
            Assert.IsNull(fw.controller(code));
            Assert.IsFalse(modules.listGrouped().Any(row => row["icode"] == code));
            modules.update(id, DB.h("status", FwModel.STATUS_DELETED));
            Assert.IsNull(fw.controller(code));
            modules.update(id, DB.h("status", FwModel.STATUS_ACTIVE));
            Assert.IsNotNull(fw.controller(code));
            Assert.IsTrue(modules.listGrouped().Any(row => row["icode"] == code));
        }
        finally
        {
            modules.removeCacheAll();
        }
    }
}
