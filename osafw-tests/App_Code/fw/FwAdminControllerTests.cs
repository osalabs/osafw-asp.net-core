using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace osafw.Tests;

[TestClass]
public class FwAdminControllerTests
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
        public int Added { get; private set; }
        public int Updated { get; private set; }

        public StubModel() : base() => table_name = "stub";

        public override int add(FwDict item)
        {
            Added++;
            return 42;
        }

        public override void convertUserInput(FwDict item)
        {
            // no-op for tests
        }

        public override bool update(int id, FwDict item)
        {
            Updated++;
            return true;
        }

        public override DBRow one(int id) => new DBRow(new FwDict { ["id"] = id.toStr(), ["iname"] = "existing" });
    }

    private class TestAdminController : FwAdminController
    {
        public TestAdminController(FW fw, StubModel model)
        {
            init(fw);
            this.model0 = model;
            this.db = fw.db;
            base_url = "/admin/items";
            save_fields = "iname";
        }
    }

    private static (FW fw, StubModel model, TestAdminController controller) BuildController(bool expectJson = false)
    {
        var fw = TestHelpers.CreateFw();
        fw.route.method = "GET";
        if (expectJson)
            fw.request.Headers.Accept = "application/json";

        var model = new StubModel();
        TestHelpers.RegisterModel(fw, (Users)new StubUsers());
        var controller = new TestAdminController(fw, model);
        return (fw, model, controller);
    }

    [TestMethod]
    public void ShowFormAction_BuildsDefaultsForNewItem()
    {
        var (fw, _, controller) = BuildController();
        controller.form_new_defaults = new FwDict { ["iname"] = "from-defaults" };

        var ps = controller.ShowFormAction(0)!;

        var item = ps["i"] as FwDict ?? [];
        Assert.AreEqual("from-defaults", item["iname"]);
        Assert.AreEqual(0, ps["id"]);
    }

    [TestMethod]
    public void SaveAction_AddsNewRecord()
    {
        var (fw, model, controller) = BuildController(expectJson: true);
        fw.FORM["item"] = new FwDict { ["iname"] = "new-item" };

        var ps = controller.SaveAction(0)!;

        Assert.AreEqual(1, model.Added);
        var json = ps["_json"] as FwDict ?? [];
        Assert.AreEqual(42, json["id"]);
        Assert.IsTrue(json["success"].toBool());
    }

    [TestMethod]
    public void SaveAction_UpdatesExistingRecord()
    {
        var (fw, model, controller) = BuildController(expectJson: true);
        fw.FORM["item"] = new FwDict { ["iname"] = "updated" };

        controller.SaveAction(7);

        Assert.AreEqual(1, model.Updated);
    }
}
