using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace osafw.Tests;

[TestClass]
public class DemoComputedFieldTests
{
    [TestMethod]
    public void DemoClassicController_SavesSourcesButNotComputedDisplayName()
    {
        var controller = new AdminDemosController();
        controller.init(TestHelpers.CreateFw());
        var saveFields = controller.save_fields
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        CollectionAssert.Contains(saveFields, "icode");
        CollectionAssert.Contains(saveFields, "iname");
        CollectionAssert.DoesNotContain(saveFields, "display_name");

        var formPath = Path.Combine(
            repoRoot(),
            "osafw-app",
            "App_Data",
            "template",
            "admin",
            "demos",
            "showform",
            "form_left.html");
        var form = File.ReadAllText(formPath);
        StringAssert.Contains(form, "<~i[display_name]>");
        Assert.IsFalse(form.Contains("name=\"item[display_name]\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DemoDynamicConfigs_KeepDisplayNameReadonlyAndOutOfSaves()
    {
        foreach (var configFolder in new[] { "demosdynamic", "demosvue" })
        {
            var configPath = Path.Combine(
                repoRoot(),
                "osafw-app",
                "App_Data",
                "template",
                "admin",
                configFolder,
                "config.json");
            using var document = JsonDocument.Parse(File.ReadAllText(configPath));
            var config = document.RootElement;
            var saveFields = config.GetProperty("save_fields")
                .EnumerateArray()
                .Select(static field => field.GetString() ?? string.Empty)
                .ToList();

            CollectionAssert.Contains(saveFields, "icode");
            CollectionAssert.Contains(saveFields, "iname");
            CollectionAssert.DoesNotContain(saveFields, "display_name");

            var showField = findField(config.GetProperty("show_fields"), "display_name");
            var showFormField = findField(config.GetProperty("showform_fields"), "display_name");
            Assert.AreEqual("plaintext", showField.GetProperty("type").GetString());
            Assert.AreEqual("plaintext", showFormField.GetProperty("type").GetString());
            Assert.IsFalse(showFormField.TryGetProperty("required", out _));
            Assert.IsFalse(showFormField.TryGetProperty("validate", out _));

            Assert.IsTrue(config.GetProperty("view_list_map").TryGetProperty("display_name", out _));
            CollectionAssert.Contains(splitFields(config.GetProperty("view_list_defaults").GetString()), "display_name");

            if (config.TryGetProperty("edit_list_map", out var editListMap))
            {
                Assert.IsTrue(editListMap.TryGetProperty("display_name", out _));
                CollectionAssert.Contains(splitFields(config.GetProperty("edit_list_defaults").GetString()), "display_name");
            }
        }
    }

    [TestMethod]
    public void DemoSchemas_DefineComputedDisplayNameForSupportedProviders()
    {
        var root = repoRoot();
        var sqlServer = File.ReadAllText(Path.Combine(root, "osafw-app", "App_Data", "sql", "demo.sql"));
        var mySql = File.ReadAllText(Path.Combine(root, "osafw-app", "App_Data", "sql", "mysql", "demo.sql"));
        var sqlite = File.ReadAllText(Path.Combine(root, "osafw-app", "App_Data", "sql", "sqlite", "demo.sql"));
        var sqlServerUpdate = File.ReadAllText(Path.Combine(root, "osafw-app", "App_Data", "sql", "updates", "upd2026-08-20-demo-computed-display-name.sql"));

        StringAssert.Contains(sqlServer, "display_name          AS CAST(CONCAT");
        StringAssert.Contains(sqlServer, "PERSISTED");
        StringAssert.Contains(mySql, "display_name          VARCHAR(128) GENERATED ALWAYS AS");
        StringAssert.Contains(mySql, "STORED");
        StringAssert.Contains(sqlite, "display_name          TEXT GENERATED ALWAYS AS");
        StringAssert.Contains(sqlite, "STORED");
        StringAssert.Contains(sqlServerUpdate, "ADD display_name AS CAST(CONCAT");
        StringAssert.Contains(sqlServerUpdate, "PERSISTED");

        Assert.IsNotNull(typeof(Demos.Row).GetProperty("icode"));
        Assert.IsNotNull(typeof(Demos.Row).GetProperty("display_name"));
    }

    private static JsonElement findField(JsonElement fields, string fieldName)
    {
        foreach (var field in fields.EnumerateArray())
        {
            if (field.TryGetProperty("field", out var value) && value.GetString() == fieldName)
                return field;
        }

        throw new InvalidOperationException($"Field '{fieldName}' was not found.");
    }

    private static List<string> splitFields(string? fields)
    {
        return (fields ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static string repoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "osafw-asp.net-core.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Cannot locate repository root from " + Directory.GetCurrentDirectory());
    }
}
