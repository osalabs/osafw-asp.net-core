// App Configuration class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace osafw
{
    public class FwConfig
    {
        public static string hostname;
        public static Hashtable settings;
        public static string route_prefixes_rx = "";
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
                hostname = context.GetServerVariable("HTTP_HOST");
            settings["hostname"] = hostname;

            string ApplicationPath = req.PathBase; //TODO MIGRATE test with IIS subfolder if this is correct variable
            settings["ROOT_URL"] = Regex.Replace(ApplicationPath, @"\/$", ""); // removed last / if any
            string PhysicalApplicationPath = AppDomain.CurrentDomain.BaseDirectory.Substring(0, AppDomain.CurrentDomain.BaseDirectory.IndexOf(@"\bin"));//TODO MIGRATE what is bin???
            settings["site_root"] = Regex.Replace(PhysicalApplicationPath, @"\\$", ""); // removed last \ if any

            settings["template"] = settings["site_root"] + @"\App_Data\template";
            settings["log"] = settings["site_root"] + @"\App_Data\logs\main.log";
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

        private static void readSettingsSection(IConfigurationSection section, ref Hashtable settings)
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
                    foreach (string url in route_prefixes.Keys)
                        r.Add(Regex.Escape(url));

                    route_prefixes_rx = "^(" + string.Join("|", (string[])r.ToArray(typeof(string))) + ")(/.*)?$";
                }
            }

            return route_prefixes_rx;
        }
    }
}