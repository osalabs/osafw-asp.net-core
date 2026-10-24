using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
#if isSQLite
using System.IO;
#endif

namespace osafw.Tests;

[TestClass]
public class FwDatabaseInitTests
{
    [TestMethod]
    public void IsCommandMatchesOnlyDatabaseInit()
    {
        Assert.IsTrue(FwDatabaseInit.isCommand(["DaTaBaSe-InIt"]));
        Assert.IsFalse(FwDatabaseInit.isCommand(["settings-migrate"]));
        Assert.IsFalse(FwDatabaseInit.isCommand([]));
    }

    [TestMethod]
    public void InitializeRejectsNonDevelopmentBeforeInspectingDatabase()
    {
        using var scope = createScope(isDev: false);
        var listed = false;
        var initialized = false;

        Assert.ThrowsExactly<FwDatabaseInit.EnvironmentException>(() => FwDatabaseInit.initialize(
            scope.Fw,
            () => { listed = true; return []; },
            () => { initialized = true; return DB.h("pwd", "unexpected"); }));

        Assert.IsFalse(listed);
        Assert.IsFalse(initialized);
    }

    [TestMethod]
    public void InitializeRejectsAnyExistingApplicationTable()
    {
        using var scope = createScope(isDev: true);
        var initialized = false;

        Assert.ThrowsExactly<UserException>(() => FwDatabaseInit.initialize(
            scope.Fw,
            () => ["settings"],
            () => { initialized = true; return DB.h("pwd", "unexpected"); }));

        Assert.IsFalse(initialized);
    }

    [TestMethod]
    public void InitializeEmptyDevelopmentDatabaseReturnsBootstrapPassword()
    {
        using var scope = createScope(isDev: true);
        var calls = 0;

        var password = FwDatabaseInit.initialize(
            scope.Fw,
            () => [],
            () => { calls++; return DB.h("pwd", "generated-password"); });

        Assert.AreEqual("generated-password", password);
        Assert.AreEqual(1, calls);
    }

#if isSQLite
    [TestMethod]
    public void InitializeBootstrapsEmptySqliteDatabaseAndRejectsSecondRun()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "osafw-database-init-" + Guid.NewGuid().ToString("N") + ".sqlite");
        var db = new DB(DB.h(
            "connection_string", "Data Source=" + dbPath + ";Mode=ReadWriteCreate;Foreign Keys=True;Default Timeout=30;Pooling=False;",
            "type", DB.DBTYPE_SQLITE,
            "timezone", "UTC"), "database-init-test");

        try
        {
            using var scope = new FwTestScope(
                _ => db,
                new Dictionary<string, string?>
                {
                    ["appSettings:IS_DEV"] = "true",
                    ["appSettings:site_root"] = Path.Combine(repoRoot(), "osafw-app")
                });

            var password = FwDatabaseInit.initialize(scope.Fw);
            var tables = db.tables();

            Assert.IsFalse(string.IsNullOrWhiteSpace(password));
            CollectionAssert.Contains(tables, "users");
            CollectionAssert.Contains(tables, "settings");
            CollectionAssert.Contains(tables, "fwkeys");
            CollectionAssert.Contains(tables, "fwsessions");

            var passwordHash = db.value("users", DB.h("id", 1), "pwd").toStr();
            Assert.AreNotEqual(password, passwordHash);
            Assert.IsTrue(scope.Fw.model<Users>().checkPwd(password, passwordHash));
            Assert.IsGreaterThan(0, db.valuep("SELECT COUNT(*) FROM settings").toInt());

            var settingsCount = db.valuep("SELECT COUNT(*) FROM settings").toInt();
            Assert.ThrowsExactly<UserException>(() => FwDatabaseInit.initialize(scope.Fw));
            Assert.AreEqual(passwordHash, db.value("users", DB.h("id", 1), "pwd").toStr());
            Assert.AreEqual(settingsCount, db.valuep("SELECT COUNT(*) FROM settings").toInt());
        }
        finally
        {
            db.disconnect();
            deleteIfExists(dbPath);
            deleteIfExists(dbPath + "-shm");
            deleteIfExists(dbPath + "-wal");
        }
    }
#endif

    private static FwTestScope createScope(bool isDev)
    {
        return new FwTestScope(
            _ => new RejectingDb(),
            new Dictionary<string, string?>
            {
                ["appSettings:IS_DEV"] = isDev ? "true" : "false"
            });
    }

#if isSQLite
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

    private static void deleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
#endif
}
