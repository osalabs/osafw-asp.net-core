using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class DevCliTests
{
    [TestMethod]
    public void IsCommand_MatchesScaffoldCaseInsensitively()
    {
        Assert.IsTrue(DevCli.isCommand(["ScAfFoLd", "--help"]));
        Assert.IsFalse(DevCli.isCommand(["run"]));
        Assert.IsFalse(DevCli.isCommand([]));
    }

    [TestMethod]
    public void TryParse_CrudAcceptsOverridesAndNormalizesDynamicType()
    {
        var parsed = DevCli.tryParse(
            ["scaffold", "crud", "sales_orders", "--model", "SalesOrders", "--url", "/Admin/SalesOrders", "--title", "Sales Orders", "--type", "dynamic", "--force"],
            out var options,
            out var error);

        Assert.IsTrue(parsed, error);
        Assert.AreEqual("crud", options.Operation);
        Assert.AreEqual("sales_orders", options.Subject);
        Assert.AreEqual("SalesOrders", options.ModelName);
        Assert.AreEqual("/Admin/SalesOrders", options.ControllerUrl);
        Assert.AreEqual("Sales Orders", options.ControllerTitle);
        Assert.AreEqual(string.Empty, options.ControllerType);
        Assert.IsTrue(options.Force);
    }

    [TestMethod]
    public void TryParse_ModelUsesNameOption()
    {
        var parsed = DevCli.tryParse(
            ["scaffold", "model", "sales_orders", "--name", "SalesOrders"],
            out var options,
            out var error);

        Assert.IsTrue(parsed, error);
        Assert.AreEqual("SalesOrders", options.ModelName);
    }

    [TestMethod]
    public void TryParse_RejectsUnsafeControllerUrl()
    {
        var parsed = DevCli.tryParse(
            ["scaffold", "controller", "SalesOrders", "--url", "/Admin/../../Program"],
            out _,
            out var error);

        Assert.IsFalse(parsed);
        StringAssert.Contains(error, "exactly two");
    }

    [TestMethod]
    public void TryParse_RejectsOptionsFromAnotherOperation()
    {
        var parsed = DevCli.tryParse(
            ["scaffold", "report", "sales-summary", "--force"],
            out _,
            out var error);

        Assert.IsFalse(parsed);
        StringAssert.Contains(error, "not supported");
    }

    [TestMethod]
    public void TryParse_RejectsInvalidModelName()
    {
        var parsed = DevCli.tryParse(
            ["scaffold", "crud", "sales_orders", "--model", "sales-orders"],
            out _,
            out var error);

        Assert.IsFalse(parsed);
        StringAssert.Contains(error, "PascalCase");
    }

    [TestMethod]
    public void Run_HelpDoesNotRequireApplicationConfiguration()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = DevCli.run(
            ["scaffold", "--help"],
            new ConfigurationBuilder().Build(),
            output,
            error);

        Assert.AreEqual(DevCli.EXIT_SUCCESS, exitCode);
        StringAssert.Contains(output.ToString(), "scaffold crud <table>");
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public void Run_RejectsNonDevelopmentConfigurationBeforeFrameworkInitialization()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["appSettings:IS_DEV"] = "false",
            })
            .Build();

        var exitCode = DevCli.run(
            ["scaffold", "report", "no-write-probe"],
            configuration,
            output,
            error);

        Assert.AreEqual(DevCli.EXIT_ENVIRONMENT, exitCode);
        Assert.AreEqual(string.Empty, output.ToString());
        StringAssert.Contains(error.ToString(), "IS_DEV=true");
    }
}
