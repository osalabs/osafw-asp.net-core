#if isSQLite
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class SettingsProtectionTests
{
    [TestMethod]
    public void BootstrapCookieProviderCannotBeUsedForPersistedSettings()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("The durable production key contract requires Windows DPAPI.");

        var path = Path.Combine(Path.GetTempPath(), "osafw-bootstrap-keys-" + Guid.NewGuid().ToString("N") + ".sqlite");
        var definition = DB.h("type", DB.DBTYPE_SQLITE, "connection_string", "Data Source=" + path + ";Pooling=False", "timezone", "UTC");
        try
        {
            using var db = new DB(definition);
            var settings = new FwDict
            {
                ["DATA_PROTECTION_APPLICATION_NAME"] = "bootstrap-test",
                ["db"] = new FwDict { ["main"] = definition }
            };
            var services = new ServiceCollection();
            Settings.configureProtection(services, settings, isRequired: false);
            using var webServices = services.BuildServiceProvider();
            var cookie = webServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-cookie").Protect("temporary");
            Assert.IsFalse(string.IsNullOrWhiteSpace(cookie));

            var context = TestHelpers.CreateHttpContext("bootstrap-test");
            context.RequestServices = webServices;
            using var scope = new FwTestScope(_ => db, new System.Collections.Generic.Dictionary<string, string?>
            {
                ["appSettings:db:main:type"] = DB.DBTYPE_SQLITE,
                ["appSettings:db:main:connection_string"] = definition["connection_string"].toStr(),
                ["appSettings:db:main:timezone"] = "UTC",
                ["appSettings:DATA_PROTECTION_APPLICATION_NAME"] = "bootstrap-test",
            }, context);

            Assert.ThrowsExactly<System.Security.Cryptography.CryptographicException>(
                () => scope.Fw.settings.protect("credential", "test-value"));
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                File.Delete(path + suffix);
        }
    }

    [TestMethod]
    public void DurableDpapiKeyRingSurvivesProviderRecreation()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("The durable production key contract requires Windows DPAPI.");

        var path = Path.Combine(Path.GetTempPath(), "osafw-durable-keys-" + Guid.NewGuid().ToString("N") + ".sqlite");
        var definition = DB.h("type", DB.DBTYPE_SQLITE, "connection_string", "Data Source=" + path + ";Pooling=False", "timezone", "UTC");
        try
        {
            using var db = new DB(definition);
            db.exec("CREATE TABLE fwkeys (id INTEGER PRIMARY KEY AUTOINCREMENT, itype INTEGER NOT NULL, iname TEXT NOT NULL, XmlValue TEXT NOT NULL, upd_time DATETIME)");
            var settings = new FwDict
            {
                ["DATA_PROTECTION_APPLICATION_NAME"] = "durable-settings-test",
                ["db"] = new FwDict { ["main"] = definition }
            };

            string encrypted;
            var firstServices = new ServiceCollection();
            Settings.configureProtection(firstServices, settings);
            using (var first = firstServices.BuildServiceProvider())
                encrypted = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-purpose").Protect("test-value");

            var persisted = db.col("fwkeys", DB.h("itype", 10), "XmlValue");
            Assert.IsGreaterThan(0, persisted.Count);
            StringAssert.Contains(persisted[0], "encryptedSecret");

            var secondServices = new ServiceCollection();
            Settings.configureProtection(secondServices, settings);
            using var second = secondServices.BuildServiceProvider();
            Assert.AreEqual("test-value", second.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-purpose").Unprotect(encrypted));
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                File.Delete(path + suffix);
        }
    }
}
#endif
