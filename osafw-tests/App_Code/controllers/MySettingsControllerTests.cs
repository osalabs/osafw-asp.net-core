using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace osafw.Tests;

[TestClass]
public class MySettingsControllerTests
{
    private class StubUsers : Users
    {
        private readonly FwDict row;

        public FwDict LastUpdate { get; private set; } = [];

        public StubUsers(string timezone)
        {
            row = userRow(timezone);
        }

        public override DBRow one(int id) => new(row);

        public override bool isExists(object uniq_key, int not_id) => false;

        public override bool isReadOnly(int id = -1) => false;

        public override bool update(int id, FwDict item)
        {
            LastUpdate = new FwDict(item);
            foreach (var kv in item)
                row[kv.Key] = kv.Value;
            return true;
        }
    }

    private class TestMySettingsController : MySettingsController
    {
        public void UseModel(Users users) => model = users;
    }

    [TestMethod]
    public void SaveAction_AutoTimezoneStoresBlankAndUsesDetectedSessionTimezone()
    {
        var fw = createFw("UTC");
        var users = new StubUsers("UTC");
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);

        var controller = new TestMySettingsController();
        controller.init(fw);
        controller.UseModel(users);
        fw.FORM["item"] = settingsItem("", "Europe/Kyiv");

        controller.SaveAction();

        Assert.AreEqual("", users.LastUpdate["timezone"]);
        Assert.AreEqual("Europe/Kyiv", fw.Session("timezone"));
    }

    [TestMethod]
    public void SaveAction_ExplicitUtcPersistsUtcAndDoesNotUseDetectedTimezone()
    {
        var fw = createFw("Europe/Kyiv");
        var users = new StubUsers("");
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);

        var controller = new TestMySettingsController();
        controller.init(fw);
        controller.UseModel(users);
        fw.FORM["item"] = settingsItem(DateUtils.TZ_UTC, "Europe/Kyiv");

        controller.SaveAction();

        Assert.AreEqual(DateUtils.TZ_UTC, users.LastUpdate["timezone"]);
        Assert.AreEqual(DateUtils.TZ_UTC, fw.Session("timezone"));
    }

    private static FW createFw(string currentTimezone)
    {
        var fw = TestHelpers.CreateFw();
        fw.is_log_events = false;
        fw.G["date_format"] = DateUtils.DATE_FORMAT_MDY;
        fw.G["time_format"] = DateUtils.TIME_FORMAT_12;
        fw.G["timezone"] = currentTimezone;
        fw.Session("user_id", "9");
        fw.context.Request.Headers.Accept = "application/json";
        return fw;
    }

    private static FwDict settingsItem(string timezone, string detectedTimezone) => new()
    {
        ["email"] = "user@example.com",
        ["fname"] = "Test",
        ["lname"] = "User",
        ["date_format"] = DateUtils.DATE_FORMAT_MDY,
        ["time_format"] = DateUtils.TIME_FORMAT_12,
        ["timezone"] = timezone,
        ["timezone_auto"] = detectedTimezone,
    };

    private static FwDict userRow(string timezone) => new()
    {
        ["id"] = "9",
        ["email"] = "user@example.com",
        ["access_level"] = Users.ACL_MEMBER,
        ["is_readonly"] = "0",
        ["lang"] = "en",
        ["ui_theme"] = "",
        ["ui_mode"] = "",
        ["date_format"] = DateUtils.DATE_FORMAT_MDY,
        ["time_format"] = DateUtils.TIME_FORMAT_12,
        ["timezone"] = timezone,
        ["fname"] = "Test",
        ["lname"] = "User",
        ["att_id"] = "0",
    };
}
