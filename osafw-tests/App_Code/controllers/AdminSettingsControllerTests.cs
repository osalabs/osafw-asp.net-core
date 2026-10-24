using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if isSQLite
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System.Text.Json;
#endif

namespace osafw.Tests;

[TestClass]
public class AdminSettingsControllerTests
{
    private class StubSettings : Settings
    {
        public Dictionary<int, FwDict> Rows { get; } = [];
        public int UpdateCalls { get; private set; }
        public int LastUpdateId { get; private set; }
        public FwDict LastUpdate { get; private set; } = [];

        public override DBRow one(int id)
        {
            return Rows.TryGetValue(id, out FwDict? row) ? new DBRow(row) : [];
        }

        public override DBRow oneByIcode(string icode)
        {
            foreach (var row in Rows.Values)
                if (row["icode"].toStr() == icode)
                    return new DBRow(row);
            return [];
        }

        public override bool update(int id, FwDict item)
        {
            UpdateCalls++;
            LastUpdateId = id;
            LastUpdate = new FwDict(item);

            if (!Rows.ContainsKey(id))
                Rows[id] = [];

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
            return DB.h(
                Permissions.PERMISSION_LIST, true,
                Permissions.PERMISSION_VIEW, true,
                Permissions.PERMISSION_ADD, true,
                Permissions.PERMISSION_EDIT, true,
                Permissions.PERMISSION_DELETE, true);
        }
    }

    private sealed class TestAdminSettingsController : AdminSettingsController
    {
        public void MakeReadOnly()
        {
            is_readonly = true;
        }
    }

    private static (FW fw, StubSettings model, TestAdminSettingsController controller) BuildController(
        FwDict setting,
        FwDict item,
        FwDict? extraForm = null,
        int accessLevel = Users.ACL_SITEADMIN)
    {
        var fw = TestHelpers.CreateFw();
        fw.request.Headers.Accept = "application/json";
        fw.route.method = "POST";
        fw.Session("access_level", accessLevel.ToString());
        fw.Session("XSS", "token");

        var model = new StubSettings();
        model.Rows[1] = setting;
        model.init(fw);
        TestHelpers.RegisterModel(fw, (Settings)model);
        var users = new StubUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);

        var controller = new TestAdminSettingsController();
        controller.init(fw);

        fw.FORM = new FwDict
        {
            ["item"] = item,
            ["XSS"] = "token",
        };

        if (extraForm != null)
        {
            foreach (var entry in extraForm)
                fw.FORM[entry.Key] = entry.Value;
        }

        return (fw, model, controller);
    }

    private static FwDict Setting(
        int input,
        string value = "",
        string allowedValues = "",
        int accessLevel = Users.ACL_ADMIN,
        int mask = Settings.MASK_NONE,
        int basis = Settings.BASIS_VALUE,
        bool isUserEdit = true,
        string code = "test_setting")
    {
        return new FwDict
        {
            ["id"] = "1",
            ["icode"] = code,
            ["input"] = input.toStr(),
            ["ivalue"] = value,
            ["allowed_values"] = allowedValues,
            ["access_level"] = accessLevel,
            ["mask"] = input == Settings.INPUT_CREDENTIAL && mask == Settings.MASK_NONE ? Settings.MASK_HIDDEN : mask,
            ["basis"] = basis,
            ["is_user_edit"] = isUserEdit,
        };
    }

    private static void AssertValidationFailure(Action action)
    {
        try
        {
            action();
            Assert.Fail("Expected ValidationException");
        }
        catch (ValidationException)
        {
        }
    }

    [TestMethod]
    public void ShowForm_CredentialNeverReturnsRawValue()
    {
        var (fw, _, controller) = BuildController(
            Setting(Settings.INPUT_CREDENTIAL, "submitted-secret", code: "OPENAI_API_KEY"),
            new FwDict { ["ivalue"] = "submitted-secret" });
        fw.route.method = "POST";

        var ps = controller.ShowFormAction(1);
        var item = (FwDict)ps["i"]!;

        Assert.AreEqual("", item["ivalue"]);
        Assert.IsFalse(item.Values.Any(value => value.toStr().Contains("submitted-secret", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ShowForm_MaskedOrdinaryValueUsesProtectedEditorProjection()
    {
        var (fw, _, controller) = BuildController(
            Setting(Settings.INPUT_TEXT, "private-value", mask: Settings.MASK_SUFFIX),
            []);
        fw.route.method = "GET";

        var ps = controller.ShowFormAction(1);
        var item = (FwDict)ps["i"]!;

        Assert.AreEqual("", item["ivalue"]);
        Assert.AreEqual(Settings.INPUT_CREDENTIAL, ps["editor_input"].toInt());
        Assert.AreEqual("***alue", item["ivalue_display"]);
    }

    [TestMethod]
    public void Credential_BlankSubmissionKeepsExistingValue()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_CREDENTIAL, "ABCDEF-secret-XYZ123"),
            new FwDict { ["ivalue"] = "" });

        controller.SaveAction(1);

        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("ABCDEF-secret-XYZ123", model.Rows[1]["ivalue"]);
    }

    [TestMethod]
    public void SaveAction_ReadOnlyAdminCannotUpdateSetting()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_TEXT, "old"),
            new FwDict { ["ivalue"] = "new" });
        controller.MakeReadOnly();

        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction(1));

        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("old", model.Rows[1]["ivalue"]);
    }

    [TestMethod]
    public void SaveAction_RejectsGetBeforeUpdating()
    {
        var (fw, model, controller) = BuildController(
            Setting(Settings.INPUT_TEXT, "old"),
            new FwDict { ["ivalue"] = "new" });
        fw.route.method = "GET";

        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
    }

    [TestMethod]
    public void SaveAction_RejectsWrongXssTokenBeforeUpdating()
    {
        var (fw, model, controller) = BuildController(
            Setting(Settings.INPUT_TEXT, "old"),
            new FwDict { ["ivalue"] = "new" });
        fw.FORM["XSS"] = "wrong";

        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
    }

    [TestMethod]
    public void SaveAction_AdminCannotEditSiteAdminRow()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_TEXT, "old", accessLevel: Users.ACL_SITEADMIN),
            new FwDict { ["ivalue"] = "new" },
            accessLevel: Users.ACL_ADMIN);

        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
    }

    [TestMethod]
    public void SaveAction_CredentialAlwaysRequiresSiteAdmin()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_CREDENTIAL, "old", accessLevel: Users.ACL_ADMIN, code: "OPENAI_API_KEY"),
            new FwDict { ["ivalue"] = "new" },
            accessLevel: Users.ACL_ADMIN);

        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
    }

    [TestMethod]
    public void SaveAction_RejectsNonEditableRow()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_TEXT, "old", isUserEdit: false),
            new FwDict { ["ivalue"] = "new" });

        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
    }

    [TestMethod]
    public void Credential_NewSubmissionReplacesExistingValue()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_CREDENTIAL, "old-secret"),
            new FwDict { ["ivalue"] = "new-secret" });

        controller.SaveAction(1);

        Assert.AreEqual(1, model.UpdateCalls);
        Assert.AreEqual("new-secret", model.LastUpdate["ivalue"]);
    }

    [TestMethod]
    public void Credential_ClearIsExplicitEmptyValue()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_CREDENTIAL, "old-secret"),
            new FwDict { ["ivalue"] = "", ["clear"] = "1", ["basis"] = Settings.BASIS_VALUE });

        controller.SaveAction(1);

        Assert.AreEqual(1, model.UpdateCalls);
        Assert.AreEqual(Settings.BASIS_VALUE, model.LastUpdate["basis"].toInt());
        Assert.AreEqual("", model.LastUpdate["ivalue"]);
    }

    [TestMethod]
    public void Credential_UseDefaultStoresInheritanceWithoutSubmittedSecret()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_CREDENTIAL, "old-secret"),
            new FwDict { ["ivalue"] = "must-not-save", ["basis"] = Settings.BASIS_INHERIT });

        controller.SaveAction(1);

        Assert.AreEqual(1, model.UpdateCalls);
        Assert.AreEqual(Settings.BASIS_INHERIT, model.LastUpdate["basis"].toInt());
        Assert.IsFalse(model.LastUpdate.ContainsKey("ivalue"));
    }

    [TestMethod]
    public void Credential_InheritedValueCannotBecomeExplicitWithoutValueOrClear()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_CREDENTIAL, "", basis: Settings.BASIS_INHERIT),
            new FwDict { ["ivalue"] = "", ["basis"] = Settings.BASIS_VALUE });

        Assert.ThrowsExactly<UserException>(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
    }

    [TestMethod]
    public void RevealAction_RejectsHiddenPolicy()
    {
        var (_, _, controller) = BuildController(
            Setting(Settings.INPUT_CREDENTIAL, "old-secret", mask: Settings.MASK_HIDDEN),
            []);

        Assert.ThrowsExactly<AuthException>(() => controller.RevealAction(1));
    }

#if isSQLite
    [TestMethod]
    public void RevealAction_ThroughFrameworkDispatchReturnsValueAndFinalNoStoreHeader()
    {
        using var fixture = new TransferFixture();
        int secretId = fixture.Seed("OPENAI_API_KEY", Settings.INPUT_CREDENTIAL, mask: Settings.MASK_REVEAL);
        fixture.Model.update(secretId, DB.h("ivalue", "revealed-secret", "basis", Settings.BASIS_VALUE));
        fixture.Fw.route.controller = "AdminSettings";
        fixture.Fw.route.action = "Reveal";
        fixture.Fw.route.method = "POST";
        fixture.Fw.route.id = secretId.ToString();
        fixture.Fw.request.Headers.Accept = "application/json";
        var action = typeof(AdminSettingsController).GetMethod(nameof(AdminSettingsController.RevealAction))!;

        fixture.Fw.callController(fixture.Controller, action, [secretId]);

        fixture.Fw.response.Body.Position = 0;
        using var document = JsonDocument.Parse(fixture.Fw.response.Body);
        Assert.AreEqual("revealed-secret", document.RootElement.GetProperty("value").GetString());
        Assert.AreEqual("no-store", fixture.Fw.response.Headers.CacheControl.ToString());
        Assert.AreEqual("no-cache", fixture.Fw.response.Headers.Pragma.ToString());
    }
#endif

    [TestMethod]
    public void ShowAction_AdminCannotReadSiteAdminRowById()
    {
        var (_, _, controller) = BuildController(
            Setting(Settings.INPUT_TEXT, "sensitive", accessLevel: Users.ACL_SITEADMIN),
            [],
            accessLevel: Users.ACL_ADMIN);

        Assert.ThrowsExactly<AuthException>(() => controller.ShowAction(1));
    }

    [TestMethod]
    public void CheckAccess_BlocksInheritedQuickSearch()
    {
        var (fw, _, controller) = BuildController(Setting(Settings.INPUT_TEXT), []);
        fw.route.action = "QuickSearch";

        Assert.ThrowsExactly<AuthException>(() => controller.checkAccess());
    }

    [TestMethod]
    public void TextSetting_BlankSubmissionClearsExistingValue()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_TEXT, "configured@example.test", code: "feedback_email"),
            new FwDict { ["ivalue"] = "" });

        controller.SaveAction(1);

        Assert.AreEqual(1, model.UpdateCalls);
        Assert.AreEqual("", model.LastUpdate["ivalue"]);
    }

    [TestMethod]
    public void Switch_UncheckedSavesZero()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_SWITCH, "1"),
            []);

        controller.SaveAction(1);

        Assert.AreEqual("0", model.LastUpdate["ivalue"]);
    }

    [TestMethod]
    public void Switch_CheckedSavesOne()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_SWITCH, "0"),
            new FwDict { ["ivalue"] = "1" });

        controller.SaveAction(1);

        Assert.AreEqual("1", model.LastUpdate["ivalue"]);
    }

    [TestMethod]
    public void Number_InvalidValueFailsValidation()
    {
        var (fw, model, controller) = BuildController(
            Setting(Settings.INPUT_NUMBER, "5", "min|1 step|1"),
            new FwDict { ["ivalue"] = "not-number" });

        AssertValidationFailure(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("NUMBER", fw.getFormErrors()["ivalue"]);
    }

    [TestMethod]
    public void Number_EnforcesStepMetadata()
    {
        var (fw, model, controller) = BuildController(
            Setting(Settings.INPUT_NUMBER, "5", "min|1 step|1"),
            new FwDict { ["ivalue"] = "2.5" });

        AssertValidationFailure(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("STEP", fw.getFormErrors()["ivalue"]);
    }

    [TestMethod]
    public void Range_EnforcesMinimum()
    {
        var (fw, model, controller) = BuildController(
            Setting(Settings.INPUT_RANGE, "3", "min|1 max|5 step|1"),
            new FwDict { ["ivalue"] = "0" });

        AssertValidationFailure(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("MIN", fw.getFormErrors()["ivalue"]);
    }

    [TestMethod]
    public void Range_EnforcesMaximum()
    {
        var (fw, model, controller) = BuildController(
            Setting(Settings.INPUT_RANGE, "3", "min|1 max|5 step|1"),
            new FwDict { ["ivalue"] = "6" });

        AssertValidationFailure(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("MAX", fw.getFormErrors()["ivalue"]);
    }

    [TestMethod]
    public void Range_EnforcesStep()
    {
        var (fw, model, controller) = BuildController(
            Setting(Settings.INPUT_RANGE, "3", "min|1 max|5 step|2"),
            new FwDict { ["ivalue"] = "4" });

        AssertValidationFailure(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("STEP", fw.getFormErrors()["ivalue"]);
    }

    [TestMethod]
    public void Select_RejectsValuesOutsideAllowedValues()
    {
        var (fw, model, controller) = BuildController(
            Setting(Settings.INPUT_SELECT, "auto", "auto|Auto json|JSON native|Native"),
            new FwDict { ["ivalue"] = "xml" });

        AssertValidationFailure(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("INVALID", fw.getFormErrors()["ivalue"]);
    }

    [TestMethod]
    public void Radio_RejectsValuesOutsideAllowedValues()
    {
        var (fw, model, controller) = BuildController(
            Setting(Settings.INPUT_RADIO, "yes", "yes|Yes no|No"),
            new FwDict { ["ivalue"] = "maybe" });

        AssertValidationFailure(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("INVALID", fw.getFormErrors()["ivalue"]);
    }

    [TestMethod]
    public void Checkbox_SavesSelectedOptionsAsStableCommaSeparatedValue()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_CHECKBOX, "", "a|Alpha b|Beta c|Gamma"),
            [],
            new FwDict
            {
                ["ivalue_multi"] = new FwDict
                {
                    ["b"] = "1",
                    ["a"] = "1",
                },
            });

        controller.SaveAction(1);

        Assert.AreEqual("a,b", model.LastUpdate["ivalue"]);
    }

#if isSQLite
    private sealed class TransferFixture : IDisposable
    {
        private readonly string path;
        private readonly ServiceProvider services;
        private readonly FwTestScope scope;

        public DB Db { get; }
        public FW Fw => scope.Fw;
        public Settings Model { get; }
        public AdminSettingsController Controller { get; }

        public TransferFixture()
        {
            path = Path.Combine(Path.GetTempPath(), "osafw-settings-controller-" + Guid.NewGuid().ToString("N") + ".sqlite");
            string connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Pooling = false,
                ForeignKeys = true,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();
            Db = new DB(DB.h(
                "type", DB.DBTYPE_SQLITE,
                "connection_string", connectionString,
                "timezone", "UTC"), "main");
            Db.exec("""
                CREATE TABLE settings (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  icat TEXT NOT NULL DEFAULT '',
                  icode TEXT NOT NULL DEFAULT '',
                  ivalue TEXT NOT NULL DEFAULT '',
                  iname TEXT NOT NULL DEFAULT '',
                  idesc TEXT,
                  input INTEGER NOT NULL DEFAULT 0,
                  allowed_values TEXT,
                  is_user_edit INTEGER DEFAULT 0,
                  access_level INTEGER NOT NULL DEFAULT 100,
                  mask INTEGER NOT NULL DEFAULT 0,
                  basis INTEGER NOT NULL DEFAULT 0,
                  add_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                  add_users_id INTEGER DEFAULT 0,
                  upd_time DATETIME,
                  upd_users_id INTEGER DEFAULT 0
                );
                CREATE UNIQUE INDEX UX_settings_icode ON settings (icode);
                """);
            services = new ServiceCollection()
                .AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider())
                .BuildServiceProvider();
            var context = TestHelpers.CreateHttpContext("settings-controller-tests");
            context.RequestServices = services;
            context.Response.Body = new MemoryStream();
            scope = new FwTestScope(
                name => name == "main" ? Db : throw new InvalidOperationException("Unknown DB name."),
                new Dictionary<string, string?>
                {
                    ["appSettings:ROOT_DOMAIN"] = "https://example.test",
                    ["appSettings:DATA_PROTECTION_APPLICATION_NAME"] = "settings-controller-tests"
                },
                context);
            Fw.route.method = "POST";
            Fw.Session("user_id", "1");
            Fw.Session("access_level", Users.ACL_SITEADMIN.ToString());
            Fw.Session("XSS", "token");
            Fw.FORM = new FwDict { ["XSS"] = "token" };

            var users = new StubUsers();
            users.init(Fw);
            TestHelpers.RegisterModel(Fw, (Users)users);
            Model = Fw.model<Settings>();
            Model.is_log_changes = false;
            Controller = new AdminSettingsController();
            Controller.init(Fw);
        }

        public int Seed(string code, int input, string value = "", int? mask = null)
        {
            return Db.insert("settings", DB.h(
                "icode", code,
                "ivalue", value,
                "iname", code,
                "input", input,
                "is_user_edit", 1,
                "access_level", Users.ACL_SITEADMIN,
                "mask", mask ?? (input == Settings.INPUT_CREDENTIAL ? Settings.MASK_HIDDEN : Settings.MASK_NONE),
                "basis", Settings.BASIS_VALUE));
        }

        public void SetUpload(string json)
        {
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            var file = new FormFile(stream, 0, stream.Length, "settings_file", "settings.json")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/json"
            };
            var files = new FormFileCollection { file };
            Fw.request.Form = new FormCollection(
                new Dictionary<string, StringValues> { ["XSS"] = "token" },
                files);
        }

        public void Dispose()
        {
            scope.Dispose();
            services.Dispose();
            Db.disconnect();
            foreach (string suffix in new[] { "", "-wal", "-shm", "-journal" })
                File.Delete(path + suffix);
        }
    }
#endif
}
