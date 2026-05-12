using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

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
