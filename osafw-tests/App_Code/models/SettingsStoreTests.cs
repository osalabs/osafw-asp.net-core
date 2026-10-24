#if isSQLite
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class SettingsStoreTests
{
    [TestMethod]
    public void CredentialWriteStoresCiphertextAndOnlyTrustedReadDecrypts()
    {
        using var fixture = new SettingsFixture(new EphemeralDataProtectionProvider());
        var secretId = fixture.Seed("OPENAI_API_KEY", Settings.INPUT_CREDENTIAL);
        var ordinaryId = fixture.Seed("ordinary", Settings.INPUT_TEXT, "visible");

        fixture.Model.update(secretId, DB.h("ivalue", "secret-value", "basis", Settings.BASIS_VALUE));

        var stored = fixture.Db.value("settings", DB.h("id", secretId), "ivalue").toStr();
        Assert.StartsWith(FwSettingsProtection.PREFIX, stored);
        Assert.DoesNotContain("secret-value", stored);
        Assert.AreEqual(stored, fixture.Model.one(secretId)["ivalue"]);
        Assert.AreEqual("secret-value", fixture.Model.readSecret("OPENAI_API_KEY"));
        Assert.AreEqual("visible", fixture.Model.getValue("ordinary"));
        Assert.AreEqual("visible", fixture.Model.one(ordinaryId)["ivalue"]);
    }

    [TestMethod]
    public void ExportImportAcrossDifferentProtectorsReencryptsCredential()
    {
        string transfer;
        string sourceCiphertext;

        using (var source = new SettingsFixture(new EphemeralDataProtectionProvider()))
        {
            var id = source.Seed("OPENAI_API_KEY", Settings.INPUT_CREDENTIAL);
            source.Model.update(id, DB.h("ivalue", "portable-secret", "basis", Settings.BASIS_VALUE));
            sourceCiphertext = source.Db.value("settings", DB.h("id", id), "ivalue").toStr();
            transfer = source.Model.exportJson();
        }

        using var target = new SettingsFixture(new EphemeralDataProtectionProvider());
        var targetId = target.Seed("OPENAI_API_KEY", Settings.INPUT_CREDENTIAL);

        Assert.AreEqual(1, target.Model.importJson(transfer));

        var targetCiphertext = target.Db.value("settings", DB.h("id", targetId), "ivalue").toStr();
        Assert.StartsWith(FwSettingsProtection.PREFIX, targetCiphertext);
        Assert.AreNotEqual(sourceCiphertext, targetCiphertext);
        Assert.AreEqual("portable-secret", target.Model.readSecret("OPENAI_API_KEY"));
    }

    [TestMethod]
    public void InheritAndExplicitEmptyRemainDistinct()
    {
        using var fixture = new SettingsFixture(new EphemeralDataProtectionProvider());
        var id = fixture.Seed("support_email", Settings.INPUT_TEXT, "stored@example.test");

        fixture.Model.writeBatch([
            new Settings.ValueChange { icode = "support_email", basis = Settings.BASIS_INHERIT, ivalue = null }
        ]);

        Assert.AreEqual("support@website.com", fixture.Model.getValue("support_email"));
        Assert.AreEqual(Settings.BASIS_INHERIT, fixture.Db.value("settings", DB.h("id", id), "basis").toInt());

        fixture.Model.writeBatch([
            new Settings.ValueChange { icode = "support_email", basis = Settings.BASIS_VALUE, ivalue = "" }
        ]);

        Assert.AreEqual("", fixture.Model.getValue("support_email"));
        Assert.AreEqual(Settings.BASIS_VALUE, fixture.Db.value("settings", DB.h("id", id), "basis").toInt());
        Assert.AreEqual("", fixture.Db.value("settings", DB.h("id", id), "ivalue").toStr());
    }

    [TestMethod]
    public void InvalidUnknownAndDuplicateImportsLeaveValuesUnchanged()
    {
        using var fixture = new SettingsFixture(new EphemeralDataProtectionProvider());
        fixture.Seed("first", Settings.INPUT_TEXT, "first-before");
        fixture.Seed("second", Settings.INPUT_TEXT, "second-before");

        Assert.ThrowsExactly<UserException>(() => fixture.Model.importJson("not-json"));
        assertValuesUnchanged(fixture);

        const string unknown = """
            {"version":1,"settings":[
              {"icode":"first","basis":1,"ivalue":"first-after"},
              {"icode":"missing","basis":1,"ivalue":"missing-after"}
            ]}
            """;
        Assert.ThrowsExactly<UserException>(() => fixture.Model.importJson(unknown));
        assertValuesUnchanged(fixture);

        const string duplicate = """
            {"version":1,"settings":[
              {"icode":"first","basis":1,"ivalue":"first-after"},
              {"icode":"first","basis":1,"ivalue":"duplicate-after"}
            ]}
            """;
        Assert.ThrowsExactly<UserException>(() => fixture.Model.importJson(duplicate));
        assertValuesUnchanged(fixture);
    }

    [TestMethod]
    public void SnapshotLoadsOnceUntilCommittedWriteInvalidatesIt()
    {
        using var fixture = new SettingsFixture(new EphemeralDataProtectionProvider());
        var id = fixture.Seed("ordinary", Settings.INPUT_TEXT, "before");

        Assert.AreEqual("before", fixture.Model.getValue("ordinary"));
        Assert.AreEqual("before", fixture.Model.getValue("ordinary"));
        Assert.AreEqual(1, fixture.Db.SettingsArrayCalls);

        fixture.Model.update(id, DB.h("ivalue", "after", "basis", Settings.BASIS_VALUE));

        Assert.AreEqual("after", fixture.Model.getValue("ordinary"));
        Assert.AreEqual("after", fixture.Model.getValue("ordinary"));
        Assert.AreEqual(2, fixture.Db.SettingsArrayCalls);
    }

    [TestMethod]
    public void MidBatchDatabaseFailureRollsBackValuesAndInvalidatesSnapshot()
    {
        using var fixture = new SettingsFixture(new EphemeralDataProtectionProvider());
        fixture.Seed("first", Settings.INPUT_TEXT, "first-before");
        var secondId = fixture.Seed("second", Settings.INPUT_TEXT, "second-before");
        Assert.AreEqual("first-before", fixture.Model.read("first"));
        fixture.Db.FailUpdateId = secondId;

        Assert.ThrowsExactly<UserException>(() => fixture.Model.writeBatch([
            new Settings.ValueChange { icode = "first", basis = Settings.BASIS_VALUE, ivalue = "first-after" },
            new Settings.ValueChange { icode = "second", basis = Settings.BASIS_VALUE, ivalue = "second-after" }
        ]));

        assertValuesUnchanged(fixture);
        Assert.AreEqual("first-before", fixture.Model.read("first"));
    }
    private static void assertValuesUnchanged(SettingsFixture fixture)
    {
        Assert.AreEqual("first-before", fixture.Db.value("settings", DB.h("icode", "first"), "ivalue").toStr());
        Assert.AreEqual("second-before", fixture.Db.value("settings", DB.h("icode", "second"), "ivalue").toStr());
    }

    private sealed class SettingsFixture : IDisposable
    {
        private readonly string path;
        private readonly ServiceProvider services;
        private readonly FwTestScope scope;

        public CountingSqliteDb Db { get; }
        public Settings Model { get; }

        public SettingsFixture(IDataProtectionProvider provider)
        {
            path = Path.Combine(Path.GetTempPath(), "osafw-settings-" + Guid.NewGuid().ToString("N") + ".sqlite");
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Pooling = false,
                ForeignKeys = true,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();
            Db = new CountingSqliteDb(DB.h(
                "type", DB.DBTYPE_SQLITE,
                "connection_string", connectionString,
                "timezone", "UTC"));
            Db.exec("""
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
                .AddSingleton<IDataProtectionProvider>(provider)
                .BuildServiceProvider();
            var context = TestHelpers.CreateHttpContext("settings-tests");
            context.RequestServices = services;
            scope = new FwTestScope(
                name => name == "main" ? Db : throw new InvalidOperationException("Unknown DB name."),
                new Dictionary<string, string?>
                {
                    ["appSettings:ROOT_DOMAIN"] = "https://example.test",
                    ["appSettings:DATA_PROTECTION_APPLICATION_NAME"] = "settings-tests"
                },
                context);
            Model = scope.Fw.model<Settings>();
            Model.is_log_changes = false;
        }

        public int Seed(string code, int input, string value = "")
        {
            return Db.insert("settings", DB.h(
                "icode", code,
                "ivalue", value,
                "iname", code,
                "input", input,
                "is_user_edit", 1,
                "access_level", Users.ACL_SITEADMIN,
                "mask", input == Settings.INPUT_CREDENTIAL ? Settings.MASK_HIDDEN : Settings.MASK_NONE,
                "basis", Settings.BASIS_VALUE));
        }

        public void Dispose()
        {
            scope.Dispose();
            services.Dispose();

            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                File.Delete(path + suffix);
        }
    }

    private sealed class CountingSqliteDb : DB
    {
        public int SettingsArrayCalls;
        public int FailUpdateId;

        public CountingSqliteDb(FwDict configuration) : base(configuration, "main")
        {
        }

        public override int update(string table, FwDict fields, FwDict where)
        {
            if (table == "settings" && FailUpdateId > 0 && where["id"].toInt() == FailUpdateId)
                throw new UserException("Simulated database write failure.");
            return base.update(table, fields, where);
        }
        public override DBList array(
            string table,
            FwDict where,
            string order_by = "",
            ICollection? aselect_fields = null,
            int offset = 0,
            int limit = -1)
        {
            if (table == "settings")
                SettingsArrayCalls++;

            return base.array(table, where, order_by, aselect_fields, offset, limit);
        }
    }
}
#endif
