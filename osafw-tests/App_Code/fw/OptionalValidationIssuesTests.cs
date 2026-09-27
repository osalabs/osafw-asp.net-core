using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace osafw.Tests;

[TestClass]
public class OptionalValidationIssuesTests
{
    private sealed class MemoryModel : FwModel
    {
        public int Writes;

        public MemoryModel() => table_name = "demo";
        public override DBRow one(int id) => new() { ["id"] = id.toStr(), ["title"] = "Original" };
        public override int add(FwDict item) { Writes++; return 7; }
        public override bool update(int id, FwDict item) { Writes++; return true; }
        public override void convertUserInput(FwDict item) { }
    }

    private sealed class PlainController : FwController
    {
        public MemoryModel Model = new();

        public PlainController(FW context) : base(context)
        {
            base_url = "/Plain";
            model0 = Model;
            Model.init(context);
        }

        public void Issue(string severity, string field, string? code = null, string? message = null,
            string? row_id = null) => addFormIssue(severity, field, code, message, row_id: row_id);
        public void Error(string field, string? code = null, string? message = null) => addFormError(field, code, message);
        public void Warning(string field, string? code = null, string? message = null) => addFormWarning(field, code, message);

        public FwDict? Save(int id, string severity)
        {
            if (severity == FW.ISSUE_ERROR)
                addFormError("title", message: "Blocked title");
            else if (severity == FW.ISSUE_WARNING)
                addFormWarning("title", message: "Review title");

            validateCheckResult();
            var isNew = id == 0;
            if (isNew)
                id = Model.add([]);
            else
                Model.update(id, []);
            return afterSave(true, id, isNew);
        }
    }

    private sealed class DynamicController : FwDynamicController
    {
        public string Severity = string.Empty;
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
                ["showform_fields"] = new FwList
                {
                    new FwDict { ["field"] = "title", ["type"] = "input", ["required"] = true }
                },
                ["form_tabs"] = new FwList { new FwDict { ["tab"] = string.Empty }, new FwDict { ["tab"] = "details" } },
                ["showform_fields_details"] = new FwList
                {
                    new FwDict
                    {
                        ["field"] = "lines",
                        ["type"] = "subtable_edit",
                        ["showform_fields"] = new FwList
                        {
                            new FwDict { ["field"] = "secret", ["type"] = "password", ["validation_show_value"] = true }
                        }
                    }
                }
            });
        }

        public override void checkReadOnly() { }
        public override void setAddUpdUser(FwDict ps, FwDict item) { }

        public override void Validate(int id, FwDict item)
        {
            base.Validate(id, item);
            if (Severity == FW.ISSUE_ERROR)
                addFormError("title", message: "Please review this title.", attempted_value: item["title"]);
            else if (Severity == FW.ISSUE_WARNING)
                addFormWarning("title", message: "Please review this title.", attempted_value: item["title"]);
        }

        public void RowIssue() => addFormWarning("item-lines#new-1[secret]", message: "Review row.", attempted_value: "sensitive");
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
    public void CodesMessagesAndReplacementRemainDistinct()
    {
        var fw = Context();
        var controller = new PlainController(fw);
        controller.Error("email", "EMAIL", "Custom address text");
        controller.Warning("email", message: "Review address");
        controller.Error("title", "EMAIL", "First text");
        controller.Error("title", "WRONG", "Last text");

        var emailError = fw.FormIssues.Single(issue => issue["field"].toStr() == "email"
            && issue["severity"].toStr() == FW.ISSUE_ERROR);
        Assert.AreEqual("EMAIL", emailError["code"]);
        Assert.AreEqual("Custom address text", emailError["message"]);
        Assert.AreEqual(2, fw.FormIssues.Count(issue => issue["field"].toStr() == "email"));

        var titleError = fw.FormIssues.Single(issue => issue["field"].toStr() == "title");
        Assert.AreEqual("WRONG", titleError["code"]);
        Assert.AreEqual("Last text", titleError["message"]);
        Assert.AreEqual("WRONG", fw.getFormErrors()["title"]);
    }

    [TestMethod]
    public void UnsupportedSeverityIsRejected()
    {
        var controller = new PlainController(Context());
        Assert.ThrowsExactly<ArgumentException>(() => controller.Issue("info", "title", message: "FYI"));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(7)]
    public void PlainErrorsBlockPersistenceWhileWarningsAllowIt(int id)
    {
        var fw = Context();
        var controller = new PlainController(fw);
        Assert.ThrowsExactly<ValidationException>(() => controller.Save(id, FW.ISSUE_ERROR));
        Assert.AreEqual(0, controller.Model.Writes);

        fw = Context();
        controller = new PlainController(fw);
        var json = (FwDict)controller.Save(id, FW.ISSUE_WARNING)!["_json"]!;
        Assert.AreEqual(1, controller.Model.Writes);
        Assert.IsFalse(json.ContainsKey("error"));
        Assert.AreEqual(FW.ISSUE_WARNING, ((FwList)json["form_issues"]!).Single()["severity"]);
    }

    [TestMethod]
    public void MixedFeedbackBlocksAndReturnsGenericDetails()
    {
        var fw = Context();
        var controller = new PlainController(fw);
        controller.Error("email", "EMAIL");
        controller.Warning("title", message: "Review title");

        var exception = Assert.ThrowsExactly<ValidationException>(() => controller.validateCheckResult());
        var json = (FwDict)controller.actionError(exception, [7])!["_json"]!;
        var details = (FwDict)((FwDict)json["error"]!)["details"]!;
        Assert.AreEqual("EMAIL", details["email"]);
        Assert.IsTrue(details["INVALID"].toBool());
        Assert.AreEqual(2, ((FwList)json["form_issues"]!).Count);
        Assert.AreEqual(400, fw.response.StatusCode);
    }

    [TestMethod]
    public void RequiredProjectionKeepsFieldAndSummaryFlags()
    {
        var fw = Context();
        var controller = new PlainController(fw);
        var item = new FwDict { ["present"] = "value" };

        Assert.IsFalse(controller.validateRequired(0, item, new[] { "present", "missing" }));
        var errors = fw.getFormErrors();
        Assert.IsTrue(errors["missing"].toBool());
        Assert.IsTrue(errors["REQUIRED"].toBool());
        Assert.IsFalse(errors.ContainsKey("INVALID"));
    }

    [TestMethod]
    public void LocalRequiredDictionaryAndPartialUpdatesStayLocal()
    {
        var fw = Context();
        var controller = new PlainController(fw);
        var localErrors = new FwDict();

        Assert.IsFalse(controller.validateRequired(0, [], new[] { "missing" }, localErrors));
        Assert.IsTrue(localErrors["missing"].toBool());
        Assert.AreEqual(0, fw.FormIssues.Count);
        Assert.IsTrue(controller.validateRequired(7, [], new[] { "missing" }));
        Assert.AreEqual(0, fw.FormIssues.Count);
    }

    [TestMethod]
    public void ExternalFailureCreatesFormWideInvalidIssue()
    {
        var fw = Context();
        var controller = new PlainController(fw);

        Assert.ThrowsExactly<ValidationException>(() => controller.validateCheckResult(false));
        var issue = fw.FormIssues.Single();
        Assert.AreEqual(string.Empty, issue["field"]);
        Assert.AreEqual("INVALID", issue["code"]);
        Assert.IsTrue(fw.getFormErrors()["INVALID"].toBool());
    }

    [TestMethod]
    public void ExplicitGenericErrorDetailsArePreserved()
    {
        var fw = Context();
        var controller = new PlainController(fw);
        controller.Error("title", "WRONG");
        var explicitDetails = new FwDict { ["external"] = "kept" };
        var moreJson = new FwDict
        {
            ["error"] = new FwDict { ["message"] = "External", ["details"] = explicitDetails }
        };

        var json = (FwDict)controller.afterSave(false, 7, more_json: moreJson)!["_json"]!;
        Assert.AreSame(explicitDetails, ((FwDict)json["error"]!)["details"]);
    }

    [TestMethod]
    public void ControllerInitializationKeepsRequestIssues()
    {
        var fw = Context();
        var controller = new PlainController(fw);
        controller.Warning("title", message: "Keep me");

        controller.init(fw);
        Assert.AreEqual(1, fw.FormIssues.Count);
        Assert.AreEqual("Keep me", fw.FormIssues.Single()["message"]);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(7)]
    public void DynamicErrorsBlockAndWarningsAllowBothSavePaths(int id)
    {
        var fw = Context();
        var controller = new DynamicController(fw) { Severity = FW.ISSUE_ERROR };
        var exception = Assert.ThrowsExactly<ValidationException>(() => controller.SaveAction(id));
        Assert.AreEqual(0, controller.Model.Writes);
        var json = (FwDict)controller.actionError(exception, [id])!["_json"]!;
        Assert.AreEqual("Please review this title.", fw.getFormErrors()["title"]);
        Assert.IsFalse(((FwList)json["form_issues"]!).Single().ContainsKey("value"));

        fw = Context();
        controller = new DynamicController(fw) { Severity = FW.ISSUE_WARNING };
        json = (FwDict)controller.SaveAction(id)!["_json"]!;
        Assert.AreEqual(1, controller.Model.Writes);
        Assert.IsFalse(json.ContainsKey("error"));
        Assert.AreEqual(FW.ISSUE_WARNING, ((FwList)json["form_issues"]!).Single()["severity"]);
    }

    [TestMethod]
    public void DynamicRequiredValidationReturnsStructuredIssue()
    {
        var fw = Context();
        fw.FORM["item"] = new FwDict { ["title"] = string.Empty };
        var controller = new DynamicController(fw);
        Assert.ThrowsExactly<ValidationException>(() => controller.SaveAction(0));

        var json = (FwDict)controller.afterSave(false, 0)!["_json"]!;
        Assert.AreEqual("REQUIRED", ((FwList)json["form_issues"]!).Single()["code"]);
        Assert.IsTrue(((FwDict)((FwDict)json["error"]!)["details"]!)["title"].toBool());
        Assert.AreEqual(0, controller.Model.Writes);
    }

    [TestMethod]
    public void DynamicRowMetadataAndHtmlFeedbackDoNotExposeSensitiveValues()
    {
        var fw = Context(false);
        var controller = new DynamicController(fw);
        controller.RowIssue();
        var state = controller.ShowFormAction(7)!;
        var issue = ((FwList)state["form_issues"]!).Single();
        Assert.AreEqual("details", issue["tab"]);
        Assert.AreEqual("new-1", issue["row_id"]);
        Assert.IsFalse(issue.ContainsKey("value"));
        Assert.ThrowsExactly<RedirectException>(() => controller.afterSave(true, 7, location: "/Dynamic/7/edit"));
        Assert.AreEqual("Review row.", fw.SessionDict("_flash")!["warning"]);
    }
}
