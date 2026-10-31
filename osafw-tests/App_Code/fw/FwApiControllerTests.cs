using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;

namespace osafw.Tests;

[TestClass]
public class FwApiControllerTests
{
    private sealed class StubUsers : Users
    {
        public override bool isReadOnly(int id = -1) => false;

        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    private sealed class StubSettings : Settings
    {
        public override string readSecret(string code) => code == "API_KEY" ? "secret" : "";
    }

    private sealed class UnavailableSettings : Settings
    {
        public override string readSecret(string code) => throw new System.InvalidOperationException("Settings unavailable");
    }

    private class TestApiController : FwApiController
    {
        public void InvokePrepare(bool isAuth = true) => base.prepare(isAuth);
    }

    private static FW BuildFw()
    {
        var fw = TestHelpers.CreateFw();
        var settings = FwConfig.GetCurrentSettings();
        settings["hostname"] = "example.com";
        settings["API_ALLOW_ORIGIN"] = "http://allowed.test";
        TestHelpers.RegisterModel(fw, (Users)new StubUsers());
        TestHelpers.RegisterModel(fw, (Settings)new StubSettings());
        return fw;
    }

    [TestMethod]
    public void Prepare_RejectsInvalidOrigin()
    {
        var fw = BuildFw();
        fw.request.Headers.Origin = "http://evil.test";
        var controller = new TestApiController();
        controller.init(fw);

        try
        {
            controller.InvokePrepare();
            Assert.Fail("Expected auth failure");
        }
        catch (AuthException)
        {
        }
    }

    [TestMethod]
    public void Prepare_RejectsBadApiKey()
    {
        var fw = BuildFw();
        fw.request.Headers.Origin = "http://example.com";
        fw.request.Headers["X-API-Key"] = "wrong";
        var controller = new TestApiController();
        controller.init(fw);

        AuthException? ex = null;
        try
        {
            controller.InvokePrepare();
        }
        catch (AuthException aex)
        {
            ex = aex;
        }
        Assert.IsNotNull(ex);
        Assert.AreEqual((int)HttpStatusCode.Forbidden, fw.response.StatusCode);
        Assert.IsTrue(ex!.Message.Contains("auth", System.StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Prepare_LoggedInUserDoesNotNeedApiKeyStorage()
    {
        var fw = BuildFw();
        fw.request.Headers.Origin = "http://example.com";
        var controller = new TestApiController();
        controller.init(fw);
        fw.Session("user_id", "1");
        TestHelpers.RegisterModel(fw, (Settings)new UnavailableSettings());

        controller.InvokePrepare();

        Assert.AreEqual((int)HttpStatusCode.OK, fw.response.StatusCode);
        Assert.AreEqual("http://example.com", fw.response.Headers.AccessControlAllowOrigin.ToString());
    }

    [TestMethod]
    public void Prepare_AllowsConfiguredApiKey()
    {
        var fw = BuildFw();
        fw.request.Headers.Origin = "http://example.com";
        fw.request.Headers["X-API-Key"] = "secret";
        var controller = new TestApiController();
        controller.init(fw);

        controller.InvokePrepare();

        Assert.AreEqual("http://example.com", fw.response.Headers.AccessControlAllowOrigin.ToString());
        StringAssert.Contains(fw.response.Headers.AccessControlAllowMethods!, "GET");
    }
}
