using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace osafw.Tests;

[TestClass]
public class FwConfigScopeTests
{
    private static Dictionary<string, string?> Settings(string label) => new()
    {
        ["appSettings:SITE_NAME"] = label,
        ["appSettings:ROOT_DOMAIN"] = "https://" + label + ".example.test",
        ["appSettings:nested:value"] = label,
        ["appSettings:route_prefixes:/" + label] = "true",
    };

    [TestMethod]
    public void NestedScopes_RestoreSettingsAndHostTrust_AfterFailure()
    {
        var original = FwConfig.GetCurrentSettings();
        using (var outer = new FwTestScope(_ => new RejectingDb(), Settings("outer")))
        {
            var outerSettings = FwConfig.GetCurrentSettings();
            try
            {
                using var inner = new FwTestScope(_ => new RejectingDb(), Settings("inner"));
                Assert.AreEqual("inner", inner.Fw.config("SITE_NAME"));
                Assert.IsTrue(FwConfig.isTrustedHost("inner.example.test"));
                Assert.IsFalse(FwConfig.isTrustedHost("outer.example.test"));
                ((FwDict)inner.Fw.config("nested")!)["value"] = "changed";
                throw new InvalidOperationException("Controlled scope failure.");
            }
            catch (InvalidOperationException) { }

            Assert.AreSame(outerSettings, FwConfig.GetCurrentSettings());
            Assert.AreEqual("outer", outer.Fw.config("SITE_NAME"));
            Assert.AreEqual("outer", ((FwDict)outer.Fw.config("nested")!)["value"]);
            Assert.IsTrue(FwConfig.isTrustedHost("outer.example.test"));
            Assert.IsFalse(FwConfig.isTrustedHost("inner.example.test"));
        }
        Assert.AreSame(original, FwConfig.GetCurrentSettings());
    }

    [TestMethod]
    public async Task ParallelScopes_IsolateStaticAndFwConsumers_WhileChildWorkInheritsSettings()
    {
        var original = FwConfig.GetCurrentSettings();
        var bothReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = Task.Run(async () =>
        {
            using var scope = new FwTestScope(_ => new RejectingDb(), Settings("first"));
            ((FwDict)scope.Fw.config("nested")!)["value"] = "first-mutated";
            firstReady.SetResult();
            await bothReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual("first", scope.Fw.config("SITE_NAME"));
            Assert.AreEqual("first-mutated", ((FwDict)FwConfig.GetCurrentSetting("nested")!)["value"]);
            Assert.IsTrue(FwConfig.isTrustedHost("first.example.test"));
            Assert.IsFalse(FwConfig.isTrustedHost("second.example.test"));
            StringAssert.Contains(FwConfig.getRoutePrefixesRX(), "/first");
            Assert.AreEqual("first", await Task.Run(() => FwConfig.GetCurrentSetting("SITE_NAME")));
        });
        var second = Task.Run(async () =>
        {
            await firstReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var scope = new FwTestScope(_ => new RejectingDb(), Settings("second"));
            scope.Fw.config()["SITE_NAME"] = "temporary";
            FwConfig.reload(scope.Fw);
            bothReady.SetResult();
            await first;
            Assert.AreEqual("second", scope.Fw.config("SITE_NAME"));
            Assert.AreEqual("second", ((FwDict)scope.Fw.config("nested")!)["value"]);
            Assert.IsTrue(FwConfig.isTrustedHost("second.example.test"));
        });
        await Task.WhenAll(first, second);
        Assert.AreSame(original, FwConfig.GetCurrentSettings());
    }

    [TestMethod]
    public void SameConfigurationProvider_ProducesIndependentMutableBuckets()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(Settings("shared")).Build();
        using var outer = FwConfig.beginScope();
        FwConfig.init(null, configuration);
        var outerSettings = FwConfig.GetCurrentSettings();
        using (FwConfig.beginScope())
        {
            FwConfig.init(null, configuration);
            ((FwDict)FwConfig.GetCurrentSetting("nested")!)["value"] = "inner";
            FwConfig.init(null, new ConfigurationBuilder().AddInMemoryCollection(Settings("replacement")).Build());
            Assert.AreEqual("replacement", FwConfig.GetCurrentSetting("SITE_NAME"));
        }
        Assert.AreSame(outerSettings, FwConfig.GetCurrentSettings());
        Assert.AreEqual("shared", ((FwDict)FwConfig.GetCurrentSetting("nested")!)["value"]);
    }

    [TestMethod]
    public void ScopeDisposal_CanBeRepeated_WithoutChangingTheActiveParentOrNewScope()
    {
        using var parent = FwConfig.beginScope();
        var parentSettings = FwConfig.GetCurrentSettings();
        using var scope = FwConfig.beginScope();

        scope.Dispose();
        scope.Dispose();
        Assert.AreSame(parentSettings, FwConfig.GetCurrentSettings());

        using var next = FwConfig.beginScope();
        var nextSettings = FwConfig.GetCurrentSettings();
        scope.Dispose();
        Assert.AreSame(nextSettings, FwConfig.GetCurrentSettings());
    }

    [TestMethod]
    public void TestScope_RepeatedDisposal_DoesNotMaskAnException()
    {
        var original = FwConfig.GetCurrentSettings();
        var expected = new InvalidOperationException("Controlled operation failure.");
        var actual = Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            using var scope = new FwTestScope(_ => new RejectingDb());
            scope.Dispose();
            scope.Dispose();
            throw expected;
        });

        Assert.AreSame(expected, actual);
        Assert.AreSame(original, FwConfig.GetCurrentSettings());
    }

    [TestMethod]
    public void Scopes_RejectOutOfOrderDisposal()
    {
        var outer = FwConfig.beginScope();
        var inner = FwConfig.beginScope();
        Assert.ThrowsExactly<InvalidOperationException>(() => outer.Dispose());
        inner.Dispose();
        outer.Dispose();
    }
}
