using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace osafw.Tests;

[TestClass]
public class FwDbConnectionsTests
{
    [TestMethod]
    public void ResolveInlineConnectionStringReturnsIndependentDefinition()
    {
        var factory = new FakeClientFactory();
        var resolver = new FwDbConnections(factory);
        var definition = new FwDict()
        {
            ["connection_string"] = "Server=local;Database=app;Integrated Security=true;",
            ["driver"] = "mssql"
        };

        var result = resolver.Resolve(definition, "main");
        result["driver"] = "changed";

        Assert.AreNotSame(definition, result);
        Assert.AreEqual("Server=local;Database=app;Integrated Security=true;", result["connection_string"]);
        Assert.AreEqual("mssql", definition["driver"]);
        Assert.AreEqual(0, factory.CreateCount);
    }

    [TestMethod]
    public async Task ResolveAsyncMemoizesSecretAcrossIndependentDefinitions()
    {
        var factory = new FakeClientFactory();
        factory.Client.Result = "Server=db;Database=one;User Id=user;Password=secret;";
        var resolver = new FwDbConnections(factory);

        var first = await resolver.ResolveAsync(secretDefinition("shared"), "one");
        var second = await resolver.ResolveAsync(secretDefinition("shared"), "two");

        Assert.AreEqual(first["connection_string"], second["connection_string"]);
        Assert.IsFalse(first.ContainsKey("connection_string_secret"));
        Assert.AreEqual(1, factory.CreateCount);
        Assert.AreEqual(1, factory.Client.CallCount);
    }

    [TestMethod]
    public async Task ResolveAsyncCoalescesConcurrentFetches()
    {
        var factory = new FakeClientFactory();
        var pending = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Client.PendingResult = pending.Task;
        var resolver = new FwDbConnections(factory);

        var tasks = Enumerable.Range(0, 12)
            .Select(index => resolver.ResolveAsync(secretDefinition("concurrent"), $"db{index}"))
            .ToArray();

        await factory.Client.WaitForFirstCallAsync();
        pending.SetResult("Server=db;Database=concurrent;Integrated Security=true;");
        await Task.WhenAll(tasks);

        Assert.AreEqual(1, factory.CreateCount);
        Assert.AreEqual(1, factory.Client.CallCount);
        Assert.AreEqual(12, tasks.Select(task => task.Result).Distinct().Count());
    }

    [TestMethod]
    public void ResolveRejectsInlineAndSecretSourcesWithoutCallingProvider()
    {
        var factory = new FakeClientFactory();
        var resolver = new FwDbConnections(factory);
        var definition = secretDefinition("conflict");
        definition["connection_string"] = "Password=must-not-appear";

        var error = Assert.ThrowsExactly<FwDbConnectionException>(() => resolver.Resolve(definition, "reports"));

        StringAssert.Contains(error.Message, "reports");
        Assert.IsFalse(error.Message.Contains("must-not-appear", StringComparison.Ordinal));
        Assert.IsFalse(error.Message.Contains("conflict", StringComparison.Ordinal));
        Assert.AreEqual(0, factory.CreateCount);
    }

    [TestMethod]
    public async Task ResolveAsyncDoesNotFallBackWhenRequiredSecretFails()
    {
        var factory = new FakeClientFactory();
        factory.Client.Error = new InvalidOperationException("provider detail");
        var resolver = new FwDbConnections(factory);
        var definition = secretDefinition("unavailable");

        var error = await Assert.ThrowsExactlyAsync<FwDbConnectionException>(
            () => resolver.ResolveAsync(definition, "archive"));

        StringAssert.Contains(error.Message, "archive");
        Assert.IsFalse(error.Message.Contains("unavailable", StringComparison.Ordinal));
        Assert.IsFalse(error.ToString().Contains("provider detail", StringComparison.Ordinal));
        Assert.IsNull(definition["connection_string"]);
        Assert.AreEqual(1, factory.Client.CallCount);
    }

    [TestMethod]
    public async Task ResolveAsyncKeepsRegionAndSecretCachesIndependent()
    {
        var factory = new FakeClientFactory();
        factory.Results[("us-east-1", "first")] = "Server=east-one;";
        factory.Results[("us-east-1", "second")] = "Server=east-two;";
        factory.Results[("us-west-2", "first")] = "Server=west-one;";
        var resolver = new FwDbConnections(factory);

        var eastOne = await resolver.ResolveAsync(secretDefinition("first", "us-east-1"), "eastOne");
        var eastTwo = await resolver.ResolveAsync(secretDefinition("second", "us-east-1"), "eastTwo");
        var westOne = await resolver.ResolveAsync(secretDefinition("first", "us-west-2"), "westOne");

        Assert.AreEqual("Server=east-one;", eastOne["connection_string"]);
        Assert.AreEqual("Server=east-two;", eastTwo["connection_string"]);
        Assert.AreEqual("Server=west-one;", westOne["connection_string"]);
        Assert.AreEqual(2, factory.CreateCount);
        Assert.AreEqual(2, factory.CreatedRegions.Distinct(StringComparer.Ordinal).Count());
    }

    [TestMethod]
    public void ResolveRejectsUnsupportedProviderWithoutDisclosingDescriptor()
    {
        var factory = new FakeClientFactory();
        var resolver = new FwDbConnections(factory);
        var definition = secretDefinition("private-name");
        ((FwDict)definition["connection_string_secret"]!)["provider"] = "unknown-provider";

        var error = Assert.ThrowsExactly<FwDbConnectionException>(() => resolver.Resolve(definition, "legacy"));

        StringAssert.Contains(error.Message, "legacy");
        Assert.IsFalse(error.Message.Contains("unknown-provider", StringComparison.Ordinal));
        Assert.IsFalse(error.Message.Contains("private-name", StringComparison.Ordinal));
        Assert.AreEqual(0, factory.CreateCount);
    }

    private static FwDict secretDefinition(string secretId, string? region = null)
    {
        var descriptor = new FwDict()
        {
            ["provider"] = FwDbConnections.PROVIDER_AWS_SECRETS_MANAGER,
            ["secret_id"] = secretId
        };

        if (region != null)
            descriptor["region"] = region;

        return new FwDict()
        {
            ["driver"] = "mssql",
            ["connection_string_secret"] = descriptor
        };
    }

    private sealed class FakeClientFactory : IFwDbSecretClientFactory
    {
        public readonly FakeClient Client = new();
        public readonly ConcurrentDictionary<(string Region, string SecretId), string> Results = new();
        public readonly ConcurrentBag<string> CreatedRegions = [];

        public int CreateCount;

        public IFwDbSecretClient Create(string? region)
        {
            Interlocked.Increment(ref CreateCount);
            CreatedRegions.Add(region ?? "");
            Client.Region = region ?? "";
            Client.Results = Results;
            return Client;
        }
    }

    private sealed class FakeClient : IFwDbSecretClient
    {
        private readonly TaskCompletionSource firstCall = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount;
        public Exception? Error;
        public Task<string?>? PendingResult;
        public string? Region;
        public string? Result;
        public ConcurrentDictionary<(string Region, string SecretId), string>? Results;

        public Task WaitForFirstCallAsync()
        {
            return firstCall.Task;
        }

        public async Task<string?> GetSecretStringAsync(string secretId, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            firstCall.TrySetResult();

            if (Error != null)
                throw Error;

            if (PendingResult != null)
                return await PendingResult.WaitAsync(cancellationToken);

            if (Results != null && Results.TryGetValue((Region ?? "", secretId), out var result))
                return result;

            return Result;
        }
    }
}
