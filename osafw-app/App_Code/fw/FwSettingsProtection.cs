using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace osafw;

/// <summary>Protects recoverable settings under the application's durable, host-protected key ring.</summary>
public static class FwSettingsProtection
{
    public const string PREFIX = "enc:v1:";
    private static readonly ConcurrentDictionary<string, Lazy<ServiceProvider>> providers = new();

    /// <summary>Registers the same durable key store for HTTP and offline consumers. Keys are never expired by deleting rows.</summary>
    public static void configure(IServiceCollection services, FwDict settings)
    {
        var definition = (settings["db"] as FwDict)?["main"] as FwDict ?? [];
        var resolved = definition;
        var name = settings["DATA_PROTECTION_APPLICATION_NAME"].toStr();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("DATA_PROTECTION_APPLICATION_NAME must be configured before using protected data.");

        var builder = services.AddDataProtection().SetApplicationName(name);
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Durable Settings protection requires Windows DPAPI.");
        builder.ProtectKeysWithDpapi(protectToLocalMachine: true);
        services.Configure<KeyManagementOptions>(options =>
            options.XmlRepository = new FwKeysXmlRepository(() => new DB(new FwDict(resolved), "main")));
    }

    /// <summary>Marks the entire request sensitive before parsing or handling protected values.</summary>
    public static void markSensitiveRequest(FW fw)
    {
        fw.context.Items["OSAFW.SensitiveSettings"] = true;
#if isSentry
        Sentry.SentrySdk.ConfigureScope(scope => scope.SetTag("osafw_sensitive_settings", "1"));
#endif
    }

    private static IDataProtector protector(FW fw, string code)
    {
        var provider = fw.context.RequestServices?.GetService<IDataProtectionProvider>();
        if (provider == null)
        {
            var identity = fw.db.cacheIdentity + ":" + fw.config("DATA_PROTECTION_APPLICATION_NAME").toStr();
            var settings = new FwDict(fw.config());
            provider = providers.GetOrAdd(identity, _ => new Lazy<ServiceProvider>(() =>
            {
                var services = new ServiceCollection();
                configure(services, settings);
                return services.BuildServiceProvider();
            })).Value.GetRequiredService<IDataProtectionProvider>();
        }
        return provider.CreateProtector("OSAFW.Settings.v1", code);
    }

    /// <summary>Accepts plaintext only. Empty means an intentional clear and needs no encryption key.</summary>
    public static string protect(FW fw, string code, string value)
    {
        markSensitiveRequest(fw);
        if (value.Length == 0)
            return "";
        try
        {
            return PREFIX + protector(fw, code).Protect(value);
        }
        catch (Exception)
        {
            // Neither backend exceptions nor supplied values belong in diagnostics.
            throw new CryptographicException("Unable to persist a protected setting. Check durable key storage.");
        }
    }

    /// <summary>Fails closed for legacy plaintext or unreadable ciphertext; never treats it as missing/default.</summary>
    public static string unprotect(FW fw, string code, string value)
    {
        markSensitiveRequest(fw);
        if (value.Length == 0)
            return "";
        if (!value.StartsWith(PREFIX, StringComparison.Ordinal))
            throw new CryptographicException("A credential requires the explicit Settings migration.");
        try
        {
            return protector(fw, code).Unprotect(value[PREFIX.Length..]);
        }
        catch (Exception)
        {
            throw new CryptographicException("A protected setting cannot be read. Restore the matching keys or replace its value.");
        }
    }
}
