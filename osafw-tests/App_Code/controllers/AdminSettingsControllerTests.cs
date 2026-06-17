using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

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

    private static (FW fw, StubSettings model, AdminSettingsController controller) BuildController(FwDict setting, FwDict item, FwDict? extraForm = null)
    {
        var fw = TestHelpers.CreateFw();
        fw.request.Headers.Accept = "application/json";

        var model = new StubSettings();
        model.Rows[1] = setting;
        TestHelpers.RegisterModel(fw, (Settings)model);

        var controller = new AdminSettingsController();
        controller.init(fw);

        fw.FORM = new FwDict
        {
            ["item"] = item,
        };

        if (extraForm != null)
        {
            foreach (var entry in extraForm)
                fw.FORM[entry.Key] = entry.Value;
        }

        return (fw, model, controller);
    }

    private static FwDict Setting(int input, string value = "", string allowedValues = "")
    {
        return new FwDict
        {
            ["id"] = "1",
            ["input"] = input.toStr(),
            ["ivalue"] = value,
            ["allowed_values"] = allowedValues,
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
    public void MaskCredential_ShowsEdgesOnlyForLongValues()
    {
        Assert.AreEqual("ABCDEF...XYZ123", AdminSettingsController.maskCredential("ABCDEF-secret-XYZ123"));
        Assert.AreEqual("******", AdminSettingsController.maskCredential("short"));
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
        Assert.AreEqual("NUMBER", fw.FormErrors["ivalue"]);
    }

    [TestMethod]
    public void Number_EnforcesStepMetadata()
    {
        var (fw, model, controller) = BuildController(
            Setting(Settings.INPUT_NUMBER, "5", "min|1 step|1"),
            new FwDict { ["ivalue"] = "2.5" });

        AssertValidationFailure(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("STEP", fw.FormErrors["ivalue"]);
    }

    [TestMethod]
    public void Range_EnforcesMinimum()
    {
        var (fw, model, controller) = BuildController(
            Setting(Settings.INPUT_RANGE, "3", "min|1 max|5 step|1"),
            new FwDict { ["ivalue"] = "0" });

        AssertValidationFailure(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("MIN", fw.FormErrors["ivalue"]);
    }

    [TestMethod]
    public void Range_EnforcesMaximum()
    {
        var (fw, model, controller) = BuildController(
            Setting(Settings.INPUT_RANGE, "3", "min|1 max|5 step|1"),
            new FwDict { ["ivalue"] = "6" });

        AssertValidationFailure(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("MAX", fw.FormErrors["ivalue"]);
    }

    [TestMethod]
    public void Range_EnforcesStep()
    {
        var (fw, model, controller) = BuildController(
            Setting(Settings.INPUT_RANGE, "3", "min|1 max|5 step|2"),
            new FwDict { ["ivalue"] = "4" });

        AssertValidationFailure(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("STEP", fw.FormErrors["ivalue"]);
    }

    [TestMethod]
    public void Select_RejectsValuesOutsideAllowedValues()
    {
        var (fw, model, controller) = BuildController(
            Setting(Settings.INPUT_SELECT, "auto", "auto|Auto json|JSON native|Native"),
            new FwDict { ["ivalue"] = "xml" });

        AssertValidationFailure(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("INVALID", fw.FormErrors["ivalue"]);
    }

    [TestMethod]
    public void Radio_RejectsValuesOutsideAllowedValues()
    {
        var (fw, model, controller) = BuildController(
            Setting(Settings.INPUT_RADIO, "yes", "yes|Yes no|No"),
            new FwDict { ["ivalue"] = "maybe" });

        AssertValidationFailure(() => controller.SaveAction(1));
        Assert.AreEqual(0, model.UpdateCalls);
        Assert.AreEqual("INVALID", fw.FormErrors["ivalue"]);
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
}
