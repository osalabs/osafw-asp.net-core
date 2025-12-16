using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
