using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class FwVirtualControllerTests
{
    private class StubUsers : Users
    {
        private readonly bool editAllowed;

        public StubUsers(bool editAllowed = true) => this.editAllowed = editAllowed;

        public override void checkReadOnly(int id = -1) { }
        public override bool isReadOnly(int id = -1) => false;
        public override bool isAccessLevel(int min_acl) => editAllowed;
        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    private class StubModel : FwModel
    {
        public StubModel() : base() => table_name = "stub";
        public override void convertUserInput(FwDict item) { }
    }

    private static FwDict DefaultControllerConfig(string url = "/Virtual")
    {
        var config = new FwDict
        {
            ["model"] = "StubModel",
            ["controller"] = new FwDict
            {
                ["url"] = url,
                ["title"] = "Virtual",
                ["type"] = "vue",
                ["is_dynamic_index_edit"] = true
            },
            ["save_fields"] = "iname"
        };
        return config;
    }

    private static FwDict BuildFwController(FwDict? config = null)
    {
        config ??= DefaultControllerConfig();
        return new FwDict
        {
            ["url"] = config["controller"] is FwDict controller ? controller["url"] : "/Virtual",
            ["iname"] = "Virtual",
            ["model"] = "StubModel",
            ["config"] = Utils.jsonEncode(config),
            ["access_level_edit"] = Users.ACL_SITEADMIN
        };
    }

    private static FW BuildFw(string templateRoot, bool editAllowed = true)
    {
        var fw = TestHelpers.CreateFw();
        FwConfig.GetCurrentSettings()["template"] = templateRoot;
        TestHelpers.RegisterModel(fw, (Users)new StubUsers(editAllowed));
        TestHelpers.RegisterModel(fw, new StubModel());
        return fw;
    }

    [TestMethod]
    public void VirtualController_UsesCommonTemplatesByDefault()
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), "virtual-tests-" + System.Guid.NewGuid());
        Directory.CreateDirectory(templateRoot);
        var fw = BuildFw(templateRoot);
        var controller = new FwVirtualController(fw, BuildFwController());

        Assert.AreEqual("/common/virtual", controller.template_basedir);
    }

    [TestMethod]
    public void VirtualController_PrefersControllerTemplatesWhenPresent()
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), "virtual-tests-" + System.Guid.NewGuid());
        var controllerDir = Path.Combine(templateRoot, "virtual", "index");
        Directory.CreateDirectory(controllerDir);
        var fw = BuildFw(templateRoot);
        var controller = new FwVirtualController(fw, BuildFwController());

        Assert.AreEqual("/virtual", controller.template_basedir);
    }

    [TestMethod]
    public void VirtualController_BlocksMutationBelowConfiguredEditAccess()
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), "virtual-tests-" + System.Guid.NewGuid());
        Directory.CreateDirectory(templateRoot);
        var fw = BuildFw(templateRoot, editAllowed: false);
        var controller = new FwVirtualController(fw, BuildFwController());

        Assert.ThrowsExactly<AuthException>(() => controller.checkReadOnly());
    }
}
