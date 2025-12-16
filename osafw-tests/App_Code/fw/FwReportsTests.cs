using Microsoft.VisualStudio.TestTools.UnitTesting;

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
}
