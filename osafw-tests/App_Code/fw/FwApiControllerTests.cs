using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;

namespace osafw.Tests;

[TestClass]
public class FwApiControllerTests
{
    private class TestApiController : FwApiController
    {
        public void InvokePrepare(bool isAuth = true) => base.prepare(isAuth);
    }

    private static FW BuildFw()
    {
        var fw = TestHelpers.CreateFw();
        FwConfig.settings["hostname"] = "example.com";
        FwConfig.settings["API_ALLOW_ORIGIN"] = "http://allowed.test";
        FwConfig.settings["API_KEY"] = "secret";
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
