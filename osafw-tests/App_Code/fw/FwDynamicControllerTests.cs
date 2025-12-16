using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace osafw.Tests;

[TestClass]
public class FwDynamicControllerTests
{
    private class TestDynamicController : FwDynamicController
    {
        private readonly StrList ids;

        public TestDynamicController(StrList ids)
        {
            this.ids = ids;
            base_url = "/dynamic";
        }

        public override void init(FW fw)
        {
            base.init(fw);
            fw.route.method = "GET";
            list_sortmap = Utils.qh("id|id");
            list_sortdef = "id asc";
        }

        public override FwDict initFilter(string? session_key = null)
        {
            list_filter = [];
            return list_filter;
        }

        public override void setListSorting() { }
        public override void setListSearch() { }
        public override void setListSearchStatus() { }

        public override StrList getListIds(string list_view = "")
        {
            return ids;
        }
    }

    private static FwDict RunNext(StrList ids, int id, bool isPrev = false, bool isEdit = false)
    {
        var fw = TestHelpers.CreateFw();
        var controller = new TestDynamicController(ids);
        controller.init(fw);
        fw.FORM["prev"] = isPrev ? "1" : "0";
        fw.FORM["edit"] = isEdit ? "1" : "0";
        return controller.NextAction(id.toStr());
    }

    [TestMethod]
    public void NextAction_ReturnsNextId()
    {
        var result = RunNext(new StrList { "1", "2", "3" }, 1);

        Assert.AreEqual(2, result["id"]);
        Assert.AreEqual("/dynamic/2", result["_redirect"]);
    }

    [TestMethod]
    public void NextAction_WrapsAroundAndKeepsMode()
    {
        var result = RunNext(new StrList { "5", "6" }, 6, isPrev: true, isEdit: true);

        Assert.AreEqual(5, result["id"]);
        Assert.AreEqual("/dynamic/5/edit", result["_redirect"]);
    }
}
