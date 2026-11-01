using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if isSQLite
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
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
        public bool IsValidateCalled { get; private set; }

        public override void Validate(int id, FwDict item)
        {
            IsValidateCalled = true;
            base.Validate(id, item);
        }

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
    [DataRow(Settings.INPUT_CREDENTIAL, false)]
    [DataRow(Settings.INPUT_TEXT, true)]
    public void ShowForm_RendersDefaultSelectionOnlyForOrdinarySettings(int input, bool hasDefaultSelection)
    {
        var (fw, _, controller) = BuildController(
            Setting(input, "", basis: Settings.BASIS_INHERIT), []);
        fw.route.method = "GET";
        fw.config()["template"] = TestHelpers.TemplateRoot();

        var ps = controller.ShowFormAction(1);
        string html = fw.parsePage("/admin/settings/showform", "form.html", ps);

        Assert.AreEqual(hasDefaultSelection, html.Contains("id=\"basis_inherit\"", StringComparison.Ordinal));
        if (!hasDefaultSelection)
        {
            StringAssert.Contains(html, "Not configured");
            StringAssert.Contains(html, "Leave blank to keep the current value.");
            StringAssert.Contains(html, "Clear the saved value");
        }
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
    public void Credential_SubmittedInheritanceCannotRestoreConfigurationFallback()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_CREDENTIAL, "old-secret"),
            new FwDict { ["ivalue"] = "new-secret", ["basis"] = Settings.BASIS_INHERIT });

        controller.SaveAction(1);

        Assert.AreEqual(1, model.UpdateCalls);
        Assert.AreEqual(Settings.BASIS_VALUE, model.LastUpdate["basis"].toInt());
        Assert.AreEqual("new-secret", model.LastUpdate["ivalue"]);
    }

    [TestMethod]
    public void Credential_UnconfiguredBlankSubmissionKeepsItUnconfigured()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_CREDENTIAL, "", basis: Settings.BASIS_INHERIT),
            new FwDict { ["ivalue"] = "" });

        controller.SaveAction(1);
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
    [DataRow("QuickSearch")]
    [DataRow("SaveMulti")]
    [DataRow("ShowDelete")]
    [DataRow("Delete")]
    [DataRow("RestoreDeleted")]
    [DataRow("Next")]
    [DataRow("Go")]
    public void InheritedActions_AreDeniedBeforeGenericHandling(string action)
    {
        var (_, model, controller) = BuildController(Setting(Settings.INPUT_TEXT), []);
        FwAdminController admin = controller;
        Action invoke = action switch
        {
            "QuickSearch" => () => admin.QuickSearchAction(),
            "SaveMulti" => () => admin.SaveMultiAction(),
            "ShowDelete" => () => admin.ShowDeleteAction(1),
            "Delete" => () => admin.DeleteAction(1),
            "RestoreDeleted" => () => admin.RestoreDeletedAction(1),
            "Next" => () => admin.NextAction("1"),
            "Go" => () => admin.GoAction(),
            _ => throw new InvalidOperationException("Unknown test action"),
        };

        Assert.ThrowsExactly<AuthException>(invoke);
        Assert.AreEqual(0, model.UpdateCalls);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(FW.ACTION_INDEX)]
    [DataRow(FW.ACTION_SHOW)]
    [DataRow(FW.ACTION_SHOW_FORM)]
    [DataRow(FW.ACTION_SAVE)]
    [DataRow("Reveal")]
    public void CheckAccess_AllowsSupportedActions(string action)
    {
        var (fw, _, controller) = BuildController(Setting(Settings.INPUT_TEXT), []);
        fw.route.action = action;

        controller.checkAccess();
    }

    [TestMethod]
    [DataRow("csv")]
    [DataRow("xls")]
    public void ExportList_BlocksGenericExport(string format)
    {
        var (fw, _, controller) = BuildController(Setting(Settings.INPUT_TEXT), []);
        fw.route.action = FW.ACTION_INDEX;
        controller.export_format = format;

        Assert.ThrowsExactly<AuthException>(() => ((FwController)controller).exportList());
    }

    [TestMethod]
    public void SaveAction_UsesTheOverridableValidationHook()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_TEXT, "old"),
            new FwDict { ["ivalue"] = "new" });

        controller.SaveAction(1);

        Assert.IsTrue(controller.IsValidateCalled);
        Assert.AreEqual(1, model.UpdateCalls);
        Assert.AreEqual("new", model.LastUpdate["ivalue"]);
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
    public void Number_BlankExplicitValueFailsValidation()
    {
        var (fw, model, controller) = BuildController(
            Setting(Settings.INPUT_NUMBER, "5"),
            new FwDict { ["ivalue"] = "" });

        AssertValidationFailure(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("NUMBER", fw.getFormErrors()["ivalue"]);
    }

    [TestMethod]
    public void Number_ProtectedBlankSubmissionKeepsExistingValue()
    {
        var (_, model, controller) = BuildController(
            Setting(Settings.INPUT_NUMBER, "5", mask: Settings.MASK_HIDDEN),
            new FwDict { ["ivalue"] = "" });

        controller.SaveAction(1);

        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("5", model.Rows[1]["ivalue"]);
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
    [TestMethod]
    [DataRow(Users.ACL_ADMIN, 1, false)]
    [DataRow(Users.ACL_SITEADMIN, 3, true)]
    public void IndexAction_FiltersRowsCountsAndCategoriesBeforeProjection(int accessLevel, int expectedCount, bool isSiteAdmin)
    {
        using var fixture = new TransferFixture();
        fixture.Seed("ordinary", Settings.INPUT_TEXT, "visible", accessLevel: Users.ACL_ADMIN, category: "public");
        fixture.Seed("restricted", Settings.INPUT_TEXT, "restricted", category: "restricted");
        fixture.Seed("credential", Settings.INPUT_CREDENTIAL, accessLevel: Users.ACL_ADMIN, category: "credentials");
        fixture.Fw.Session("access_level", accessLevel.ToString());
        fixture.Fw.route.controller = "AdminSettings";
        fixture.Fw.route.action = FW.ACTION_INDEX;
        fixture.Fw.route.method = "GET";

        var pageState = fixture.Controller.IndexAction()!;

        Assert.AreEqual(expectedCount, pageState["count"].toInt());
        Assert.AreEqual(isSiteAdmin, pageState["is_site_admin"].toBool());
        var rows = (FwList)pageState["list_rows"]!;
        Assert.AreEqual(expectedCount, rows.Count);
        Assert.IsTrue(rows.All(row => !row.ContainsKey("ivalue")));
        string[] expectedCodes = isSiteAdmin ? ["ordinary", "restricted", "credential"] : ["ordinary"];
        CollectionAssert.AreEquivalent(expectedCodes, rows.Select(row => row["icode"].toStr()).ToArray());
        var categories = (FwList)pageState["settings_categories"]!;
        string[] expectedCategories = isSiteAdmin ? ["public", "restricted", "credentials"] : ["public"];
        CollectionAssert.AreEquivalent(expectedCategories, categories.Select(row => row["icat"].toStr()).ToArray());
    }

    private sealed class TransferFixture : IDisposable
    {
        private readonly string path;
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
            Db.exec("CREATE TABLE fwkeys (id INTEGER PRIMARY KEY AUTOINCREMENT, itype INTEGER NOT NULL, iname TEXT NOT NULL, XmlValue TEXT NOT NULL, upd_time DATETIME)");
            var context = TestHelpers.CreateHttpContext("settings-controller-tests");
            context.Response.Body = new MemoryStream();
            scope = new FwTestScope(
                name => name == "main" ? Db : throw new InvalidOperationException("Unknown DB name."),
                new Dictionary<string, string?>
                {
                    ["appSettings:db:main:type"] = DB.DBTYPE_SQLITE,
                    ["appSettings:db:main:connection_string"] = connectionString,
                    ["appSettings:db:main:timezone"] = "UTC",
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

        public int Seed(string code, int input, string value = "", int? mask = null, int accessLevel = Users.ACL_SITEADMIN, string category = "")
        {
            return Db.insert("settings", DB.h(
                "icode", code,
                "ivalue", value,
                "iname", code,
                "icat", category,
                "input", input,
                "is_user_edit", 1,
                "access_level", accessLevel,
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
            Db.disconnect();
            foreach (string suffix in new[] { "", "-wal", "-shm", "-journal" })
                File.Delete(path + suffix);
        }
    }
#endif
}
