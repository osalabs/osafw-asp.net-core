using Microsoft.VisualStudio.TestTools.UnitTesting;
using osafw;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace osafw.Tests;

[TestClass]
public class DevCodeGenTests
{
    private static readonly Type CodeGenType = typeof(FW).Assembly.GetType("osafw.DevCodeGen")
        ?? throw new InvalidOperationException("DevCodeGen type was not found.");

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

    [TestMethod]
    public void BuildLookupInsertSql_ChecksForExistingIcode()
    {
        var fw = TestHelpers.CreateFw();
        var item = new FwDict
        {
            ["igroup"] = "User",
            ["icode"] = "AdminGeneratedLookups",
            ["url"] = "/Admin/GeneratedLookups",
            ["iname"] = "Generated Lookups",
            ["model"] = "GeneratedLookups",
            ["access_level"] = Users.ACL_MANAGER
        };

        var sql = InvokeBuildLookupInsertSql(fw, item);

        StringAssert.Contains(sql, "IF NOT EXISTS");
        StringAssert.Contains(sql, "SELECT 1 FROM fwcontrollers WHERE icode='AdminGeneratedLookups'");
        StringAssert.Contains(sql, "INSERT INTO fwcontrollers");
        StringAssert.Contains(sql, "'/Admin/GeneratedLookups'");
    }

    [TestMethod]
    public void AddToFormColumns_PlacesWideTextInPrimaryColumn()
    {
        var showFieldsTabs = new Dictionary<string, List<List<FwDict>>>();
        var showFormFieldsTabs = new Dictionary<string, List<List<FwDict>>>();
        var allFields = TenGeneratedFields();
        var fld = Field("idesc", "varchar", 0, isNullable: true);
        var sf = new FwDict { ["field"] = "idesc", ["type"] = "markdown" };
        var sff = new FwDict { ["field"] = "idesc", ["type"] = "textarea" };

        var col = InvokeAddToFormColumns(fld, sf, sff, showFieldsTabs, showFormFieldsTabs, Utils.qh("id status add_time add_users_id upd_time upd_users_id"), allFields);

        Assert.AreEqual(0, col);
        Assert.AreSame(sf, showFieldsTabs[""][0][0]);
        Assert.AreEqual(2, showFieldsTabs[""].Count);
        Assert.AreEqual(0, showFieldsTabs[""][1].Count);
    }

    [TestMethod]
    public void AddToFormColumns_BalancesCompactFieldsOnlyWhenRightStaysLighter()
    {
        var showFieldsTabs = new Dictionary<string, List<List<FwDict>>>();
        var showFormFieldsTabs = new Dictionary<string, List<List<FwDict>>>();
        var allFields = TenGeneratedFields();
        var firstField = Field("short_name", "varchar", 80, isNullable: false);
        var firstShow = new FwDict { ["field"] = "short_name", ["type"] = "plaintext" };
        var firstForm = new FwDict { ["field"] = "short_name", ["type"] = "input" };
        var secondField = Field("short_code", "varchar", 80, isNullable: false);
        var secondShow = new FwDict { ["field"] = "short_code", ["type"] = "plaintext" };
        var secondForm = new FwDict { ["field"] = "short_code", ["type"] = "input" };
        var thirdField = Field("short_type", "varchar", 80, isNullable: false);
        var thirdShow = new FwDict { ["field"] = "short_type", ["type"] = "plaintext" };
        var thirdForm = new FwDict { ["field"] = "short_type", ["type"] = "input" };

        var firstCol = InvokeAddToFormColumns(firstField, firstShow, firstForm, showFieldsTabs, showFormFieldsTabs, Utils.qh("id status add_time add_users_id upd_time upd_users_id"), allFields);
        var secondCol = InvokeAddToFormColumns(secondField, secondShow, secondForm, showFieldsTabs, showFormFieldsTabs, Utils.qh("id status add_time add_users_id upd_time upd_users_id"), allFields);
        var thirdCol = InvokeAddToFormColumns(thirdField, thirdShow, thirdForm, showFieldsTabs, showFormFieldsTabs, Utils.qh("id status add_time add_users_id upd_time upd_users_id"), allFields);

        Assert.AreEqual(0, firstCol);
        Assert.AreEqual(0, secondCol);
        Assert.AreEqual(1, thirdCol);
        Assert.AreSame(firstShow, showFieldsTabs[""][0][0]);
        Assert.AreSame(secondShow, showFieldsTabs[""][0][1]);
        Assert.AreSame(thirdShow, showFieldsTabs[""][1][0]);
    }

    [TestMethod]
    public void AddToFormColumns_KeepsMajorLookupFieldsInPrimaryColumn()
    {
        var showFieldsTabs = new Dictionary<string, List<List<FwDict>>>();
        var showFormFieldsTabs = new Dictionary<string, List<List<FwDict>>>();
        var allFields = TenGeneratedFields();
        var firstField = Field("short_name", "varchar", 80, isNullable: false);
        var firstShow = new FwDict { ["field"] = "short_name", ["type"] = "plaintext" };
        var firstForm = new FwDict { ["field"] = "short_name", ["type"] = "input" };
        var inameField = Field("iname", "varchar", 80, isNullable: false);
        var inameShow = new FwDict { ["field"] = "iname", ["type"] = "plaintext" };
        var inameForm = new FwDict { ["field"] = "iname", ["type"] = "input" };
        var icodeField = Field("icode", "varchar", 80, isNullable: false);
        var icodeShow = new FwDict { ["field"] = "icode", ["type"] = "plaintext" };
        var icodeForm = new FwDict { ["field"] = "icode", ["type"] = "input" };

        var firstCol = InvokeAddToFormColumns(firstField, firstShow, firstForm, showFieldsTabs, showFormFieldsTabs, Utils.qh("id status add_time add_users_id upd_time upd_users_id"), allFields);
        var inameCol = InvokeAddToFormColumns(inameField, inameShow, inameForm, showFieldsTabs, showFormFieldsTabs, Utils.qh("id status add_time add_users_id upd_time upd_users_id"), allFields);
        var icodeCol = InvokeAddToFormColumns(icodeField, icodeShow, icodeForm, showFieldsTabs, showFormFieldsTabs, Utils.qh("id status add_time add_users_id upd_time upd_users_id"), allFields);

        Assert.AreEqual(0, firstCol);
        Assert.AreEqual(0, inameCol);
        Assert.AreEqual(0, icodeCol);
        Assert.AreSame(firstShow, showFieldsTabs[""][0][0]);
        Assert.AreSame(inameShow, showFieldsTabs[""][0][1]);
        Assert.AreSame(icodeShow, showFieldsTabs[""][0][2]);
        Assert.AreEqual(0, showFieldsTabs[""][1].Count);
    }

    [TestMethod]
    public void AddToFormColumns_PlacesPriorityInRightColumn()
    {
        var showFieldsTabs = new Dictionary<string, List<List<FwDict>>>();
        var showFormFieldsTabs = new Dictionary<string, List<List<FwDict>>>();
        var allFields = TenGeneratedFields();
        var fld = Field("prio", "int", 0, isNullable: false);
        var sf = new FwDict { ["field"] = "prio", ["type"] = "plaintext" };
        var sff = new FwDict { ["field"] = "prio", ["type"] = "number" };

        var col = InvokeAddToFormColumns(fld, sf, sff, showFieldsTabs, showFormFieldsTabs, Utils.qh("id status add_time add_users_id upd_time upd_users_id"), allFields);

        Assert.AreEqual(1, col);
        Assert.AreEqual(2, showFieldsTabs[""].Count);
        Assert.AreSame(sf, showFieldsTabs[""][1][0]);
    }

    [TestMethod]
    public void AddToFormColumns_PlacesLifecycleTimeInRightColumn()
    {
        var showFieldsTabs = new Dictionary<string, List<List<FwDict>>>();
        var showFormFieldsTabs = new Dictionary<string, List<List<FwDict>>>();
        var allFields = new FwList
        {
            Field("id", "int", 0, isNullable: false, isIdentity: true),
            Field("iname", "varchar", 80, isNullable: false),
            Field("idesc", "varchar", 0, isNullable: true),
            Field("applied_time", "datetime", 0, isNullable: true)
        };
        var fld = Field("applied_time", "datetime", 0, isNullable: true);
        var sf = new FwDict { ["field"] = "applied_time", ["type"] = "datetime" };
        var sff = new FwDict { ["field"] = "applied_time", ["type"] = "datetime_popup" };

        var col = InvokeAddToFormColumns(fld, sf, sff, showFieldsTabs, showFormFieldsTabs, Utils.qh("id status add_time add_users_id upd_time upd_users_id"), allFields);

        Assert.AreEqual(1, col);
        Assert.AreEqual(2, showFieldsTabs[""].Count);
        Assert.AreSame(sf, showFieldsTabs[""][1][0]);
    }

    [TestMethod]
    public void AddToFormColumns_KeepsRelationPrimaryWhenRightWouldBecomeHeavier()
    {
        var showFieldsTabs = new Dictionary<string, List<List<FwDict>>>();
        var showFormFieldsTabs = new Dictionary<string, List<List<FwDict>>>();
        var allFields = TenGeneratedFields();
        var sysFields = Utils.qh("id status add_time add_users_id upd_time upd_users_id");

        InvokeAddToFormColumns(Field("icode", "varchar", 80, isNullable: false), new FwDict { ["field"] = "icode", ["type"] = "plaintext" }, new FwDict { ["field"] = "icode", ["type"] = "input" }, showFieldsTabs, showFormFieldsTabs, sysFields, allFields);
        InvokeAddToFormColumns(Field("iname", "varchar", 80, isNullable: false), new FwDict { ["field"] = "iname", ["type"] = "plaintext" }, new FwDict { ["field"] = "iname", ["type"] = "input" }, showFieldsTabs, showFormFieldsTabs, sysFields, allFields);
        InvokeAddToFormColumns(Field("idesc", "varchar", 0, isNullable: true), new FwDict { ["field"] = "idesc", ["type"] = "markdown" }, new FwDict { ["field"] = "idesc", ["type"] = "textarea" }, showFieldsTabs, showFormFieldsTabs, sysFields, allFields);
        InvokeAddToFormColumns(Field("id", "int", 0, isNullable: false, isIdentity: true), new FwDict { ["field"] = "id", ["type"] = "id" }, new FwDict { ["field"] = "id", ["type"] = "id" }, showFieldsTabs, showFormFieldsTabs, sysFields, allFields);
        InvokeAddToFormColumns(Field("prio", "int", 0, isNullable: false), new FwDict { ["field"] = "prio", ["type"] = "plaintext" }, new FwDict { ["field"] = "prio", ["type"] = "number" }, showFieldsTabs, showFormFieldsTabs, sysFields, allFields);
        InvokeAddToFormColumns(Field("status", "int", 0, isNullable: false), new FwDict { ["field"] = "status", ["type"] = "plaintext" }, new FwDict { ["field"] = "status", ["type"] = "select" }, showFieldsTabs, showFormFieldsTabs, sysFields, allFields);
        InvokeAddToFormColumns(Field("add_time", "datetime", 0, isNullable: false), new FwDict { ["field"] = "add_time", ["type"] = "added" }, new FwDict { ["field"] = "add_time", ["type"] = "added" }, showFieldsTabs, showFormFieldsTabs, sysFields, allFields);
        InvokeAddToFormColumns(Field("upd_time", "datetime", 0, isNullable: true), new FwDict { ["field"] = "upd_time", ["type"] = "updated" }, new FwDict { ["field"] = "upd_time", ["type"] = "updated" }, showFieldsTabs, showFormFieldsTabs, sysFields, allFields);

        var resourceCol = InvokeAddToFormColumns(Field("resources_id", "int", 0, isNullable: true), new FwDict { ["field"] = "resources_id", ["type"] = "plaintext_link" }, new FwDict { ["field"] = "resources_id", ["type"] = "select" }, showFieldsTabs, showFormFieldsTabs, sysFields, allFields);

        Assert.AreEqual(0, resourceCol);
        Assert.AreEqual("resources_id", showFieldsTabs[""][0][showFieldsTabs[""][0].Count - 1]["field"]);
    }

    [TestMethod]
    public void MakeLayoutForFields_OrdersRightSupportFieldsBeforeBottomMetadata()
    {
        var fieldsCols = new List<List<FwDict>>
        {
            new() { new FwDict { ["field"] = "idesc", ["type"] = "markdown" } },
            new()
            {
                new FwDict { ["field"] = "id", ["type"] = "id" },
                new FwDict { ["field"] = "prio", ["type"] = "number" },
                new FwDict { ["field"] = "status", ["type"] = "select" },
                new FwDict { ["field"] = "add_time", ["type"] = "added" },
                new FwDict { ["field"] = "upd_time", ["type"] = "updated" },
                new FwDict { ["field"] = "resources_id", ["type"] = "select" }
            }
        };

        var layout = InvokeMakeLayoutForFields(fieldsCols);

        Assert.AreEqual("id", ((FwDict)layout[5]!)["field"]);
        Assert.AreEqual("resources_id", ((FwDict)layout[6]!)["field"]);
        Assert.AreEqual("prio", ((FwDict)layout[7]!)["field"]);
        Assert.AreEqual("status", ((FwDict)layout[8]!)["field"]);
        Assert.AreEqual("add_time", ((FwDict)layout[9]!)["field"]);
        Assert.AreEqual("upd_time", ((FwDict)layout[10]!)["field"]);
    }

    [TestMethod]
    public void AddToTabColumn_CollapsesLegacyMetadataColumnIntoRightColumn()
    {
        var showFieldsTabs = new Dictionary<string, List<List<FwDict>>>();
        var metadataField = new FwDict { ["field"] = "id", ["type"] = "id" };

        InvokeAddToTabColumn(showFieldsTabs, "", 2, metadataField);

        Assert.AreEqual(2, showFieldsTabs[""].Count);
        Assert.AreEqual(0, showFieldsTabs[""][0].Count);
        Assert.AreSame(metadataField, showFieldsTabs[""][1][0]);
    }

    [TestMethod]
    public void MakeLayoutForFields_CollapsesSecondaryAndMetadataIntoRightColumn()
    {
        var fieldsCols = new List<List<FwDict>>
        {
            new() { new FwDict { ["field"] = "idesc", ["type"] = "markdown" } },
            new() { new FwDict { ["field"] = "applied_time", ["type"] = "date_long" } },
            new() { new FwDict { ["field"] = "id", ["type"] = "id" } }
        };

        var layout = InvokeMakeLayoutForFields(fieldsCols);

        Assert.AreEqual("row", ((FwDict)layout[0]!)["type"]);
        Assert.AreEqual("col", ((FwDict)layout[1]!)["type"]);
        Assert.AreEqual("col-12 col-lg-8", ((FwDict)layout[1]!)["class"]);
        Assert.AreEqual("col", ((FwDict)layout[4]!)["type"]);
        Assert.AreEqual("col-12 col-lg-4", ((FwDict)layout[4]!)["class"]);
        Assert.AreEqual("id", ((FwDict)layout[5]!)["field"]);
        Assert.AreEqual("applied_time", ((FwDict)layout[6]!)["field"]);
        Assert.AreEqual("col_end", ((FwDict)layout[7]!)["type"]);
        Assert.AreEqual("row_end", ((FwDict)layout[8]!)["type"]);
    }

    private static void InvokeUpdateControllerConfig(FW fw, FwDict entity, FwDict config)
    {
        var codeGen = CreateCodeGen(fw);
        var method = CodeGenType.GetMethod("updateControllerConfig", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("DevCodeGen.updateControllerConfig was not found.");

        method.Invoke(codeGen, new object?[] { entity, config, new FwList() });
    }

    private static string InvokeBuildLookupInsertSql(FW fw, FwDict item)
    {
        var codeGen = CreateCodeGen(fw);
        var method = CodeGenType.GetMethod("buildLookupInsertSql", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DevCodeGen.buildLookupInsertSql was not found.");

        return method.Invoke(codeGen, new object?[] { item })?.toStr() ?? "";
    }

    private static int InvokeAddToFormColumns(
        FwDict fld,
        FwDict sf,
        FwDict sff,
        Dictionary<string, List<List<FwDict>>> showFieldsTabs,
        Dictionary<string, List<List<FwDict>>> showFormFieldsTabs,
        FwDict sysFields,
        FwList fields)
    {
        var method = CodeGenType.GetMethod("addToFormColumns", BindingFlags.Static | BindingFlags.Public)
            ?? throw new InvalidOperationException("DevCodeGen.addToFormColumns was not found.");

        return method.Invoke(null, new object?[] { fld, sf, sff, showFieldsTabs, showFormFieldsTabs, sysFields, fields }).toInt();
    }

    private static void InvokeAddToTabColumn(Dictionary<string, List<List<FwDict>>> showFieldsTabs, string tab, int col, FwDict field)
    {
        var method = CodeGenType.GetMethod("addToTabColumn", BindingFlags.Static | BindingFlags.Public)
            ?? throw new InvalidOperationException("DevCodeGen.addToTabColumn was not found.");

        method.Invoke(null, new object?[] { showFieldsTabs, tab, col, field });
    }

    private static FwList InvokeMakeLayoutForFields(List<List<FwDict>> fieldsCols)
    {
        var method = CodeGenType.GetMethod("makeLayoutForFields", BindingFlags.Static | BindingFlags.Public)
            ?? throw new InvalidOperationException("DevCodeGen.makeLayoutForFields was not found.");

        return (FwList)(method.Invoke(null, new object?[] { fieldsCols }) ?? new FwList());
    }

    private static object CreateCodeGen(FW fw)
    {
        var ctor = CodeGenType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(FW), typeof(DB) },
            modifiers: null)
            ?? throw new InvalidOperationException("DevCodeGen constructor was not found.");
        return ctor.Invoke(new object?[] { fw, fw.db });
    }

    private static FwList TenGeneratedFields()
    {
        var fields = new FwList();
        for (var i = 0; i < 10; i++)
            fields.Add(Field("field" + i, "varchar", 80, isNullable: true));
        return fields;
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
