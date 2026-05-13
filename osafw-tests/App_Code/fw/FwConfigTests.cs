using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace osafw.Tests
{
    [TestClass]
    public class FwConfigTests
    {
        [TestMethod]
        public void GetRoutePrefixesRX_UsesCachedHostSettings()
        {
            var host = "route-test-cache";
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["appSettings:route_prefixes:/Admin"] = "True",
                })
                .Build();

            FwConfig.init(null, config, host);

            var rx1 = FwConfig.getRoutePrefixesRX();
            var compiled1 = new Regex(rx1);
            Assert.IsTrue(compiled1.IsMatch("/Admin/test"));
        }

        [TestMethod]
        public void SettingsForEnvironment_ReturnsFlatAppSettings()
        {
            var previousEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            try
            {
                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["appSettings:SITE_NAME"] = "Base Site",
                        ["appSettings:db:main:connection_string"] = "Server=test;Database=main;",
                        ["appSettings:db:main:type"] = "SQL",
                    })
                    .Build();

                var settings = FwConfig.settingsForEnvironment(config);
                var db = (FwDict)settings["db"]!;
                var main = (FwDict)db["main"]!;

                Assert.AreEqual("Base Site", settings["SITE_NAME"]);
                Assert.IsFalse(settings.ContainsKey("appSettings"));
                Assert.AreEqual("Server=test;Database=main;", main["connection_string"]);
                Assert.AreEqual("SQL", main["type"]);
            }
            finally
            {
                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnvironment);
            }
        }

        [TestMethod]
        public void SettingsForEnvironment_AppliesEnvironmentOverrideToFlatSettings()
        {
            var previousEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            try
            {
                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["appSettings:SITE_NAME"] = "Base Site",
                        ["appSettings:db:main:connection_string"] = "Server=base;Database=main;",
                        ["appSettings:db:main:type"] = "SQL",
                        ["appSettings:override:Development:SITE_NAME"] = "Development Site",
                        ["appSettings:override:Development:db:main:connection_string"] = "Server=dev;Database=main;",
                    })
                    .Build();

                var settings = FwConfig.settingsForEnvironment(config);
                var db = (FwDict)settings["db"]!;
                var main = (FwDict)db["main"]!;

                Assert.AreEqual("Development", settings["config_override"]);
                Assert.AreEqual("Development Site", settings["SITE_NAME"]);
                Assert.IsFalse(settings.ContainsKey("appSettings"));
                Assert.AreEqual("Server=dev;Database=main;", main["connection_string"]);
                Assert.AreEqual("SQL", main["type"]);
            }
            finally
            {
                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnvironment);
            }
        }

        [TestMethod]
        public void OverrideSettingsByName_AppliesExactOverride()
        {
            var settings = new FwDict
            {
                ["lang"] = "en",
                ["nested"] = new FwDict { ["value"] = "old" },
                ["override"] = new FwDict
                {
                    ["TenantA"] = new FwDict
                    {
                        ["hostname_match"] = "example.com",
                        ["lang"] = "fr",
                        ["nested"] = new FwDict { ["value"] = "new" },
                    },
                },
            };

            FwConfig.overrideSettingsByName("TenantA", settings, false);

            Assert.AreEqual("TenantA", settings["config_override"]);
            Assert.AreEqual("fr", settings["lang"]);
            Assert.AreEqual("new", ((FwDict)settings["nested"]!)["value"]);
        }

        [TestMethod]
        public void OverrideSettingsByName_RespectsRegexMatching()
        {
            var settings = new FwDict
            {
                ["timezone"] = "UTC",
                ["override"] = new FwDict
                {
                    ["Geo"] = new FwDict
                    {
                        ["hostname_match"] = "example",
                        ["timezone"] = "Local",
                    },
                },
            };

            FwConfig.overrideSettingsByName("us.example.com", settings, true);

            Assert.AreEqual("Geo", settings["config_override"]);
            Assert.AreEqual("Local", settings["timezone"]);
        }
    }
}
