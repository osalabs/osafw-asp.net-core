using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace osafw;

[TestClass]
public class UsersSqliteTests
{
    [TestMethod]
    public void UsersCrudSqlite()
    {
        var repoRoot = FindRepoRoot();
        var dbPath = Path.Combine(Path.GetTempPath(), $"osafw-test-{Guid.NewGuid():N}.sqlite");

        try
        {
            var configuration = BuildTestConfiguration(repoRoot, dbPath);
            using var fw = FW.initOffline(configuration);
            InitializeSqliteSchema(fw, repoRoot);

            var users = fw.model<Users>();
            users.is_log_changes = false;

            var userId = users.add(DB.h(
                "email", "sqlite@example.com",
                "pwd", "password123",
                "fname", "Sql",
                "lname", "Lite"));

            Assert.IsGreaterThan(userId, 0, "Expected a new user id from insert.");

            var inserted = users.one(userId);
            Assert.AreEqual("sqlite@example.com", inserted["email"]);
            Assert.AreEqual("Sql", inserted["fname"]);

            users.update(userId, DB.h("fname", "Updated"));
            var updated = users.one(userId);
            Assert.AreEqual("Updated", updated["fname"]);

            users.delete(userId, true);
            var deleted = users.one(userId);
            Assert.AreEqual(string.Empty, deleted["email"]);
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    private static IConfiguration BuildTestConfiguration(string repoRoot, string dbPath)
    {
        return new ConfigurationBuilder()
            .SetBasePath(repoRoot)
            .AddJsonFile(Path.Combine(repoRoot, "osafw-app", "appsettings.json"), optional: false, reloadOnChange: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["appSettings:db:main:connection_string"] = $"Data Source={dbPath}",
                ["appSettings:db:main:type"] = DB.DBTYPE_SQLITE,
                ["appSettings:db:main:timezone"] = DateUtils.TZ_UTC,
                ["appSettings:is_test"] = "true"
            })
            .Build();
    }

    private static void InitializeSqliteSchema(FW fw, string repoRoot)
    {
        var sqlRoot = Path.Combine(repoRoot, "osafw-app", "App_Data", "sql", "sqlite");
        var schemaSql = File.ReadAllText(Path.Combine(sqlRoot, "fwdatabase.sql"));
        fw.db.execMultipleSQL(schemaSql);

        var lookupsSql = File.ReadAllText(Path.Combine(sqlRoot, "lookups.sql"));
        fw.db.execMultipleSQL(lookupsSql);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "osafw-asp.net-core.sln")))
            dir = dir.Parent;

        if (dir == null)
            throw new DirectoryNotFoundException("Unable to locate repository root for osafw-asp.net-core.");

        return dir.FullName;
    }
}
