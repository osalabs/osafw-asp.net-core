using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace osafw.Tests
{
    [TestClass]
    public class FwConfigTests
    {
        private AsyncLocal<FwDict>? _current;
        private FwDict? _originalSettings;

        [TestInitialize]
        public void SetUp()
        {
            var currentField = typeof(FwConfig).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static);
            _current = (AsyncLocal<FwDict>?)currentField?.GetValue(null);
            _originalSettings = _current?.Value != null ? new FwDict(_current.Value) : null;
            if (_current != null)
                _current.Value = _originalSettings != null ? new FwDict(_originalSettings) : [];
        }

        [TestCleanup]
        public void TearDown()
        {
            if (_current != null)
                _current.Value = _originalSettings ?? [];
        }

        [TestMethod]
        public void GetRoutePrefixesRX_BuildsRegexFromSettings()
        {
            var settings = FwConfig.settings;
            settings["route_prefixes"] = new FwDict
            {
                ["/Admin"] = true,
                ["/Api"] = true,
            };

            var rx = FwConfig.getRoutePrefixesRX();
            var compiled = new Regex(rx);

            Assert.IsTrue(compiled.IsMatch("/Admin"));
            Assert.IsTrue(compiled.IsMatch("/Admin/test"));
            Assert.IsTrue(compiled.IsMatch("/Api/demo"));
            Assert.IsFalse(compiled.IsMatch("/Other"));
            Assert.AreEqual(rx, settings["_route_prefixes_rx"]);
        }

        [TestMethod]
        public void OverrideSettingsByName_AppliesExactOverride()
        {
            var settings = FwConfig.settings;
            settings["lang"] = "en";
            settings["nested"] = new FwDict { ["value"] = "old" };
            settings["override"] = new FwDict
            {
                ["TenantA"] = new FwDict
                {
                    ["hostname_match"] = "example.com",
                    ["lang"] = "fr",
                    ["nested"] = new FwDict { ["value"] = "new" },
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
            var settings = FwConfig.settings;
            settings["timezone"] = "UTC";
            settings["override"] = new FwDict
            {
                ["Geo"] = new FwDict
                {
                    ["hostname_match"] = "example",
                    ["timezone"] = "Local",
                },
            };

            FwConfig.overrideSettingsByName("us.example.com", settings, true);

            Assert.AreEqual("Geo", settings["config_override"]);
            Assert.AreEqual("Local", settings["timezone"]);
        }
    }
}
