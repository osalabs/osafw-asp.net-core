using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class DevCliTests
{
    [TestMethod]
    public void IsCommand_MatchesDevelopmentCommandsCaseInsensitively()
    {
        Assert.IsTrue(DevCli.isCommand(["ScAfFoLd", "--help"]));
        Assert.IsTrue(DevCli.isCommand(["DaTaBaSe-InIt"]));
        Assert.IsFalse(DevCli.isCommand(["run"]));
        Assert.IsFalse(DevCli.isCommand([]));
    }

    [TestMethod]
    public void TryParse_CrudAcceptsOverridesAndNormalizesDynamicType()
    {
        var parsed = DevCli.tryParse(
            ["scaffold", "crud", "sales_orders", "--model", "SalesOrders", "--url", "/Admin/SalesOrders", "--title", "Sales Orders", "--type", "dynamic", "--force"],
            out var options,
            out var error);

        Assert.IsTrue(parsed, error);
        Assert.AreEqual("crud", options.Operation);
        Assert.AreEqual("sales_orders", options.Subject);
        Assert.AreEqual("SalesOrders", options.ModelName);
        Assert.AreEqual("/Admin/SalesOrders", options.ControllerUrl);
        Assert.AreEqual("Sales Orders", options.ControllerTitle);
        Assert.AreEqual(string.Empty, options.ControllerType);
        Assert.IsTrue(options.Force);
    }

    [TestMethod]
    public void TryParse_AcceptsReservedApiControllerType()
    {
        var parsed = DevCli.tryParse(
            ["scaffold", "controller", "SalesOrders", "--type", "api"],
            out var options,
            out var error);

        Assert.IsTrue(parsed, error);
        Assert.AreEqual("api", options.ControllerType);
    }

    [TestMethod]
    public void TryParse_ModelUsesNameOption()
    {
        var parsed = DevCli.tryParse(
            ["scaffold", "model", "sales_orders", "--name", "SalesOrders"],
            out var options,
            out var error);

        Assert.IsTrue(parsed, error);
        Assert.AreEqual("SalesOrders", options.ModelName);
    }

    [TestMethod]
    public void TryParse_RejectsUnsafeControllerUrl()
    {
        var parsed = DevCli.tryParse(
            ["scaffold", "controller", "SalesOrders", "--url", "/Admin/../../Program"],
            out _,
            out var error);

        Assert.IsFalse(parsed);
        StringAssert.Contains(error, "exactly two");
    }

    [TestMethod]
    public void TryParse_RejectsOptionsFromAnotherOperation()
    {
        var parsed = DevCli.tryParse(
            ["scaffold", "report", "sales-summary", "--force"],
            out _,
            out var error);

        Assert.IsFalse(parsed);
        StringAssert.Contains(error, "not supported");
    }

    [TestMethod]
    public void TryParse_RejectsInvalidModelName()
    {
        var parsed = DevCli.tryParse(
            ["scaffold", "crud", "sales_orders", "--model", "sales-orders"],
            out _,
            out var error);

        Assert.IsFalse(parsed);
        StringAssert.Contains(error, "PascalCase");
    }

    [TestMethod]
    [DataRow("FW")]
    [DataRow("DevCli")]
    public void EnsureModelTargetAvailable_RejectsExistingTypeInGeneratedNamespace(string typeName)
    {
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            "osafw-missing-" + Guid.NewGuid().ToString("N"),
            typeName + ".cs");

        var ex = Assert.ThrowsExactly<UserException>(
            () => DevCli.ensureModelTargetAvailable(typeName, missingPath, force: false));

        StringAssert.Contains(ex.Message, $"type named '{typeName}'");
    }

    [TestMethod]
    public void EnsureModelTargetAvailable_AllowsSameTypeNameFromAnotherNamespace()
    {
        Assert.AreEqual("osafw.Parsers", typeof(global::osafw.Parsers.HtmlParser).Namespace);
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            "osafw-missing-" + Guid.NewGuid().ToString("N"),
            "HtmlParser.cs");

        DevCli.ensureModelTargetAvailable("HtmlParser", missingPath, force: false);
    }

    [TestMethod]
    public void EnsureControllerTargetsAvailable_LookupRejectsKnownCompiledRoute()
    {
        var existingSourcePath = typeof(AdminUsersController).Assembly.Location;
        var plan = new DevCli.ControllerPlan(
            Url: "/Admin/Users",
            Title: "Users",
            Type: "lookup",
            ClassName: "AdminUsers",
            SourcePath: existingSourcePath,
            TemplatePath: Path.GetDirectoryName(existingSourcePath)!,
            IsLookup: true);

        var ex = Assert.ThrowsExactly<UserException>(
            () => DevCli.ensureControllerTargetsAvailable(plan, force: true));

        StringAssert.Contains(ex.Message, "AdminUsersController");
    }

    [TestMethod]
    public void Run_HelpDoesNotRequireApplicationConfiguration()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = DevCli.run(
            ["scaffold", "--help"],
            new ConfigurationBuilder().Build(),
            output,
            error);

        Assert.AreEqual(DevCli.EXIT_SUCCESS, exitCode);
        StringAssert.Contains(output.ToString(), "scaffold crud <table>");
        StringAssert.Contains(output.ToString(), "dynamic|vue|lookup|api");
        StringAssert.Contains(output.ToString(), "reserved for future support");
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public void Run_ReservedApiFailsBeforeApplicationConfiguration()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = DevCli.run(
            ["scaffold", "controller", "SalesOrders", "--type", "api"],
            new ConfigurationBuilder().Build(),
            output,
            error);

        Assert.AreEqual(DevCli.EXIT_ERROR, exitCode);
        Assert.AreEqual(string.Empty, output.ToString());
        StringAssert.Contains(error.ToString(), "not yet available");
    }

    [TestMethod]
    public void Run_RejectsNonDevelopmentConfigurationBeforeFrameworkInitialization()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["appSettings:IS_DEV"] = "false",
            })
            .Build();

        var exitCode = DevCli.run(
            ["scaffold", "report", "no-write-probe"],
            configuration,
            output,
            error);

        Assert.AreEqual(DevCli.EXIT_ENVIRONMENT, exitCode);
        Assert.AreEqual(string.Empty, output.ToString());
        StringAssert.Contains(error.ToString(), "IS_DEV=true");
    }

    [TestMethod]
    [DataRow("--force")]
    [DataRow("--help")]
    [DataRow("unexpected")]
    public void Run_DatabaseInitRejectsAdditionalArgumentsBeforeConfiguration(string argument)
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = DevCli.run(
            ["database-init", argument],
            new ConfigurationBuilder().Build(),
            output,
            error);

        Assert.AreEqual(DevCli.EXIT_USAGE, exitCode);
        Assert.AreEqual(string.Empty, output.ToString());
        StringAssert.Contains(error.ToString(), "without additional arguments");
    }

    [TestMethod]
    public void Run_DatabaseInitRejectsNonDevelopmentBeforeOpeningDatabase()
    {
        using var configurationScope = FwConfig.beginScope();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["appSettings:IS_DEV"] = "false",
                ["appSettings:db:main:type"] = "unusable-provider",
                ["appSettings:db:main:connection_string"] = "must-not-be-opened",
            })
            .Build();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = DevCli.run(["database-init"], configuration, output, error);

        Assert.AreEqual(DevCli.EXIT_ENVIRONMENT, exitCode);
        Assert.AreEqual(string.Empty, output.ToString());
        StringAssert.Contains(error.ToString(), "IS_DEV=true");
    }

    [TestMethod]
    public void Run_DatabaseInitFailureDoesNotExposeConfigurationOrExceptionDetails()
    {
        using var configurationScope = FwConfig.beginScope();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["appSettings:IS_DEV"] = "true",
                ["appSettings:log_level"] = "0",
                ["appSettings:log"] = "",
                ["appSettings:db:main:type"] = "private-provider-sentinel",
                ["appSettings:db:main:connection_string"] = "private-connection-sentinel",
            })
            .Build();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = DevCli.run(["database-init"], configuration, output, error);

        Assert.AreEqual(DevCli.EXIT_ERROR, exitCode);
        Assert.AreEqual(string.Empty, output.ToString());
        Assert.AreEqual(
            "Error: database initialization failed. Verify that the configured database is empty and reachable." + Environment.NewLine,
            error.ToString());
    }

#if isSQLite
    [TestMethod]
    public void Run_DatabaseInitBootstrapsEmptySqliteDatabaseAndRejectsSecondRun()
    {
        using var configurationScope = FwConfig.beginScope();
        var dbPath = Path.Combine(Path.GetTempPath(), "osafw-database-init-" + Guid.NewGuid().ToString("N") + ".sqlite");
        var configuration = sqliteConfiguration(dbPath);

        try
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = DevCli.run(["database-init"], configuration, output, error);

            Assert.AreEqual(DevCli.EXIT_SUCCESS, exitCode, error.ToString());
            Assert.AreEqual(string.Empty, error.ToString());
            var password = output.ToString().TrimEnd('\r', '\n');
            Assert.IsFalse(string.IsNullOrWhiteSpace(password));
            Assert.AreEqual(password + Environment.NewLine, output.ToString());

            using var fw = new FW(null, configuration);
            var db = fw.db;
            var tables = db.tables();
            CollectionAssert.Contains(tables, "users");
            CollectionAssert.Contains(tables, "settings");
            CollectionAssert.Contains(tables, "fwkeys");
            CollectionAssert.Contains(tables, "fwsessions");

            var passwordHash = db.value("users", DB.h("id", 1), "pwd").toStr();
            Assert.AreNotEqual(password, passwordHash);
            Assert.IsTrue(fw.model<Users>().checkPwd(password, passwordHash));
            var settingsCount = db.valuep("SELECT COUNT(*) FROM settings").toInt();
            Assert.IsGreaterThan(0, settingsCount);

            output.GetStringBuilder().Clear();
            error.GetStringBuilder().Clear();
            exitCode = DevCli.run(["database-init"], configuration, output, error);

            Assert.AreEqual(DevCli.EXIT_ERROR, exitCode);
            Assert.AreEqual(string.Empty, output.ToString());
            StringAssert.Contains(error.ToString(), "empty and reachable");
            Assert.AreEqual(passwordHash, db.value("users", DB.h("id", 1), "pwd").toStr());
            Assert.AreEqual(settingsCount, db.valuep("SELECT COUNT(*) FROM settings").toInt());
        }
        finally
        {
            deleteSqliteDatabase(dbPath);
        }
    }

    [TestMethod]
    public void Run_DatabaseInitPreservesUnrelatedTableInNonEmptySqliteDatabase()
    {
        using var configurationScope = FwConfig.beginScope();
        var dbPath = Path.Combine(Path.GetTempPath(), "osafw-database-init-" + Guid.NewGuid().ToString("N") + ".sqlite");
        var configuration = sqliteConfiguration(dbPath);

        try
        {
            using var fw = new FW(null, configuration);
            fw.db.exec("CREATE TABLE preserve_me (id INTEGER PRIMARY KEY)");
            fw.db.exec("INSERT INTO preserve_me (id) VALUES (17)");
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = DevCli.run(["database-init"], configuration, output, error);

            Assert.AreEqual(DevCli.EXIT_ERROR, exitCode);
            Assert.AreEqual(string.Empty, output.ToString());
            StringAssert.Contains(error.ToString(), "empty and reachable");
            Assert.AreEqual(17, fw.db.valuep("SELECT id FROM preserve_me").toInt());
            CollectionAssert.DoesNotContain(fw.db.tables(), "users");
        }
        finally
        {
            deleteSqliteDatabase(dbPath);
        }
    }

    private static IConfiguration sqliteConfiguration(string dbPath)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["appSettings:IS_DEV"] = "true",
                ["appSettings:site_root"] = Path.Combine(repoRoot(), "osafw-app"),
                ["appSettings:log_level"] = "0",
                ["appSettings:log"] = "",
                ["appSettings:db:main:type"] = DB.DBTYPE_SQLITE,
                ["appSettings:db:main:timezone"] = "UTC",
                ["appSettings:db:main:connection_string"] = "Data Source=" + dbPath + ";Mode=ReadWriteCreate;Foreign Keys=True;Default Timeout=30;Pooling=False;",
            })
            .Build();
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

    private static void deleteSqliteDatabase(string dbPath)
    {
        foreach (var path in new[] { dbPath, dbPath + "-shm", dbPath + "-wal" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
#endif
}
