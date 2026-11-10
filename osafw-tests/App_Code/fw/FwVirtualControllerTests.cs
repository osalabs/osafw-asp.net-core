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
        public override string iname(int id) => "";
        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    private class StubUserViews : UserViews
    {
        public override DBRow oneByIcode(string icode) => [];
        public override FwList listSelectByIcode(string icode) => [];
    }

    private class StubModel : FwModel
    {
        public StubModel() : base() => table_name = "stub";
        public FwDict Saved { get; private set; } = [];

        public override DBRow one(int id) => new() { ["id"] = id.toStr(), ["icode"] = "original" };

        public override int add(FwDict item)
        {
            Saved = item;
            return 7;
        }

        public override bool update(int id, FwDict item)
        {
            Saved = item;
            return true;
        }

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
    [DataRow(false, true)]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public void VirtualController_ProtectsLookupCodeOnEditUnlessConfigOptsIn(bool is_editable, bool is_code_on_form)
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), "virtual-tests-" + System.Guid.NewGuid());
        var controllerDir = Directory.CreateDirectory(Path.Combine(templateRoot, "virtual"));
        try
        {
            if (is_editable)
                File.WriteAllText(Path.Combine(controllerDir.FullName, "config.json"), "{\"is_lookup_icode_editable\":true}");

            var fw = BuildFw(templateRoot);
            var model = fw.model<StubModel>();
            fw.request.Headers.Accept = "application/json";
            TestHelpers.RegisterModel(fw, (UserViews)new StubUserViews());
            var config = DefaultControllerConfig();
            config["save_fields"] = "icode iname";
            FwList fields = [new FwDict { ["field"] = "iname", ["type"] = "input" }];
            if (is_code_on_form)
                fields.Add(new FwDict { ["field"] = "icode", ["type"] = "input" });
            config["showform_fields"] = fields;

            var controller = new FwVirtualController(fw, BuildFwController(config));

            fw.FORM["scope"] = "init";
            var initialState = controller.IndexAction();
            var formFields = new FwList((System.Collections.IList)initialState["showform_fields"]!);
            Assert.IsFalse(formFields[0]["is_edit_readonly"].toBool());
            if (is_code_on_form)
                Assert.AreEqual(!is_editable, formFields[1]["is_edit_readonly"].toBool());

            fw.request.Method = "POST";
            fw.route.method = "POST";
            fw.FORM["item"] = new FwDict { ["icode"] = "changed", ["iname"] = "Edited" };
            controller.SaveAction(7);
            Assert.AreEqual(is_editable ? "changed" : null, model.Saved["icode"]);
            Assert.AreEqual("Edited", model.Saved["iname"]);

            fw.FORM["item"] = new FwDict { ["icode"] = "new-code", ["iname"] = "New" };
            controller.SaveAction(0);
            Assert.AreEqual("new-code", model.Saved["icode"]);
        }
        finally
        {
            File.Delete(Path.Combine(controllerDir.FullName, "config.json"));
            controllerDir.Delete();
            Directory.Delete(templateRoot);
        }
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
