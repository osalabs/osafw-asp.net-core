// App Configuration class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace osafw;

public class FwConfig
{
    public static string hostname;
    public static Hashtable settings;
    public static string route_prefixes_rx = "";
    public static readonly char path_separator = Path.DirectorySeparatorChar;
    public static IConfiguration configuration;

    private static readonly object locker = new();

    public static void init(HttpContext context, IConfiguration configuration, string hostname = "")
    {
        // appSettings is Shared, so it's lifetime same as application lifetime
        // if appSettings already initialized no need to read web.config again
        lock (locker)
        {
            if (settings != null && settings.Count > 0 && settings.ContainsKey("_SETTINGS_OK"))
            {
                // already initialized, but re-init web-related settings on each request
                if (context != null)
                    initWeb(context, hostname);
                return;
            }

            FwConfig.configuration = configuration;
            FwConfig.hostname = hostname;
            initDefaults(context, hostname);
            readSettings();

            if (string.IsNullOrEmpty(hostname))
            {
                //if no hostname - use environment
                var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "";
                overrideSettingsByName(environment, settings);
            }
            else
                overrideSettingsByName(hostname, settings, true);

            settings["_SETTINGS_OK"] = true; // just a marker to ensure we have all settings set
        }
    }

    // reload settings
    public static void reload(FW fw)
    {
        initDefaults(fw.context, FwConfig.hostname);
        readSettings();
        overrideSettingsByName(hostname, settings, true);
    }

    // update web-specific settings like hostname and ROOT_URL
    private static void initWeb(HttpContext context, string hostname = "")
    {
        overrideContextSettings(context, hostname);
        overrideSettingsByName(hostname, settings, true);
    }

    // 
    /// <summary>
    /// init default settings
    /// </summary>
    /// <param name="context">can be null for offline execution</param>
    /// <param name="hostname"></param>
    private static void initDefaults(HttpContext context, string hostname = "")
    {
        settings = new Hashtable
        {
            ["hostname"] = "",
            ["ROOT_URL"] = "",
            ["ROOT_DOMAIN"] = "",
        };

        overrideContextSettings(context, hostname);

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

        settings["site_root"] = Regex.Replace(PhysicalApplicationPath, @$"\{path_separator}$", ""); // removed last \ if any

        // default or theme template dir
        // make absolute path to templates from site root
        settings["template"] = (string)settings["site_root"] + $@"{path_separator}App_Data{path_separator}template";

        settings["log"] = settings["site_root"] + $@"{path_separator}App_Data{path_separator}logs{path_separator}main.log";
        settings["log_max_size"] = 100 * 1024 * 1024; // 100 MB is max log size
        settings["tmp"] = Utils.getTmpDir(); // TODO not used? remove?

        settings["lang"] ??= "en"; // default language
        settings["is_lang_update"] ??= false; // default language update flag
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
                Hashtable s = (Hashtable)settings[section.Key];
                readSettingsSection(sub_section, ref s);
            }
        }
    }

    // read setting into appSettings
    private static void readSettings()
    {
        var valuesSection = configuration.GetSection("appSettings");
        foreach (IConfigurationSection section in valuesSection.GetChildren())
        {
            readSettingsSection(section, ref settings);
        }
    }

    // prefixes used so Dispatcher will know that url starts not with a full controller name, but with a prefix, need to be added to controller name
    // return regexp str that cut the prefix from the url, second capturing group captures rest of url after the prefix
    public static string getRoutePrefixesRX()
    {
        if (string.IsNullOrEmpty(route_prefixes_rx))
        {
            // prepare regexp - escape all prefixes
            ArrayList r = [];
            var route_prefixes = (Hashtable)settings["route_prefixes"];
            if (route_prefixes != null)
            {
                //sort prefixes, so longer prefixes mathced first, also escape to use in regex
                var prefixes = from string prefix in route_prefixes.Keys orderby prefix.Length descending, prefix select Regex.Escape(prefix);
                route_prefixes_rx = @"^(" + string.Join("|", prefixes) + @")(/.*)?$";
            }
        }

        return route_prefixes_rx;
    }


    public static void overrideContextSettings(HttpContext context, string hostname = "")
    {
        if (context == null) return;

        HttpRequest req = context.Request;
        if (string.IsNullOrEmpty(hostname))
            hostname = context.Request.Host.ToString();

        settings["hostname"] = hostname;
        FwConfig.hostname = hostname;

        string ApplicationPath = req.PathBase;
        settings["ROOT_URL"] = Regex.Replace(ApplicationPath, @"/$", "");

        string http = "http://";
        if (context.GetServerVariable("HTTPS") == "on")
            http = "https://";
        string port = ":" + context.GetServerVariable("SERVER_PORT");
        if (port == ":80" || port == ":443")
            port = "";
        settings["ROOT_DOMAIN"] = http + context.GetServerVariable("SERVER_NAME") + port;
    }

    public static void overrideSettingsByName(string override_name, Hashtable settings, bool is_regex_match = false)
    {
        Hashtable overs = (Hashtable)settings["override"];
        if (overs != null)
        {
            foreach (string over_name in overs.Keys)
            {
                Hashtable over = (Hashtable)overs[over_name];
                if (!is_regex_match && over_name == override_name
                    || is_regex_match && Regex.IsMatch(override_name, (string)over["hostname_match"])
                    )
                {
                    settings["config_override"] = over_name;
                    Utils.mergeHashDeep(settings, over);
                    break;
                }
            }
        }

        // convert strings to specific types
        LogLevel log_level = LogLevel.INFO; // default log level if none or Wrong level in config
        if (settings.ContainsKey("log_level") && settings["log_level"] != null)
        {
            if (settings["log_level"].GetType() != typeof(LogLevel))
            {
                Enum.TryParse<LogLevel>((string)settings["log_level"], true, out log_level);
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
        var settings = (Hashtable)appSettings["appSettings"];
        // Override by name if environment-based overrides are used
        overrideSettingsByName(environment, settings);

        return settings;
    }
}