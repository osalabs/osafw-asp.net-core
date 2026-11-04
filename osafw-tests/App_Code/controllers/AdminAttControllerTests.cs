using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace osafw.Tests;

[TestClass]
public class AdminAttControllerTests
{
    private sealed class StubAtt : Att
    {
        public Dictionary<int, FwDict> Rows { get; } = [];
        public FwDict LastUpdate { get; private set; } = [];

        public StubAtt()
        {
            table_schema = new FwDict
            {
                ["att_categories_id"] = new FwDict { ["fw_type"] = "int", ["is_nullable"] = 1 },
                ["fwentities_id"] = new FwDict { ["fw_type"] = "int", ["is_nullable"] = 1 },
                ["item_id"] = new FwDict { ["fw_type"] = "int", ["is_nullable"] = 1 },
                ["iname"] = new FwDict { ["fw_type"] = "varchar", ["is_nullable"] = 0 },
                ["status"] = new FwDict { ["fw_type"] = "int", ["is_nullable"] = 0 }
            };
        }

        public bool IsParentAllowed { get; set; } = true;
        protected override bool isParentAccessAllowed(FwDict item, string action)
        {
            return item["fwentities_id"].toInt() <= 0 || IsParentAllowed;
        }

        public override DBRow one(int id)
        {
            return Rows.TryGetValue(id, out var row) ? new DBRow(new FwDict(row)) : [];
        }

        public override bool update(int id, FwDict item)
        {
            LastUpdate = new FwDict(item);
            if (!Rows.ContainsKey(id))
                Rows[id] = DB.h("id", id);

            foreach (var entry in item)
                Rows[id][entry.Key] = entry.Value;

            return true;
        }
    }

    private sealed class StubUsers : Users
    {
        public override bool isReadOnly(int id = -1) => false;

        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null)
        {
            return [];
        }
    }

    [TestMethod]
    public void SaveAction_RejectsUnauthorizedExistingParentBeforeClearingBinding()
    {
        var fw = TestHelpers.CreateFw();
        fw.request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection());
        fw.FORM["item"] = DB.h("iname", "Changed", "fwentities_id", 0, "item_id", 0);
        var att = new StubAtt { IsParentAllowed = false };
        att.init(fw);
        att.Rows[15] = DB.h("id", 15, "fwentities_id", 7, "item_id", 20, "fsize", 123);
        var users = new StubUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        TestHelpers.RegisterModel(fw, (Att)att);
        var controller = new AdminAttController();
        controller.init(fw);
        fw.route.id = "15";
        foreach (var action in new[] { FW.ACTION_SAVE, FW.ACTION_SHOW_FORM, FW.ACTION_DELETE, "RestoreDeleted" })
        {
            fw.route.action = action;
            Assert.ThrowsExactly<AuthException>(() => controller.checkAccess());
        }
        fw.route.id = "";
        fw.route.action = "SaveMulti";
        fw.FORM["cb"] = DB.h("15", "1");
        fw.FORM["delete"] = "1";
        Assert.ThrowsExactly<AuthException>(() => controller.checkAccess());
        Assert.IsEmpty(att.LastUpdate);

        // The existing row is allowed, but a forged new parent must still be denied during save.
        att.Rows[15]["fwentities_id"] = 0;
        fw.route.id = "15";
        fw.route.action = FW.ACTION_SAVE;
        controller.checkAccess();
        fw.FORM["item"] = DB.h("iname", "Changed", "fwentities_id", 7, "item_id", 20);
        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction(15));
        Assert.IsEmpty(att.LastUpdate);
    }

    [TestMethod]
    public void CheckAccess_RejectsUnauthorizedProposedParentAndAllowsInactiveAdministration()
    {
        var att = new StubAtt { IsParentAllowed = false };
        Assert.ThrowsExactly<AuthException>(() => att.checkAccess(DB.h("fwentities_id", 7, "item_id", 20), "edit"));
        Assert.ThrowsExactly<AuthException>(() => att.checkAccess(new FwDict(), "view"));
        att.checkAccess(DB.h("id", 15, "status", FwModel.STATUS_INACTIVE, "fwentities_id", 0), "edit");
        att.Rows[15] = DB.h("id", 15, "status", FwModel.STATUS_INACTIVE, "fwentities_id", 0);
        Assert.ThrowsExactly<AuthException>(() => att.checkAccess(15, "view"));
    }

    [TestMethod]
    public void SaveAction_EditMetadataWithoutFileDoesNotRequireUpload()
    {
        var fw = TestHelpers.CreateFw();
        fw.request.Headers.Accept = "application/json";
        fw.request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection());
        fw.FORM = new FwDict
        {
            ["item"] = new FwDict
            {
                ["att_categories_id"] = "",
                ["iname"] = "Updated file",
                ["status"] = FwModel.STATUS_ACTIVE
            }
        };

        var att = new StubAtt();
        att.init(fw);
        att.Rows[15] = DB.h(
            "id", 15,
            "icode", "file15",
            "att_categories_id", 3,
            "iname", "Original file",
            "is_image", 0,
            "fsize", 123,
            "fname", "original.txt",
            "ext", "txt",
            "status", FwModel.STATUS_ACTIVE);

        var users = new StubUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        TestHelpers.RegisterModel(fw, (Att)att);

        var controller = new AdminAttController();
        controller.init(fw);

        var ps = controller.SaveAction(15)!;

        Assert.AreEqual("Updated file", att.LastUpdate["iname"]);
        Assert.AreSame(DBNull.Value, att.LastUpdate["att_categories_id"]);
        var json = ps["_json"] as FwDict ?? [];
        Assert.AreEqual(true, json["success"]);
    }
}
