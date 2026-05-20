using Microsoft.VisualStudio.TestTools.UnitTesting;
using OtpNet;

namespace osafw.Tests;

[TestClass]
public class UsersTimezoneTests
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

        public override bool update(int id, FwDict item)
        {
            LastUpdate = new FwDict(item);
            foreach (var kv in item)
                row[kv.Key] = kv.Value;
            return true;
        }
    }

    private class LoginContextUsers : Users
    {
        public DBRow User { get; } = new(userRow(""));

        public override DBRow oneByEmail(string email) => User;

        public override bool checkPwd(string plain_pwd, string pwd_hash, int trim_at = 32) => true;

        public override bool update(int id, FwDict item) => true;

        public override DBRow one(int id) => User;
    }

    private class TestLoginController : LoginController
    {
        public void UseModel(Users users) => model = users;
    }

    private class TestMyMFAController : MyMFAController
    {
        public void UseModel(Users users) => model = users;
    }

    [TestMethod]
    public void DoLogin_AutoTimezoneUsesBrowserTimezoneForSessionOnly()
    {
        var fw = createFw();
        var users = new StubUsers("");
        users.init(fw);

        users.doLogin(9, "Europe/Kyiv");

        Assert.AreEqual("Europe/Kyiv", fw.Session("timezone"));
        Assert.IsFalse(users.LastUpdate.ContainsKey("timezone"));
    }

    [TestMethod]
    public void DoLogin_ExplicitUtcIsNotOverwrittenByBrowserTimezone()
    {
        var fw = createFw();
        var users = new StubUsers(DateUtils.TZ_UTC);
        users.init(fw);

        users.doLogin(9, "Europe/Kyiv");

        Assert.AreEqual(DateUtils.TZ_UTC, fw.Session("timezone"));
        Assert.IsFalse(users.LastUpdate.ContainsKey("timezone"));
    }

    [TestMethod]
    public void EnforcedMfaSetupPreservesBrowserTimezoneForLogin()
    {
        var fw = createFw();
        fw.config()["is_mfa_enforced"] = true;
        fw.FORM["item"] = new FwDict
        {
            ["login"] = "user@example.com",
            ["pwdh"] = "secret",
            ["timezone"] = "Europe/Kyiv",
        };

        var users = new LoginContextUsers();
        users.init(fw);
        var login = new TestLoginController();
        login.init(fw);
        login.UseModel(users);

        Assert.ThrowsExactly<RedirectException>(() => login.SaveAction());
        Assert.AreEqual("Europe/Kyiv", fw.Session("mfa_login_timezone"));

        var mfa = new TestMyMFAController();
        mfa.init(fw);
        mfa.UseModel(users);
        var secret = "JBSWY3DPEHPK3PXP";
        var code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();
        fw.Session("mfa_secret", secret);
        fw.FORM["mfa_code"] = code;

        Assert.IsTrue(users.isValidMFACode(fw.Session("mfa_secret"), code));
        mfa.SaveAction();

        Assert.AreEqual("Europe/Kyiv", fw.Session("timezone"));
        Assert.AreEqual("", fw.Session("mfa_login_timezone"));
    }

    private static FW createFw()
    {
        var fw = TestHelpers.CreateFw();
        fw.is_log_events = false;
        return fw;
    }

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
