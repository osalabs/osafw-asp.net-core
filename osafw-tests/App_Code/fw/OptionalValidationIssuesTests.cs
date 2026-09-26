using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace osafw.Tests;

[TestClass]
public class OptionalValidationIssuesTests
{
    private sealed class PlainController : FwController
    {
        public PlainController(FW context) : base(context) { base_url = "/Plain"; }
        public void Issue(string severity, string field, string message) => addValidationIssue(severity, field, message);
    }

    private sealed class MemoryModel : FwModel
    {
        public int Writes;
        public MemoryModel() { table_name = "demo"; }
        public override DBRow one(int id) => new() { ["id"] = id.toStr(), ["title"] = "Original" };
        public override int add(FwDict item) { Writes++; return 7; }
        public override bool update(int id, FwDict item) { Writes++; return true; }
        public override void convertUserInput(FwDict item) { }
    }

    private sealed class DynamicController : FwDynamicController
    {
        public string Severity = "";
        public MemoryModel Model = new();
        public DynamicController(FW context)
        {
            fw = context;
            db = context.db;
            base_url = "/Dynamic";
            model0 = Model;
            Model.init(context);
            loadControllerConfig(new FwDict
            {
                ["is_dynamic_showform"] = true,
                ["save_fields"] = "title",
                ["showform_fields"] = new FwList { new FwDict { ["field"] = "title", ["type"] = "input", ["required"] = true } },
                ["form_tabs"] = new FwList { new FwDict { ["tab"] = "" }, new FwDict { ["tab"] = "details" } },
                ["showform_fields_details"] = new FwList
                {
                    new FwDict { ["field"] = "lines", ["type"] = "subtable_edit", ["showform_fields"] = new FwList
                    {
                        new FwDict { ["field"] = "secret", ["type"] = "password", ["validation_show_value"] = true }
                    } }
                }
            });
        }
        public override void checkReadOnly() { }
        public override void setAddUpdUser(FwDict ps, FwDict item) { }
        public override void Validate(int id, FwDict item)
        {
            base.Validate(id, item);
            if (Severity.Length > 0)
                addValidationIssue(Severity, "title", "Please review this title.", attempted_value: item["title"]);
        }
        public void RowIssue() => addValidationIssue("warning", "item-lines#new-1[secret]", "Review row.", attempted_value: "sensitive");
    }

    private static FW Context(bool is_json = true)
    {
        var fw = TestHelpers.CreateFw();
        fw.request.Headers.Accept = is_json ? "application/json" : "text/html";
        fw.route.method = "POST";
        fw.FORM["item"] = new FwDict { ["title"] = "Entered" };
        return fw;
    }

    [TestMethod]
    public void PlainController_OptInErrorsPreserveLegacyCodesAndWarningsDoNotBecomeErrors()
    {
        var fw = Context();
        var controller = new PlainController(fw);
        fw.FormErrors["email"] = "EMAIL";
        controller.Issue("error", "email", "Check the address.");
        controller.Issue("warning", "title", "Review the title.");
        var ex = Assert.ThrowsExactly<ValidationException>(() => controller.validateCheckResult());
        var json = (FwDict)controller.actionError(ex, [7])!["_json"]!;
        Assert.AreEqual("EMAIL", ((FwDict)((FwDict)json["error"]!)["details"]!)["email"]);
        Assert.IsFalse(fw.FormErrors.ContainsKey("title"));
        Assert.AreEqual(2, ((FwList)json["validation_issues"]!).Count);
        Assert.AreEqual(400, fw.response.StatusCode);
    }

    [TestMethod]
    public void LegacyOnlyAndUnrelatedFailuresKeepTheirExistingPaths()
    {
        var fw = Context();
        var controller = new PlainController(fw);
        Assert.IsFalse(((FwDict)controller.afterSave(true, 7)!["_json"]!).ContainsKey("validation_issues"));
        fw.FormErrors["title"] = "WRONG";
        var validation = Assert.ThrowsExactly<ValidationException>(() => controller.validateCheckResult());
        Assert.ThrowsExactly<ValidationException>(() => controller.actionError(validation, [7]));
        controller.Issue("warning", "title", "Review.");
        Assert.ThrowsExactly<AuthException>(() => controller.actionError(new AuthException("Denied"), [7]));
        Assert.ThrowsExactly<InvalidOperationException>(() => controller.actionError(new InvalidOperationException("Server"), [7]));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(7)]
    public void DynamicOptionalErrorsBlockBothSavePathsAndWarningsAllowSaving(int id)
    {
        var fw = Context();
        var controller = new DynamicController(fw) { Severity = "error" };
        var ex = Assert.ThrowsExactly<ValidationException>(() => controller.SaveAction(id));
        Assert.AreEqual(0, controller.Model.Writes);
        var json = (FwDict)controller.actionError(ex, [id])!["_json"]!;
        Assert.AreEqual("Please review this title.", fw.FormErrors["title"]);
        Assert.AreEqual(1, ((FwList)json["validation_issues"]!).Count);
        Assert.IsFalse(((FwDict)((FwList)json["validation_issues"]!)[0]).ContainsKey("value"));

        fw = Context();
        controller = new DynamicController(fw) { Severity = "warning" };
        json = (FwDict)controller.SaveAction(id)!["_json"]!;
        Assert.AreEqual(1, controller.Model.Writes);
        Assert.IsTrue(json["success"].toBool());
        Assert.IsFalse(json.ContainsKey("error"));
        Assert.AreEqual("warning", ((FwDict)((FwList)json["validation_issues"]!)[0])["severity"]);
    }

    [TestMethod]
    public void DynamicLegacyValidationDoesNotOptIntoStructuredIssues()
    {
        var fw = Context();
        fw.FORM["item"] = new FwDict { ["title"] = "" };
        var controller = new DynamicController(fw);
        Assert.ThrowsExactly<ValidationException>(() => controller.SaveAction(0));
        var json = (FwDict)controller.afterSave(false, 0)!["_json"]!;
        Assert.IsFalse(json.ContainsKey("validation_issues"));
        Assert.IsTrue(fw.FormErrors["REQUIRED"].toBool());
        Assert.AreEqual(0, controller.Model.Writes);
    }

    [TestMethod]
    public void DynamicRowMetadataAndHtmlFeedbackDoNotExposeSensitiveValues()
    {
        var fw = Context(false);
        var controller = new DynamicController(fw);
        controller.RowIssue();
        var state = controller.ShowFormAction(7)!;
        var issue = ((FwList)state["validation_issues"]!).Single();
        Assert.AreEqual("details", issue["tab"]);
        Assert.AreEqual("new-1", issue["row_id"]);
        Assert.IsFalse(issue.ContainsKey("value"));
        Assert.ThrowsExactly<RedirectException>(() => controller.afterSave(true, 7, location: "/Dynamic/7/edit"));
        Assert.AreEqual("Review row.", fw.SessionDict("_flash")!["warning"]);
    }
}
