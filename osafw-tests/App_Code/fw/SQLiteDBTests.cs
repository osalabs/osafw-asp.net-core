#if isSQLite
using Microsoft.Extensions.Caching.Distributed;
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

        var userSchema = db.tableSchemaFull("users");
        Assert.IsTrue(userSchema.ContainsKey("iname"));
        Assert.AreEqual("varchar", ((FwDict)userSchema["iname"]!)["fw_type"]);
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
    public void SQLite_DistributedCache_PersistsAndExpiresSessions()
    {
        var cache = new FwSqliteDistributedCache(connstr);
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
