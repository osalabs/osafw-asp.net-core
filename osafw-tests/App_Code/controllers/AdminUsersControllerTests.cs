using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using System.Collections;

namespace osafw.Tests;

public class TestAdminUsersController : osafw.AdminUsersController
{
    private readonly InMemoryUsersModel testModel = new();

    public override void init(osafw.FW fw)
    {
        base.init(fw);
        // replace model with in-memory version
        testModel.init(fw);
        this.model0 = testModel;
        this.model = testModel;
    }

    public InMemoryUsersModel Model => testModel;
}

[TestClass]
public class AdminUsersControllerTests
{
    private TestAdminUsersController controller = null!;

    [TestInitialize]
    public void Setup()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var fw = osafw.FW.initOffline(config);
        controller = new TestAdminUsersController();
        controller.init(fw);
    }

    [TestMethod]
    public void SaveActionCreatesUser()
    {
        var user = osafw.DB.h("fname", "Jane", "lname", "Doe", "email", "jane@example.com", "pwd", "pass");
        controller.Model.add(user); // use model directly to bypass form processing
        var ps = controller.IndexAction();
        Assert.IsNotNull(ps);
        var list = controller.Model.list();
        Assert.AreEqual(1, list.Count);
    }
}
