using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace osafw
{
    public class FwConfig
    {
        public static string hostname;
        public static Hashtable settings = null;
        public static string route_prefixes_rx;

        private static readonly object locker = new object();

        public static void init(HttpRequest req, IConfiguration conf_settings, string hostname = "") {
            // appSettings is static, so it's lifetime same as application lifetime 
            // if (appSettings already initialized no need to read web.config again
            lock (locker) {
                if (settings != null && settings.Count > 0 && settings.ContainsKey("_SETTINGS_OK")) {
                    return;
                }
                FwConfig.hostname = hostname;
                initDefaults(req, hostname);
                readSettings(conf_settings);
                //specialSettings();

                settings["_SETTINGS_OK"] = true; // just a marker to ensure we have all settings set
            }
        }

        // reload settings
        public static void reload() {
            //initDefaults(HttpContext.Current.Request, FwConfig.hostname);
            //readSettings();
            //specialSettings();
        }

        // init default settings
        private static void initDefaults(HttpRequest req, string hostname = "") {
            settings = new Hashtable();

            if (string.IsNullOrEmpty(hostname)) {
                hostname = req.Host.Host;
            }
            settings["hostname"] = hostname;

            settings["ROOT_URL"] = Regex.Replace(req.Path, "\\/$", ""); // removed last / if (any
            string physicalApplicationPath = AppDomain.CurrentDomain.BaseDirectory.Substring(0, AppDomain.CurrentDomain.BaseDirectory.IndexOf("\\bin"));
            settings["site_root"] = Regex.Replace(physicalApplicationPath, "\\$", ""); // removed last \ if (any



            settings["template"] = settings["site_root"] + "\\App_Data\template";
            settings["log"] = settings["site_root"] + "\\App_Data\\logs\\main.log";
            settings["log_max_size"] = 100 * 1024 * 1024; // 100 MB is max log size
            settings["tmp"] = Path.GetTempPath();
            settings["log_level"] = "ALL";

            string http = "http://";
            if (req.IsHttps) { http = "https://"; }
            string port = ":" + req.HttpContext.Connection.LocalPort;
            if (port == ":80" || port == ":443") { port = ""; }
            settings["ROOT_DOMAIN"] = http + req.Host.Host + port;

        }
        private static void readSettingsSeaction(IConfigurationSection section, ref Hashtable settings)
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
                    readSettingsSeaction(sub_section, ref s);
                }
            }
        }
        // read setting into appSettings
        private static void readSettings(IConfiguration conf_settings) {
            if (settings == null) 
            {
                settings = new Hashtable();
            }
            var valuesSection = conf_settings.GetSection("appSettings");
            foreach (IConfigurationSection section in valuesSection.GetChildren())
            {
                readSettingsSeaction(section, ref settings);
            }
        }

        // set special settings after we read config
        private static void specialSettings() 
        {
            String hostname = (String)settings["hostname"];

                        /*
            Dim overs As Hashtable = settings("override")
            For Each over_name As String In overs.Keys
                if (Regex.IsMatch(hostname, overs(over_name) ("hostname_match")) Then
                     settings("config_override") = over_name
                     Utils.mergeHashDeep(settings, overs(over_name))
                    Exit For
                End If
            Next

            'convert strings to specific types
            Dim log_level As LogLevel = LogLevel.INFO 'default log level if (No or Wrong level in config
            if (settings.ContainsKey("log_level") Then
                [Enum].TryParse(Of LogLevel)(settings("log_level"), True, log_level)
            End If
            settings("log_level") = log_level
        */

            // default settings that depend on other settings
            if (settings.ContainsKey("ASSETS_URL"))
            {
                settings["ASSETS_URL"] = settings["ROOT_URL"] + "/assets";
            }
        }

        // prefixes used so Dispatcher will know that url starts not with a full controller name, but with a prefix, need to be added to controller name
        // return regexp str that cut the prefix from the url, second capturing group captures rest of url after the prefix
        public static String getRoutePrefixesRX() {
            if (String.IsNullOrEmpty(route_prefixes_rx)) {
                // prepare regexp - escape all prefixes
                ArrayList r = new ArrayList();
                Hashtable route_prefixes = (Hashtable)settings["route_prefixes"];
                foreach (String url in route_prefixes.Keys) {
                    r.Add(Regex.Escape(url));
                }

                route_prefixes_rx = "^(" + String.Join("|", r.ToArray()) + ")(/.*)?$";
            }
            return route_prefixes_rx;
        }
    }
}
