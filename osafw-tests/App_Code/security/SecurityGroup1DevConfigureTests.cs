using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace osafw.Tests;

[TestClass]
public class SecurityGroup1DevConfigureTests
{
    private const string CanonicalOrigin = "https://app.example.test";
    private const string SpoofedHost = "evil.example.test";

    private sealed class InitDbProbeController : DevConfigureController
    {
        public bool InitDatabaseCalled { get; private set; }

        protected override FwDict initDatabase()
        {
            InitDatabaseCalled = true;
            return new FwDict { ["ok"] = true };
        }
    }

    [TestMethod]
    public void HostTrust_RejectsUnconfiguredHost()
    {
        var config = configWithRootAndDevelopmentOverride();
        _ = createFw(config, "app.example.test");

        Assert.IsFalse(FwConfig.isTrustedHost(SpoofedHost));
    }

    [TestMethod]
    public void HostTrust_AcceptsConfiguredRootAndOverride()
    {
        var config = configWithRootAndDevelopmentOverride();
        _ = createFw(config, "app.example.test");

        Assert.IsTrue(FwConfig.isTrustedHost("app.example.test"));
        Assert.IsTrue(FwConfig.isTrustedHost("localhost:44315"));
    }

    [TestMethod]
    public void HostTrust_DoesNotTrustWildcardOverridePattern()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["appSettings:override:Development:hostname_match"] = ".*",
                ["appSettings:override:Development:IS_DEV"] = "true",
            })
            .Build();
        _ = createFw(config, "localhost");

        Assert.IsFalse(FwConfig.isTrustedHost(SpoofedHost));
        Assert.IsFalse(FwConfig.isTrustedHost("localhost"));
    }

    [TestMethod]
    public void HostTrust_DoesNotSubstringMatchOverridePattern()
    {
        var config = configWithRootAndDevelopmentOverride();
        _ = createFw(config, "app.example.test");

        Assert.IsFalse(FwConfig.isTrustedHost("localhost.attacker.example"));

        var fw = createFw(config, "localhost.attacker.example");
        Assert.AreEqual(CanonicalOrigin, fw.config("ROOT_DOMAIN"));
        Assert.AreEqual("", fw.config("config_override").toStr());
        Assert.IsFalse(fw.config("IS_DEV").toBool());
    }

    [TestMethod]
    public void HostTrust_RejectsWhenOnlyWildcardOverrideConfigured()
    {
        var wildcardOnly = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["appSettings:override:Development:hostname_match"] = ".*",
            })
            .Build();
        _ = createFw(wildcardOnly, "localhost");

        Assert.IsFalse(FwConfig.isTrustedHost("localhost"));
        Assert.IsFalse(FwConfig.isTrustedHost(SpoofedHost));
    }

    [TestMethod]
    public void SpoofedHost_DoesNotSelectHostOverrideOrRootDomain()
    {
        var fw = createFw(configWithRootAndDevelopmentOverride(), SpoofedHost);

        Assert.AreEqual(CanonicalOrigin, fw.config("ROOT_DOMAIN"));
        Assert.AreEqual("", fw.config("config_override").toStr());
        Assert.IsFalse(fw.config("IS_DEV").toBool());
    }

    [TestMethod]
    public void TrustedHostOverride_UsesConfiguredCanonicalRootDomain()
    {
        var fw = createFw(configWithRootAndDevelopmentOverride(), "localhost:44315");

        Assert.AreEqual("Development", fw.config("config_override"));
        Assert.AreEqual("https://local.example.test", fw.config("ROOT_DOMAIN"));
        Assert.IsTrue(fw.config("IS_DEV").toBool());
    }

    [TestMethod]
    public void TrustedHostOverrideWithoutRootDomain_DerivesTrustedHostOrigin()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["appSettings:ROOT_DOMAIN"] = CanonicalOrigin,
                ["appSettings:override:Beta:hostname_match"] = "beta\\.example\\.test",
                ["appSettings:override:Beta:IS_DEV"] = "true",
            })
            .Build();

        var fw = createFw(config, "beta.example.test");

        Assert.AreEqual("Beta", fw.config("config_override"));
        Assert.AreEqual("https://beta.example.test", fw.config("ROOT_DOMAIN"));

        var spoofedFw = createFw(config, "beta.example.test.attacker");
        Assert.AreEqual("", spoofedFw.config("config_override").toStr());
        Assert.AreEqual(CanonicalOrigin, spoofedFw.config("ROOT_DOMAIN"));
    }

    [TestMethod]
    public void HostOverride_DoesNotMutateBaseNestedSettings()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["appSettings:ROOT_DOMAIN"] = CanonicalOrigin,
                ["appSettings:db:main:connection_string"] = "base-connection",
                ["appSettings:db:main:type"] = "SQLServer",
                ["appSettings:override:Development:hostname_match"] = "^localhost$",
                ["appSettings:override:Development:db:main:connection_string"] = "override-connection",
            })
            .Build();

        var overrideFw = createFw(config, "localhost:44315");
        Assert.AreEqual("override-connection", mainDb(overrideFw)["connection_string"]);

        var rootFw = createFw(config, "app.example.test");
        Assert.AreEqual("base-connection", mainDb(rootFw)["connection_string"]);
    }

    [TestMethod]
    public void PasswordResetEmail_UsesConfiguredRootDomainForSpoofedHost()
    {
        var fw = createFw(configWithRootAndDevelopmentOverride(), SpoofedHost);
        var body = fw.parsePage("/emails", "email_pwd.txt", new FwDict
        {
            ["fname"] = "Test",
            ["lname"] = "User",
            ["pwd_reset_token"] = "reset-token",
            ["email"] = "user@example.test",
        });

        Assert.IsTrue(body.Contains(CanonicalOrigin + "/PasswordReset"));
        Assert.IsFalse(body.Contains(SpoofedHost));
    }

    [TestMethod]
    public void DevConfigure_IndexAllowsAnonymousButHidesConnectionDetails()
    {
        var fw = createFw(new Dictionary<string, string?>
        {
            ["appSettings:ROOT_DOMAIN"] = CanonicalOrigin,
            ["appSettings:db:main:connection_string"] = "SensitiveServer=internal-db;SensitivePassword=secret;",
            ["appSettings:db:main:type"] = "SensitiveBogusProvider",
        });
        var controller = new DevConfigureController();
        controller.init(fw);

        controller.checkAccess();
        var ps = controller.IndexAction();
        var flattenedValues = string.Join("\n", ps.Values.Select(value => value?.ToString() ?? ""));

        Assert.IsTrue(ps["is_db_config"].toBool());
        Assert.IsFalse(ps["is_db_conn"].toBool());
        Assert.IsFalse(ps.ContainsKey("db_conn_err"));
        Assert.IsFalse(ps.ContainsKey("db_tables_err"));
        Assert.IsFalse(flattenedValues.Contains("SensitiveBogusProvider"));
        Assert.IsFalse(flattenedValues.Contains("internal-db"));
        Assert.IsFalse(flattenedValues.Contains("secret"));
    }

    [TestMethod]
    public void InitDB_RejectsGetEvenWithToken()
    {
        var fw = createFw(configWithRootAndDevelopmentOverride(), "localhost");
        fw.route.method = "GET";
        setXssTokens(fw);
        var controller = createInitDbController(fw);

        Assert.ThrowsExactly<AuthException>(() => controller.InitDBAction());
        Assert.IsFalse(controller.InitDatabaseCalled);
    }

    [TestMethod]
    public void InitDB_RejectsPostWithMissingToken()
    {
        var fw = createFw(configWithRootAndDevelopmentOverride(), "localhost");
        fw.route.method = "POST";
        fw.Session("XSS", "token");
        var controller = createInitDbController(fw);

        Assert.ThrowsExactly<AuthException>(() => controller.InitDBAction());
        Assert.IsFalse(controller.InitDatabaseCalled);
    }

    [TestMethod]
    public void InitDB_RejectsPostWithWrongToken()
    {
        var fw = createFw(configWithRootAndDevelopmentOverride(), "localhost");
        fw.route.method = "POST";
        setXssTokens(fw, formToken: "wrong-token");
        var controller = createInitDbController(fw);

        Assert.ThrowsExactly<AuthException>(() => controller.InitDBAction());
        Assert.IsFalse(controller.InitDatabaseCalled);
    }

    [TestMethod]
    public void InitDB_RejectsPostWhenNotDev()
    {
        var fw = createFw(configWithRootAndDevelopmentOverride(), CanonicalOrigin.Replace("https://", ""));
        fw.route.method = "POST";
        setXssTokens(fw);
        var controller = createInitDbController(fw);

        Assert.ThrowsExactly<AuthException>(() => controller.InitDBAction());
        Assert.IsFalse(controller.InitDatabaseCalled);
    }

    [TestMethod]
    public void InitDB_AllowsAnonymousDevPostWithToken()
    {
        var fw = createFw(configWithRootAndDevelopmentOverride(), "localhost");
        fw.route.method = "POST";
        setXssTokens(fw);
        var controller = createInitDbController(fw);

        var ps = controller.InitDBAction();

        Assert.IsTrue(controller.InitDatabaseCalled);
        Assert.IsTrue(ps["ok"].toBool());
    }

    private static InitDbProbeController createInitDbController(FW fw)
    {
        var controller = new InitDbProbeController();
        controller.init(fw);
        return controller;
    }

    private static IConfiguration configWithRootAndDevelopmentOverride()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["appSettings:ROOT_DOMAIN"] = CanonicalOrigin,
                ["appSettings:template"] = findRepoPath("osafw-app", "App_Data", "template"),
                ["appSettings:override:Development:hostname_match"] = "^localhost$",
                ["appSettings:override:Development:ROOT_DOMAIN"] = "https://local.example.test",
                ["appSettings:override:Development:IS_DEV"] = "true",
            })
            .Build();
    }

    private static FW createFw(IConfiguration configuration, string host = "app.example.test")
    {
        var context = createHttpContext(host);

        var fw = new FW(context, configuration);
        fw.is_log_events = false;
        return fw;
    }

    private static FW createFw(IDictionary<string, string?> settings, string host = "app.example.test")
    {
        return createFw(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), host);
    }

    private static FwDict mainDb(FW fw)
    {
        var db = (FwDict)fw.config("db")!;
        return (FwDict)db["main"]!;
    }

    private static DefaultHttpContext createHttpContext(string host)
    {
        var context = new DefaultHttpContext
        {
            Session = new TestHelpers.FakeSession(),
        };
        context.Request.Host = new HostString(host);
        context.Request.Scheme = "https";
        return context;
    }

    private static void setXssTokens(FW fw, string sessionToken = "token", string? formToken = "token")
    {
        fw.Session("XSS", sessionToken);
        if (formToken != null)
            fw.FORM["XSS"] = formToken;
    }

    private static string findRepoPath(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(Path.Combine(relativeParts));
    }
}
