// Optional external secrets, independent of database-backed Site Settings.
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net

using System;
#if isSecretsManager
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System.Collections.Generic;
#endif

namespace osafw;

public static class FwSecretsManager
{
    /// <summary>
    /// Reads a secret string from a provider reference: provider, secret_id, and provider-specific options.
    /// AWS accepts an optional region and uses the SDK credential chain, never Site Settings credentials.
    /// Successful reads are cached until process restart; failures can be retried. Values are confidential:
    /// never log them or place them in template globals. JSON is returned unchanged; binary values are unsupported.
    /// </summary>
    /// <exception cref="InvalidOperationException">Invalid reference, unavailable provider/build feature, or retrieval failure; messages omit secret data.</exception>
    public static string read(FwDict reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        string provider = reference["provider"].toStr().Trim().ToLowerInvariant();
        if (reference["secret_id"] is not string secretId || string.IsNullOrWhiteSpace(secretId))
            throw new InvalidOperationException("External secret reference requires secret_id.");

        switch (provider)
        {
            case "aws-secrets-manager":
#if isSecretsManager
                if (reference["region"] != null && reference["region"] is not string)
                    throw new InvalidOperationException("External secret region must be a string.");
                return readAws(secretId.Trim(), reference["region"].toStr().Trim());
#else
                throw new InvalidOperationException("AWS Secrets Manager support is disabled. Enable isSecretsManager and rebuild the application.");
#endif
            default:
                throw new InvalidOperationException("Unsupported external secret provider.");
        }
    }

#if isSecretsManager
    private static readonly object cacheLock = new();
    private static readonly Dictionary<(string Region, string SecretId), string> cache = [];

    private static string readAws(string secretId, string region)
    {
        // Cold reads are rare. One lock prevents duplicate paid requests; no refresh service is needed.
        lock (cacheLock)
        {
            var key = (region, secretId);
            if (cache.TryGetValue(key, out var cached))
                return cached;

            string? value;
            try
            {
                using var client = region.Length == 0
                    ? new AmazonSecretsManagerClient()
                    : new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));
                value = client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretId })
                    .GetAwaiter().GetResult().SecretString;
            }
            catch (Exception)
            {
                // Provider exceptions can contain private identifiers or response data.
                throw new InvalidOperationException("Unable to read external secret. Check AWS credentials, region, access permissions and secret availability.");
            }

            if (value == null)
                throw new InvalidOperationException("External secret must contain a string value; binary secrets are not supported.");

            cache[key] = value;
            return value;
        }
    }
#endif
}
