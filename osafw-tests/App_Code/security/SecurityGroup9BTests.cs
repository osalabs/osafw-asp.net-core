using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace osafw.Tests;

[TestClass]
public class SecurityGroup9BTests
{
    private sealed class AdminPolicyUsers : Users
    {
        private readonly Dictionary<int, DBRow> rows;

        public FwDict? LastAdded { get; private set; }
        public FwDict? LastUpdated { get; private set; }
        public int LastUpdatedId { get; private set; }
        public int DeletedId { get; private set; }
        public int PasswordResetId { get; private set; }

        public AdminPolicyUsers(Dictionary<int, DBRow> rows)
        {
            this.rows = rows;
        }

        public override DBRow one(int id) => rows.TryGetValue(id, out var row) ? row : [];

        public override bool isExists(object uniq_key, int not_id) => false;

        public override bool isReadOnly(int id = -1) => false;

        public override void checkReadOnly(int id = -1) { }

        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];

        public override void convertUserInput(FwDict item) { }

        public override int add(FwDict item)
        {
            LastAdded = new FwDict(item);
            return 70;
        }

        public override bool update(int id, FwDict item)
        {
            LastUpdatedId = id;
            LastUpdated = new FwDict(item);
            return true;
        }

        public override void deleteWithPermanentCheck(int id)
        {
            DeletedId = id;
        }

        public override bool sendPwdReset(int id)
        {
            PasswordResetId = id;
            return true;
        }
    }

    private sealed class TestAdminUsersController : AdminUsersController
    {
        public TestAdminUsersController(FW fw, AdminPolicyUsers users)
        {
            this.fw = fw;
            db = fw.db;
            model = users;
            model0 = users;
            base_url = "/Admin/Users";
            required_fields = "email access_level";
            save_fields = "email pwd access_level fname lname status";
            save_fields_checkboxes = "is_readonly";
            rbac = [];
        }
    }

    private sealed class StubLogTypes : FwLogTypes
    {
        public override DBRow oneByIcode(string icode) => new(new FwDict
        {
            ["id"] = 2,
            ["icode"] = icode,
            ["itype"] = ITYPE_USER
        });
    }

    private sealed class StubEntities : FwEntities
    {
        public override DBRow oneByIcode(string icode) => new(new FwDict
        {
            ["id"] = 3,
            ["icode"] = icode
        });
    }

    private sealed class ActivityPolicyUsers : Users
    {
        public override bool isReadOnly(int id = -1) => false;

        public override void checkReadOnly(int id = -1) { }

        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    private sealed class ActivityPolicyLogs : FwActivityLogs
    {
        public FwDict? LastAdded { get; private set; }

        public override void convertUserInput(FwDict item) { }

        public override int add(FwDict item)
        {
            LastAdded = new FwDict(item);
            return 88;
        }
    }

    private sealed class CommentTargetDemos : Demos
    {
        private readonly bool allowAccess;

        public string LastAction { get; private set; } = "";

        public CommentTargetDemos(bool allowAccess)
        {
            this.allowAccess = allowAccess;
        }

        public override DBRow one(int id) => id == 5
            ? new DBRow(new FwDict { ["id"] = 5, ["iname"] = "demo" })
            : [];

        public override void checkAccess(int id = 0, string action = "")
        {
            LastAction = action;
            if (!allowAccess)
                throw new AuthException("denied");
        }
    }

    private sealed class DefaultCommentDemos : Demos
    {
        public override DBRow one(int id) => id == 5
            ? new DBRow(new FwDict { ["id"] = 5, ["iname"] = "demo" })
            : [];
    }

    private sealed class DefaultCommentDemoDicts : DemoDicts
    {
        public override DBRow one(int id) => id == 5
            ? new DBRow(new FwDict { ["id"] = 5, ["iname"] = "demo dict" })
            : [];
    }

    private sealed class TestActivityLogsController : AdminActivityLogsController
    {
        public TestActivityLogsController(FW fw, ActivityPolicyLogs logs)
        {
            this.fw = fw;
            db = fw.db;
            model = logs;
            model0 = logs;
            base_url = "/Admin/ActivityLogs";
            required_fields = "log_type entity item_id";
            save_fields = "reply_id item_id idate idesc";
            rbac = [];
        }
    }

    [TestMethod]
    public void AdminUsers_LowerAdminCannotSaveEqualUser()
    {
        var (fw, users, controller) = buildAdminUsersController(Users.ACL_ADMIN, userRow(20, Users.ACL_ADMIN));
        fw.FORM["item"] = saveUserForm(Users.ACL_MANAGER);

        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction(20));

        Assert.IsNull(users.LastUpdated);
    }

    [TestMethod]
    public void AdminUsers_LowerAdminCannotElevateLowerUserToEqualAccess()
    {
        var (fw, users, controller) = buildAdminUsersController(Users.ACL_ADMIN, userRow(20, Users.ACL_MANAGER));
        fw.FORM["item"] = saveUserForm(Users.ACL_ADMIN);

        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction(20));

        Assert.IsNull(users.LastUpdated);
    }

    [TestMethod]
    public void AdminUsers_LowerAdminCanSaveLowerUserBelowOwnAccess()
    {
        var (fw, users, controller) = buildAdminUsersController(Users.ACL_ADMIN, userRow(20, Users.ACL_MANAGER));
        fw.FORM["item"] = saveUserForm(Users.ACL_MANAGER);

        var ps = controller.SaveAction(20)!;

        Assert.AreEqual(20, users.LastUpdatedId);
        Assert.AreEqual(Users.ACL_MANAGER, users.LastUpdated!["access_level"].toInt());
        var json = ps["_json"] as FwDict ?? [];
        Assert.IsTrue(json["success"].toBool());
    }

    [TestMethod]
    public void AdminUsers_LowerAdminCannotDeleteEqualUser()
    {
        var (_, users, controller) = buildAdminUsersController(Users.ACL_ADMIN, userRow(20, Users.ACL_ADMIN));

        Assert.ThrowsExactly<AuthException>(() => controller.DeleteAction(20));

        Assert.AreEqual(0, users.DeletedId);
    }

    [TestMethod]
    public void AdminUsers_LowerAdminCannotBulkDeleteEqualUser()
    {
        var (fw, users, controller) = buildAdminUsersController(Users.ACL_ADMIN, userRow(20, Users.ACL_ADMIN));
        fw.FORM["delete"] = "1";
        fw.FORM["cb"] = new FwDict { ["20"] = "1" };

        Assert.ThrowsExactly<AuthException>(() => controller.SaveMultiAction());

        Assert.AreEqual(0, users.DeletedId);
    }

    [TestMethod]
    public void AdminUsers_LowerAdminCannotSendPasswordResetForHigherUser()
    {
        var (_, users, controller) = buildAdminUsersController(Users.ACL_ADMIN, userRow(20, Users.ACL_SITEADMIN));

        Assert.ThrowsExactly<AuthException>(() => controller.SendPwdAction(20));

        Assert.AreEqual(0, users.PasswordResetId);
    }

    [TestMethod]
    public void AdminUsers_LowerAdminCannotResetMfaForEqualUser()
    {
        var (_, users, controller) = buildAdminUsersController(Users.ACL_ADMIN, userRow(20, Users.ACL_ADMIN));

        Assert.ThrowsExactly<AuthException>(() => controller.ResetMFAAction(20));

        Assert.IsNull(users.LastUpdated);
    }

    [TestMethod]
    public void AdminUsers_LowerAdminCannotRestoreEqualUser()
    {
        var (_, users, controller) = buildAdminUsersController(Users.ACL_ADMIN, userRow(20, Users.ACL_ADMIN));

        Assert.ThrowsExactly<AuthException>(() => controller.RestoreDeletedAction(20));

        Assert.IsNull(users.LastUpdated);
    }

    [TestMethod]
    public void ActivityLogs_ForgedUsersIdIsIgnored()
    {
        var (fw, logs, controller, target) = buildActivityController(allowTargetAccess: true, usersId: 12);
        fw.FORM["item"] = commentForm(usersId: 999);

        controller.SaveAction();

        Assert.AreEqual(12, logs.LastAdded!["users_id"].toInt());
        Assert.AreEqual(Permissions.PERMISSION_VIEW, target.LastAction);
    }

    [TestMethod]
    public void ActivityLogs_VisitorAttributionStoresNullUser()
    {
        var (fw, logs, controller, _) = buildActivityController(allowTargetAccess: true, usersId: 0);
        fw.FORM["item"] = commentForm(usersId: 999);

        controller.SaveAction();

        Assert.IsTrue(logs.LastAdded!.ContainsKey("users_id"));
        Assert.IsNull(logs.LastAdded["users_id"]);
    }

    [TestMethod]
    public void ActivityLogs_UnauthorizedObjectCommentIsRejected()
    {
        var (fw, logs, controller, target) = buildActivityController(allowTargetAccess: false, usersId: 12);
        fw.FORM["item"] = commentForm();

        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction());

        Assert.IsNull(logs.LastAdded);
        Assert.AreEqual(Permissions.PERMISSION_VIEW, target.LastAction);
    }

    [TestMethod]
    public void ActivityLogs_ExistingLogRowsCannotBeUpdated()
    {
        var (fw, logs, controller, _) = buildActivityController(allowTargetAccess: true, usersId: 12);
        fw.FORM["item"] = commentForm();

        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction(77));

        Assert.IsNull(logs.LastAdded);
    }

    [TestMethod]
    public void ActivityLogs_DefaultDemoEntityAllowsCommentsWithoutModelAccessHook()
    {
        var (fw, logs, controller) = buildActivityControllerWithDefaultDemo(usersId: 12);
        fw.FORM["item"] = commentForm();

        controller.SaveAction();

        Assert.AreEqual(12, logs.LastAdded!["users_id"].toInt());
        Assert.AreEqual(5, logs.LastAdded["item_id"].toInt());
    }

    [TestMethod]
    public void ActivityLogs_ModelWithoutAccessHookIsRejectedUnlessEntityIsAllowlisted()
    {
        var (fw, logs, controller) = buildActivityControllerWithDefaultDemoDicts(usersId: 12);
        fw.FORM["item"] = commentForm(entity: "demo_dicts");

        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction());

        Assert.IsNull(logs.LastAdded);
    }

    [TestMethod]
    public void DataProtectionKeyProtection_DefaultConstDisallowsPlaintextFallback()
    {
        var field = typeof(Program).GetField("ALLOW_PLAINTEXT_DP_KEYS", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertFailedException("Expected ALLOW_PLAINTEXT_DP_KEYS constant");

        Assert.AreEqual(false, field.GetRawConstantValue());
    }

    [TestMethod]
    public void DataProtectionKeyProtection_StartupUsesDpapiOrFailsClosedInline()
    {
        var programPath = findRepoFile("osafw-app", "Program.cs");
        var source = File.ReadAllText(programPath);

        StringAssert.Contains(source, "AddDataProtection().SetApplicationName(appName)");
        StringAssert.Contains(source, "ProtectKeysWithDpapi(protectToLocalMachine: true)");
        StringAssert.Contains(source, "else if (!ALLOW_PLAINTEXT_DP_KEYS)");
        StringAssert.Contains(source, "throw new ApplicationException(\"Data Protection key encryption requires Windows DPAPI");
        Assert.IsFalse(source.Contains("ConfigureDataProtectionKeyProtection", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ErrorHandling_DeveloperExceptionPageRequiresFrameworkDevMode()
    {
        var programPath = findRepoFile("osafw-app", "Program.cs");
        var source = File.ReadAllText(programPath);

        StringAssert.Contains(source, "if (isDevelopmentEnv)");
        StringAssert.Contains(source, "app.UseDeveloperExceptionPage();");
        StringAssert.Contains(source, "app.UseExceptionHandler(errorApp =>");
        Assert.IsFalse(source.Contains("app.Environment.IsDevelopment()", StringComparison.Ordinal));
    }

    private static (FW fw, AdminPolicyUsers users, TestAdminUsersController controller) buildAdminUsersController(int currentAccessLevel, DBRow target)
    {
        var fw = TestHelpers.CreateFw();
        fw.request.Headers.Accept = "application/json";
        fw.route.method = "POST";
        fw.Session("user_id", "10");
        fw.Session("access_level", currentAccessLevel.toStr());
        var users = new AdminPolicyUsers(new Dictionary<int, DBRow>
        {
            [target["id"].toInt()] = target
        });
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        var controller = new TestAdminUsersController(fw, users);
        return (fw, users, controller);
    }

    private static DBRow userRow(int id, int accessLevel) => new(new FwDict
    {
        ["id"] = id,
        ["email"] = $"user{id}@example.test",
        ["access_level"] = accessLevel,
        ["status"] = Users.STATUS_ACTIVE,
        ["is_readonly"] = 0
    });

    private static FwDict saveUserForm(int accessLevel) => new()
    {
        ["ehack"] = "target@example.test",
        ["access_level"] = accessLevel,
        ["fname"] = "Target",
        ["lname"] = "User",
        ["pwd"] = ""
    };

    private static (FW fw, ActivityPolicyLogs logs, TestActivityLogsController controller, CommentTargetDemos target) buildActivityController(bool allowTargetAccess, int usersId)
    {
        var (fw, logs, controller) = buildActivityControllerBase(usersId);

        var target = new CommentTargetDemos(allowTargetAccess);
        target.init(fw);
        TestHelpers.RegisterModel(fw, (Demos)target);

        return (fw, logs, controller, target);
    }

    private static (FW fw, ActivityPolicyLogs logs, TestActivityLogsController controller) buildActivityControllerWithDefaultDemo(int usersId)
    {
        var (fw, logs, controller) = buildActivityControllerBase(usersId);
        var target = new DefaultCommentDemos();
        target.init(fw);
        TestHelpers.RegisterModel(fw, (Demos)target);
        return (fw, logs, controller);
    }

    private static (FW fw, ActivityPolicyLogs logs, TestActivityLogsController controller) buildActivityControllerWithDefaultDemoDicts(int usersId)
    {
        var (fw, logs, controller) = buildActivityControllerBase(usersId);
        var target = new DefaultCommentDemoDicts();
        target.init(fw);
        TestHelpers.RegisterModel(fw, (DemoDicts)target);
        return (fw, logs, controller);
    }

    private static (FW fw, ActivityPolicyLogs logs, TestActivityLogsController controller) buildActivityControllerBase(int usersId)
    {
        var fw = TestHelpers.CreateFw();
        fw.request.Headers.Accept = "application/json";
        fw.route.method = "POST";
        fw.Session("access_level", Users.ACL_MANAGER.toStr());
        if (usersId > 0)
            fw.Session("user_id", usersId.toStr());

        var users = new ActivityPolicyUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        TestHelpers.RegisterModel(fw, (FwLogTypes)new StubLogTypes());
        TestHelpers.RegisterModel(fw, (FwEntities)new StubEntities());

        var logs = new ActivityPolicyLogs();
        logs.init(fw);
        var controller = new TestActivityLogsController(fw, logs);
        return (fw, logs, controller);
    }

    private static FwDict commentForm(int usersId = 0, string entity = "demos") => new()
    {
        ["log_type"] = FwLogTypes.ICODE_COMMENT,
        ["entity"] = entity,
        ["item_id"] = 5,
        ["idesc"] = "hello",
        ["users_id"] = usersId
    };

    private static string findRepoFile(params string[] relativeParts)
    {
        var relativePath = Path.Combine(relativeParts);
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not find repo file", relativePath);
    }
}
