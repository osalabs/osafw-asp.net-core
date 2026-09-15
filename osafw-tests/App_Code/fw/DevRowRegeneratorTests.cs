using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace osafw.Tests;

[TestClass]
public class DevRowRegeneratorTests
{
#if !isRowRegeneration
    [TestMethod]
    public void DefaultBuildHasNoRegenerationActionOrRoslynReference()
    {
        Assert.IsNull(typeof(DevManageController).GetMethod("RegenerateModelRowsAction"));
        Assert.IsFalse(typeof(FW).Assembly.GetReferencedAssemblies().Any(reference => reference.Name!.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)));
    }
#else
    private sealed class SchemaDb : DB
    {
        internal int Reads;
        internal string IdType = "int";
        internal SchemaDb() : base("regeneration-" + Guid.NewGuid().ToString("N"), DBTYPE_SQLSRV) { }
        public override DBRow row(string table, FwDict where, string order_by = "") => new() { ["is_readonly"] = "0" };
        public override DBList arrayp(string sql, FwDict? parameters = null)
        {
            Reads++;
            Assert.IsTrue(parameters?["@table_name"].toStr() is "demo_dicts" or "demos");
            return
            [
                new DBRow { ["name"] = "id", ["type"] = IdType, ["is_nullable"] = "0" },
                new DBRow { ["name"] = "display name", ["type"] = "nvarchar", ["is_nullable"] = "1", ["comments"] = "Display <name>" },
            ];
        }
    }

    [TestMethod]
    public void PostRegeneratesAllRowsIncludingCustomMembersAndPreservesOuterSource()
    {
        using var fixture = new Fixture();
        var second = fixture.addSource("Demos.cs", source("Demos"));

        var result = fixture.run();

        CollectionAssert.AreEquivalent(new[] { "DemoDicts", "Demos" }, ((StrList)result["updated"]!).ToArray());
        Assert.IsTrue(result["success"].toBool());
        Assert.IsTrue(result["_json"].toBool());
        foreach (var path in new[] { fixture.SourcePath, second })
        {
            var output = File.ReadAllText(path);
            StringAssert.Contains(output, ": CustomModelBase");
            StringAssert.Contains(output, "public string KeepCustomCode() => \"} public class Row {\";");
            StringAssert.Contains(output, "public int id { get; set; }");
            StringAssert.Contains(output, "public string? display_name { get; set; }");
            StringAssert.Contains(output, "[DBName(\"display name\")]");
            StringAssert.Contains(output, "/// Display &lt;name&gt;");
            Assert.IsFalse(output.Contains("CustomRowMethod", StringComparison.Ordinal));
            Assert.IsFalse(output.Contains("legacy_id", StringComparison.Ordinal));
            Assert.IsFalse(output.Replace("\r\n", "", StringComparison.Ordinal).Contains('\n'));
            Assert.IsFalse(File.ReadAllBytes(path).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        }
        var repeat = fixture.run();
        Assert.HasCount(0, (StrList)repeat["updated"]!);
        Assert.HasCount(2, (StrList)repeat["unchanged"]!);
    }

    [TestMethod]
    public void RegenerationReadsCurrentSchemaWithoutChangingThePublicWarmCache()
    {
        using var fixture = new Fixture();
        var cached = fixture.Db.loadTableSchemaFull("demo_dicts");
        fixture.Db.IdType = "bigint";

        fixture.run();

        Assert.AreEqual(2, fixture.Db.Reads);
        StringAssert.Contains(File.ReadAllText(fixture.SourcePath), "public long id { get; set; }");
        Assert.AreSame(cached, fixture.Db.loadTableSchemaFull("demo_dicts"));
        Assert.AreEqual("int", cached[0]["fw_subtype"]);
        Assert.AreEqual(2, fixture.Db.Reads);
    }

    [TestMethod]
    [DataRow(false, "POST", true, Users.ACL_SITEADMIN)]
    [DataRow(true, "GET", true, Users.ACL_SITEADMIN)]
    [DataRow(true, "POST", false, Users.ACL_SITEADMIN)]
    [DataRow(true, "POST", true, Users.ACL_ADMIN)]
    public void EntryRejectsNonDevelopmentGetMissingTokenAndLowerAccess(bool isDev, string method, bool token, int access)
    {
        using var fixture = new Fixture(isDev);
        fixture.Fw.route.method = method;
        fixture.Fw.request.Method = method;
        fixture.Fw.Session("access_level", access.ToString());
        if (!token) fixture.Fw.FORM.Remove("XSS");

        Assert.ThrowsExactly<AuthException>(() => fixture.run());

        Assert.AreEqual(0, fixture.Db.Reads);
        Assert.AreEqual(source("DemoDicts"), File.ReadAllText(fixture.SourcePath));
    }

    [TestMethod]
    public void AmbiguousOrMissingRowsAreSkippedWithoutReadingSchema()
    {
        using var fixture = new Fixture();
        fixture.addSource("nested/DemoDicts.cs", source("DemoDicts"));
        fixture.addSource("Demos.cs", "namespace osafw; public class Demos { }");

        var result = fixture.run();

        Assert.HasCount(2, (FwList)result["skipped"]!);
        Assert.HasCount(0, (StrList)result["updated"]!);
        Assert.AreEqual(0, fixture.Db.Reads);
        Assert.AreEqual(source("DemoDicts"), File.ReadAllText(fixture.SourcePath));
    }

    [TestMethod]
    public void LockedFileIsReportedAndOtherModelsStillRegenerate()
    {
        using var fixture = new Fixture();
        var second = fixture.addSource("Demos.cs", source("Demos"));
        using (var held = new FileStream(fixture.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var result = fixture.run();
            CollectionAssert.AreEqual(new[] { "DemoDicts" }, ((StrList)result["failed"]!).ToArray());
            CollectionAssert.AreEqual(new[] { "Demos" }, ((StrList)result["updated"]!).ToArray());
            Assert.IsFalse(result["success"].toBool());
        }
        Assert.AreEqual(source("DemoDicts"), File.ReadAllText(fixture.SourcePath));
        StringAssert.Contains(File.ReadAllText(second), "public int id { get; set; }");
    }

    [TestMethod]
    public void InvalidSyntaxIsSkippedWithoutWriting()
    {
        using var fixture = new Fixture();
        const string invalid = "namespace osafw; public class DemoDicts { public class Row {";
        File.WriteAllText(fixture.SourcePath, invalid);

        var result = fixture.run();

        Assert.HasCount(1, (FwList)result["skipped"]!);
        Assert.AreEqual(0, fixture.Db.Reads);
        Assert.AreEqual(invalid, File.ReadAllText(fixture.SourcePath));
    }

    private static string source(string name) =>
        $"namespace osafw;\r\npublic class {name} : CustomModelBase\r\n{{\r\n"
        + "    public class Row\r\n    {\r\n        public int legacy_id { get; set; }\r\n"
        + "        public string CustomRowMethod() => \"}\";\r\n    }\r\n"
        + "    public string KeepCustomCode() => \"} public class Row {\";\r\n}\r\n";

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "row-regeneration-" + Guid.NewGuid().ToString("N"));
        private readonly FwTestScope scope;
        internal SchemaDb Db { get; } = new();
        internal FW Fw => scope.Fw;
        internal string SourcePath { get; }
        internal Fixture(bool isDev = true)
        {
            SourcePath = addSource("DemoDicts.cs", source("DemoDicts"));
            scope = new FwTestScope(_ => Db, new Dictionary<string, string?>
            {
                ["appSettings:site_root"] = root,
                ["appSettings:IS_DEV"] = isDev ? "true" : "false",
                ["appSettings:route_prefixes:/Dev"] = "true",
            }, TestHelpers.CreateHttpContext());
            Fw.Session("user_id", "1");
            Fw.Session("access_level", Users.ACL_SITEADMIN.ToString());
            Fw.route.method = "POST";
            Fw.request.Method = "POST";
            Fw.Session("XSS", "token");
            Fw.FORM["XSS"] = "token";
        }
        internal FwDict run()
        {
            var route = Fw.getRoute("/Dev/Manage/(RegenerateModelRows)");
            Assert.AreEqual("DevManage", route.controller);
            Assert.AreEqual("RegenerateModelRows", route.action);
            var controller = new DevManageController();
            controller.init(Fw);
            return controller.RegenerateModelRowsAction();
        }
        internal string addSource(string relative, string text)
        {
            var path = Path.Combine(root, "App_Code", "models", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            return path;
        }
        public void Dispose()
        {
            scope.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }
#endif
}
