using Microsoft.VisualStudio.TestTools.UnitTesting;

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
}
