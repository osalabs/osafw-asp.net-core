using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace osafw.Tests;

[TestClass]
public class FwDynamicControllerTests
{
    private class TestModel : FwModel
    {
        public TestModel()
        {
            table_name = "test_records";
        }
    }

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
            model0 = new TestModel();
            model0.init(fw);
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

    [TestMethod]
    public void PrepareFields_PrettyPrintsPlaintextJsonAndKeepsInvalidText()
    {
        var fw = TestHelpers.CreateFw();
        var controller = new TestDynamicController(new StrList());
        controller.init(fw);
        controller.loadControllerConfig(new FwDict
        {
            ["show_fields"] = new FwList
            {
                new FwDict { ["field"] = "metadata_json", ["type"] = "plaintext_json" },
                new FwDict { ["field"] = "bad_json", ["type"] = "plaintext_json" }
            },
            ["showform_fields"] = new FwList
            {
                new FwDict { ["field"] = "metadata_json", ["type"] = "plaintext_json" },
                new FwDict { ["field"] = "bad_json", ["type"] = "plaintext_json" }
            }
        });
        var item = new FwDict
        {
            ["id"] = 1,
            ["metadata_json"] = "{\"b\":2,\"a\":[1]}",
            ["bad_json"] = "{bad"
        };

        var showFields = controller.prepareShowFields(item, []);
        var showFormFields = controller.prepareShowFormFields(item, []);

        StringAssert.Contains(((FwDict)showFields[0])["value"].toStr(), "\n");
        StringAssert.Contains(((FwDict)showFields[0])["value"].toStr(), "\"b\": 2");
        StringAssert.Contains(((FwDict)showFields[0])["value"].toStr(), "\"a\": [");
        Assert.AreEqual("{bad", ((FwDict)showFields[1])["value"]);
        StringAssert.Contains(((FwDict)showFormFields[0])["value"].toStr(), "\"b\": 2");
        Assert.AreEqual("{bad", ((FwDict)showFormFields[1])["value"]);
    }
}
