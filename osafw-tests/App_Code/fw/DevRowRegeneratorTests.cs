using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace osafw.Tests;

[TestClass]
public class DevRowRegeneratorTests
{
    private sealed class SchemaDb : DB
    {
        internal SchemaDb() : base("row-regenerator-" + Guid.NewGuid().ToString("N"), DBTYPE_MYSQL) { }

        public int SchemaReads { get; private set; }

        public override DBList arrayp(string sql, FwDict? @params = null)
        {
            SchemaReads++;
            Assert.IsTrue(@params?["@table_name"].toStr() is "demo_dicts" or "demos");
            return new DBList
            {
                mysqlField("id", "int", "int", isNullable: false, comments: "Database identifier"),
                mysqlField("display name", "varchar", "varchar(200)", isNullable: false, comments: "Display <name> & value"),
                mysqlField("total", "bigint", "bigint unsigned", isNullable: true),
                mysqlField("danger<script>", "varchar", "varchar(200)", isNullable: true),
            };
        }
    }

    private sealed class CachingSchemaDb : DB
    {
        internal CachingSchemaDb() : base("row-regenerator-" + Guid.NewGuid().ToString("N"), DBTYPE_SQLSRV) { }

        internal string ColumnType { get; set; } = "int";
        internal int SchemaReads { get; private set; }

        public override DBList arrayp(string sql, FwDict? @params = null)
        {
            SchemaReads++;
            return new DBList { sqlServerField("id", ColumnType) };
        }
    }

    private sealed class RacingSchemaDb : DB
    {
        internal RacingSchemaDb() : base("row-regenerator-race-" + Guid.NewGuid().ToString("N"), DBTYPE_SQLSRV) { }

        internal ManualResetEventSlim OldReady { get; } = new(false);
        internal ManualResetEventSlim ReleaseOld { get; } = new(false);
        internal ManualResetEventSlim OldPublished { get; } = new(false);
        internal int SchemaReads => schemaReads;

        private int schemaReads;

        public override DBList arrayp(string sql, FwDict? @params = null)
        {
            var read = Interlocked.Increment(ref schemaReads);
            if (read == 1)
            {
                OldReady.Set();
                if (!ReleaseOld.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Timed out waiting to release the first schema read.");
                return new DBList { sqlServerField("id", "int") };
            }

            if (read == 2)
            {
                ReleaseOld.Set();
                if (!OldPublished.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Timed out waiting for the first schema read to publish.");
                return new DBList { sqlServerField("id", "bigint") };
            }

            throw new InvalidOperationException("Unexpected schema read.");
        }
    }

    [TestMethod]
    public void PreviewAndApply_ReplaceOnlyRecognizableRowAndPreserveCustomSource()
    {
        using var fixture = new Fixture(standardSource());
        var regenerator = new DevRowRegenerator(fixture.Fw);

        var previews = regenerator.preview(["DemoDicts"]);

        Assert.HasCount(1, previews);
        Assert.IsTrue(previews[0]["is_changed"].toBool());
        StringAssert.Contains(previews[0]["diff"].toStr(), "+        public int id { get; set; }");
        StringAssert.Contains(previews[0]["diff"].toStr(), "+        [DBName(\"display name\")]");
        StringAssert.Contains(previews[0]["diff"].toStr(), "+        public ulong? total { get; set; }");
        Assert.AreEqual(1, fixture.SchemaReads);
        Assert.AreEqual(standardSource(), File.ReadAllText(fixture.SourcePath));

        var changed = regenerator.apply(
            ["DemoDicts"],
            new Dictionary<string, string> { ["DemoDicts"] = previews[0]["preview_hash"].toStr() });

        Assert.AreEqual(1, changed);
        var updated = File.ReadAllText(fixture.SourcePath);
        StringAssert.Contains(updated, "public class DemoDicts : CustomModelBase");
        StringAssert.Contains(updated, "public string KeepCustomCode() => \"public class Row { fake }\";");
        StringAssert.Contains(updated, "/// Display &lt;name&gt; &amp; value");
        StringAssert.Contains(updated, "[DBName(\"display name\")]");
        StringAssert.Contains(updated, "public ulong? total { get; set; }");
        Assert.IsFalse(updated.Contains("legacy_id", StringComparison.Ordinal));
        StringAssert.Contains(updated, "public string? ");
        var unchanged = regenerator.preview(["DemoDicts"]);
        Assert.IsFalse(unchanged[0]["is_changed"].toBool(), "Generated nullable text must be accepted on the next preview.");
    }

    [TestMethod]
    public void Apply_RejectsStalePreviewBeforeWriting()
    {
        using var fixture = new Fixture(standardSource());
        var regenerator = new DevRowRegenerator(fixture.Fw);
        var preview = regenerator.preview(["DemoDicts"])[0];
        var externallyEdited = standardSource() + "// concurrent custom edit\r\n";
        File.WriteAllText(fixture.SourcePath, externallyEdited, new UTF8Encoding(false));

        var ex = Assert.ThrowsExactly<UserException>(() => regenerator.apply(
            ["DemoDicts"],
            new Dictionary<string, string> { ["DemoDicts"] = preview["preview_hash"].toStr() }));

        StringAssert.Contains(ex.Message, "stale");
        Assert.AreEqual(externallyEdited, File.ReadAllText(fixture.SourcePath));
    }

    [TestMethod]
    public void PreviewAndApply_RefreshWarmSchemaMetadata()
    {
        var database = new CachingSchemaDb();
        using var fixture = new Fixture(standardSource(), database: database);

        _ = database.loadTableSchemaFull("demo_dicts");
        Assert.AreEqual(1, database.SchemaReads);
        database.ColumnType = "bigint";

        var regenerator = new DevRowRegenerator(fixture.Fw);
        var preview = regenerator.preview(["DemoDicts"])[0];

        Assert.AreEqual(2, database.SchemaReads);
        StringAssert.Contains(preview["diff"].toStr(), "public long id { get; set; }");

        database.ColumnType = "varchar";
        var ex = Assert.ThrowsExactly<UserException>(() => regenerator.apply(
            ["DemoDicts"],
            new Dictionary<string, string> { ["DemoDicts"] = preview["preview_hash"].toStr() }));

        StringAssert.Contains(ex.Message, "stale");
        Assert.AreEqual(3, database.SchemaReads);
        Assert.AreEqual(standardSource(), File.ReadAllText(fixture.SourcePath));
    }

    [TestMethod]
    public void LoadTableSchemaFull_PreservesWarmCacheBehavior()
    {
        var database = new CachingSchemaDb();

        var first = database.loadTableSchemaFull("demo_dicts");
        database.ColumnType = "bigint";
        var second = database.loadTableSchemaFull("demo_dicts");

        Assert.AreSame(first, second);
        Assert.AreEqual(1, database.SchemaReads);
        Assert.AreEqual("int", second[0]["fw_subtype"]);
    }

    [TestMethod]
    public void ReloadTableSchemaFull_ReturnsFreshReadWhenOlderCachedReadPublishesFirst()
    {
        var database = new RacingSchemaDb();
        var oldRead = Task.Run(() =>
        {
            try
            {
                return database.loadTableSchemaFull("demo_dicts");
            }
            finally
            {
                database.OldPublished.Set();
            }
        });

        try
        {
            Assert.IsTrue(database.OldReady.Wait(TimeSpan.FromSeconds(10)), "The first schema read did not start.");

            var fresh = database.reloadTableSchemaFull("demo_dicts");
            var cached = oldRead.GetAwaiter().GetResult();

            Assert.AreEqual(2, database.SchemaReads);
            Assert.AreEqual("bigint", fresh[0]["fw_subtype"]);
            Assert.AreEqual("int", cached[0]["fw_subtype"]);
        }
        finally
        {
            database.ReleaseOld.Set();
            database.OldPublished.Set();
        }
    }

    [TestMethod]
    public void Preview_RejectsCustomizedRowAndUnknownSelectionWithoutWriting()
    {
        var customized = standardSource().Replace(
            "        public int legacy_id { get; set; }",
            "        public int legacy_id { get; set; }\r\n        public int Calculate() => 1;",
            StringComparison.Ordinal);
        using var fixture = new Fixture(customized);
        var regenerator = new DevRowRegenerator(fixture.Fw);

        var customizedEx = Assert.ThrowsExactly<UserException>(() => regenerator.preview(["DemoDicts"]));
        StringAssert.Contains(customizedEx.Message, "customized");
        var unknownEx = Assert.ThrowsExactly<UserException>(() => regenerator.preview(["../DemoDicts"]));
        StringAssert.Contains(unknownEx.Message, "not an available model source file");
        Assert.AreEqual(customized, File.ReadAllText(fixture.SourcePath));
    }

    [TestMethod]
    public void Preview_RejectsPrimaryConstructorsCustomInitializersAndCustomTypes()
    {
        var customizedSources = new[]
        {
            standardSource().Replace("public class Row", "public class Row(int seed)", StringComparison.Ordinal),
            standardSource().Replace(
                "public int legacy_id { get; set; }",
                "public string legacy_id { get; set; } = Guid.NewGuid().ToString();",
                StringComparison.Ordinal),
            standardSource().Replace("public int legacy_id", "public CustomIdentifier legacy_id", StringComparison.Ordinal),
        };

        foreach (var source in customizedSources)
        {
            using var fixture = new Fixture(source);
            var ex = Assert.ThrowsExactly<UserException>(() => new DevRowRegenerator(fixture.Fw).preview(["DemoDicts"]));
            StringAssert.Contains(ex.Message, "customized");
            Assert.AreEqual(0, fixture.SchemaReads);
            Assert.AreEqual(source, File.ReadAllText(fixture.SourcePath));
        }
    }

    [TestMethod]
    public void Apply_AcquiresEverySelectedSourceBeforeWriting()
    {
        using var fixture = new Fixture(standardSource());
        var secondSource = standardSource().Replace("DemoDicts", "Demos", StringComparison.Ordinal);
        var secondPath = fixture.addModelSource("Demos", secondSource);
        var regenerator = new DevRowRegenerator(fixture.Fw);
        var previews = regenerator.preview(["DemoDicts", "Demos"]);
        var hashes = previews.ToDictionary(x => x["model_name"].toStr(), x => x["preview_hash"].toStr(), StringComparer.Ordinal);
        using var externalReader = new FileStream(secondPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var ex = Assert.ThrowsExactly<UserException>(() => regenerator.apply(
            ["DemoDicts", "Demos"],
            hashes));

        StringAssert.Contains(ex.Message, "Demos could not be locked");
        Assert.AreEqual(standardSource(), File.ReadAllText(fixture.SourcePath));
        Assert.AreEqual(secondSource, File.ReadAllText(secondPath));
    }

    [TestMethod]
    public void Controller_GatesSourceToolByDevelopmentPostTokenAndSiteAdminAccess()
    {
        Assert.AreEqual(Users.ACL_SITEADMIN, DevManageController.access_level);

        using (var lowerAccess = new Fixture(standardSource()))
        {
            lowerAccess.Fw.Session("access_level", Users.ACL_ADMIN.ToString());
            Assert.ThrowsExactly<AuthException>(() => lowerAccess.Fw.controller("DevManage"));
            Assert.AreEqual(0, lowerAccess.SchemaReads);
        }

        using (var nonDev = new Fixture(standardSource(), isDev: false))
        {
            var controller = createController(nonDev.Fw);
            Assert.ThrowsExactly<AuthException>(() => controller.ModelRowsAction());
            Assert.AreEqual(0, nonDev.SchemaReads);
        }

        using (var getFixture = new Fixture(standardSource()))
        {
            getFixture.Fw.route.method = "GET";
            setXssTokens(getFixture.Fw);
            var controller = createController(getFixture.Fw);
            Assert.ThrowsExactly<AuthException>(() => controller.ApplyModelRowsAction());
            Assert.AreEqual(standardSource(), File.ReadAllText(getFixture.SourcePath));
        }

        using (var missingToken = new Fixture(standardSource()))
        {
            missingToken.Fw.route.method = "POST";
            missingToken.Fw.Session("XSS", "token");
            missingToken.Fw.FORM["item"] = new FwDict { ["DemoDicts"] = 1 };
            var controller = createController(missingToken.Fw);
            Assert.ThrowsExactly<AuthException>(() => controller.ModelRowsAction());
            Assert.ThrowsExactly<AuthException>(() => controller.ApplyModelRowsAction());
            Assert.AreEqual(0, missingToken.SchemaReads);
            Assert.AreEqual(standardSource(), File.ReadAllText(missingToken.SourcePath));
        }
    }

    [TestMethod]
    public void Controller_AppliesReviewedPreviewThroughBrowserEntryPath()
    {
        using var fixture = new Fixture(standardSource());
        fixture.Fw.route.method = "POST";
        setXssTokens(fixture.Fw);
        fixture.Fw.FORM["item"] = new FwDict { ["DemoDicts"] = 1 };
        var controller = createController(fixture.Fw);
        var previewPage = controller.ModelRowsAction();
        var changes = (FwList)previewPage["changes"]!;
        Assert.HasCount(1, changes);
        var renderedPreview = fixture.Fw.parsePage("/dev/manage/modelrows", "main.html", previewPage);
        StringAssert.Contains(renderedPreview, "Apply reviewed Row changes");
        StringAssert.Contains(renderedPreview, "danger&lt;script&gt;");
        Assert.IsFalse(renderedPreview.Contains("danger<script>", StringComparison.Ordinal));

        fixture.Fw.FORM["hash"] = new FwDict
        {
            ["DemoDicts"] = changes[0]["preview_hash"],
        };

        Assert.ThrowsExactly<RedirectException>(() => controller.ApplyModelRowsAction());

        StringAssert.Contains(File.ReadAllText(fixture.SourcePath), "public ulong? total { get; set; }");
        Assert.AreEqual("/Dev/Manage/(ModelRows)", fixture.Fw.response.Headers.Location.ToString());
    }

    private static DevManageController createController(FW fw)
    {
        var controller = new DevManageController();
        controller.init(fw);
        return controller;
    }

    private static void setXssTokens(FW fw)
    {
        fw.Session("XSS", "token");
        fw.FORM["XSS"] = "token";
    }

    private static DBRow mysqlField(string name, string type, string columnType, bool isNullable, string comments = "")
    {
        return new DBRow
        {
            ["name"] = name,
            ["type"] = type,
            ["column_type"] = columnType,
            ["is_nullable"] = isNullable ? "1" : "0",
            ["default"] = "",
            ["maxlen"] = type == "varchar" ? "200" : "0",
            ["numeric_precision"] = "0",
            ["numeric_scale"] = "0",
            ["charset"] = "",
            ["collation"] = "",
            ["pos"] = "1",
            ["is_identity"] = "0",
            ["is_computed"] = "0",
            ["comments"] = comments,
        };
    }

    private static DBRow sqlServerField(string name, string type)
    {
        return new DBRow
        {
            ["name"] = name,
            ["type"] = type,
            ["is_nullable"] = "0",
            ["default"] = "",
            ["maxlen"] = "0",
            ["numeric_precision"] = "0",
            ["numeric_scale"] = "0",
            ["charset"] = "",
            ["collation"] = "",
            ["pos"] = "1",
            ["is_identity"] = "0",
            ["is_computed"] = "0",
            ["comments"] = "",
        };
    }

    private static string standardSource()
    {
        return "namespace osafw;\r\n"
            + "\r\n"
            + "public class DemoDicts : CustomModelBase\r\n"
            + "{\r\n"
            + "    public class Row\r\n"
            + "    {\r\n"
            + "        public int legacy_id { get; set; }\r\n"
            + "        public string legacy_name { get; set; } = \"\";\r\n"
            + "        public string current_name { get; set; } = string.Empty;\r\n"
            + "        public DateTimeOffset? event_time { get; set; }\r\n"
            + "    }\r\n"
            + "\r\n"
            + "    public string KeepCustomCode() => \"public class Row { fake }\";\r\n"
            + "}\r\n";
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string tempRoot;
        private readonly FwTestScope scope;

        internal Fixture(string source, bool isDev = true, DB? database = null)
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "osafw-row-regenerator-" + Guid.NewGuid().ToString("N"));
            var modelsDir = Path.Combine(tempRoot, "App_Code", "models");
            Directory.CreateDirectory(modelsDir);
            SourcePath = Path.Combine(modelsDir, "DemoDicts.cs");
            File.WriteAllText(SourcePath, source, new UTF8Encoding(false));

            Db = database ?? new SchemaDb();
            scope = new FwTestScope(
                _ => Db,
                new Dictionary<string, string?>
                {
                    ["appSettings:site_root"] = tempRoot,
                    ["appSettings:template"] = findRepoPath("osafw-app", "App_Data", "template"),
                    ["appSettings:IS_DEV"] = isDev ? "true" : "false",
                },
                TestHelpers.CreateHttpContext());
            Fw = scope.Fw;
        }

        internal DB Db { get; }
        internal FW Fw { get; }
        internal string SourcePath { get; }
        internal int SchemaReads => Db switch
        {
            SchemaDb schemaDb => schemaDb.SchemaReads,
            CachingSchemaDb cachingDb => cachingDb.SchemaReads,
            _ => 0,
        };

        internal string addModelSource(string modelName, string source)
        {
            var sourcePath = Path.Combine(tempRoot, "App_Code", "models", modelName + ".cs");
            File.WriteAllText(sourcePath, source, new UTF8Encoding(false));
            return sourcePath;
        }

        public void Dispose()
        {
            scope.Dispose();
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }

        private static string findRepoPath(params string[] relativeParts)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
                if (Directory.Exists(candidate))
                    return candidate;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(Path.Combine(relativeParts));
        }
    }
}
