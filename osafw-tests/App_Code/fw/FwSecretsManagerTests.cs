using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
#if isSecretsManager
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
#endif

namespace osafw.Tests;

[TestClass]
[DoNotParallelize]
public class FwSecretsManagerTests
{
    private static FwDict reference(string id, string? region = "us-east-1") => new()
    {
        ["provider"] = "aws-secrets-manager",
        ["secret_id"] = id,
        ["region"] = region,
    };

    private static FwDict settings(FwDict definition) => new()
    {
        ["DATA_PROTECTION_APPLICATION_NAME"] = "external-secret-test",
        ["db"] = new FwDict { ["main"] = definition, ["reports"] = definition },
    };

    [TestMethod]
    public void InlineDefinitionsRemainIndependentAndDoNotNeedAProvider()
    {
        var definition = DB.h("type", "SQL", "connection_string", "Server=local;Database=app;Integrated Security=true;");
        var config = settings(definition);
        var result = FwConfig.getDatabaseSettings(config, "reports");
        Assert.AreEqual(definition["connection_string"], result["connection_string"]);
        Assert.AreNotSame(definition, result);
        result["connection_string"] = "changed";
        Assert.AreNotEqual("changed", definition["connection_string"]);
        Assert.AreEqual(0, FwConfig.getDatabaseSettings(config, "missing").Count);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("azure-key-vault")]
    [DataRow("unknown-private-provider")]
    public void UnsupportedProvidersFailWithoutDisclosingTheReference(string provider)
    {
        var descriptor = reference("private-secret-id");
        descriptor["provider"] = provider;
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => FwSecretsManager.read(descriptor));
        StringAssert.Contains(error.Message, "Unsupported");
        Assert.IsFalse(error.ToString().Contains("private", StringComparison.Ordinal));
    }

    [TestMethod]
    public void InvalidReferencesAndConflictingSourcesAreRejectedBeforeProviderAccess()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => FwSecretsManager.read(new FwDict()));
        Assert.ThrowsExactly<InvalidOperationException>(() => FwConfig.getDatabaseSettings(settings(DB.h("connection_string_secret", "invalid"))));

        var definition = DB.h("connection_string", "Password=private-value", "connection_string_secret", reference("private-id"));
        var config = settings(definition);
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => FwConfig.getDatabaseSettings(config));
        StringAssert.Contains(error.Message, "both");
        Assert.IsFalse(error.ToString().Contains("private", StringComparison.Ordinal));
        Assert.ThrowsExactly<InvalidOperationException>(() => FwSettings.configureProtection(new ServiceCollection(), config));
    }

#if !isSecretsManager
    [TestMethod]
    public void DisabledFeatureFailsClearlyAtConfigurationAndRuntimeEntryPoints()
    {
        var descriptor = reference("private-id");
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => FwSecretsManager.read(descriptor));
        StringAssert.Contains(error.Message, "isSecretsManager");
        Assert.IsFalse(error.ToString().Contains("private-id", StringComparison.Ordinal));
        var config = settings(DB.h("connection_string_secret", descriptor));
        Assert.ThrowsExactly<InvalidOperationException>(() => FwConfig.getDatabaseSettings(config));
        Assert.ThrowsExactly<InvalidOperationException>(() => FwSettings.configureProtection(new ServiceCollection(), config, false));

        using var scope = FwConfig.beginScope();
        var values = new Dictionary<string, string?>
        {
            ["appSettings:log"] = "",
            ["appSettings:db:main:type"] = "SQL",
            ["appSettings:db:main:connection_string_secret:provider"] = "aws-secrets-manager",
            ["appSettings:db:main:connection_string_secret:secret_id"] = "private-id",
        };
        Assert.ThrowsExactly<InvalidOperationException>(() => new FW(null, new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
        Assert.IsFalse(typeof(FwSecretsManager).Assembly.GetReferencedAssemblies().Any(assembly => assembly.Name == "AWSSDK.SecretsManager"));
    }
#else
    private static WebApplication server = null!;
    private static readonly ConcurrentDictionary<string, int> calls = new();
    private static readonly Dictionary<string, string?> previousEnvironment = new();
    private static Func<string, int, (int Status, object Body)> respond = null!;

    [ClassInitialize]
    public static async Task StartEndpoint(TestContext _)
    {
        // Exercise the real SDK/production entry points against its supported local endpoint override.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        server = builder.Build();
        server.MapPost("/", async context =>
        {
            using var request = await JsonDocument.ParseAsync(context.Request.Body);
            string id = request.RootElement.GetProperty("SecretId").GetString()!;
            int count = calls.AddOrUpdate(id, 1, (_, old) => old + 1);
            var result = respond(id, count);
            context.Response.StatusCode = result.Status;
            context.Response.ContentType = "application/x-amz-json-1.1";
            await context.Response.WriteAsync(JsonSerializer.Serialize(result.Body));
        });
        await server.StartAsync();
        var environment = new Dictionary<string, string?>
        {
            ["AWS_ENDPOINT_URL_SECRETS_MANAGER"] = server.Urls.Single(),
            ["AWS_IGNORE_CONFIGURED_ENDPOINT_URLS"] = "false",
            ["AWS_ACCESS_KEY_ID"] = "test-access-key",
            ["AWS_SECRET_ACCESS_KEY"] = "test-secret-key",
            ["AWS_SESSION_TOKEN"] = null,
            ["AWS_REGION"] = "us-east-1",
            ["AWS_EC2_METADATA_DISABLED"] = "true",
            ["AWS_MAX_ATTEMPTS"] = "1",
        };
        foreach (var pair in environment)
        {
            previousEnvironment[pair.Key] = Environment.GetEnvironmentVariable(pair.Key);
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    [ClassCleanup]
    public static async Task StopEndpoint()
    {
        foreach (var pair in previousEnvironment)
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        if (server != null)
        {
            await server.StopAsync();
            await server.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task ConcurrentReadsFetchOnceAndRemainCachedAcrossGeneralCacheClear()
    {
        string id = Guid.NewGuid().ToString("N");
        const string value = "{\"api_key\":\"dummy-example\"}";
        respond = (_, _) => (200, new { SecretString = value });
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => FwSecretsManager.read(reference(id)))));
        Assert.IsTrue(results.All(result => result == value));
        FwCache.clear();
        Assert.AreEqual(value, FwSecretsManager.read(reference(id)));
        Assert.AreEqual(1, calls[id]);
    }

    [TestMethod]
    public void RegionsAndSecretIdsHaveIndependentEntriesIncludingSdkDefaultRegion()
    {
        string id = Guid.NewGuid().ToString("N");
        respond = (key, _) => (200, new { SecretString = key });
        foreach (var region in new string?[] { "us-east-1", "us-west-2", null })
        {
            Assert.AreEqual(id, FwSecretsManager.read(reference(id, region)));
            Assert.AreEqual(id, FwSecretsManager.read(reference(id, region)));
        }
        Assert.AreEqual(3, calls[id]);
        Assert.AreEqual(id + "other", FwSecretsManager.read(reference(id + "other")));
        Assert.AreEqual(1, calls[id + "other"]);
    }

    [TestMethod]
    public void FailureIsRedactedAndNextReadCanRetry()
    {
        string id = "private-" + Guid.NewGuid().ToString("N");
        respond = (_, count) => count == 1
            ? (400, new { __type = "AccessDeniedException", Message = "private-provider-data" })
            : (200, new { SecretString = "recovered-value" });
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => FwSecretsManager.read(reference(id)));
        Assert.IsFalse(error.ToString().Contains("private", StringComparison.Ordinal));
        Assert.IsNull(error.InnerException);
        Assert.AreEqual("recovered-value", FwSecretsManager.read(reference(id)));
        Assert.AreEqual(2, calls[id]);
    }

    [TestMethod]
    public void BinaryIsRejectedAndEmptyDatabaseValueIsNotUsable()
    {
        string id = Guid.NewGuid().ToString("N");
        respond = (_, _) => (200, new { SecretBinary = "cHJpdmF0ZS1iaW5hcnk=" });
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => FwSecretsManager.read(reference(id)));
        Assert.IsFalse(error.ToString().Contains("private-binary", StringComparison.Ordinal));

        respond = (_, _) => (200, new { SecretString = "" });
        var descriptor = reference(id + "empty");
        Assert.AreEqual("", FwSecretsManager.read(descriptor));
        Assert.ThrowsExactly<InvalidOperationException>(() => FwConfig.getDatabaseSettings(settings(DB.h("connection_string_secret", descriptor))));
    }

    [TestMethod]
    public void StartupProtectionAndRuntimeNamedDatabasesShareTheCachedValueWithoutChangingConfig()
    {
        string id = Guid.NewGuid().ToString("N");
        const string connection = "Server=127.0.0.1,1;Database=secret_test;Integrated Security=true;Connect Timeout=1;";
        respond = (_, _) => (200, new { SecretString = connection });
        var values = new Dictionary<string, string?>
        {
            ["appSettings:log"] = "",
            ["appSettings:DATA_PROTECTION_APPLICATION_NAME"] = "external-secret-test",
        };
        foreach (string name in new[] { "main", "reports", "archive" })
        {
            values[$"appSettings:db:{name}:type"] = "SQL";
            values[$"appSettings:db:{name}:connection_string_secret:provider"] = "aws-secrets-manager";
            values[$"appSettings:db:{name}:connection_string_secret:secret_id"] = id;
            values[$"appSettings:db:{name}:connection_string_secret:region"] = "us-east-1";
        }
        using var scope = FwConfig.beginScope();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var config = FwConfig.settingsForEnvironment(configuration);
        var startup = FwConfig.getDatabaseSettings(config);
        Assert.AreEqual(connection, startup["connection_string"]);
        Assert.IsFalse(startup.ContainsKey("connection_string_secret"));
        var services = new ServiceCollection();
        services.AddFwSessionCache(startup["connection_string"].toStr(), startup["type"].toStr());
        FwSettings.configureProtection(services, config, false);
        FwSettings.configureProtection(new ServiceCollection(), config, true);

        using var fw = new FW(null, configuration);
        using var expected = new DB(connection, DB.DBTYPE_SQLSRV);
        Assert.AreEqual(expected.cacheIdentity, fw.db.cacheIdentity);
        Assert.AreEqual(expected.cacheIdentity, fw.getDB("reports").cacheIdentity);
        Assert.AreEqual(expected.cacheIdentity, fw.getDB("archive").cacheIdentity);
        Assert.IsFalse(fw.G.ContainsKey("db"));
        var original = (FwDict)((FwDict)fw.config("db")!)["main"]!;
        Assert.IsFalse(original.ContainsKey("connection_string"));
        Assert.IsTrue(original.ContainsKey("connection_string_secret"));
        Assert.AreEqual(1, calls[id]);
    }
#endif
}
