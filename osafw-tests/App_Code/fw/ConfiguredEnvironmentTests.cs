using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace osafw.Tests;

[TestClass, DoNotParallelize]
public class ConfiguredEnvironmentTests
{
    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["appSettings:SITE_NAME"] = "Base",
        ["appSettings:ROOT_DOMAIN"] = "https://example.test",
        ["appSettings:override:Hosted:SITE_NAME"] = "Hosted",
        ["appSettings:override:Asp:SITE_NAME"] = "Asp",
        ["appSettings:override:Dotnet:SITE_NAME"] = "Dotnet",
        ["appSettings:override:Tenant:hostname_match"] = "tenant.example.test",
        ["appSettings:override:Tenant:ROOT_DOMAIN"] = "https://tenant.example.test",
        ["appSettings:override:Tenant:SITE_NAME"] = "Tenant",
    }).Build();

    [TestMethod]
    public void ProgramCliUsesTheActualHostResolvedEnvironmentBeforeEarlyExit()
    {
        using var scope = FwConfig.beginScope();
        var asp = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var exitCode = Environment.ExitCode;
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Asp");
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Dotnet");
            // The real builder resolves conflicting host inputs; help exits before DB/scaffold work.
            Program.Main(["scaffold", "--help"]);
            Assert.AreEqual(0, Environment.ExitCode);
            Assert.AreEqual("Dotnet", FwConfig.settingsForEnvironment(Configuration())["SITE_NAME"]);
            FwConfig.init(null, Configuration());
            Assert.AreEqual("Dotnet", FwConfig.GetCurrentSetting("SITE_NAME"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", asp);
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", dotnet);
            Environment.ExitCode = exitCode;
        }
    }
    [TestMethod]
    public void ExplicitHostSelectionAppliesToStartupAndOfflineAndScopesRestoreIndependently()
    {
        using var scope = FwConfig.beginScope();
        var configuration = Configuration();
        FwConfig.setDefaultOverrideName(" Hosted ");
        Assert.AreEqual("Hosted", FwConfig.settingsForEnvironment(configuration)["SITE_NAME"]);
        FwConfig.init(null, configuration);
        Assert.AreEqual("Hosted", FwConfig.GetCurrentSetting("SITE_NAME"));
        using (FwConfig.beginScope())
        {
            FwConfig.setDefaultOverrideName("Dotnet");
            FwConfig.init(null, configuration);
            Assert.AreEqual("Dotnet", FwConfig.GetCurrentSetting("SITE_NAME"));
        }
        Assert.AreEqual("Hosted", FwConfig.GetCurrentSetting("SITE_NAME"));
        FwConfig.init(null, configuration, "tenant.example.test");
        Assert.AreEqual("Tenant", FwConfig.GetCurrentSetting("SITE_NAME"));
        Assert.IsFalse(FwConfig.isTrustedHost("attacker.example.test"));
        FwConfig.setDefaultOverrideName("Dotnet");
        FwConfig.init(null, configuration);
        Assert.AreEqual("Dotnet", FwConfig.GetCurrentSetting("SITE_NAME"));
    }

    [TestMethod]
    public void FallbackTrimsAspThenDotnetAndExplicitSelectionWinsBoth()
    {
        using var scope = FwConfig.beginScope();
        var asp = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        try
        {
            var configuration = Configuration();
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", " Asp ");
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", " Dotnet ");
            Assert.AreEqual("Asp", FwConfig.settingsForEnvironment(configuration)["SITE_NAME"]);
            FwConfig.setDefaultOverrideName("Hosted");
            Assert.AreEqual("Hosted", FwConfig.settingsForEnvironment(configuration)["SITE_NAME"]);
            FwConfig.setDefaultOverrideName(null);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", " ");
            Assert.AreEqual("Dotnet", FwConfig.settingsForEnvironment(configuration)["SITE_NAME"]);
            FwConfig.init(null, configuration);
            Assert.AreEqual("Dotnet", FwConfig.GetCurrentSetting("SITE_NAME"));
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
            Assert.AreEqual("Base", FwConfig.settingsForEnvironment(configuration)["SITE_NAME"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", asp);
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", dotnet);
        }
    }
}
