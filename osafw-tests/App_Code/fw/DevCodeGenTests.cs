using Microsoft.VisualStudio.TestTools.UnitTesting;
using osafw;
using System;
using System.Collections;
using System.Reflection;

namespace osafw.Tests;

[TestClass]
public class DevCodeGenTests
{
    [TestMethod]
    public void UpdateControllerConfig_RemovesCopiedDemoTabFields()
    {
        var fw = TestHelpers.CreateFw();
        var config = new FwDict
        {
            ["form_tabs"] = new FwList
            {
                new FwDict { ["tab"] = "", ["label"] = "Main" },
                new FwDict { ["tab"] = "details", ["label"] = "Details" },
                new FwDict { ["tab"] = "relations", ["label"] = "Relations" },
                new FwDict { ["tab"] = "meta", ["label"] = "Attachments" }
            },
            ["show_fields_details"] = new FwList { new FwDict { ["field"] = "demo_details" } },
            ["show_fields_relations"] = new FwList { new FwDict { ["field"] = "demo_relations" } },
            ["showform_fields_details"] = new FwList { new FwDict { ["field"] = "demo_details" } },
            ["showform_fields_meta"] = new FwList { new FwDict { ["field"] = "demo_meta" } }
        };
        var entity = new FwDict
        {
            ["model_name"] = "GeneratedWidgets",
            ["table"] = "generated_widgets",
            ["is_fw"] = true,
            ["controller"] = new FwDict
            {
                ["url"] = "/Admin/GeneratedWidgets",
                ["title"] = "Generated Widgets",
                ["type"] = "dynamic"
            },
            ["fields"] = new FwList
            {
                Field("id", "int", 0, isNullable: false, isIdentity: true, defaultValue: null),
                Field("iname", "varchar", 80, isNullable: false),
                Field("status", "int", 0, isNullable: false, defaultValue: "0")
            },
            ["foreign_keys"] = new FwList()
        };

        InvokeUpdateControllerConfig(fw, entity, config);

        var formTabs = (IList)config["form_tabs"]!;
        Assert.AreEqual(1, formTabs.Count);
        var mainTab = (FwDict)formTabs[0]!;
        Assert.AreEqual("", mainTab["tab"]);
        Assert.AreEqual("Main", mainTab["label"]);
        Assert.IsTrue(config.ContainsKey("show_fields"));
        Assert.IsTrue(config.ContainsKey("showform_fields"));
        Assert.IsFalse(config.ContainsKey("show_fields_details"));
        Assert.IsFalse(config.ContainsKey("show_fields_relations"));
        Assert.IsFalse(config.ContainsKey("showform_fields_details"));
        Assert.IsFalse(config.ContainsKey("showform_fields_meta"));
    }

    private static void InvokeUpdateControllerConfig(FW fw, FwDict entity, FwDict config)
    {
        var codeGenType = typeof(FW).Assembly.GetType("osafw.DevCodeGen")
            ?? throw new InvalidOperationException("DevCodeGen type was not found.");
        var ctor = codeGenType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(FW), typeof(DB) },
            modifiers: null)
            ?? throw new InvalidOperationException("DevCodeGen constructor was not found.");
        var codeGen = ctor.Invoke(new object?[] { fw, fw.db });
        var method = codeGenType.GetMethod("updateControllerConfig", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("DevCodeGen.updateControllerConfig was not found.");

        method.Invoke(codeGen, new object?[] { entity, config, new FwList() });
    }

    private static FwDict Field(
        string name,
        string fwType,
        int maxlen,
        bool isNullable,
        bool isIdentity = false,
        object? defaultValue = null)
    {
        return new FwDict
        {
            ["name"] = name,
            ["iname"] = Utils.name2human(name),
            ["fw_name"] = name,
            ["fw_type"] = fwType,
            ["fw_subtype"] = fwType,
            ["default"] = defaultValue,
            ["maxlen"] = maxlen,
            ["is_nullable"] = isNullable,
            ["is_identity"] = isIdentity
        };
    }
}
