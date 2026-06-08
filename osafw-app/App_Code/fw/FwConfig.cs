// App Configuration class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace osafw;

public static class FwConfig
{
    // public API
    public static string hostname => (GetCurrentSetting("hostname") as string) ?? "";

    /// <summary>Get per-request, host-specific settings bucket.</summary>
    public static FwDict GetCurrentSettings() => GetSettingsForHost();

    /// <summary>Get specific setting from the current bucket.</summary>
    public static object? GetCurrentSetting(string name) => GetSettingsForHost()[name];

    // internals
    private static readonly AsyncLocal<string?> _currentHostKey = new();          // per-async-flow cache key
    private static readonly ConcurrentDictionary<string, Lazy<FwDict>> _hostCache = new();
    private static readonly object _configLock = new();
    private static IConfiguration? configuration;                                   // appsettings.* provider
    private static FwDict? _baseSettings;
    private static string? _trustedRootHost;
    private static string[]? _trustedHostPatterns;

    private const string DefaultHostKey = "__default__";

    public static readonly char path_separator = Path.DirectorySeparatorChar;

    public static string getRoutePrefixesRX()
    {
        return GetSettingsForHost()["_route_prefixes_rx"].toStr();
    }

    /// <remarks>Called exactly once per _http request_ by FW.</remarks>
    public static void init(HttpContext? ctx, IConfiguration cfg, string? host = null)
    {
        setConfiguration(cfg);                                          // record for offline tools

        host ??= ctx?.Request.Host.ToString() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(host))
            host = isTrustedHost(host) ? host.Trim() : string.Empty;
        else
            host = string.Empty;
        var cacheKey = resolveHostKey(host);

        GetSettingsForHost(host, ctx); // ensure host bucket built
        _currentHostKey.Value = cacheKey;
    }

    // clears cache entry for request's host.
    public static void reload(FW fw)
    {
        var cacheKey = _currentHostKey.Value ?? getHostCacheKey(hostname);

        _hostCache.TryRemove(cacheKey, out _);     // force re-build on next request

        if (configuration == null)
            throw new InvalidOperationException("FwConfig.init must be called before reload");

        init(fw.context, configuration, fw.context?.Request.Host.ToString());
    }

    /// <summary>
    /// Checks whether a request host is present in the configured trusted public origin or host override patterns.
    /// </summary>
    public static bool isTrustedHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        var patterns = Volatile.Read(ref _trustedHostPatterns);
        var rootHost = Volatile.Read(ref _trustedRootHost);
        if (patterns == null || rootHost == null)
        {
            _ = getBaseSettings();
            patterns = Volatile.Read(ref _trustedHostPatterns) ?? [];
            rootHost = Volatile.Read(ref _trustedRootHost) ?? string.Empty;
        }

        var hostText = host.Trim();
        var hostName = hostNameOnly(hostText);
        if (hostName.Length == 0)
            return false;

        if (rootHost.Length > 0 && string.Equals(hostName, rootHost, StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var pattern in patterns)
        {
            if (hostMatchesPattern(hostText, hostName, pattern))
                return true;
        }

        return false;
    }

    private static void setConfiguration(IConfiguration cfg)
    {
        if (ReferenceEquals(configuration, cfg))
            return;

        lock (_configLock)
        {
            if (ReferenceEquals(configuration, cfg))
                return;

            configuration = cfg;
            Volatile.Write(ref _baseSettings, null);
            Volatile.Write(ref _trustedRootHost, null);
            Volatile.Write(ref _trustedHostPatterns, null);
            _hostCache.Clear();
            _currentHostKey.Value = null;
        }
    }

    // One-time base (read-only) initialisation shared by all hosts for the active configuration.
    private static FwDict getBaseSettings()
    {
        var cached = Volatile.Read(ref _baseSettings);
        if (cached != null)
            return cached;

        lock (_configLock)
        {
            if (_baseSettings != null)
                return _baseSettings;

            var tmp = new FwDict();
            initDefaults(null, "", ref tmp);
            if (configuration != null)
                applyAppSettings(configuration, tmp);
            var patterns = new List<string>();
            if (tmp["override"] is FwDict overs)
            {
                foreach (string overName in overs.Keys)
                {
                    if (overs[overName] is not FwDict over)
                        continue;

                    var pattern = over["hostname_match"].toStr().Trim();
                    if (pattern.Length > 0 && !isWildcardHostPattern(pattern))
                        patterns.Add(pattern);
                }
            }
            Volatile.Write(ref _trustedRootHost, hostNameOnly(tmp["ROOT_DOMAIN"].toStr()));
            Volatile.Write(ref _trustedHostPatterns, patterns.ToArray());
            Volatile.Write(ref _baseSettings, tmp);
            return tmp;
        }
    }

    private static FwDict buildForHost(HttpContext? ctx, string overrideName, bool isHostProvided)
    {
        // clone deep - each host gets its own mutable copy
        var hs = Utils.cloneHashDeep(getBaseSettings()) ?? [];

        var configuredRootDomain = configuredRootDomainForHost(hs, overrideName, isHostProvided);

        overrideSettingsByName(overrideName, hs, isHostProvided); // hostname overrides use regex if host provided
        if (isHostProvided && string.IsNullOrWhiteSpace(configuredRootDomain))
            hs["ROOT_DOMAIN"] = "";

        overrideContextSettings(ctx, overrideName, hs, configuredRootDomain);
        hs["_route_prefixes_rx"] = buildRoutePrefixesRx(hs);
        return hs;
    }
    // 
    /// <summary>
    /// init default settings
    /// </summary>
    /// <param name="context">can be null for offline execution</param>
    private static void initDefaults(HttpContext? context, string host, ref FwDict st)
    {
        st = new FwDict
        {
            ["hostname"] = "",
            ["ROOT_URL"] = "",
            ["ROOT_DOMAIN"] = "",
        };

        overrideContextSettings(context, host, st);

        string PhysicalApplicationPath;
        string basedir = AppDomain.CurrentDomain.BaseDirectory; //application root directory
        if (!basedir.Contains($@"{path_separator}bin"))
        {
            // try to find bin directory - if it's NOT found than we working under published-only site setup,
            // so basedir is our app path
            PhysicalApplicationPath = basedir;
        }
        else
        {
            //if bin found - then app path is parent folder of the bin
            PhysicalApplicationPath = basedir.Substring(0, basedir.IndexOf($@"{path_separator}bin"));
        }

        st["site_root"] = Regex.Replace(PhysicalApplicationPath, @$"\{path_separator}$", ""); // removed last \ if any

        // default or theme template dir
        // make absolute path to templates from site root
        st["template"] = st["site_root"] + $@"{path_separator}App_Data{path_separator}template";

        st["log"] = st["site_root"] + $@"{path_separator}App_Data{path_separator}logs{path_separator}main.log";
        st["log_max_size"] = 100 * 1024 * 1024; // 100 MB is max log size
        st["tmp"] = Utils.getTmpDir(); // TODO not used? remove?

        st["lang"] ??= "en"; // default language
        st["is_lang_update"] ??= false; // default language update flag
        st["is_fwupdates_auto_apply"] ??= true; // keep the existing dev Home update redirect unless explicitly disabled

        st["date_format"] ??= DateUtils.DATE_FORMAT_MDY;
        st["time_format"] ??= DateUtils.TIME_FORMAT_12;
        st["timezone"] ??= DateUtils.TZ_UTC;
    }

    /// <summary>
    /// Copies direct appSettings children into an existing framework settings dictionary.
    /// </summary>
    /// <param name="cfg">Application configuration provider containing the appSettings section.</param>
    /// <param name="st">Flat settings dictionary that receives keys from inside appSettings.</param>
    private static void applyAppSettings(IConfiguration cfg, FwDict st)
    {
        foreach (IConfigurationSection section in cfg.GetSection("appSettings").GetChildren())
            applySettingsSection(section, st);
    }

    /// <summary>
    /// Copies one configuration section into the supplied dictionary while preserving nested child shape.
    /// </summary>
    /// <param name="section">Configuration section to copy.</param>
    /// <param name="st">Dictionary that receives this section's key at the current level.</param>
    private static void applySettingsSection(IConfigurationSection section, FwDict st)
    {
        if (section.Value != null)
        {
            st[section.Key] = section.Value;
        }
        else if (section.Key != null)
        {
            st[section.Key] = new FwDict();
            foreach (IConfigurationSection sub_section in section.GetChildren())
            {
                FwDict s = (FwDict)st[section.Key]!;
                applySettingsSection(sub_section, s);
            }
        }
    }

    private static void overrideContextSettings(HttpContext? ctx, string host, FwDict st, string configuredRootDomain = "")
    {
        st["hostname"] = host;
        if (ctx == null) return;
        var req = ctx.Request;

        string appBase = req.PathBase;
        st["ROOT_URL"] = Regex.Replace(appBase, @"/$", "");

        if (!string.IsNullOrWhiteSpace(configuredRootDomain))
        {
            st["ROOT_DOMAIN"] = configuredRootDomain;
            return;
        }

        if (!string.IsNullOrWhiteSpace(st["ROOT_DOMAIN"].toStr()))
            return;

        var httpsValue = ctx.GetServerVariable("HTTPS") ?? string.Empty;
        var isHttps = req.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
            || httpsValue.Equals("on", StringComparison.OrdinalIgnoreCase);
        var hostForOrigin = string.IsNullOrWhiteSpace(host) ? req.Host.ToString() : host;
        if (!string.IsNullOrWhiteSpace(hostForOrigin))
            st["ROOT_DOMAIN"] = (isHttps ? "https://" : "http://") + hostForOrigin;
    }

    public static void overrideSettingsByName(string override_name, FwDict with_settings, bool is_regex_match = false)
    {
        var hostName = is_regex_match ? hostNameOnly(override_name) : string.Empty;
        if (with_settings["override"] is FwDict overs)
        {
            foreach (string over_name in overs.Keys)
            {
                if (overs[over_name] is FwDict over)
                {
                    var pattern = over["hostname_match"].toStr();
                    if (!is_regex_match && over_name == override_name
                        || is_regex_match
                            && pattern.Length > 0
                            && !isWildcardHostPattern(pattern)
                            && hostMatchesPattern(override_name, hostName, pattern)
                        )
                    {
                        with_settings["config_override"] = over_name;
                        Utils.mergeHashDeep(with_settings, over);
                        break;
                    }
                }
            }
        }

        // convert strings to specific types
        LogLevel log_level = LogLevel.INFO; // default log level if none or Wrong level in config
        if (with_settings.TryGetValue("log_level", out object? logLevelValue) && logLevelValue != null)
        {
            if (logLevelValue is LogLevel level)
            {
                log_level = level;
            }
            else
            {
                Enum.TryParse<LogLevel>(logLevelValue.toStr(), true, out log_level);
                with_settings["log_level"] = log_level;
            }
        }
        else
            with_settings["log_level"] = log_level;


        // default settings that depend on other settings
        if (!with_settings.ContainsKey("ASSETS_URL"))
            with_settings["ASSETS_URL"] = with_settings["ROOT_URL"].toStr() + "/assets";
    }

    /// <summary>
    /// Builds startup settings for the current ASP.NET Core environment with environment overrides applied.
    /// </summary>
    /// <param name="cfg">Application configuration provider containing the appSettings section.</param>
    /// <returns>A flat settings dictionary whose keys are direct children of appSettings.</returns>
    public static FwDict settingsForEnvironment(IConfiguration cfg)
    {
        setConfiguration(cfg);

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "";
        FwDict st = [];
        applyAppSettings(cfg, st);

        // Override by name if environment-based overrides are used
        overrideSettingsByName(environment, st);

        return st;
    }

    private static string getHostCacheKey(string host)
    {
        var trimmed = host?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(trimmed)) return trimmed;

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? string.Empty;
        return string.IsNullOrEmpty(environment) ? DefaultHostKey : environment;
    }

    private static string getOverrideName(string host, string cacheKey)
    {
        if (!string.IsNullOrEmpty(host))
            return host;

        return cacheKey == DefaultHostKey ? string.Empty : cacheKey;
    }

    private static FwDict GetSettingsForHost(string? host = null, HttpContext? ctx = null)
    {
        if (host != null)
            host = host.Trim();

        var cacheKey = host != null ? resolveHostKey(host) : (_currentHostKey.Value ?? resolveHostKey(null));
        var overrideName = getOverrideName(host ?? string.Empty, cacheKey);
        var isHostProvided = !string.IsNullOrEmpty(host);

        var hostSettings = _hostCache.GetOrAdd(cacheKey,
            _ => new Lazy<FwDict>(() => buildForHost(ctx, overrideName, isHostProvided), LazyThreadSafetyMode.ExecutionAndPublication)
        );

        return hostSettings.Value;
    }

    private static string resolveHostKey(string? host)
    {
        return getHostCacheKey(host ?? string.Empty);
    }

    private static string configuredRootDomainForHost(FwDict settings, string host, bool isHostProvided)
    {
        var rootDomain = settings["ROOT_DOMAIN"].toStr();
        if (!isHostProvided || string.IsNullOrWhiteSpace(host))
            return rootDomain;

        var hostName = hostNameOnly(host);
        if (!string.IsNullOrWhiteSpace(rootDomain)
            && string.Equals(hostName, hostNameOnly(rootDomain), StringComparison.OrdinalIgnoreCase))
            return rootDomain;

        if (settings["override"] is not FwDict overs)
            return string.Empty;

        foreach (string overName in overs.Keys)
        {
            if (overs[overName] is not FwDict over)
                continue;

            var pattern = over["hostname_match"].toStr();
            if (pattern.Length == 0 || isWildcardHostPattern(pattern))
                continue;

            if (hostMatchesPattern(host.Trim(), hostName, pattern))
                return over["ROOT_DOMAIN"].toStr();
        }

        return string.Empty;
    }

    private static bool hostMatchesPattern(string host, string hostName, string pattern)
    {
        try
        {
            var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
            var hostMatch = Regex.Match(host, pattern, options);
            if (hostMatch.Success && hostMatch.Index == 0 && hostMatch.Length == host.Length)
                return true;

            if (hostName.Length == 0 || string.Equals(host, hostName, StringComparison.OrdinalIgnoreCase))
                return false;

            var hostNameMatch = Regex.Match(hostName, pattern, options);
            return hostNameMatch.Success && hostNameMatch.Index == 0 && hostNameMatch.Length == hostName.Length;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool isWildcardHostPattern(string pattern)
    {
        var normalized = pattern.Trim();
        return normalized is "*" or ".*" or "^.*$" or ".+" or "^.+$";
    }

    private static string hostNameOnly(string host)
    {
        var raw = host.Trim();
        if (raw.Length == 0)
            return string.Empty;

        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            return uri.Host.TrimEnd('.').ToLowerInvariant();

        try
        {
            var hostString = HostString.FromUriComponent(raw);
            if (!string.IsNullOrWhiteSpace(hostString.Host))
                return hostString.Host.TrimEnd('.').ToLowerInvariant();
        }
        catch (Exception)
        {
            // Fall through to conservative parsing.
        }

        if (raw.StartsWith("[", StringComparison.Ordinal) && raw.Contains(']'))
            raw = raw[1..raw.IndexOf(']')];
        else
        {
            var colonIndex = raw.LastIndexOf(':');
            if (colonIndex > -1 && raw.IndexOf(':') == colonIndex)
                raw = raw[..colonIndex];
        }

        return raw.Trim().TrimEnd('.').ToLowerInvariant();
    }

    private static string buildRoutePrefixesRx(FwDict currentSettings)
    {
        // convert settings["route_prefixes"] FwRow (ex: /Admin => True) to FwList routePrefixes
        var routePrefixes = new StrList((currentSettings["route_prefixes"] as FwDict ?? []).Keys);

        var escaped = from string p in routePrefixes orderby p.Length descending select Regex.Escape(p);
        return @"^(" + string.Join("|", escaped) + @")(/.*)?$";
    }
}
