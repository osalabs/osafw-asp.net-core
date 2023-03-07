// App Configuration class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

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
                return;
            FwConfig.configuration = configuration;
            FwConfig.hostname = hostname;
            initDefaults(context, hostname);
            readSettings();
            specialSettings();

            settings["_SETTINGS_OK"] = true; // just a marker to ensure we have all settings set
        }
    }

    // reload settings
    public static void reload(FW fw)
    {
        initDefaults(fw.context, FwConfig.hostname);
        readSettings();
        specialSettings();
    }

    // init default settings
    private static void initDefaults(HttpContext context, string hostname = "")
    {
        settings = new Hashtable();
        HttpRequest req = context.Request;

        if (string.IsNullOrEmpty(hostname))
            hostname = context.Request.Host.ToString();
            //hostname = context.GetServerVariable("HTTP_HOST") ?? "";
        settings["hostname"] = hostname;

        string ApplicationPath = req.PathBase;
        settings["ROOT_URL"] = Regex.Replace(ApplicationPath, @"/$", ""); // removed last / if any

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

        settings["log"] = settings["site_root"] + $@"{path_separator}App_Data{path_separator}logs{path_separator}main.log";
        settings["log_max_size"] = 100 * 1024 * 1024; // 100 MB is max log size
        settings["tmp"] = Path.GetTempPath();

        string http = "http://";
        if (context.GetServerVariable("HTTPS") == "on")
            http = "https://";
        string port = ":" + context.GetServerVariable("SERVER_PORT");
        if (port == ":80" || port == ":443")
            port = "";
        settings["ROOT_DOMAIN"] = http + context.GetServerVariable("SERVER_NAME") + port;
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

    // set special settings after we read config
    private static void specialSettings()
    {
        string hostname = (string)settings["hostname"];

        Hashtable overs = (Hashtable)settings["override"];
        if (overs != null)
        {
            foreach (string over_name in overs.Keys)
            {
                Hashtable over = (Hashtable)overs[over_name];
                if (Regex.IsMatch(hostname, (string)over["hostname_match"]))
                {
                    settings["config_override"] = over_name;
                    Utils.mergeHashDeep(ref settings, ref over);
                    break;
                }
            }
        }

        // convert strings to specific types
        LogLevel log_level = LogLevel.INFO; // default log level if No or Wrong level in config
        if (settings.ContainsKey("log_level") && settings["log_level"] != null)
            Enum.TryParse<LogLevel>((string)settings["log_level"], true, out log_level);

        settings["log_level"] = log_level;

        // default settings that depend on other settings
        if (!settings.ContainsKey("ASSETS_URL"))
            settings["ASSETS_URL"] = settings["ROOT_URL"] + "/assets";

        // default or theme template dir
        if (!settings.ContainsKey("template_theme") || (string)settings["template_theme"] == "default")
            settings["template"] = (string)settings["site_root"] + $@"{path_separator}App_Data{path_separator}template";
        else
            settings["template"] = (string)settings["site_root"] + $@"{path_separator}App_Data{path_separator}template_{(string)settings["template_theme"]}";
    }


    // prefixes used so Dispatcher will know that url starts not with a full controller name, but with a prefix, need to be added to controller name
    // return regexp str that cut the prefix from the url, second capturing group captures rest of url after the prefix
    public static string getRoutePrefixesRX()
    {
        if (string.IsNullOrEmpty(route_prefixes_rx))
        {
            // prepare regexp - escape all prefixes
            ArrayList r = new();
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

    public static void overrideSettingsByName(string override_name, ref Hashtable settings)
    {
        Hashtable overs = (Hashtable)settings["override"];
        if (overs != null)
        {
            foreach (string over_name in overs.Keys)
            {
                if (over_name == override_name)
                {
                    settings["config_override"] = over_name;
                    Hashtable over = (Hashtable)overs[over_name];
                    Utils.mergeHashDeep(ref settings, ref over);
                    break;
                }
            }
        }

        // convert strings to specific types
        LogLevel log_level = LogLevel.INFO; // default log level if No or Wrong level in config
        if (settings.ContainsKey("log_level") && settings["log_level"] != null)
            Enum.TryParse<LogLevel>((string)settings["log_level"], true, out log_level);

        settings["log_level"] = log_level;

        // default settings that depend on other settings
        if (!settings.ContainsKey("ASSETS_URL"))
            settings["ASSETS_URL"] = settings["ROOT_URL"] + "/assets";
    }
}