using Microsoft.VisualStudio.TestTools.UnitTesting;
using osafw;
using System.Collections;

namespace osafw.Tests;

[TestClass]
public class UserOwnedPreferencesSecurityTests
{
    private sealed class StubUsers : Users
    {
        private readonly int accessLevel;

        public StubUsers(int accessLevel)
        {
            this.accessLevel = accessLevel;
        }

        public override void checkReadOnly(int id = -1) { }
        public override bool isReadOnly(int id = -1) => false;
        public override bool isAccessLevel(int min_acl) => accessLevel >= min_acl;
        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    private sealed class WriteDb : DB
    {
        public int InsertCalls { get; private set; }
        public int UpdateCalls { get; private set; }

        public WriteDb() : base("", DBTYPE_SQLSRV) { }

        public override int insert(string table, IDictionary fields)
        {
            InsertCalls++;
            return 101;
        }

        public override int insert(string table, FwDict fields)
        {
            InsertCalls++;
            return 101;
        }

        public override int update(string table, IDictionary fields, IDictionary where)
        {
            UpdateCalls++;
            return 1;
        }

        public override int update(string table, FwDict fields, FwDict where)
        {
            UpdateCalls++;
            return 1;
        }

        public override object? value(string table, FwDict where, string field_name = "", string order_by = "")
        {
            return "";
        }
    }

    private sealed class PolicyUserViews : UserViews
    {
        private readonly DBRow row;

        public WriteDb WriteDb { get; } = new();

        public PolicyUserViews(DBRow row)
        {
            this.row = row;
        }

        public void InitForTest(FW fw)
        {
            init(fw);
            db = WriteDb;
        }

        public override DBRow oneAvail(int id) => row;
    }

    private sealed class PolicyUserFilters : UserFilters
    {
        private readonly DBRow row;

        public WriteDb WriteDb { get; } = new();

        public PolicyUserFilters(DBRow row)
        {
            this.row = row;
        }

        public void InitForTest(FW fw)
        {
            init(fw);
            db = WriteDb;
        }

        public override DBRow oneAvail(int id) => row;
    }

    private sealed class PolicyUserLists : UserLists
    {
        private readonly DBRow row;
        private readonly DBRow itemRow;

        public WriteDb WriteDb { get; } = new();

        public PolicyUserLists(DBRow row, DBRow? itemRow = null)
        {
            this.row = row;
            this.itemRow = itemRow ?? [];
        }

        public void InitForTest(FW fw)
        {
            init(fw);
            db = WriteDb;
        }

        public override DBRow oneMine(int id) => row;

        protected override DBRow oneItemMine(int id) => itemRow;
    }

    private sealed class FilterLookupModel : UserFilters
    {
        private readonly DBRow row;

        public int OneAvailCalls { get; private set; }

        public FilterLookupModel(DBRow row)
        {
            this.row = row;
        }

        public override DBRow oneAvail(int id)
        {
            OneAvailCalls++;
            return row;
        }
    }

    private sealed class TestFilterController : FwController
    {
        public TestFilterController(FW fw) : base(fw) { }

        public FwDict InitFilterForTest(string sessionKey) => initFilter(sessionKey);
    }

    [TestMethod]
    public void UserViews_UpdateOtherUserViewFailsBeforeWrite()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserViews([]);
        model.InitForTest(fw);

        Assert.ThrowsExactly<NotFoundException>(() => model.update(7, DB.h("iname", "x")));

        Assert.AreEqual(0, model.WriteDb.UpdateCalls);
    }

    [TestMethod]
    public void UserViews_OneOtherUserViewReturnsEmpty()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserViews([]);
        model.InitForTest(fw);

        var row = model.one(7);

        Assert.AreEqual(0, row.Count);
    }

    [TestMethod]
    public void UserViews_DeleteOtherUserViewFails()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserViews([]);
        model.InitForTest(fw);

        Assert.ThrowsExactly<NotFoundException>(() => model.delete(7));
    }

    [TestMethod]
    public void UserViews_NonAdminSystemUpdateFailsBeforeWrite()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserViews(savedPreferenceRow(7, addUsersId: 1, isSystem: true));
        model.InitForTest(fw);

        Assert.ThrowsExactly<AuthException>(() => model.update(7, DB.h("iname", "x")));

        Assert.AreEqual(0, model.WriteDb.UpdateCalls);
    }

    [TestMethod]
    public void UserViews_NonAdminSystemDeleteFails()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserViews(savedPreferenceRow(7, addUsersId: 1, isSystem: true));
        model.InitForTest(fw);

        Assert.ThrowsExactly<AuthException>(() => model.delete(7));
    }

    [TestMethod]
    public void UserViews_AdminSystemUpdateWrites()
    {
        var fw = createFw(Users.ACL_ADMIN);
        var model = new PolicyUserViews(savedPreferenceRow(7, addUsersId: 1, isSystem: true));
        model.InitForTest(fw);

        model.update(7, DB.h("iname", "x"));

        Assert.AreEqual(1, model.WriteDb.UpdateCalls);
    }

    [TestMethod]
    public void UserViews_NonAdminCannotCreateSystemView()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserViews([]);
        model.InitForTest(fw);

        Assert.ThrowsExactly<AuthException>(() => model.add(DB.h("iname", "x", "is_system", 1)));

        Assert.AreEqual(0, model.WriteDb.InsertCalls);
    }

    [TestMethod]
    public void UserFilters_UpdateOtherUserFilterFailsBeforeWrite()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserFilters([]);
        model.InitForTest(fw);

        Assert.ThrowsExactly<NotFoundException>(() => model.update(8, DB.h("iname", "x")));

        Assert.AreEqual(0, model.WriteDb.UpdateCalls);
    }

    [TestMethod]
    public void UserFilters_OneOtherUserFilterReturnsEmpty()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserFilters([]);
        model.InitForTest(fw);

        var row = model.one(8);

        Assert.AreEqual(0, row.Count);
    }

    [TestMethod]
    public void UserFilters_DeleteOtherUserFilterFails()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserFilters([]);
        model.InitForTest(fw);

        Assert.ThrowsExactly<NotFoundException>(() => model.delete(8));
    }

    [TestMethod]
    public void UserFilters_NonAdminSystemUpdateFailsBeforeWrite()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserFilters(savedPreferenceRow(8, addUsersId: 1, isSystem: true));
        model.InitForTest(fw);

        Assert.ThrowsExactly<AuthException>(() => model.update(8, DB.h("iname", "x")));

        Assert.AreEqual(0, model.WriteDb.UpdateCalls);
    }

    [TestMethod]
    public void UserFilters_NonAdminSystemDeleteFails()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserFilters(savedPreferenceRow(8, addUsersId: 1, isSystem: true));
        model.InitForTest(fw);

        Assert.ThrowsExactly<AuthException>(() => model.delete(8));
    }

    [TestMethod]
    public void UserFilters_AdminSystemUpdateWrites()
    {
        var fw = createFw(Users.ACL_ADMIN);
        var model = new PolicyUserFilters(savedPreferenceRow(8, addUsersId: 1, isSystem: true));
        model.InitForTest(fw);

        model.update(8, DB.h("iname", "x"));

        Assert.AreEqual(1, model.WriteDb.UpdateCalls);
    }

    [TestMethod]
    public void UserLists_UpdateOtherUserListFailsBeforeWrite()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserLists([]);
        model.InitForTest(fw);

        Assert.ThrowsExactly<NotFoundException>(() => model.update(9, DB.h("iname", "x")));

        Assert.AreEqual(0, model.WriteDb.UpdateCalls);
    }

    [TestMethod]
    public void UserLists_OneOtherUserListReturnsEmpty()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserLists([]);
        model.InitForTest(fw);

        var row = model.one(9);

        Assert.AreEqual(0, row.Count);
    }

    [TestMethod]
    public void UserLists_DeleteOtherUserListFails()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserLists([]);
        model.InitForTest(fw);

        Assert.ThrowsExactly<NotFoundException>(() => model.delete(9));
    }

    [TestMethod]
    public void UserLists_OwnerUpdateWrites()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserLists(savedListRow(9, addUsersId: 9));
        model.InitForTest(fw);

        model.update(9, DB.h("iname", "x"));

        Assert.AreEqual(1, model.WriteDb.UpdateCalls);
    }

    [TestMethod]
    public void UserLists_AddItemOtherUserListFailsBeforeWrite()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserLists([]);
        model.InitForTest(fw);

        Assert.ThrowsExactly<NotFoundException>(() => model.addItems(9, 77));

        Assert.AreEqual(0, model.WriteDb.InsertCalls);
    }

    [TestMethod]
    public void UserLists_DeleteItemOtherUserListFails()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new PolicyUserLists(savedListRow(9, addUsersId: 9), []);
        model.InitForTest(fw);

        Assert.ThrowsExactly<NotFoundException>(() => model.deleteItems(21));
    }

    [TestMethod]
    public void InitFilter_UnauthorizedExplicitSavedFilterClearsSessionId()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new FilterLookupModel([]);
        model.init(fw);
        TestHelpers.RegisterModel(fw, (UserFilters)model);
        fw.FORM["dofilter"] = "1";
        fw.FORM["userfilters_id"] = "12";
        var controller = new TestFilterController(fw);

        var filter = controller.InitFilterForTest("filter:test");

        Assert.AreEqual(1, model.OneAvailCalls);
        Assert.IsFalse(filter.ContainsKey("userfilters_id"));
        Assert.IsFalse((fw.SessionDict("filter:test") ?? []).ContainsKey("userfilters_id"));
    }

    [TestMethod]
    public void InitFilter_AuthorizedOwnerSavedFilterAppliesAndStoresMetadata()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new FilterLookupModel(savedFilterRow(12, addUsersId: 9, isSystem: false, new FwDict { ["s"] = "mine" }));
        model.init(fw);
        TestHelpers.RegisterModel(fw, (UserFilters)model);
        fw.FORM["dofilter"] = "1";
        fw.FORM["userfilters_id"] = "12";
        var controller = new TestFilterController(fw);

        var filter = controller.InitFilterForTest("filter:test");

        Assert.AreEqual("mine", filter["s"]);
        Assert.AreEqual(12, filter["userfilters_id"]);
        Assert.IsTrue(filter.ContainsKey("userfilter"));
    }

    [TestMethod]
    public void InitFilter_AuthorizedOwnerSavedFilterRestoresSplitSearchPayload()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var search = new FwDict
        {
            ["name"] = Utils.jsonEncode(new FwDict { ["type"] = "text", ["op"] = "starts_with", ["value"] = "Acme" }),
        };
        var model = new FilterLookupModel(savedFilterRow(12, addUsersId: 9, isSystem: false, new FwDict
        {
            ["f"] = new FwDict { ["s"] = "mine" },
            ["search"] = search,
        }));
        model.init(fw);
        TestHelpers.RegisterModel(fw, (UserFilters)model);
        fw.FORM["dofilter"] = "1";
        fw.FORM["userfilters_id"] = "12";
        var controller = new TestFilterController(fw);

        var filter = controller.InitFilterForTest("filter:test");
        var restoredSearch = fw.SessionDict("_filtersearch_AdminDemos.Index") ?? [];

        Assert.AreEqual("mine", filter["s"]);
        Assert.AreEqual(12, filter["userfilters_id"]);
        Assert.AreEqual(search["name"], restoredSearch["name"]);
    }

    [TestMethod]
    public void InitFilter_SystemSavedFilterAppliesButClearsEditableMetadata()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new FilterLookupModel(savedFilterRow(12, addUsersId: 1, isSystem: true, new FwDict
        {
            ["s"] = "system",
            ["userfilters_id"] = 999,
            ["userfilter"] = new FwDict { ["id"] = 999 },
        }));
        model.init(fw);
        TestHelpers.RegisterModel(fw, (UserFilters)model);
        fw.FORM["dofilter"] = "1";
        fw.FORM["userfilters_id"] = "12";
        var controller = new TestFilterController(fw);

        var filter = controller.InitFilterForTest("filter:test");

        Assert.AreEqual("system", filter["s"]);
        Assert.IsFalse(filter.ContainsKey("userfilters_id"));
        Assert.IsFalse(filter.ContainsKey("userfilter"));
    }

    [TestMethod]
    public void InitFilter_NormalRequestClearsStaleMetadataWithoutDbLookup()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new FilterLookupModel(savedFilterRow(12, addUsersId: 9, isSystem: false, new FwDict { ["s"] = "mine" }));
        model.init(fw);
        TestHelpers.RegisterModel(fw, (UserFilters)model);
        fw.SessionDict("filter:test", new FwDict
        {
            ["userfilters_id"] = 12,
            ["userfilter"] = new FwDict { ["is_system"] = 0, ["add_users_id"] = 17 },
        });
        var controller = new TestFilterController(fw);

        var filter = controller.InitFilterForTest("filter:test");

        Assert.AreEqual(0, model.OneAvailCalls);
        Assert.IsFalse(filter.ContainsKey("userfilters_id"));
        Assert.IsFalse(filter.ContainsKey("userfilter"));
    }

    [TestMethod]
    public void InitFilter_NormalRequestClearsBareSavedFilterIdWithoutDbLookup()
    {
        var fw = createFw(Users.ACL_MEMBER);
        var model = new FilterLookupModel(savedFilterRow(12, addUsersId: 9, isSystem: false, new FwDict { ["s"] = "mine" }));
        model.init(fw);
        TestHelpers.RegisterModel(fw, (UserFilters)model);
        fw.SessionDict("filter:test", new FwDict { ["userfilters_id"] = 12 });
        var controller = new TestFilterController(fw);

        var filter = controller.InitFilterForTest("filter:test");

        Assert.AreEqual(0, model.OneAvailCalls);
        Assert.IsFalse(filter.ContainsKey("userfilters_id"));
    }

    private static FW createFw(int accessLevel)
    {
        var fw = TestHelpers.CreateFw();
        fw.Session("user_id", "9");
        fw.Session("access_level", accessLevel.toStr());
        fw.request.Headers.Accept = "application/json";
        fw.route.method = "POST";
        fw.G["controller.action"] = "AdminDemos.Index";
        TestHelpers.RegisterModel(fw, (Users)new StubUsers(accessLevel));
        return fw;
    }

    private static DBRow savedPreferenceRow(int id, int addUsersId, bool isSystem) => new()
    {
        ["id"] = id.toStr(),
        ["icode"] = "/Admin/Demos",
        ["iname"] = "Saved",
        ["is_system"] = isSystem ? "1" : "0",
        ["add_users_id"] = addUsersId.toStr(),
        ["status"] = FwModel.STATUS_ACTIVE.toStr(),
    };

    private static DBRow savedListRow(int id, int addUsersId) => new()
    {
        ["id"] = id.toStr(),
        ["entity"] = "/Admin/Demos",
        ["iname"] = "List",
        ["add_users_id"] = addUsersId.toStr(),
        ["status"] = FwModel.STATUS_ACTIVE.toStr(),
    };

    private static DBRow savedFilterRow(int id, int addUsersId, bool isSystem, FwDict filter) => new()
    {
        ["id"] = id.toStr(),
        ["icode"] = "AdminDemos.Index",
        ["iname"] = "Filter",
        ["idesc"] = Utils.jsonEncode(filter),
        ["is_system"] = isSystem ? "1" : "0",
        ["add_users_id"] = addUsersId.toStr(),
        ["status"] = FwModel.STATUS_ACTIVE.toStr(),
    };
}
