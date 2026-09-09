using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace osafw.Tests;

[TestClass]
public class DevEntityBuilderTests
{
    [TestMethod]
    public void ConfigJsonConverter_DoesNotDuplicateModelKeys()
    {
        var config = new FwDict
        {
            ["model"] = "GeneratedWidgets",
            ["show_fields"] = new FwList
            {
                new FwDict
                {
                    ["field"] = "roles_link",
                    ["type"] = "multi",
                    ["label"] = "Roles",
                    ["model"] = "UsersRoles"
                }
            }
        };
        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };
        ConfigJsonConverter converter = new();
        converter.setOrderedKeys(converter.ordered_keys_controller);
        options.Converters.Add(converter);

        var json = JsonSerializer.Serialize(config, config.GetType(), options);

        Assert.AreEqual(1, Regex.Matches(json, "\"model\": \"GeneratedWidgets\"").Count);
        Assert.AreEqual(1, Regex.Matches(json, "\"model\": \"UsersRoles\"").Count);
    }

    [TestMethod]
    public void ParseField_DefaultsForeignKeyWithoutTypeToInt()
    {
        var field = InvokeParseField("fieldname2 FK(parent_table.id)", "");

        Assert.AreEqual("fieldname2", field["name"]);
        Assert.AreEqual("int", field["fw_type"]);
        Assert.AreEqual("int", field["fw_subtype"]);
        Assert.AreEqual(10, field["maxlen"]);
        Assert.AreEqual(0, field["default"]);
        var foreignKey = (Dictionary<string, string>)field["foreign_key"]!;
        Assert.AreEqual("fieldname2", foreignKey["column"]);
        Assert.AreEqual("parent_table", foreignKey["pk_table"]);
        Assert.AreEqual("id", foreignKey["pk_column"]);
    }

    [TestMethod]
    public void IsFwTableName_ExcludesCurrentFrameworkPersistenceTables()
    {
        foreach (var table in new[]
        {
            "users_cookies", "fwreports", "kb_articles", "rag_sources", "rag_chunks",
            "assistant_threads", "assistant_messages", "assistant_runs", "assistant_runs_events",
            "assistant_memories", "assistant_feedback", "resources", "permissions", "roles",
            "roles_resources_permissions", "users_roles"
        })
            Assert.IsTrue(DevEntityBuilder.isFwTableName(table), table);

        Assert.IsFalse(DevEntityBuilder.isFwTableName("customer_orders"));
    }

    private static Dictionary<string, object?> InvokeParseField(string line, string comment)
    {
        return DevEntityBuilder.ParseField(line, comment)
            ?? throw new InvalidOperationException("DevEntityBuilder.ParseField returned null.");
    }
}
