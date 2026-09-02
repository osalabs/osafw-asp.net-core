using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

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

    private sealed class PostGuardController : FwController
    {
        public PostGuardController(FW fw) : base(fw) { }

        public void EnforcePostForTest() => enforcePost();
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
    [DataRow("/\t/evil.example.test", false)]
    [DataRow("/\r/evil.example.test", false)]
    [DataRow("/\n/evil.example.test", false)]
    [DataRow("/Main\0", false)]
    [DataRow(RootDomain + "/Main\n", false)]
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
    [DataRow("//evil.example.test/path", "/Main")]
    [DataRow("/\\evil.example.test", "/Main")]
    [DataRow("http:evil.example.test", "/Main")]
    [DataRow("javascript:alert(1)", "/Main")]
    [DataRow("https://evil.example.test/path", "/Main")]
    [DataRow("https://google.com", "/Main")]
    [DataRow("/\t/google.com", "/Main")]
    [DataRow(RootDomain + ".evil.example.test/path", "/Main")]
    [DataRow("", "/Main")]
    [DataRow("/Admin/DemosDynamic?dofilter=1&f[title]=A%26B", "/Admin/DemosDynamic?dofilter=1&f[title]=A%26B")]
    [DataRow(RootDomain + "/Admin/DemosDynamic", RootDomain + "/Admin/DemosDynamic")]
    public void Login_SaveUsesOnlyAppLocalGourl(string gourl, string expected)
    {
        var fw = createFw(new Dictionary<string, string?>
        {
            ["appSettings:LOGGED_DEFAULT_URL"] = "/Main",
        });
        fw.is_log_events = false;
        fw.config()["LOGGED_DEFAULT_URL"] = "/Main";
        fw.config()["ROOT_DOMAIN"] = RootDomain;
        fw.config()["ROOT_URL"] = "";
        fw.config()["is_mfa_enforced"] = false;
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
        Assert.AreEqual(expected, fw.response.Headers["Location"].ToString());
    }

    [TestMethod]
    public void Login_IndexPreservesSafeLocalGourl()
    {
        var gourl = "/Admin/FwUpdates?dofilter=1&f[status]=0";
        var fw = createFw(new Dictionary<string, string?>
        {
            ["appSettings:ROOT_DOMAIN"] = RootDomain,
        });
        fw.route.method = "GET";
        fw.FORM["gourl"] = gourl;
        var users = new LoginUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        var controller = new TestLoginController();
        controller.init(fw);
        controller.UseModel(users);

        var ps = controller.IndexAction();

        Assert.AreEqual(gourl, ps["gourl"]);
    }

    [TestMethod]
    public void Login_IndexDropsUnsafeGourl()
    {
        var fw = createFw(new Dictionary<string, string?>
        {
            ["appSettings:ROOT_DOMAIN"] = RootDomain,
        });
        fw.route.method = "GET";
        fw.FORM["gourl"] = "https://evil.example.test/Admin/FwUpdates";
        var users = new LoginUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        var controller = new TestLoginController();
        controller.init(fw);
        controller.UseModel(users);

        var ps = controller.IndexAction();

        Assert.IsFalse(ps.ContainsKey("gourl"));
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

    [TestMethod]
    public void Assistant_LegacySqlAndRedirectPayloadIsIgnored()
    {
        string payload = """
        {
          "title": "Unsafe response",
          "explanation": "This SQL must not be stored.",
          "information": "Safe answer",
          "sql": "select * from users",
          "redirect_url": "https://evil.example.test/phish",
          "sources": [],
          "confidence": 0.25
        }
        """;

        var result = JsonSerializer.Deserialize<AssistantResult>(payload);

        Assert.IsNotNull(result);
        Assert.AreEqual("Unsafe response", result.title);
        Assert.AreEqual("Safe answer", result.information);
        Assert.IsFalse(typeof(AssistantResult).GetProperties().Any(static prop => prop.Name == "sql" || prop.Name == "redirect_url"));
    }

    [TestMethod]
    public void EnforcePost_RejectsGetEvenWithMatchingToken()
    {
        var fw = createFw();
        fw.route.method = "GET";
        setXssTokens(fw);
        var controller = new PostGuardController(fw);

        Assert.ThrowsExactly<AuthException>(() => controller.EnforcePostForTest());
    }

    [TestMethod]
    public void EnforcePost_RejectsPostWithMissingToken()
    {
        var fw = createFw();
        fw.route.method = "POST";
        fw.Session("XSS", "token");
        var controller = new PostGuardController(fw);

        Assert.ThrowsExactly<AuthException>(() => controller.EnforcePostForTest());
    }

    [TestMethod]
    public void EnforcePost_RejectsPostWithWrongToken()
    {
        var fw = createFw();
        fw.route.method = "POST";
        setXssTokens(fw, formToken: "wrong-token");
        var controller = new PostGuardController(fw);

        Assert.ThrowsExactly<AuthException>(() => controller.EnforcePostForTest());
    }

    [TestMethod]
    public void EnforcePost_AllowsPostWithMatchingToken()
    {
        var fw = createFw();
        fw.route.method = "POST";
        setXssTokens(fw);
        var controller = new PostGuardController(fw);

        controller.EnforcePostForTest();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not-an-email")]
    [DataRow("valid@example.test, bad-address")]
    public void ReportSendEmail_RejectsEmptyOrInvalidRecipients(string recipients)
    {
        var fw = createFw();
        fw.route.method = "POST";
        setXssTokens(fw);
        fw.FORM["f"] = new FwDict
        {
            ["to_emails"] = recipients,
            ["email_as"] = "pdf"
        };
        TestHelpers.RegisterModel(fw, (Users)new LoginUsers());
        var controller = new AdminReportsController();
        controller.init(fw);

        Assert.ThrowsExactly<UserException>(() => controller.SendEmailAction("Sample"));
    }

    [TestMethod]
    [DataRow("/Admin/DemosDynamic?page=2", "", "/Admin/DemosDynamic?page=2")]
    [DataRow("/Admin/DemosDynamic?page=2", "/portal", "/portal/Admin/DemosDynamic?page=2")]
    [DataRow("https://google.com", "", "/Main")]
    [DataRow("/\t/google.com", "", "/Main")]
    [DataRow("//google.com", "", "/Main")]
    [DataRow("", "", "/Main")]
    public void Login_AlreadyLoggedInHonorsOnlySafeGourl(string gourl, string rootUrl, string expected)
    {
        var fw = createFw();
        fw.config()["ROOT_DOMAIN"] = RootDomain;
        fw.config()["ROOT_URL"] = rootUrl;
        fw.config()["LOGGED_DEFAULT_URL"] = "/Main";
        fw.Session("user_id", "9");
        fw.FORM["gourl"] = gourl;
        TestHelpers.RegisterModel(fw, (Users)new LoginUsers());
        var controller = new LoginController();
        controller.init(fw);

        Assert.ThrowsExactly<RedirectException>(() => controller.IndexAction());

        Assert.AreEqual(expected, fw.response.Headers.Location.ToString());
    }

    [TestMethod]
    public void Login_FailedSubmissionPreservesGourlForRetry()
    {
        var fw = createFw();
        fw.config()["ROOT_URL"] = "";
        var repo = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (repo != null && !File.Exists(Path.Combine(repo.FullName, "osafw-asp.net-core.sln")))
            repo = repo.Parent;
        Assert.IsNotNull(repo);
        fw.config()["template"] = Path.Combine(repo.FullName, "osafw-app", "App_Data", "template");
        fw.config()["is_lang_update"] = false;
        fw.G["PAGE_LAYOUT_PUBLIC"] = "/login/index/form.html";
        fw.response.Body = new MemoryStream();
        fw.setController("Login", FW.ACTION_SAVE);
        fw.route.method = "POST";
        fw.FORM["gourl"] = "/Admin/DemosDynamic?page=2";
        fw.FORM["item"] = new FwDict { ["login"] = "user@example.test", ["pwdh"] = "" };
        TestHelpers.RegisterModel(fw, (Users)new LoginUsers());
        var controller = new LoginController();
        controller.init(fw);

        controller.SaveAction();

        fw.response.Body.Position = 0;
        var html = new StreamReader(fw.response.Body).ReadToEnd();
        StringAssert.Contains(html, "name=\"gourl\" value=\"/Admin/DemosDynamic?page=2\"");
        StringAssert.Contains(html, "method=\"post\" action=\"/Login\"");
        Assert.AreEqual(0, fw.userId);
    }

    [TestMethod]
    [DataRow("/Admin/DemosDynamic?page=2", "/Admin/DemosDynamic?page=2")]
    [DataRow("https://google.com", "/Main")]
    [DataRow("/\t/google.com", "/Main")]
    public void Login_MfaCompletionValidatesReturnUrl(string gourl, string expected)
    {
        var fw = createFw();
        fw.config()["ROOT_URL"] = "";
        fw.config()["LOGGED_DEFAULT_URL"] = "/Main";
        fw.Session("mfa_login_users_id", "9");
        fw.Session("mfa_login_time", DateUtils.UnixTimestamp().ToString());
        fw.Session("mfa_login_gourl", gourl);
        setXssTokens(fw);
        var users = new LoginUsers();
        users.User["mfa_secret"] = "JBSWY3DPEHPK3PXP";
        fw.FORM["item"] = new FwDict
        {
            ["code"] = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(users.User["mfa_secret"])).ComputeTotp()
        };
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        var controller = new TestLoginController();
        controller.init(fw);
        controller.UseModel(users);

        Assert.ThrowsExactly<RedirectException>(() => controller.SaveMFAAction());

        Assert.AreEqual(9, fw.userId);
        Assert.AreEqual(expected, fw.response.Headers.Location.ToString());
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

    private static void setXssTokens(FW fw, string sessionToken = "token", string? formToken = "token")
    {
        fw.Session("XSS", sessionToken);
        if (formToken != null)
            fw.FORM["XSS"] = formToken;
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
