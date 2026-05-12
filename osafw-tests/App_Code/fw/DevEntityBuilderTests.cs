using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace osafw.Tests;

[TestClass]
public class DevEntityBuilderTests
{
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

    private static Dictionary<string, object?> InvokeParseField(string line, string comment)
    {
        var entityBuilderType = typeof(FW).Assembly.GetType("osafw.DevEntityBuilder")
            ?? throw new InvalidOperationException("DevEntityBuilder type was not found.");
        var method = entityBuilderType.GetMethod("ParseField", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DevEntityBuilder.ParseField was not found.");

        return (Dictionary<string, object?>)(method.Invoke(null, new object?[] { line, comment })
            ?? throw new InvalidOperationException("DevEntityBuilder.ParseField returned null."));
    }
}
