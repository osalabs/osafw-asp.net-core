using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace osafw.Tests;

[TestClass]
public class FwControllerBehaviorTests
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
        public int AddCalls { get; private set; }
        public int UpdateCalls { get; private set; }

        public StubModel() : base() => table_name = "stub";

        public override int add(FwDict item)
        {
            AddCalls++;
            return item["id"].toInt(1);
        }

        public override bool update(int id, FwDict item)
        {
            UpdateCalls++;
            return true;
        }
    }

    private class TestController : FwController
    {
        public TestController(FW fw, StubModel model) : base(fw)
        {
            this.model0 = model;
            this.db = fw.db;
            base_url = "/items";
        }

        public void SetForm(FwDict form) => fw.FORM = form;

        public void SetNavigation(string relatedId, string tab)
        {
            related_id = relatedId;
            form_tab = tab;
        }

        public void ConfigureSorting(string sortdef, FwDict sortmap, FwDict filter)
        {
            list_sortdef = sortdef;
            list_sortmap = sortmap;
            list_filter = filter;
        }

        public FwDict CurrentFilter => list_filter;
        public string CurrentOrderBy => list_orderby;
    }

    private static (FW fw, StubModel model, TestController controller) BuildController(bool expectJson = false)
    {
        var fw = TestHelpers.CreateFw();
        if (expectJson)
            fw.request.Headers.Accept = "application/json";

        var model = new StubModel();
        TestHelpers.RegisterModel(fw, (Users)new StubUsers());
        var controller = new TestController(fw, model);
        return (fw, model, controller);
    }

    [TestMethod]
    public void ValidateRequired_FlagsMissingFields()
    {
        var (fw, _, controller) = BuildController();
        var item = new FwDict { ["present"] = "value" };

        var result = controller.validateRequired(0, item, new[] { "present", "missing" });

        Assert.IsFalse(result);
        Assert.IsTrue(fw.FormErrors.ContainsKey("missing"));
        Assert.IsTrue(fw.FormErrors.ContainsKey("REQUIRED"));
    }

    [TestMethod]
    public void ValidateCheckResult_ThrowsWhenErrorsPresent()
    {
        var (fw, _, controller) = BuildController();
        fw.FormErrors["field"] = "invalid";

        try
        {
            controller.validateCheckResult();
            Assert.Fail("Expected ValidationException");
        }
        catch (ValidationException)
        {
        }
        Assert.IsTrue(fw.FormErrors.ContainsKey("INVALID"));
    }

    [TestMethod]
    public void AfterSaveLocation_ComposesQueryString()
    {
        var (fw, _, controller) = BuildController();
        controller.base_url_suffix = "foo=1";
        controller.SetNavigation("33", "details");

        var url = controller.afterSaveLocation("5");

        Assert.AreEqual("/items/5/edit?related_id=33&foo=1&tab=details", url);
    }

    [TestMethod]
    public void AfterSave_ReturnsJsonPayload()
    {
        var (fw, _, controller) = BuildController(expectJson: true);
        fw.G["err_msg"] = "boom";
        fw.FormErrors["field"] = "missing";

        var ps = controller.afterSave(false, 9, false, "ShowForm", "/items/9");

        Assert.IsNotNull(ps);
        var json = ps!["_json"] as FwDict ?? [];
        Assert.AreEqual(9, json["id"]);
        Assert.AreEqual("/items/9", json["location"]);
        var error = json["error"] as FwDict ?? [];
        Assert.AreEqual("boom", error["message"]);
        Assert.IsGreaterThan(0, ((FwDict)error["details"]!)["field"].toStr().Length);
    }

    [TestMethod]
    public void SetListSorting_UsesDefaultsWhenMissingInput()
    {
        var (_, _, controller) = BuildController();
        controller.ConfigureSorting("iname desc", Utils.qh("iname|iname"), []);

        controller.setListSorting();

        Assert.AreEqual("iname", controller.CurrentFilter["sortby"]);
        Assert.AreEqual("desc", controller.CurrentFilter["sortdir"]);
        StringAssert.Contains(controller.CurrentOrderBy, "iname");
    }
}
