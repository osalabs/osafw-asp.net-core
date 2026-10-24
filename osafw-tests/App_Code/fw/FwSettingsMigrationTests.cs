#if isSQLite
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class FwSettingsMigrationTests
{
    [TestMethod]
    public void IsCommandMatchesOnlySettingsMigrationCommand()
    {
        Assert.IsTrue(FwSettingsMigration.isCommand(["SeTtInGs-MiGrAtE", "legacy.json"]));
        Assert.IsFalse(FwSettingsMigration.isCommand(["scaffold"]));
        Assert.IsFalse(FwSettingsMigration.isCommand([]));
    }

    [TestMethod]
    public void RunMissingArgumentsReturnsUsageBeforeOpeningDatabase()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var result = FwSettingsMigration.run(
            ["settings-migrate"],
            new ConfigurationBuilder().Build(),
            output,
            error);

        Assert.AreEqual(FwSettingsMigration.EXIT_USAGE, result);
        Assert.AreEqual("", output.ToString());
        StringAssert.Contains(error.ToString(), "use settings-migrate");
    }

    [TestMethod]
    public void ExistingExplicitValuesWinAndOverridesRequireExplicitSelection()
    {
        const string legacyJson = """
            {
              "appSettings": {
                "SITE_NAME": "legacy site",
                "support_email": "base@example.test",
                "unknown_value": "ignored",
                "override": {
                  "Beta": {
                    "support_email": "beta@example.test"
                  }
                }
              }
            }
            """;

        using (var baseFixture = new MigrationFixture())
        {
            baseFixture.Seed("SITE_NAME", Settings.INPUT_TEXT, Settings.BASIS_VALUE, "custom site");
            baseFixture.Seed("support_email", Settings.INPUT_TEXT, Settings.BASIS_INHERIT);
            baseFixture.Seed("custom_setting", Settings.INPUT_TEXT, Settings.BASIS_VALUE, "preserve me");

            Assert.AreEqual(1, FwSettingsMigration.migrate(baseFixture.Fw, legacyJson));
            Assert.AreEqual("custom site", baseFixture.Raw("SITE_NAME"));
            Assert.AreEqual("base@example.test", baseFixture.Raw("support_email"));
            Assert.AreEqual("preserve me", baseFixture.Raw("custom_setting"));
        }

        using var overrideFixture = new MigrationFixture();
        overrideFixture.Seed("support_email", Settings.INPUT_TEXT, Settings.BASIS_INHERIT);

        Assert.AreEqual(1, FwSettingsMigration.migrate(overrideFixture.Fw, legacyJson, "Beta"));
        Assert.AreEqual("beta@example.test", overrideFixture.Raw("support_email"));
    }

    [TestMethod]
    public void PlaintextCredentialWinsOverLegacyJsonAndMigrationIsIdempotent()
    {
        const string legacyJson = """
            {"appSettings":{"OPENAI_API_KEY":"legacy-file-value"}}
            """;
        using var fixture = new MigrationFixture();
        fixture.Seed("OPENAI_API_KEY", Settings.INPUT_CREDENTIAL, Settings.BASIS_VALUE, "database-value");
        fixture.Seed("CUSTOM_VENDOR_TOKEN", Settings.INPUT_CREDENTIAL, Settings.BASIS_VALUE, "custom-database-value");

        Assert.AreEqual(2, FwSettingsMigration.migrate(fixture.Fw, legacyJson));

        var ciphertext = fixture.Raw("OPENAI_API_KEY");
        Assert.StartsWith(FwSettingsProtection.PREFIX, ciphertext);
        Assert.DoesNotContain("database-value", ciphertext);
        Assert.DoesNotContain("legacy-file-value", ciphertext);
        Assert.AreEqual("database-value", fixture.Model.readSecret("OPENAI_API_KEY"));
        var customCiphertext = fixture.Raw("CUSTOM_VENDOR_TOKEN");
        Assert.StartsWith(FwSettingsProtection.PREFIX, customCiphertext);
        Assert.DoesNotContain("custom-database-value", customCiphertext);
        Assert.AreEqual("custom-database-value", fixture.Model.readSecret("CUSTOM_VENDOR_TOKEN"));

        Assert.AreEqual(0, FwSettingsMigration.migrate(fixture.Fw, legacyJson));
        Assert.AreEqual(ciphertext, fixture.Raw("OPENAI_API_KEY"));
        Assert.AreEqual(customCiphertext, fixture.Raw("CUSTOM_VENDOR_TOKEN"));
    }

    [TestMethod]
    public void CompleteAwsPairInfersStaticSourceAndEncryptsBothValues()
    {
        const string legacyJson = """
            {
              "appSettings": {
                "AWSAccessKey": "access-value",
                "AWSSecretKey": "secret-value"
              }
            }
            """;
        using var fixture = new MigrationFixture();
        fixture.Seed("AWS_CREDENTIAL_SOURCE", Settings.INPUT_SELECT, Settings.BASIS_INHERIT);
        fixture.Seed("AWSAccessKey", Settings.INPUT_CREDENTIAL, Settings.BASIS_INHERIT);
        fixture.Seed("AWSSecretKey", Settings.INPUT_CREDENTIAL, Settings.BASIS_INHERIT);

        Assert.AreEqual(3, FwSettingsMigration.migrate(fixture.Fw, legacyJson));

        Assert.AreEqual("static", fixture.Raw("AWS_CREDENTIAL_SOURCE"));
        Assert.StartsWith(FwSettingsProtection.PREFIX, fixture.Raw("AWSAccessKey"));
        Assert.StartsWith(FwSettingsProtection.PREFIX, fixture.Raw("AWSSecretKey"));
        Assert.AreEqual("access-value", fixture.Model.readSecret("AWSAccessKey"));
        Assert.AreEqual("secret-value", fixture.Model.readSecret("AWSSecretKey"));
    }

    [TestMethod]
    public void EmptyDatabaseTestEmailMaterializesLegacyFallback()
    {
        const string legacyJson = """
            {"appSettings":{"test_email":"delivery@example.test"}}
            """;
        using var fixture = new MigrationFixture();
        fixture.Seed("test_email", Settings.INPUT_TEXT, Settings.BASIS_VALUE, "");

        Assert.AreEqual(1, FwSettingsMigration.migrate(fixture.Fw, legacyJson));
        Assert.AreEqual("delivery@example.test", fixture.Raw("test_email"));
    }

    private sealed class MigrationFixture : IDisposable
    {
        private readonly string path;
        private readonly DB db;
        private readonly ServiceProvider services;
        private readonly FwTestScope scope;

        public FW Fw => scope.Fw;
        public Settings Model { get; }

        public MigrationFixture()
        {
            path = Path.Combine(Path.GetTempPath(), "osafw-settings-migration-" + Guid.NewGuid().ToString("N") + ".sqlite");
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Pooling = false,
                ForeignKeys = true,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();
            db = new DB(DB.h(
                "type", DB.DBTYPE_SQLITE,
                "connection_string", connectionString,
                "timezone", "UTC"), "main");
            db.exec("""
                CREATE TABLE settings (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  icat TEXT NOT NULL DEFAULT '',
                  icode TEXT NOT NULL DEFAULT '',
                  ivalue TEXT NOT NULL DEFAULT '',
                  iname TEXT NOT NULL DEFAULT '',
                  idesc TEXT,
                  input INTEGER NOT NULL DEFAULT 0,
                  allowed_values TEXT,
                  is_user_edit INTEGER DEFAULT 0,
                  access_level INTEGER NOT NULL DEFAULT 100,
                  mask INTEGER NOT NULL DEFAULT 0,
                  basis INTEGER NOT NULL DEFAULT 0,
                  add_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                  add_users_id INTEGER DEFAULT 0,
                  upd_time DATETIME,
                  upd_users_id INTEGER DEFAULT 0
                );
                CREATE UNIQUE INDEX UX_settings_icode ON settings (icode);
                """);
            services = new ServiceCollection()
                .AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider())
                .BuildServiceProvider();
            var context = TestHelpers.CreateHttpContext("settings-migration-tests");
            context.RequestServices = services;
            scope = new FwTestScope(
                name => name == "main" ? db : throw new InvalidOperationException("Unknown DB name."),
                new Dictionary<string, string?>
                {
                    ["appSettings:ROOT_DOMAIN"] = "https://example.test",
                    ["appSettings:DATA_PROTECTION_APPLICATION_NAME"] = "settings-migration-tests"
                },
                context);
            Model = Fw.model<Settings>();
            Model.is_log_changes = false;
        }

        public void Seed(string code, int input, int basis, string value = "")
        {
            db.insert("settings", DB.h(
                "icode", code,
                "ivalue", value,
                "iname", code,
                "input", input,
                "allowed_values", code == "AWS_CREDENTIAL_SOURCE" ? "sdk|SDK static|Static" : "",
                "is_user_edit", 1,
                "access_level", Users.ACL_SITEADMIN,
                "mask", input == Settings.INPUT_CREDENTIAL ? Settings.MASK_HIDDEN : Settings.MASK_NONE,
                "basis", basis));
        }

        public string Raw(string code)
        {
            return db.value("settings", DB.h("icode", code), "ivalue").toStr();
        }

        public void Dispose()
        {
            scope.Dispose();
            services.Dispose();

            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                File.Delete(path + suffix);
        }
    }
}
#endif
