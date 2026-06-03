using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace osafw.Tests;

[TestClass]
public class SecurityQuickFixTests
{
    private const string RootDomain = "https://app.example.test";

    private sealed class LoginUsers : Users
    {
        public DBRow User { get; } = new(userRow(STATUS_ACTIVE));

        public override DBRow oneByEmail(string email) => User;

        public override bool checkPwd(string plain_pwd, string pwd_hash, int trim_at = 32) => true;

        public override bool update(int id, FwDict item) => true;

        public override DBRow one(int id) => User;

        public override bool isAccessLevel(int min_acl) => false;

        public override bool isReadOnly(int id = -1) => false;

        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    private sealed class PasswordUsers : Users
    {
        private readonly DBRow user;

        public int ResetDeliveries { get; private set; }

        public PasswordUsers(DBRow user)
        {
            this.user = user;
        }

        public override DBRow oneByEmail(string email) => user;

        public override bool sendPwdReset(int id)
        {
            ResetDeliveries++;
            return true;
        }

        public override bool isReadOnly(int id = -1) => false;

        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    private sealed class TestLoginController : LoginController
    {
        public void UseModel(Users users) => model = users;
    }

    private sealed class TestPasswordController : PasswordController
    {
        public void UseModel(Users users) => model = users;
    }

    [TestMethod]
    public void Constructor_DoesNotThrowForShortContentType()
    {
        var context = new DefaultHttpContext
        {
            Session = new TestHelpers.FakeSession(),
        };
        context.Request.ContentType = "a";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

        var fw = new FW(context, new ConfigurationBuilder().Build());

        Assert.IsNotNull(fw);
    }

    [TestMethod]
    [DataRow("/Main", true)]
    [DataRow("/Admin/Users?tab=profile#activity", true)]
    [DataRow(RootDomain, true)]
    [DataRow(RootDomain + "/Admin/Users", true)]
    [DataRow("//evil.example.test/path", false)]
    [DataRow("/\\evil.example.test", false)]
    [DataRow("\\\\evil.example.test\\path", false)]
    [DataRow("http:evil.example.test", false)]
    [DataRow("javascript:alert(1)", false)]
    [DataRow("https://evil.example.test/path", false)]
    [DataRow(RootDomain + ".evil.example.test/Admin", false)]
    public void IsAppUrl_AllowsOnlyAppLocalUrls(string url, bool expected)
    {
        Assert.AreEqual(expected, Utils.isAppUrl(url, RootDomain));
    }

    [TestMethod]
    [DataRow("//evil.example.test/path")]
    [DataRow("/\\evil.example.test")]
    [DataRow("http:evil.example.test")]
    [DataRow("javascript:alert(1)")]
    [DataRow("https://evil.example.test/path")]
    public void Login_UnsafeGourlFallsBackToDefault(string gourl)
    {
        var fw = createFw(new Dictionary<string, string?>
        {
            ["appSettings:LOGGED_DEFAULT_URL"] = "/Main",
        });
        fw.is_log_events = false;
        fw.config()["LOGGED_DEFAULT_URL"] = "/Main";
        fw.FORM["gourl"] = gourl;
        fw.FORM["item"] = new FwDict
        {
            ["login"] = "user@example.test",
            ["pwdh"] = "secret",
        };
        var users = new LoginUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        var controller = new TestLoginController();
        controller.init(fw);
        controller.UseModel(users);

        Assert.ThrowsExactly<RedirectException>(() => controller.SaveAction());

        Assert.AreEqual(302, fw.response.StatusCode);
        Assert.AreEqual("/Main", fw.response.Headers["Location"].ToString());
    }

    [TestMethod]
    public void PasswordResetRequest_KnownEmailRedirectsAndSendsReset()
    {
        var users = new PasswordUsers(new DBRow(userRow(Users.STATUS_ACTIVE)));
        var fw = createPasswordFw(users, "known@example.test");
        var controller = createPasswordController(fw, users);

        Assert.ThrowsExactly<RedirectException>(() => controller.SaveAction());

        Assert.AreEqual(1, users.ResetDeliveries);
        Assert.AreEqual("/Password/(Sent)", fw.response.Headers["Location"].ToString());
    }

    [TestMethod]
    public void PasswordResetRequest_UnknownEmailRedirectsWithoutSendingReset()
    {
        var users = new PasswordUsers(new DBRow());
        var fw = createPasswordFw(users, "unknown@example.test");
        var controller = createPasswordController(fw, users);

        Assert.ThrowsExactly<RedirectException>(() => controller.SaveAction());

        Assert.AreEqual(0, users.ResetDeliveries);
        Assert.AreEqual("/Password/(Sent)", fw.response.Headers["Location"].ToString());
    }

    private static FW createPasswordFw(PasswordUsers users, string login)
    {
        var fw = createFw();
        TestHelpers.RegisterModel(fw, (Users)users);
        fw.FORM["item"] = new FwDict
        {
            ["login"] = login,
        };
        return fw;
    }

    private static TestPasswordController createPasswordController(FW fw, PasswordUsers users)
    {
        var controller = new TestPasswordController();
        controller.init(fw);
        controller.UseModel(users);
        return controller;
    }

    private static FW createFw(IDictionary<string, string?>? settings = null)
    {
        var fw = TestHelpers.CreateFw(settings);
        fw.is_log_events = false;
        return fw;
    }

    private static FwDict userRow(int status) => new()
    {
        ["id"] = "9",
        ["email"] = "user@example.test",
        ["access_level"] = Users.ACL_MEMBER,
        ["status"] = status,
        ["pwd"] = "hash",
        ["mfa_secret"] = "",
        ["lang"] = "en",
        ["ui_theme"] = "",
        ["ui_mode"] = "",
        ["date_format"] = DateUtils.DATE_FORMAT_DMY,
        ["time_format"] = DateUtils.TIME_FORMAT_24,
        ["timezone"] = "",
        ["fname"] = "Test",
        ["lname"] = "User",
        ["att_id"] = "0",
    };
}
