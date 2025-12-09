// App Configuration class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace osafw;

public static class FwConfig
{
    // public API
    public static string hostname => (settings?["hostname"] as string) ?? "";

    /// <summary>Per-request, host-specific settings bucket.</summary>
    public static Hashtable settings { get => _current.Value ??= new Hashtable(); private set => _current.Value = value; }

    // internals
    private static readonly AsyncLocal<Hashtable> _current = new();                // per-async-flow bucket
    private static readonly ConcurrentDictionary<string, Hashtable> _hostCache = new();
    private static IConfiguration? configuration;                                   // appsettings.* provider
    private static readonly object locker = new();

    public static readonly char path_separator = Path.DirectorySeparatorChar;

    public static string getRoutePrefixesRX()
    {
        if (settings["_route_prefixes_rx"] is string rx && rx.Length > 0) return rx;

        // convert settings["route_prefixes"] Hashtable (ex: /Admin => True) to ArrayList routePrefixes
        var routePrefixes = new ArrayList((settings["route_prefixes"] as Hashtable ?? []).Keys);

        var escaped = from string p in routePrefixes orderby p.Length descending select Regex.Escape(p);
        rx = @"^(" + string.Join("|", escaped) + @")(/.*)?$";
        settings["_route_prefixes_rx"] = rx;                            // memoise
        return rx;
    }

    /// <remarks>Called exactly once per _http request_ by FW.</remarks>
    public static void init(HttpContext? ctx, IConfiguration cfg, string? host = null)
    {
        configuration ??= cfg;                                          // record for offline tools

        host ??= ctx?.Request.Host.ToString() ?? "";
        settings = _hostCache.GetOrAdd(host, _ => buildForHost(ctx, host));
        // Some fields (ports, protocol) may vary per request even for same host - rebuild those cheap bits every time.
        overrideContextSettings(ctx, host, settings);
    }

    // clears cache entry for request's host.
    public static void reload(FW fw)
    {
        _hostCache.TryRemove(hostname, out _);     // force re-build on next request
        if (configuration == null)
            throw new InvalidOperationException("FwConfig.init must be called before reload");

        init(fw.context, configuration, fw.context?.Request.Host.ToString());
    }

    // One-time base (read-only) initialisation shared by all hosts.
    private static Lazy<Hashtable> _base = new(() =>
    {
        var tmp = new Hashtable();
        initDefaults(null, "", ref tmp);
        if (configuration != null)
            readSettings(configuration, ref tmp);                                     // appsettings:appSettings
        return tmp;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    private static Hashtable buildForHost(HttpContext? ctx, string host)
    {
        // clone deep - each host gets its own mutable copy
        var hs = Utils.cloneHashDeep(_base.Value) ?? [];

        if (string.IsNullOrEmpty(host))
            overrideSettingsByName(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? string.Empty, hs, false); // use env name override if no host
        else
            overrideSettingsByName(host, hs, true);

        overrideContextSettings(ctx, host, hs);
        return hs;
    }
    // 
    /// <summary>
    /// init default settings
    /// </summary>
    /// <param name="context">can be null for offline execution</param>
    /// <param name="hostname"></param>
    private static void initDefaults(HttpContext? context, string hostname, ref Hashtable st)
    {
        st = new Hashtable
        {
            ["hostname"] = "",
            ["ROOT_URL"] = "",
            ["ROOT_DOMAIN"] = "",
        };

        overrideContextSettings(context, hostname, st);

        string PhysicalApplicationPath;
        string basedir = AppDomain.CurrentDomain.BaseDirectory; //application root directory
        var bin_index = basedir.IndexOf($@"{path_separator}bin");
        if (bin_index == -1)
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

        st["date_format"] ??= DateUtils.DATE_FORMAT_MDY;
        st["time_format"] ??= DateUtils.TIME_FORMAT_12;
        st["timezone"] ??= DateUtils.TZ_UTC;
    }

    public static void readSettingsSection(IConfigurationSection section, ref Hashtable settings)
    {
        if (section.Value != null)
        {
            settings[section.Key] = section.Value;
        }
        else if (section.Key != null)
        {
            settings[section.Key] = new Hashtable();
            foreach (IConfigurationSection sub_section in section.GetChildren())
            {
                Hashtable s = (Hashtable)settings[section.Key]!;
                readSettingsSection(sub_section, ref s);
            }
        }
    }

    // read setting into appSettings
    private static void readSettings(IConfiguration cfg, ref Hashtable st)
    {
        var valuesSection = cfg.GetSection("appSettings");
        foreach (IConfigurationSection section in valuesSection.GetChildren())
        {
            readSettingsSection(section, ref st);
        }
    }

    private static void overrideContextSettings(HttpContext? ctx, string host, Hashtable st)
    {
        if (ctx == null) return;
        var req = ctx.Request;

        st["hostname"] = host;
        string appBase = req.PathBase;
        st["ROOT_URL"] = Regex.Replace(appBase, @"/$", "");

        var httpsValue = ctx.GetServerVariable("HTTPS") ?? string.Empty;
        bool isHttps = httpsValue.Equals("on", StringComparison.OrdinalIgnoreCase);
        string port = ctx.GetServerVariable("SERVER_PORT") ?? "80";
        string portPart = (port == "80" || port == "443") ? "" : ":" + port;
        var serverName = ctx.GetServerVariable("SERVER_NAME") ?? host;
        st["ROOT_DOMAIN"] = (isHttps ? "https://" : "http://") + serverName + portPart;
    }

    public static void overrideSettingsByName(string override_name, Hashtable settings, bool is_regex_match = false)
    {
        if (settings["override"] is Hashtable overs)
        {
            foreach (string over_name in overs.Keys)
            {
                if (overs[over_name] is Hashtable over)
                {
                    if (!is_regex_match && over_name == override_name
                        || is_regex_match && Regex.IsMatch(override_name, over["hostname_match"].toStr())
                        )
                    {
                        settings["config_override"] = over_name;
                        Utils.mergeHashDeep(settings, over);
                        break;
                    }
                }
            }
        }

        // convert strings to specific types
        LogLevel log_level = LogLevel.INFO; // default log level if none or Wrong level in config
        if (settings.ContainsKey("log_level") && settings["log_level"] != null)
        {
            if (settings["log_level"].GetType() != typeof(LogLevel))
            {
                Enum.TryParse<LogLevel>(settings["log_level"].toStr(), true, out log_level);
                settings["log_level"] = log_level;
            }
        }
        else
            settings["log_level"] = log_level;


        // default settings that depend on other settings
        if (!settings.ContainsKey("ASSETS_URL"))
            settings["ASSETS_URL"] = settings["ROOT_URL"] + "/assets";
    }

    /// <summary>
    /// Get settings for the current environment with proper overrides
    /// </summary>
    /// <returns></returns>
    public static Hashtable settingsForEnvironment(IConfiguration configuration)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "";
        var appSettings = new Hashtable();
        readSettingsSection(configuration.GetSection("appSettings"), ref appSettings);

        // The “appSettings” itself might be nested inside the hash
        var settings = (Hashtable?)appSettings["appSettings"] ?? new Hashtable();
        appSettings["appSettings"] = settings;
        // Override by name if environment-based overrides are used
        overrideSettingsByName(environment, settings);

        return settings;
    }
}
