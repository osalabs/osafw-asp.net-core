using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace osafw.Tests;

[TestClass]
public class SecurityDynamicObjectLinkTests
{
    private sealed class StubUsers : Users
    {
        public override bool isReadOnly(int id = -1) => false;

        public override bool isAccessLevel(int min_acl) => false;

        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    private sealed class EmptyModel : FwModel
    {
    }

    private sealed class ParentRows : FwModel
    {
        public Dictionary<int, FwDict> Rows { get; } = [];
        public int UpdateCalls { get; private set; }

        public ParentRows()
        {
            table_name = "parent_rows";
        }

        public override DBRow one(int id)
        {
            return Rows.TryGetValue(id, out var row) ? new DBRow(new FwDict(row)) : new DBRow();
        }

        public override void checkAccess(int id = 0, string action = "")
        {
            if (!Rows.ContainsKey(id))
                throw new AuthException();
        }

        public override bool update(int id, FwDict item)
        {
            UpdateCalls++;
            return true;
        }
    }

    private sealed class ChildRowsModel : FwModel
    {
        public Dictionary<int, FwDict> Rows { get; } = [];
        public List<int> UpdatedIds { get; } = [];
        public List<int> UnderUpdateParentIds { get; } = [];
        public List<int> DeleteUnderUpdateParentIds { get; } = [];

        public ChildRowsModel()
        {
            table_name = "child_rows";
            junction_field_main_id = "parent_rows_id";
        }

        public override DBRow one(int id)
        {
            return Rows.TryGetValue(id, out var row) ? new DBRow(new FwDict(row)) : new DBRow();
        }

        public override bool update(int id, FwDict item)
        {
            UpdatedIds.Add(id);
            if (Rows.TryGetValue(id, out var row))
                Utils.mergeHash(row, item);
            return true;
        }

        public override void setUnderUpdateByMainId(int main_id)
        {
            UnderUpdateParentIds.Add(main_id);
        }

        public override void deleteUnderUpdateByMainId(int main_id)
        {
            DeleteUnderUpdateParentIds.Add(main_id);
        }
    }

    private sealed class TestDynamicController : FwDynamicController
    {
        private readonly ParentRows parentModel;

        public TestDynamicController(ParentRows parentModel)
        {
            this.parentModel = parentModel;
        }

        public override void init(FW fw)
        {
            base.init(fw);
            model0 = parentModel;
            db = parentModel.getDB();
            base_url = "/DynamicSecurity";
            save_fields = "iname";
            is_dynamic_showform = true;
            config["showform_fields"] = new FwList
            {
                new FwDict
                {
                    ["field"] = "children",
                    ["type"] = "subtable_edit",
                    ["model"] = nameof(ChildRowsModel),
                    ["save_fields"] = "iname",
                    ["save_fields_checkboxes"] = "",
                    ["required_fields"] = ""
                }
            };
        }
    }

    private sealed class TestVueController : FwVueController
    {
        private readonly ParentRows parentModel;

        public TestVueController(ParentRows parentModel)
        {
            this.parentModel = parentModel;
        }

        public override void init(FW fw)
        {
            base.init(fw);
            model0 = parentModel;
            db = parentModel.getDB();
            base_url = "/VueSecurity";
            save_fields = "iname";
        }
    }

    private class DefaultAccessAtt : Att
    {
        public Dictionary<int, FwDict> Rows { get; } = [];

        public override DBRow one(int id)
        {
            return Rows.TryGetValue(id, out var row) ? new DBRow(new FwDict(row)) : new DBRow();
        }
    }

    private sealed class LinkRecordingAtt : DefaultAccessAtt
    {
        public List<(int Id, string Action, int EntityId, int ItemId)> LinkChecks { get; } = [];

        public override void checkAccess(int id, string action, int fwentities_id, int item_id)
        {
            LinkChecks.Add((id, action, fwentities_id, item_id));
            base.checkAccess(id, action, fwentities_id, item_id);
        }
    }

    private sealed class TestFwEntities : FwEntities
    {
        public override int idByIcodeOrAdd(string icode) => 7;

        public override DBRow one(int id)
        {
            return id == 7 ? new DBRow(DB.h("id", id, "icode", "parent_rows", "status", STATUS_ACTIVE)) : [];
        }
    }

    private sealed class TestAttLinks : AttLinks
    {
        public List<(int EntityId, int ItemId)> UnderUpdateCalls { get; } = [];
        public List<(int EntityId, int ItemId)> DeleteUnderUpdateCalls { get; } = [];

        public override FwDict oneByUK(int att_id, int fwentities_id, int item_id) => [];

        public override void setUnderUpdate(int fwentities_id, int item_id)
        {
            UnderUpdateCalls.Add((fwentities_id, item_id));
        }

        public override void deleteUnderUpdate(int fwentities_id, int item_id)
        {
            DeleteUnderUpdateCalls.Add((fwentities_id, item_id));
        }
    }

    private sealed class RecordingDb : DB
    {
        public List<FwDict> Inserts { get; } = [];
        public List<(FwDict Fields, FwDict Where)> Updates { get; } = [];

        public RecordingDb() : base("", DB.DBTYPE_SQLSRV, "recording")
        {
        }

        public override int insert(string table, FwDict fields)
        {
            Inserts.Add(new FwDict(fields));
            return Inserts.Count;
        }

        public override int update(string table, FwDict fields, FwDict where)
        {
            Updates.Add((new FwDict(fields), new FwDict(where)));
            return 1;
        }
    }

    private sealed class SignupUsers : Users
    {
        public int AddedId { get; set; } = 77;
        public FwDict LastAdded { get; private set; } = [];
        public List<int> UpdatedIds { get; } = [];

        public override bool isExists(object uniq_key, int not_id) => false;

        public override int add(FwDict item)
        {
            LastAdded = new FwDict(item);
            return AddedId;
        }

        public override bool update(int id, FwDict item)
        {
            UpdatedIds.Add(id);
            return true;
        }

        public override DBRow one(int id)
        {
            return new DBRow(new FwDict
            {
                ["id"] = id,
                ["email"] = LastAdded["email"],
                ["access_level"] = LastAdded["access_level"],
                ["status"] = STATUS_ACTIVE,
                ["pwd"] = LastAdded["pwd"],
                ["mfa_secret"] = "",
                ["lang"] = "en",
                ["ui_theme"] = "",
                ["ui_mode"] = "",
                ["date_format"] = DateUtils.DATE_FORMAT_DMY,
                ["time_format"] = DateUtils.TIME_FORMAT_24,
                ["timezone"] = "",
                ["fname"] = LastAdded["fname"],
                ["lname"] = LastAdded["lname"],
                ["att_id"] = "0",
            });
        }

        public override bool isAccessLevel(int min_acl) => false;

        public override bool isReadOnly(int id = -1) => false;

        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    [TestMethod]
    public void FwModelOne_NonpositiveIdsReturnEmptyRows()
    {
        var model = new EmptyModel();

        Assert.AreEqual(0, model.one(0).Count);
        Assert.AreEqual(0, model.one(-1).Count);
    }

    [TestMethod]
    public void DynamicSubtable_CrossParentExistingRowUpdateIsRejectedBeforeMutation()
    {
        var parentModel = new ParentRows();
        parentModel.Rows[1] = DB.h("id", 1, "iname", "Parent 1");
        var childModel = new ChildRowsModel();
        childModel.Rows[20] = DB.h("id", 20, "parent_rows_id", 2, "iname", "Other parent row");
        var fw = createDynamicFw(parentModel, childModel);
        var controller = new TestDynamicController(parentModel);
        controller.init(fw);
        postSubtableSave(fw, 20, "forged");

        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction(1));

        Assert.AreEqual(0, parentModel.UpdateCalls);
        Assert.IsEmpty(childModel.UpdatedIds);
        Assert.IsEmpty(childModel.UnderUpdateParentIds);
        Assert.IsEmpty(childModel.DeleteUnderUpdateParentIds);
    }

    [TestMethod]
    public void DynamicSaveAction_NegativeRouteIdIsRejectedBeforeMutation()
    {
        var parentModel = new ParentRows();
        var childModel = new ChildRowsModel();
        var fw = createDynamicFw(parentModel, childModel);
        var controller = new TestDynamicController(parentModel);
        controller.init(fw);

        Assert.ThrowsExactly<NotFoundException>(() => controller.SaveAction(-1));
        Assert.AreEqual(0, parentModel.UpdateCalls);
    }

    [TestMethod]
    public void DynamicSubtable_SameParentExistingRowUpdateStillSucceeds()
    {
        var parentModel = new ParentRows();
        parentModel.Rows[1] = DB.h("id", 1, "iname", "Parent 1");
        var childModel = new ChildRowsModel();
        childModel.Rows[20] = DB.h("id", 20, "parent_rows_id", 1, "iname", "Same parent row");
        var fw = createDynamicFw(parentModel, childModel);
        var controller = new TestDynamicController(parentModel);
        controller.init(fw);
        postSubtableSave(fw, 20, "updated");

        Assert.ThrowsExactly<RedirectException>(() => controller.SaveAction(1));

        Assert.AreEqual(1, parentModel.UpdateCalls);
        CollectionAssert.AreEqual(new[] { 1 }, childModel.UnderUpdateParentIds);
        CollectionAssert.AreEqual(new[] { 20 }, childModel.UpdatedIds);
        CollectionAssert.AreEqual(new[] { 1 }, childModel.DeleteUnderUpdateParentIds);
    }

    [TestMethod]
    public void DynamicSaveAttFiles_RejectsUnauthorizedParentBeforeFileValidation()
    {
        var parentModel = new ParentRows();
        var childModel = new ChildRowsModel();
        var fw = createDynamicFw(parentModel, childModel);
        var controller = new TestDynamicController(parentModel);
        controller.init(fw);

        Assert.ThrowsExactly<NotFoundException>(() => controller.SaveAttFilesAction(9));
    }

    [TestMethod]
    public void VueSaveAction_RejectsUnauthorizedParentBeforeMutation()
    {
        var parentModel = new ParentRows();
        var childModel = new ChildRowsModel();
        var fw = createDynamicFw(parentModel, childModel);
        fw.FORM["item"] = DB.h("iname", "forged");
        var controller = new TestVueController(parentModel);
        controller.init(fw);

        Assert.ThrowsExactly<NotFoundException>(() => controller.SaveAction(9));

        Assert.AreEqual(0, parentModel.UpdateCalls);
    }

    [TestMethod]
    public void AttLinks_UpdateJunctionRejectsDirectBoundDifferentTargetAttachmentBeforeMutation()
    {
        var db = new RecordingDb();
        var fw = createFw();
        fw.db = db;
        var att = new LinkRecordingAtt();
        att.Rows[42] = DB.h("id", 42, "status", FwModel.STATUS_ACTIVE, "fwentities_id", 8, "item_id", 5);
        att.init(fw);
        TestHelpers.RegisterModel(fw, (Att)att);
        var entities = new TestFwEntities();
        entities.init(fw);
        TestHelpers.RegisterModel(fw, (FwEntities)entities);
        var links = new TestAttLinks();
        links.init(fw);
        TestHelpers.RegisterModel(fw, (AttLinks)links);

        Assert.ThrowsExactly<AuthException>(() => links.updateJunction("demos", 5, DB.h("42", 1)));

        Assert.AreEqual(1, att.LinkChecks.Count);
        Assert.AreEqual((42, Att.ACCESS_ACTION_LINK, 7, 5), att.LinkChecks[0]);
        Assert.IsEmpty(links.UnderUpdateCalls);
        Assert.IsEmpty(links.DeleteUnderUpdateCalls);
        Assert.IsEmpty(db.Inserts);
        Assert.IsEmpty(db.Updates);
    }

    [TestMethod]
    public void AttLinks_UpdateJunctionAllowsUnboundActiveAttachment()
    {
        var db = new RecordingDb();
        var fw = createFw();
        fw.db = db;
        var att = new LinkRecordingAtt();
        att.Rows[42] = DB.h("id", 42, "status", FwModel.STATUS_ACTIVE);
        att.init(fw);
        TestHelpers.RegisterModel(fw, (Att)att);
        var entities = new TestFwEntities();
        entities.init(fw);
        TestHelpers.RegisterModel(fw, (FwEntities)entities);
        var links = new TestAttLinks();
        links.init(fw);
        TestHelpers.RegisterModel(fw, (AttLinks)links);

        links.updateJunction("demos", 5, DB.h("42", 1));

        Assert.AreEqual(1, att.LinkChecks.Count);
        Assert.AreEqual((42, Att.ACCESS_ACTION_LINK, 7, 5), att.LinkChecks[0]);
        Assert.AreEqual(1, links.UnderUpdateCalls.Count);
        Assert.AreEqual(1, links.DeleteUnderUpdateCalls.Count);
        Assert.AreEqual(1, db.Inserts.Count);
        Assert.AreEqual(42, db.Inserts[0]["att_id"].toInt());
        Assert.AreEqual(5, db.Inserts[0]["item_id"].toInt());
        Assert.AreEqual(7, db.Inserts[0]["fwentities_id"].toInt());
    }

    [TestMethod]
    public void AttLinks_UpdateJunctionAllowsDirectBoundSameTargetAttachment()
    {
        var db = new RecordingDb();
        var fw = createFw();
        fw.db = db;
        var att = new LinkRecordingAtt();
        att.Rows[42] = DB.h("id", 42, "status", FwModel.STATUS_ACTIVE, "fwentities_id", 7, "item_id", 5);
        att.init(fw);
        TestHelpers.RegisterModel(fw, (Att)att);
        var parent = new ParentRows();
        parent.Rows[5] = DB.h("id", 5, "status", FwModel.STATUS_ACTIVE);
        parent.init(fw);
        TestHelpers.RegisterModel(fw, parent);
        var entities = new TestFwEntities();
        entities.init(fw);
        TestHelpers.RegisterModel(fw, (FwEntities)entities);
        var links = new TestAttLinks();
        links.init(fw);
        TestHelpers.RegisterModel(fw, (AttLinks)links);

        links.updateJunction("demos", 5, DB.h("42", 1));

        Assert.AreEqual(1, att.LinkChecks.Count);
        Assert.AreEqual((42, Att.ACCESS_ACTION_LINK, 7, 5), att.LinkChecks[0]);
        Assert.AreEqual(1, links.UnderUpdateCalls.Count);
        Assert.AreEqual(1, links.DeleteUnderUpdateCalls.Count);
        Assert.AreEqual(1, db.Inserts.Count);
        Assert.AreEqual(42, db.Inserts[0]["att_id"].toInt());
        Assert.AreEqual(5, db.Inserts[0]["item_id"].toInt());
        Assert.AreEqual(7, db.Inserts[0]["fwentities_id"].toInt());
    }

    [TestMethod]
    public void AttCheckAccess_RejectsMissingOrInactiveAttachments()
    {
        var fw = createFw();
        var att = new DefaultAccessAtt();
        att.Rows[1] = DB.h("id", 1, "status", FwModel.STATUS_ACTIVE);
        att.Rows[2] = DB.h("id", 2, "status", FwModel.STATUS_DELETED);
        att.init(fw);
        TestHelpers.RegisterModel(fw, (Att)att);
        var links = new TestAttLinks();
        links.init(fw);
        TestHelpers.RegisterModel(fw, (AttLinks)links);

        att.checkAccess(1);
        Assert.ThrowsExactly<AuthException>(() => att.checkAccess(2));
        Assert.ThrowsExactly<AuthException>(() => att.checkAccess(999));
    }

    [TestMethod]
    [DataRow(123, "0")]
    [DataRow(0, "123")]
    public void SignupSaveAction_NonzeroRouteOrPostedIdCannotUpdateExistingUser(int routeId, string postedId)
    {
        var users = new SignupUsers();
        var fw = createSignupFw(users);
        fw.FORM["item"] = signupItem(postedId);
        var controller = new SignupController();
        controller.init(fw);

        Assert.ThrowsExactly<UserException>(() => controller.SaveAction(routeId));

        Assert.IsTrue(users.LastAdded.Count == 0);
        Assert.IsEmpty(users.UpdatedIds);
    }

    [TestMethod]
    public void SignupSaveAction_ValidSignupCreatesUserWithPublicDefaults()
    {
        var users = new SignupUsers();
        var fw = createSignupFw(users);
        fw.FORM["item"] = signupItem("0");
        var controller = new SignupController();
        controller.init(fw);

        Assert.ThrowsExactly<RedirectException>(() => controller.SaveAction());

        Assert.AreEqual("new@example.test", users.LastAdded["email"]);
        Assert.AreEqual(Users.ACL_VISITOR, users.LastAdded["access_level"].toInt());
        Assert.AreEqual(0, users.LastAdded["add_users_id"].toInt());
        Assert.IsFalse(users.UpdatedIds.Contains(123));
    }

    private static FW createDynamicFw(ParentRows parentModel, ChildRowsModel childModel)
    {
        var fw = createFw();
        fw.route.method = "POST";
        parentModel.init(fw);
        childModel.init(fw);
        TestHelpers.RegisterModel(fw, parentModel);
        TestHelpers.RegisterModel(fw, childModel);
        return fw;
    }

    private static FW createSignupFw(SignupUsers users)
    {
        var fw = createFw(new Dictionary<string, string?>
        {
            ["appSettings:IS_SIGNUP"] = "true",
            ["appSettings:LOGGED_DEFAULT_URL"] = "/Main",
            ["appSettings:mail_from"] = "noreply@example.test",
        });
        fw.config()["IS_SIGNUP"] = "true";
        fw.config()["LOGGED_DEFAULT_URL"] = "/Main";
        fw.config()["mail_from"] = "noreply@example.test";
        fw.config()["template"] = Path.Combine(repoRoot(), "osafw-app", "App_Data", "template");
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        return fw;
    }

    private static FW createFw(IDictionary<string, string?>? settings = null)
    {
        var fw = TestHelpers.CreateFw(settings);
        fw.is_log_events = false;
        var users = new StubUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        return fw;
    }

    private static void postSubtableSave(FW fw, int childId, string childName)
    {
        fw.FORM["item"] = DB.h("iname", "Parent 1");
        fw.FORM["item-children"] = DB.h(childId.toStr(), 1);
        fw.FORM["item-children#" + childId] = DB.h("iname", childName);
    }

    private static FwDict signupItem(string id)
    {
        return new FwDict
        {
            ["id"] = id,
            ["email"] = "new@example.test",
            ["pwd"] = "secret123",
            ["pwd2"] = "secret123",
            ["fname"] = "New",
            ["lname"] = "User",
        };
    }

    private static string repoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "osafw-asp.net-core.sln")))
            dir = dir.Parent;

        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
