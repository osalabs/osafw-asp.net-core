using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace osafw_asp_net_core.fw
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

        // read setting into appSettings
        private static void readSettings(IConfiguration conf_settings) {
            if (settings == null) 
            {
                settings = new Hashtable();
            }
            var valuesSection = conf_settings.GetSection("appSettings");
            foreach (IConfigurationSection section in valuesSection.GetChildren())
            {
                settings[section.Key] = section.Value;
            }
            /*NameValueCollection appSettings = ConfigurationManager.AppSettings();

                Dim keys() As String = appSettings.AllKeys
                For Each key As String In keys
                    parseSetting(key, appSettings(key))
                Next*/
        }
        private static void parseSetting(string key, ref string value) {
            /*Dim delim As String = "|"
            if (InStr(key, delim) = 0 Then
                settings(key) = parseSettingValue(value)
            Else
                Dim keys() As String = Split(key, delim)

                'build up all hashtables tree
                Dim ptr As Hashtable = settings
                For i As Integer = 0 To keys.Length - 2
                    Dim hkey As String = keys(i)
                    if (ptr.ContainsKey(hkey) AndAlso TypeOf (ptr) Is Hashtable Then
                        ptr = ptr(hkey) 'going deep into
                    Else
                        ptr(hkey) = New Hashtable 'this will overwrite any value, i.e. settings names must be different on same level
                        ptr = ptr(hkey)
                    End If
                Next
                'assign value to key element in deepest hashtree
                ptr(keys(keys.Length - 1)) = parseSettingValue(value)
            End If*/
        }
        /*'parse value to type, supported:
        'boolean
        'int
        'qh - using Utils.qh()
        Private static Function parseSettingValue(ByRef value As String) As Object
            Dim result As Object
            Dim m As Match = Regex.Match(value, "^~(.*?)~")
            if (m.Success) {'if (value contains type = "~int~25" -) {cast value to the type
                Dim value2 As String = Regex.Replace(value, "^~.*?~", "")
                Select Case m.Groups(1).Value
                    Case "int"
                        Dim ival As Integer
                        if (Not Integer.TryParse(value2, ival)) {ival = 0
                        result = ival
                    Case "boolean"
                        Dim ibool As Boolean
                        if (Not Boolean.TryParse(value2, ibool)) {ibool = False
                        result = ibool
                    Case "qh"
                        result = Utils.qh(value2)
                    Case Else
                        result = value2
                End Select
            Else
                result = String.Copy(value)
            End If

            Return result
        End Function

        'set special settings after we read config
        Private static Sub specialSettings()
            Dim hostname As String = settings("hostname")

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

            'default settings that depend on other settings
            if (Not settings.ContainsKey("ASSETS_URL") Then
                settings("ASSETS_URL") = settings("ROOT_URL") & "/assets"
            End If

        End Sub


        'prefixes used so Dispatcher will know that url starts not with a full controller name, but with a prefix, need to be added to controller name
        'return regexp str that cut the prefix from the url, second capturing group captures rest of url after the prefix
        public static Function getRoutePrefixesRX() As String
            if (String.IsNullOrEmpty(route_prefixes_rx) Then
                'prepare regexp - escape all prefixes
                Dim r As New ArrayList()
                For Each url As String In settings("route_prefixes").Keys
                    r.Add(Regex.Escape(url))
                Next

                route_prefixes_rx = "^(" & String.Join("|", CType(r.ToArray(GetType(String)), String())) & ")(/.*)?$"
            End If

            Return route_prefixes_rx
        End Function*/
    }
}
