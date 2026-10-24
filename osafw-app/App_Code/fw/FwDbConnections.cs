// Named database connection-string resolution
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2026 Oleg Savchuk www.osalabs.com

using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace osafw;

/// <summary>
/// Resolves named database definitions from either an inline connection string or a supported
/// external secret. Resolved definitions contain credentials and must not be logged, rendered,
/// returned from controllers, or stored in template/page state. External secrets are version 1:
/// the secret string is the complete database connection string; binary or JSON-shaped secrets
/// are not interpreted.
/// </summary>
public sealed class FwDbConnections
{
    public const string PROVIDER_AWS_SECRETS_MANAGER = "aws-secrets-manager";

    private const string DEFAULT_REGION_CACHE_KEY = "<default>";

    private static readonly ResolverState sharedState = new(new AwsDbSecretClientFactory());

    /// <summary>
    /// Process-wide resolver used by startup and runtime database creation so both paths share
    /// clients and fetched secret values. Restart the process to refresh an externally changed secret.
    /// </summary>
    public static FwDbConnections Shared { get; } = new(sharedState);

    private readonly ResolverState state;

    public FwDbConnections() : this(sharedState)
    {
    }

    /// <summary>
    /// Creates an isolated resolver with an injected external-secret client factory.
    /// This boundary supports deterministic tests and alternative client construction without
    /// introducing process-wide mutable delegates.
    /// </summary>
    public FwDbConnections(IFwDbSecretClientFactory clientFactory)
        : this(new ResolverState(clientFactory ?? throw new ArgumentNullException(nameof(clientFactory))))
    {
    }

    private FwDbConnections(ResolverState state)
    {
        this.state = state;
    }

    /// <summary>
    /// Returns a cloned database definition with a usable <c>connection_string</c>.
    /// The input definition is not modified. The returned definition is confidential and must not
    /// be logged, rendered, returned from controllers, or stored in template/page state.
    /// </summary>
    /// <param name="dbDefinition">Configuration for one named database.</param>
    /// <param name="name">Database name used only in safe diagnostics.</param>
    public FwDict Resolve(FwDict dbDefinition, string name)
    {
        return ResolveAsync(dbDefinition, name).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously returns a cloned database definition with a usable
    /// <c>connection_string</c>. The returned definition contains credentials and is confidential.
    /// </summary>
    public async Task<FwDict> ResolveAsync(
        FwDict dbDefinition,
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbDefinition);

        var safeName = string.IsNullOrWhiteSpace(name) ? "(unnamed)" : name.Trim();
        var result = new FwDict(dbDefinition);
        var inlineConnectionString = dbDefinition["connection_string"]?.ToString();
        var hasInlineConnectionString = !string.IsNullOrWhiteSpace(inlineConnectionString);
        var secretValue = dbDefinition["connection_string_secret"];
        var hasSecretDescriptor = secretValue != null;

        if (hasInlineConnectionString && hasSecretDescriptor)
            throw configurationError(safeName, "defines both connection_string and connection_string_secret");

        if (!hasSecretDescriptor)
        {
            if (!hasInlineConnectionString)
                throw configurationError(safeName, "does not define a connection-string source");

            return result;
        }

        var descriptor = toDescriptor(secretValue, safeName);
        var provider = requiredDescriptorValue(descriptor, "provider", safeName).ToLowerInvariant();
        var secretId = requiredDescriptorValue(descriptor, "secret_id", safeName);
        var region = optionalDescriptorValue(descriptor, "region");

        if (!string.Equals(provider, PROVIDER_AWS_SECRETS_MANAGER, StringComparison.Ordinal))
            throw configurationError(safeName, "uses an unsupported connection-string secret provider");

        var cacheRegion = string.IsNullOrEmpty(region) ? DEFAULT_REGION_CACHE_KEY : region;
        var cacheKey = string.Join("\n", provider, cacheRegion, secretId);
        var lazySecret = state.secrets.GetOrAdd(
            cacheKey,
            _ => new Lazy<Task<string>>(
                () => fetchSecret(cacheKey, region, secretId, safeName),
                LazyThreadSafetyMode.ExecutionAndPublication));
        var connectionString = await lazySecret.Value.WaitAsync(cancellationToken).ConfigureAwait(false);

        result["connection_string"] = connectionString;
        result.Remove("connection_string_secret");

        return result;
    }

    private async Task<string> fetchSecret(
        string cacheKey,
        string? region,
        string secretId,
        string safeName)
    {
        var clientRegion = string.IsNullOrEmpty(region) ? DEFAULT_REGION_CACHE_KEY : region;
        var lazyClient = state.clients.GetOrAdd(
            clientRegion,
            _ => new Lazy<IFwDbSecretClient>(
                () => state.clientFactory.Create(region),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            var secret = await lazyClient.Value.GetSecretStringAsync(secretId, CancellationToken.None).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(secret))
                throw configurationError(safeName, "resolved an empty connection string");

            return secret;
        }
        catch (FwDbConnectionException)
        {
            state.secrets.TryRemove(cacheKey, out _);
            throw;
        }
        catch (Exception)
        {
            state.secrets.TryRemove(cacheKey, out _);
            throw new FwDbConnectionException(
                $"Database connection '{safeName}' could not resolve its external connection string.");
        }
    }

    private static FwDict toDescriptor(object? value, string safeName)
    {
        if (value is FwDict descriptor)
            return descriptor;

        if (value is IDictionary dictionary)
            return new FwDict(dictionary);

        throw configurationError(safeName, "has an invalid connection_string_secret descriptor");
    }

    private static string requiredDescriptorValue(FwDict descriptor, string field, string safeName)
    {
        var value = optionalDescriptorValue(descriptor, field);

        if (string.IsNullOrEmpty(value))
            throw configurationError(safeName, $"has an incomplete connection_string_secret descriptor ({field})");

        return value;
    }

    private static string? optionalDescriptorValue(FwDict descriptor, string field)
    {
        var value = descriptor[field]?.ToString()?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static FwDbConnectionException configurationError(string safeName, string details)
    {
        return new FwDbConnectionException($"Database connection '{safeName}' {details}.");
    }

    private sealed class ResolverState
    {
        public readonly IFwDbSecretClientFactory clientFactory;
        public readonly ConcurrentDictionary<string, Lazy<IFwDbSecretClient>> clients = new(StringComparer.Ordinal);
        public readonly ConcurrentDictionary<string, Lazy<Task<string>>> secrets = new(StringComparer.Ordinal);

        public ResolverState(IFwDbSecretClientFactory clientFactory)
        {
            this.clientFactory = clientFactory;
        }
    }

    private sealed class AwsDbSecretClientFactory : IFwDbSecretClientFactory
    {
        public IFwDbSecretClient Create(string? region)
        {
            AmazonSecretsManagerClient client;

            if (string.IsNullOrEmpty(region))
                client = new AmazonSecretsManagerClient();
            else
                client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));

            return new AwsDbSecretClient(client);
        }
    }

    private sealed class AwsDbSecretClient : IFwDbSecretClient
    {
        private readonly IAmazonSecretsManager client;

        public AwsDbSecretClient(IAmazonSecretsManager client)
        {
            this.client = client;
        }

        public async Task<string?> GetSecretStringAsync(string secretId, CancellationToken cancellationToken)
        {
            var response = await client.GetSecretValueAsync(
                new GetSecretValueRequest() { SecretId = secretId },
                cancellationToken).ConfigureAwait(false);

            return response.SecretString;
        }
    }
}

/// <summary>
/// Creates clients for external database connection-string providers.
/// </summary>
public interface IFwDbSecretClientFactory
{
    IFwDbSecretClient Create(string? region);
}

/// <summary>
/// Reads one complete database connection string from an external provider.
/// Implementations must not include secret values in exceptions or logs.
/// </summary>
public interface IFwDbSecretClient
{
    Task<string?> GetSecretStringAsync(string secretId, CancellationToken cancellationToken);
}

/// <summary>
/// Reports a database connection-source configuration or resolution failure without including
/// connection strings or fetched secret values.
/// </summary>
public sealed class FwDbConnectionException : InvalidOperationException
{
    public FwDbConnectionException(string message) : base(message)
    {
    }
}
