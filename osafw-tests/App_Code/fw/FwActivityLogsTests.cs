using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Linq;

namespace osafw.Tests;

[TestClass]
public class FwActivityLogsTests
{
    private class StubLogTypes : FwLogTypes
    {
        public override DBRow oneByIcode(string icode) => new DBRow(new FwDict { ["id"] = "2", ["itype"] = FwLogTypes.ITYPE_SYSTEM });
        public override DBRow one(int id) => new DBRow(new FwDict { ["id"] = id.toStr(), ["itype"] = FwLogTypes.ITYPE_SYSTEM });
    }

    private class StubEntities : FwEntities
    {
        public override int idByIcodeOrAdd(string entity_icode) => 11;
    }

    private class StubUsers : Users
    {
        public override void checkReadOnly(int id = -1) { }
        public override bool isReadOnly(int id = -1) => false;
        public override bool isAccessLevel(int min_acl) => true;
        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
        public override DBRow one(int id) => new DBRow(new FwDict { ["id"] = id.toStr(), ["att_id"] = "0", ["iname"] = $"User{id}" });
        public override string iname(object? id) => $"User{id}";
    }

    private class TestActivityLogs : FwActivityLogs
    {
        public FwDict? LastAddFields;
        public DBList StubRows { get; set; } = [];

        public override int add(FwDict item)
        {
            LastAddFields = item;
            return 77;
        }

        public override DBList listByEntity(string entity_icode, int id, IList? log_types_icodes = null)
        {
            return StubRows;
        }
    }

    private static TestActivityLogs BuildLogs(FW fw)
    {
        var logs = new TestActivityLogs();
        logs.init(fw);
        return logs;
    }

    [TestMethod]
    public void AddSimple_PopulatesFieldsAndPayload()
    {
        var fw = TestHelpers.CreateFw();
        fw.Session("user_id", "3");
        TestHelpers.RegisterModel(fw, (FwLogTypes)new StubLogTypes());
        TestHelpers.RegisterModel(fw, (FwEntities)new StubEntities());
        var logs = BuildLogs(fw);

        var payload = new FwDict { ["fields"] = new FwDict { ["name"] = "value" } };
        var id = logs.addSimple("comment", "demo", 10, "desc", payload);

        Assert.AreEqual(77, id);
        Assert.IsNotNull(logs.LastAddFields);
        Assert.AreEqual(10, logs.LastAddFields!["item_id"]);
        Assert.AreEqual(3, logs.LastAddFields!["users_id"]);
        var storedPayloadObj = Utils.jsonDecode(logs.LastAddFields!["payload"].toStr());
        var storedPayload = storedPayloadObj as FwDict ?? [];
        Assert.AreEqual("value", ((FwDict)storedPayload["fields"]!)["name"]);
    }

    [TestMethod]
    public void ListByEntityForUI_MergesSystemUpdates()
    {
        var fw = TestHelpers.CreateFw();
        TestHelpers.RegisterModel(fw, (FwLogTypes)new StubLogTypes());
        TestHelpers.RegisterModel(fw, (FwEntities)new StubEntities());
        TestHelpers.RegisterModel(fw, (Users)new StubUsers());
        var logs = BuildLogs(fw);
        var now = DateTime.UtcNow;
        logs.StubRows = new DBList
        {
            new DBRow(new FwDict
            {
                ["log_types_id"] = "2",
                ["users_id"] = "1",
                ["add_time"] = now.ToString("o"),
                ["idate"] = now.ToString("o"),
                ["upd_time"] = now.ToString("o"),
                ["payload"] = Utils.jsonEncode(new FwDict { ["fields"] = new FwDict { ["pwd"] = "secret", ["name"] = "first" } })
            }),
            new DBRow(new FwDict
            {
                ["log_types_id"] = "2",
                ["users_id"] = "1",
                ["add_time"] = now.AddMinutes(-5).ToString("o"),
                ["idate"] = now.AddMinutes(-5).ToString("o"),
                ["upd_time"] = now.AddMinutes(-5).ToString("o"),
                ["payload"] = Utils.jsonEncode(new FwDict { ["fields"] = new FwDict { ["title"] = "second" } })
            })
        };

        var result = logs.listByEntityForUI("demo", 5);

        Assert.AreEqual(1, result.Count);
        var row = result[0] as FwDict ?? [];
        var fields = row["fields"] as FwList ?? [];
        Assert.AreEqual(3, fields.Count); // pwd masked + 2 fields
        var pwdField = fields.First(f => ((FwDict)f)["key"].toStr() == "pwd") as FwDict;
        Assert.AreEqual("********", pwdField!["value"]);
    }
}
