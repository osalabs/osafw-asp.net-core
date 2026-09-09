using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace osafw.Tests;

[TestClass]
public class VueInteractionBackendTests
{
    private sealed class StubUsers(FwDict permissions, bool readOnly = false) : Users
    {
        public override void checkReadOnly(int id = -1) { }
        public override bool isReadOnly(int id = -1) => readOnly;
        public override bool isAccessLevel(int min_acl) => true;
        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => new(permissions);
    }

    private sealed class RecordingUserViews : UserViews
    {
        public FwDict LastUpdate { get; private set; } = [];
        public DBRow Current { get; set; } = [];
        public DBRow Named { get; set; } = [];

        public override DBRow oneByIcode(string icode) => Current;
        public override DBRow oneAvail(int id) => Named;
        public override FwList listSelectByIcode(string icode) => [];
        public override int updateByIcode(string icode, FwDict itemdb)
        {
            LastUpdate = new FwDict(itemdb);
            return 1;
        }
    }

    private class RecordingModel : FwModel
    {
        public FwDict LastAdded { get; private set; } = [];
        public FwDict LastUpdated { get; private set; } = [];

        public RecordingModel()
        {
            table_name = "stub";
            field_status = "";
            field_add_users_id = "";
            field_upd_users_id = "";
        }

        public override DBRow one(int id) => new() { ["id"] = id.toStr() };
        public override int add(FwDict item)
        {
            LastAdded = new FwDict(item);
            return 41;
        }
        public override bool update(int id, FwDict item)
        {
            LastUpdated = new FwDict(item);
            return true;
        }
        public override void convertUserInput(FwDict item) { }
        public override void filterForJson(FwDict item) { }
    }

    private sealed class ChildModel : FwModel
    {
        public FwDict LastAdded { get; private set; } = [];
        public FwDict LastUpdated { get; private set; } = [];

        public ChildModel()
        {
            table_name = "child";
            junction_field_main_id = "parents_id";
            field_status = "status";
        }

        public override DBRow one(int id) => new() { ["id"] = id.toStr(), ["parents_id"] = "7" };
        public override int add(FwDict item)
        {
            LastAdded = new FwDict(item);
            return 51;
        }
        public override bool update(int id, FwDict item)
        {
            LastUpdated = new FwDict(item);
            return true;
        }
        public override void setUnderUpdateByMainId(int main_id) { }
        public override void deleteUnderUpdateByMainId(int main_id) { }
        public override DBList listByMainId(int main_id, FwDict? def = null) => [];
        public override void prepareSubtable(FwList list_rows, int related_id, FwDict? def = null) { }
    }

    private class InteractionController : FwVueController
    {
        private FwDict currentView = [];

        public void Configure(FW fw, RecordingModel model, FwDict definition, FwDict? view = null)
        {
            init(fw);
            base_url = "/Items";
            model0 = model;
            model.init(fw);
            db = fw.db;
            currentView = view ?? [];
            loadControllerConfig(definition);
        }

        protected override FwDict getListUserView() => currentView;
        public FwDict InitialState() { FwDict ps = []; setScopeInitial(ps); return ps; }
        public void ApplyCapabilities(FwDict row) => applyListRowCapabilities(row);
    }

    private sealed class WarningController : InteractionController
    {
        public override void Validate(int id, FwDict item)
        {
            base.Validate(id, item);
            addValidationIssue("warning", "title", "Review this title", attempted_value: item["title"]);
        }
    }

    private sealed class ErrorController : InteractionController
    {
        public override void Validate(int id, FwDict item)
        {
            base.Validate(id, item);
            addValidationIssue("error", "title", "This title is not allowed");
        }
    }

    [TestMethod]
    public void WidthNormalization_ClampsAllowlistedColumnsAndRejectsMalformedInput()
    {
        var widths = UserViews.normalizeWidths(
            "{\"title\":15,\"notes\":900,\"unknown\":200,\"bad\":\"x\"}",
            new[] { "title", "notes", "bad" });

        Assert.AreEqual(60, widths["title"]);
        Assert.AreEqual(800, widths["notes"]);
        Assert.IsFalse(widths.ContainsKey("unknown"));
        Assert.IsFalse(widths.ContainsKey("bad"));
        Assert.AreEqual(0, UserViews.normalizeWidths("not-json", new[] { "title" }).Count);
        Assert.AreEqual(0, UserViews.normalizeWidths(new FwDict { ["title"] = true }, new[] { "title" }).Count);

        var allowed = Enumerable.Range(1, 101).Select(i => "field" + i).ToArray();
        var sparse = UserViews.normalizeWidths(new FwDict { ["field101"] = 1e30 }, allowed);
        Assert.AreEqual(800, sparse["field101"]);

        var all = new FwDict();
        foreach (var field in allowed)
            all[field] = 100;
        Assert.AreEqual(100, UserViews.normalizeWidths(all, allowed).Count);
    }

    [TestMethod]
    public void SaveUserViews_RequiresPostAndPersistsOnlyNormalizedWidths()
    {
        var (fw, views, controller) = buildController(new FwDict
        {
            ["is_dynamic_index"] = true,
            ["view_list_defaults"] = "title notes",
            ["view_list_map"] = new FwDict { ["title"] = "Title", ["notes"] = "Notes" },
            ["list_sortdef"] = "title asc",
        });
        fw.Session("XSS", "token");
        fw.FORM = new FwDict
        {
            ["XSS"] = "token",
            ["widths"] = "{\"title\":20,\"notes\":900,\"secret\":300}",
        };

        fw.route.method = "GET";
        Assert.ThrowsExactly<AuthException>(() => controller.SaveUserViewsAction());
        Assert.AreEqual(0, views.LastUpdate.Count);

        fw.route.method = "POST";
        controller.SaveUserViewsAction();
        var stored = Utils.jsonDecode(views.LastUpdate["widths"].toStr()) as FwDict ?? [];
        Assert.AreEqual(60, stored["title"].toInt());
        Assert.AreEqual(800, stored["notes"].toInt());
        Assert.IsFalse(stored.ContainsKey("secret"));
    }

    [TestMethod]
    public void InitialAndRowCapabilities_UseRbacAndOnlyNarrowServerPermissions()
    {
        var (fw, _, controller) = buildController(new FwDict
        {
            ["is_dynamic_index"] = true,
            ["view_list_defaults"] = "title",
            ["view_list_map"] = new FwDict { ["title"] = "Title" },
            ["list_sortdef"] = "title asc",
        }, new FwDict
        {
            [Permissions.PERMISSION_ADD] = true,
            [Permissions.PERMISSION_EDIT] = false,
            [Permissions.PERMISSION_DELETE] = true,
        });

        var initial = controller.InitialState();
        var capabilities = (FwDict)initial["capabilities"]!;
        Assert.IsTrue(capabilities["create"].toBool());
        Assert.IsFalse(capabilities["edit"].toBool());
        Assert.IsTrue(capabilities["delete"].toBool());

        var row = new FwDict
        {
            ["_capabilities"] = new FwDict { ["edit"] = true, ["delete"] = false, ["admin"] = true },
        };
        controller.ApplyCapabilities(row);
        var rowCapabilities = (FwDict)row["_capabilities"]!;
        Assert.IsFalse(rowCapabilities["edit"].toBool());
        Assert.IsFalse(rowCapabilities["delete"].toBool());
        Assert.IsFalse(rowCapabilities.ContainsKey("admin"));
    }

    [TestMethod]
    public void SaveUserViews_LoadsNamedFieldsWithNormalizedNamedWidths()
    {
        var (fw, views, controller) = buildController(new FwDict
        {
            ["is_dynamic_index"] = true,
            ["view_list_defaults"] = "title notes",
            ["view_list_map"] = new FwDict { ["title"] = "Title", ["notes"] = "Notes" },
            ["list_sortdef"] = "title asc",
        });
        views.Named = new DBRow
        {
            ["id"] = "9",
            ["icode"] = "/Items",
            ["fields"] = "notes",
            ["widths"] = "{\"notes\":900,\"secret\":300}",
        };
        fw.Session("XSS", "token");
        fw.FORM = new FwDict { ["XSS"] = "token", ["load_id"] = "9" };

        controller.SaveUserViewsAction();

        Assert.AreEqual("notes", views.LastUpdate["fields"]);
        var stored = Utils.jsonDecode(views.LastUpdate["widths"].toStr()) as FwDict ?? [];
        Assert.AreEqual(800, stored["notes"].toInt());
        Assert.IsFalse(stored.ContainsKey("secret"));
    }

    [TestMethod]
    public void ImmutableMainField_IsSavedOnCreateAndIgnoredOnExistingRow()
    {
        var definition = basicSaveDefinition(new FwList
        {
            new FwDict { ["field"] = "code", ["type"] = "text", ["immutable_on_edit"] = true },
            new FwDict { ["field"] = "title", ["type"] = "text" },
        }, "code title");
        var (fw, _, controller, model) = buildSaveController(definition);
        fw.FORM["item"] = new FwDict { ["code"] = "forged", ["title"] = "Updated" };

        controller.SaveAction(7);
        Assert.IsFalse(model.LastUpdated.ContainsKey("code"));
        Assert.AreEqual("Updated", model.LastUpdated["title"]);

        fw.FORM["item"] = new FwDict { ["code"] = "created", ["title"] = "New" };
        controller.SaveAction(0);
        Assert.AreEqual("created", model.LastAdded["code"]);
    }

    [TestMethod]
    public void ImmutableSubtableField_IsIgnoredForExistingChildAndSavedForNewChild()
    {
        var childDefinitions = new FwList
        {
            new FwDict { ["field"] = "code", ["immutable_on_edit"] = true },
            new FwDict { ["field"] = "notes" },
        };
        var subtable = new FwDict
        {
            ["field"] = "lines",
            ["type"] = "subtable_edit",
            ["model"] = "ChildModel",
            ["save_fields"] = "code notes",
            ["showform_fields"] = childDefinitions,
        };
        var definition = basicSaveDefinition(new FwList { subtable }, "title");
        var (fw, _, controller, _) = buildSaveController(definition);
        var child = new ChildModel();
        TestHelpers.RegisterModel(fw, child);
        fw.FORM["item"] = new FwDict { ["title"] = "Updated" };
        fw.FORM["item-lines"] = new FwDict { ["22"] = "1", ["new-1"] = "1" };
        fw.FORM["item-lines#22"] = new FwDict { ["code"] = "forged", ["notes"] = "kept" };
        fw.FORM["item-lines#new-1"] = new FwDict { ["code"] = "created", ["notes"] = "new" };

        controller.SaveAction(7);

        Assert.IsFalse(child.LastUpdated.ContainsKey("code"));
        Assert.AreEqual("kept", child.LastUpdated["notes"]);
        Assert.AreEqual("created", child.LastAdded["code"]);
    }

    [TestMethod]
    public void ValidationFailure_ReturnsActionableRepeatedIssueWithoutEchoingValue()
    {
        var childField = new FwDict { ["field"] = "quantity" };
        var subtable = new FwDict
        {
            ["field"] = "lines",
            ["type"] = "subtable_edit",
            ["model"] = "ChildModel",
            ["save_fields"] = "quantity",
            ["required_fields"] = "quantity",
            ["showform_fields"] = new FwList { childField },
        };
        var definition = basicSaveDefinition([], "title");
        definition["form_tabs"] = new FwList { new FwDict { ["tab"] = "details" }, new FwDict { ["tab"] = "other" } };
        definition["showform_fields_details"] = new FwList { subtable };
        var fw = createFw();
        fw.FORM["tab"] = "details";
        var model = new RecordingModel();
        var controller = new InteractionController();
        controller.Configure(fw, model, definition);
        TestHelpers.RegisterModel(fw, new ChildModel());
        fw.FORM["item"] = new FwDict { ["title"] = "Updated" };
        fw.FORM["item-lines"] = new FwDict { ["22"] = "1" };
        fw.FORM["item-lines#22"] = new FwDict { ["quantity"] = "" };

        var exception = Assert.ThrowsExactly<ValidationException>(() => controller.SaveAction(7));
        var response = controller.actionError(exception, new object[] { 7 });
        var json = (FwDict)response!["_json"]!;
        var issue = ((FwList)json["validation_issues"]!).Single();

        Assert.AreEqual(400, fw.response.StatusCode);
        Assert.AreEqual("item-lines#22[quantity]", issue["field"]);
        Assert.AreEqual("22", issue["row_id"]);
        Assert.AreEqual("details", issue["tab"]);
        Assert.AreEqual("Required field", issue["message"]);
        Assert.IsFalse(issue.ContainsKey("value"));
        Assert.IsTrue(((FwDict)((FwDict)json["error"]!)["details"]!)["item-lines#22[quantity]"].toBool());
    }

    [TestMethod]
    public void WarningOnlySave_ReturnsSafeOptedInAttemptedValue()
    {
        var definition = basicSaveDefinition(new FwList
        {
            new FwDict { ["field"] = "title", ["type"] = "text", ["validation_show_value"] = true },
        }, "title");
        var fw = createFw();
        var model = new RecordingModel();
        var controller = new WarningController();
        controller.Configure(fw, model, definition);
        fw.FORM["item"] = new FwDict { ["title"] = "Review" };

        var response = controller.SaveAction(0);
        var json = (FwDict)response!["_json"]!;
        var issue = ((FwList)json["validation_issues"]!).Single();

        Assert.IsTrue(json["success"].toBool());
        Assert.AreEqual("warning", issue["severity"]);
        Assert.AreEqual("Review", issue["value"]);
        Assert.AreEqual("Review", model.LastAdded["title"]);
    }

    [TestMethod]
    public void ErrorIssue_BlocksSaveAndReturnsStructuredBadRequest()
    {
        var definition = basicSaveDefinition(new FwList
        {
            new FwDict { ["field"] = "title", ["type"] = "text" },
        }, "title");
        var fw = createFw();
        var model = new RecordingModel();
        var controller = new ErrorController();
        controller.Configure(fw, model, definition);
        fw.FORM["item"] = new FwDict { ["title"] = "Blocked" };

        var exception = Assert.ThrowsExactly<ValidationException>(() => controller.SaveAction(0));
        var response = controller.actionError(exception, new object[] { 0 });
        var json = (FwDict)response!["_json"]!;
        var issue = ((FwList)json["validation_issues"]!).Single();

        Assert.AreEqual(0, model.LastAdded.Count);
        Assert.AreEqual(400, fw.response.StatusCode);
        Assert.IsFalse(json["success"].toBool());
        Assert.AreEqual("error", issue["severity"]);
        Assert.AreEqual("title", issue["field"]);
        Assert.AreEqual("This title is not allowed", issue["message"]);
        Assert.AreEqual("This title is not allowed", ((FwDict)((FwDict)json["error"]!)["details"]!)["title"]);
    }

    [TestMethod]
    public void WarningIssue_NeverEchoesPasswordControlValueEvenWhenOptedIn()
    {
        var definition = basicSaveDefinition(new FwList
        {
            new FwDict { ["field"] = "title", ["type"] = "password", ["validation_show_value"] = true },
        }, "title");
        var fw = createFw();
        var model = new RecordingModel();
        var controller = new WarningController();
        controller.Configure(fw, model, definition);
        fw.FORM["item"] = new FwDict { ["title"] = "sensitive" };

        var response = controller.SaveAction(0);
        var issue = ((FwList)((FwDict)response!["_json"]!)["validation_issues"]!).Single();

        Assert.IsFalse(issue.ContainsKey("value"));
    }

    [TestMethod]
    public void UserViewWidthSchemas_KeepFreshAndAdditiveProviderPathsAligned()
    {
        var root = repoRoot();
        var sqlServerFresh = File.ReadAllText(Path.Combine(root, "osafw-app", "App_Data", "sql", "fwdatabase.sql"));
        var sqliteFresh = File.ReadAllText(Path.Combine(root, "osafw-app", "App_Data", "sql", "sqlite", "fwdatabase.sql"));
        var mysqlFresh = File.ReadAllText(Path.Combine(root, "osafw-app", "App_Data", "sql", "mysql", "fwdatabase.sql"));
        var sqlServerUpdate = File.ReadAllText(Path.Combine(root, "osafw-app", "App_Data", "sql", "updates", "upd2026-09-08-user-view-widths.sql"));
        var sqliteUpdate = File.ReadAllText(Path.Combine(root, "osafw-app", "App_Data", "sql", "sqlite", "updates", "upd2026-09-08-user-view-widths.sql"));
        var mysqlUpdate = File.ReadAllText(Path.Combine(root, "osafw-app", "App_Data", "sql", "mysql", "updates", "upd2026-09-08-user-view-widths.sql"));

        StringAssert.Contains(sqlServerFresh, "widths                NVARCHAR(MAX)");
        StringAssert.Contains(sqliteFresh, "widths                TEXT NOT NULL DEFAULT '{}'");
        StringAssert.Contains(mysqlFresh, "widths                TEXT");
        StringAssert.Contains(sqlServerUpdate, "COL_LENGTH('dbo.user_views', 'widths') IS NULL");
        StringAssert.Contains(sqliteUpdate, "ALTER TABLE user_views ADD COLUMN widths");
        StringAssert.Contains(mysqlUpdate, "information_schema.columns");
        StringAssert.Contains(mysqlUpdate, "column_name = 'widths'");
    }

    private static FwDict basicSaveDefinition(FwList fields, string saveFields) => new()
    {
        ["is_dynamic_showform"] = true,
        ["showform_fields"] = fields,
        ["save_fields"] = saveFields,
    };

    private static (FW fw, RecordingUserViews views, InteractionController controller) buildController(
        FwDict definition,
        FwDict? permissions = null)
    {
        var fw = createFw(permissions);
        var views = new RecordingUserViews { Current = new DBRow { ["fields"] = definition["view_list_defaults"].toStr(), ["widths"] = "{}" } };
        TestHelpers.RegisterModel(fw, (UserViews)views);
        var controller = new InteractionController();
        controller.Configure(fw, new RecordingModel(), definition, views.Current);
        return (fw, views, controller);
    }

    private static (FW fw, RecordingUserViews views, InteractionController controller, RecordingModel model) buildSaveController(FwDict definition)
    {
        var fw = createFw();
        var views = new RecordingUserViews();
        TestHelpers.RegisterModel(fw, (UserViews)views);
        var model = new RecordingModel();
        var controller = new InteractionController();
        controller.Configure(fw, model, definition);
        return (fw, views, controller, model);
    }

    private static FW createFw(FwDict? permissions = null)
    {
        var fw = TestHelpers.CreateFw();
        fw.request.Headers.Accept = "application/json";
        fw.route.method = "POST";
        TestHelpers.RegisterModel(fw, (Users)new StubUsers(permissions ?? new FwDict
        {
            [Permissions.PERMISSION_ADD] = true,
            [Permissions.PERMISSION_EDIT] = true,
            [Permissions.PERMISSION_DELETE] = true,
        }));
        return fw;
    }

    private static string repoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "osafw-app", "App_Data", "sql")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Cannot locate repository root from " + Directory.GetCurrentDirectory());
    }
}
