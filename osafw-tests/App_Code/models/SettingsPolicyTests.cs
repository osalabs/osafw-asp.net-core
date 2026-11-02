using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace osafw.Tests;

[TestClass]
public class SettingsPolicyTests
{
    [TestMethod]
    public void CredentialClassificationUsesInputMetadataOnly()
    {
        Assert.IsTrue(Settings.isCredential(DB.h("icode", "arbitrary", "input", Settings.INPUT_CREDENTIAL)));
        Assert.IsFalse(Settings.isCredential(DB.h("icode", "mail.password", "input", Settings.INPUT_TEXT)));
    }

    [TestMethod]
    public void LocalUrlValidationUsesMetadataForAnySettingCode()
    {
        using var scope = new FwTestScope(_ => new RejectingDb());
        var model = scope.Fw.settings;
        var row = DB.h("icode", "custom_destination", "input", Settings.INPUT_TEXT,
            "allowed_values", "local_url|1");

        Assert.AreEqual("", model.valueError(row, "/Workspace"));
        Assert.AreEqual("INVALID", model.valueError(row, "https://outside.example/"));

        row["allowed_values"] = "";
        Assert.AreEqual("", model.valueError(row, "https://outside.example/"));
    }

}
