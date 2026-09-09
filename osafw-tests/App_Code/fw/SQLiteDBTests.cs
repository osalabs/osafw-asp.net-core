#if isSQLite
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace osafw.Tests;

[TestClass]
public class SQLiteDBTests
{
    private string dbPath = "";
    private string connstr = "";
    private DB db = null!;

    [TestInitialize]
    public void Startup()
    {
        dbPath = Path.Combine(Path.GetTempPath(), "osafw-" + Guid.NewGuid().ToString("N") + ".sqlite");
        connstr = "Data Source=" + dbPath + ";Mode=ReadWriteCreate;Foreign Keys=True;Default Timeout=30;Pooling=False;";
        db = new DB(DB.h(
            "connection_string", connstr,
            "type", DB.DBTYPE_SQLITE,
            "timezone", "UTC"), "sqlite-test");
    }

    [TestCleanup]
    public void Cleanup()
    {
        db.disconnect();
        if (File.Exists(dbPath))
            File.Delete(dbPath);
    }

    [TestMethod]
    public void SQLiteSchemaScripts_CreateFreshFrameworkDatabase()
    {
        var sqlRoot = Path.Combine(repoRoot(), "osafw-app", "App_Data", "sql", "sqlite");
        foreach (var script in new[] { "fwdatabase.sql", "lookups.sql", "views.sql", "roles.sql", "demo.sql" })
            db.execMultipleSQL(File.ReadAllText(Path.Combine(sqlRoot, script)));

        var tables = db.tables();

        CollectionAssert.Contains(tables, "users");
        CollectionAssert.Contains(tables, "fwkeys");
        CollectionAssert.Contains(tables, "fwsessions");
        CollectionAssert.Contains(tables, "activity_logs");
        CollectionAssert.Contains(tables, "roles");
        CollectionAssert.Contains(tables, "demos");
        Assert.AreEqual("Website Admin", db.value("users", DB.h("id", 1), "iname").toStr());
        var testEmail = db.row("settings", DB.h("icode", Settings.ICODE_TEST_EMAIL));
        Assert.AreEqual("", testEmail["ivalue"]);
        Assert.AreEqual(Settings.INPUT_TEXT, testEmail["input"].toInt());
        Assert.AreEqual(1, testEmail["is_user_edit"].toInt());
        StringAssert.Contains(testEmail["idesc"], "current_user");

        var userSchema = db.tableSchemaFull("users");
        Assert.IsTrue(userSchema.ContainsKey("iname"));
        Assert.AreEqual("varchar", ((FwDict)userSchema["iname"]!)["fw_type"]);
        Assert.AreEqual(1, ((FwDict)userSchema["iname"]!)["is_computed"].toInt());
        Assert.AreEqual(0, ((FwDict)userSchema["fname"]!)["is_computed"].toInt());

        var demoSchema = db.tableSchemaFull("demos");
        Assert.AreEqual(0, ((FwDict)demoSchema["icode"]!)["is_computed"].toInt());
        Assert.AreEqual(1, ((FwDict)demoSchema["display_name"]!)["is_computed"].toInt());
        Assert.AreEqual("DEMO-1 — Name1", db.value("demos", DB.h("id", 1), "display_name").toStr());

        db.update("demos", DB.h("iname", "Renamed Demo"), DB.h("id", 1));

        Assert.AreEqual("DEMO-1 — Renamed Demo", db.value("demos", DB.h("id", 1), "display_name").toStr());
    }

    [TestMethod]
    public void TestEmailUpdate_IsIdempotentAndPreservesExistingValue()
    {
        db.exec(@"CREATE TABLE settings (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  icat TEXT NOT NULL DEFAULT '',
  icode TEXT NOT NULL DEFAULT '',
  ivalue TEXT NOT NULL DEFAULT '',
  iname TEXT NOT NULL DEFAULT '',
  idesc TEXT,
  input INTEGER NOT NULL DEFAULT 0,
  allowed_values TEXT,
  is_user_edit INTEGER DEFAULT 0
)");
        db.exec("CREATE UNIQUE INDEX UX_settings_icode ON settings (icode)");
        db.exec("INSERT INTO settings (icode, ivalue) VALUES ('test_email', 'existing@example.test')");
        string update = File.ReadAllText(Path.Combine(
            repoRoot(), "osafw-app", "App_Data", "sql", "sqlite", "updates", "upd2026-09-08-test-email.sql"));

        db.execMultipleSQL(update);
        db.execMultipleSQL(update);

        Assert.AreEqual(1, db.valuep("SELECT COUNT(*) FROM settings WHERE icode='test_email'").toInt());
        Assert.AreEqual("existing@example.test", db.value("settings", DB.h("icode", "test_email"), "ivalue").toStr());
    }

    [TestMethod]
    public void SQLiteSchemaMetadata_DetectsStoredAndVirtualGeneratedColumns()
    {
        db.exec(@"CREATE TABLE generated_values (
  base_value INTEGER NOT NULL,
  virtual_value INTEGER GENERATED ALWAYS AS (base_value * 2) VIRTUAL,
  stored_value INTEGER GENERATED ALWAYS AS (base_value * 3) STORED
)");

        var schema = db.tableSchemaFull("generated_values");

        Assert.AreEqual(0, ((FwDict)schema["base_value"]!)["is_computed"].toInt());
        Assert.AreEqual(1, ((FwDict)schema["virtual_value"]!)["is_computed"].toInt());
        Assert.AreEqual(1, ((FwDict)schema["stored_value"]!)["is_computed"].toInt());
        Assert.AreEqual("", ((FwDict)schema["base_value"]!)["comments"]);
    }

    [TestMethod]
    public void SQLite_CRUDIdentityParametersSchemaAndForeignKeys_Work()
    {
        db.exec(@"CREATE TABLE parents (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  iname TEXT NOT NULL DEFAULT '',
  add_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
)");
        db.exec(@"CREATE TABLE children (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  parents_id INTEGER NOT NULL REFERENCES parents(id),
  iname TEXT NOT NULL DEFAULT ''
)");

        var id = db.insert("parents", DB.h("iname", "alpha", "add_time", DateTime.UtcNow));

        Assert.AreEqual(1, id);
        Assert.AreEqual("alpha", db.value("parents", DB.h("id", id), "iname").toStr());

        db.update("parents", DB.h("iname", "beta"), DB.h("id", id));
        Assert.AreEqual("beta", db.value("parents", DB.h("id", id), "iname").toStr());

        var rows = db.arrayp("SELECT id FROM parents WHERE id IN (@ids)", DB.h("@ids", new[] { id, 999 }));
        Assert.HasCount(1, rows);

        var schema = db.tableSchemaFull("parents");
        var idSchema = (FwDict)schema["id"]!;
        var addTimeSchema = (FwDict)schema["add_time"]!;
        Assert.AreEqual("int", idSchema["fw_type"]);
        Assert.AreEqual(1, idSchema["is_identity"].toInt());
        Assert.AreEqual("datetime", addTimeSchema["fw_type"]);

        db.exec(@"CREATE TABLE composite_keys (
  users_id INTEGER NOT NULL,
  roles_id INTEGER NOT NULL,
  PRIMARY KEY (users_id, roles_id)
)");
        var compositeSchema = db.tableSchemaFull("composite_keys");
        Assert.AreEqual(0, ((FwDict)compositeSchema["users_id"]!)["is_identity"].toInt());
        Assert.AreEqual(0, ((FwDict)compositeSchema["roles_id"]!)["is_identity"].toInt());

        var fks = db.listForeignKeys("children");
        Assert.HasCount(1, fks);
        Assert.AreEqual("parents", fks[0]["pk_table"]);

        try
        {
            db.exec("INSERT INTO children (parents_id, iname) VALUES (999, 'orphan')");
            Assert.Fail("SQLite foreign key enforcement should reject orphan rows.");
        }
        catch (Exception)
        {
            // Expected provider exception.
        }
    }

    [TestMethod]
    public void SQLite_NumberExpression_ExcludesNonNumericText()
    {
        db.exec("CREATE TABLE search_values (v TEXT)");
        db.exec("INSERT INTO search_values (v) VALUES ('abc'), ('0.5'), ('2'), ('123-456'), ('1.2.3'), ('++1'), ('-0.5')");

        var rows = db.arrayp("SELECT v FROM search_values WHERE " + db.sqlNumberExpr("v") + " < 1 ORDER BY v");

        Assert.HasCount(2, rows);
        Assert.AreEqual("-0.5", rows[0]["v"]);
        Assert.AreEqual("0.5", rows[1]["v"]);
    }

    [TestMethod]
    public void SQLite_FwKeysRepository_StoresAndReadsKeys()
    {
        db.exec(@"CREATE TABLE fwkeys (
  iname TEXT NOT NULL PRIMARY KEY,
  itype INTEGER NOT NULL DEFAULT 0,
  XmlValue TEXT NOT NULL,
  add_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  upd_time DATETIME
)");

        var repo = new FwKeysXmlRepository(db);
        var element = new XElement("key", new XAttribute("id", "sqlite-key"), new XElement("value", "abc"));

        repo.StoreElement(element, "sqlite-key");
        var elements = repo.GetAllElements();

        Assert.HasCount(1, elements);
        Assert.AreEqual("sqlite-key", elements.Single().Attribute("id")?.Value);
    }

    [TestMethod]
    public void SQLite_FwSessionCache_RegistersAndPersistsSessions()
    {
        using var provider = new ServiceCollection()
            .AddFwSessionCache(connstr, DB.DBTYPE_SQLITE)
            .BuildServiceProvider();
        var cache = provider.GetRequiredService<IDistributedCache>();
        var payload = new byte[] { 1, 2, 3 };

        cache.Set("session-1", payload, new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5)
        });

        CollectionAssert.AreEqual(payload, cache.Get("session-1"));
        cache.Refresh("session-1");
        CollectionAssert.AreEqual(payload, cache.Get("session-1"));

        cache.Remove("session-1");
        Assert.IsNull(cache.Get("session-1"));
    }

    [TestMethod]
    public void SQLite_FwSessionCache_ReturnsNullAndCleansExpiredSessions()
    {
        using var provider = new ServiceCollection()
            .AddFwSessionCache(connstr, DB.DBTYPE_SQLITE)
            .BuildServiceProvider();
        var cache = provider.GetRequiredService<IDistributedCache>();
        var payload = new byte[] { 4, 5, 6 };

        cache.Set("expired-absolute", payload, new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(-5)
        });

        Assert.IsNull(cache.Get("expired-absolute"));
        Assert.AreEqual(0, db.value("fwsessions", DB.h("Id", "expired-absolute"), "count(*)").toInt());

        db.exec(
            "INSERT INTO fwsessions (Id, Value, ExpiresAtTime, SlidingExpirationInSeconds) VALUES (@id, X'09', @expires, @sliding)",
            DB.h(
                "id", "expired-sliding",
                "expires", DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"),
                "sliding", 300));

        Assert.IsNull(cache.Get("expired-sliding"));
        Assert.AreEqual(0, db.value("fwsessions", DB.h("Id", "expired-sliding"), "count(*)").toInt());
    }

    private static string repoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "osafw-app", "App_Data", "sql")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Cannot locate repository root from " + Directory.GetCurrentDirectory());
    }
}
#endif
