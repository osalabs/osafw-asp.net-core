using Microsoft.Playwright;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace osafw.Tests;

[TestClass]
public class PasswordStrengthTests
{
    private const string STRONG_PASSWORD = "River!Cobalt7Moon";

    private sealed class PasswordDb : RejectingDb
    {
        public override object? valuep(string sql, FwDict? parameters = null)
        {
            Assert.AreEqual($"SELECT {sqlNOW()}", sql);
            return DateTime.Now;
        }
    }

    private sealed class PasswordUsers : Users
    {
        public override bool isExists(object uniq_key, int not_id) => false;
        public override bool isReadOnly(int id = -1) => false;
        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
        public override DBRow one(int id) => new(new FwDict { ["pwd"] = "stored" });
        public override DBRow oneByEmail(string email) => new(new FwDict
        {
            ["id"] = 7, ["status"] = STATUS_ACTIVE, ["pwd_reset"] = "stored",
            ["pwd_reset_time"] = DateTime.Now.ToString("O")
        });
        public override bool checkPwd(string plain_pwd, string pwd_hash, int trim_at = 32) => plain_pwd == "old";
    }

    private sealed class PersonalController : MyPasswordController
    {
        public void UseModel(Users users) => model = users;
    }

    private sealed class ResetController : PasswordResetController
    {
        public void UseModel(Users users) => model = users;
    }

    [TestMethod]
    [DataRow("", false)]
    [DataRow("password", false)]
    [DataRow("Abc123!", true)]
    [DataRow("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    [DataRow(STRONG_PASSWORD, true)]
    [DataRow("long varied phrase for a password", true)]
    public void Policy_AcceptsGoodOrStrongPasswords(string password, bool expected)
    {
        Assert.AreEqual(expected, new Users().isAcceptablePwd(password));
    }

    [TestMethod]
    public void Policy_IgnoresDiscardedCharacters()
    {
        var users = new Users();
        Assert.IsFalse(users.isAcceptablePwd(new string(' ', 32) + STRONG_PASSWORD));
        Assert.IsFalse(users.isAcceptablePwd(new string('a', 32) + STRONG_PASSWORD));
        Assert.AreEqual(users.isAcceptablePwd(STRONG_PASSWORD), users.isAcceptablePwd("  " + STRONG_PASSWORD + "  "));
    }

    [TestMethod]
    public void Scoring_PreservesFractionalContributions()
    {
        var users = new Users();
        Assert.AreEqual(98.01722919472344, users.scorePwd("Abcdef1234!"), 0.00000001);
        Assert.IsTrue(users.isAcceptablePwd("Abcdef1234!"));
        Assert.IsFalse(users.isAcceptablePwd("Abcdef12")); // Score exactly 60.
        Assert.IsTrue(users.isAcceptablePwd("Abcdef123"));
        Assert.IsTrue(users.isAcceptablePwd("Abcdef12345!"));
    }

    [TestMethod]
    [DataRow("personal")]
    [DataRow("admin")]
    [DataRow("signup")]
    [DataRow("reset")]
    public void Validators_RejectWeakAndAcceptGoodOrStrong(string flow)
    {
        using var scope = createScope();
        var fw = scope.Fw;
        var controller = createController(fw, flow);
        Assert.Throws<UserException>(() => validate(controller, input("password")));
        Assert.AreEqual("BAD", fw.getFormErrors()["pwd"].toStr());
        fw.FormIssues.Clear();
        validate(controller, input("Abcdef1234!"));
        validate(controller, input(STRONG_PASSWORD));
        Assert.AreEqual(0, fw.FormIssues.Count);
    }

    [TestMethod]
    [DataRow("personal")]
    [DataRow("admin")]
    [DataRow("signup")]
    [DataRow("reset")]
    public void SaveActions_RejectWeakBeforePersistence(string flow)
    {
        using var scope = createScope();
        var fw = scope.Fw;
        var controller = createController(fw, flow);
        fw.FORM["item"] = input("password");
        fw.FORM["login"] = "person@example.test";
        fw.FORM["token"] = "old";
        Assert.Throws<UserException>(() =>
        {
            switch (controller)
            {
                case MyPasswordController personal: personal.SaveAction(); break;
                case AdminUsersController admin: admin.SaveAction(7); break;
                case SignupController signup: signup.SaveAction(); break;
                case PasswordResetController reset: reset.SaveAction(); break;
            }
        });
        Assert.AreEqual("BAD", fw.getFormErrors()["pwd"].toStr());
        // RejectingDb fails the test if an unexpected DB operation is attempted.
    }

    [TestMethod]
    public void Admin_BlankOrOmittedPasswordRemainsOptional()
    {
        using var scope = createScope();
        var controller = createController(scope.Fw, "admin");
        foreach (var password in new[] { "", "   " })
        {
            validate(controller, input(password));
        }
        var item = input(STRONG_PASSWORD);
        item.Remove("pwd");
        validate(controller, item);
        Assert.AreEqual(0, scope.Fw.FormIssues.Count);
    }

    [TestMethod]
    public void PersonalChange_StillChecksOldPassword()
    {
        using var scope = createScope();
        var controller = createController(scope.Fw, "personal");
        var item = input(STRONG_PASSWORD);
        item["old_pwd"] = "wrong";
        Assert.Throws<UserException>(() => validate(controller, item));
        Assert.AreEqual("WRONG", scope.Fw.getFormErrors()["old_pwd"].toStr());
    }

    [TestMethod]
    public void ExistingWeakPasswordsAndTokens_StillVerify()
    {
        var users = new Users();
        var hash = users.hashPwd("old");
        Assert.IsTrue(users.checkPwd("old", hash));
        Assert.IsFalse(users.checkPwd("wrong", hash));
        var token = new string('x', Users.PWD_RESET_TOKEN_LEN);
        Assert.IsTrue(users.checkPwd(token, users.hashPwd(token, Users.PWD_RESET_TOKEN_LEN), Users.PWD_RESET_TOKEN_LEN));
    }

    [TestMethod]
    [DataRow("my/password/showform/form.html")]
    [DataRow("admin/users/showform/form_left.html")]
    [DataRow("signup/showform/form.html")]
    [DataRow("passwordreset/index/main.html")]
    public void Forms_RenderConciseStrengthErrorWithoutPermanentGuidance(string template)
    {
        var parser = new ParsePage(new ParsePageOptions { TemplatesRoot = TestHelpers.TemplateRoot() });
        var html = parser.parse_page("", template, new FwDict());
        Assert.DoesNotContain("meter shows strong", html);
        StringAssert.Contains(html, "Password too weak. Use more characters, mixed case, numbers or symbols.");
        StringAssert.Contains(html, "class=\"err-BAD\"");
        StringAssert.Contains(html, "maxlength=\"32\"");

    }

    [TestMethod]
    public async Task BrowserMeter_MatchesServerAndDisplaysFieldError()
    {
        var root = Path.GetFullPath(Path.Combine(TestHelpers.TemplateRoot(), "../.."));
        var assets = Path.Combine(root, "wwwroot/assets");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        var parser = new ParsePage(new ParsePageOptions { TemplatesRoot = TestHelpers.TemplateRoot() });
        var html = parser.parse_page("", "signup/showform/form.html", new FwDict
        {
            ["error"] = new FwDict { ["details"] = new FwDict { ["pwd"] = "BAD" } }
        });
        await page.SetContentAsync(html);
        await page.AddStyleTagAsync(new() { Path = Path.Combine(assets, "lib/bootstrap/css/bootstrap.min.css") });
        await page.AddStyleTagAsync(new() { Path = Path.Combine(assets, "css/site.css") });
        await page.AddScriptTagAsync(new() { Path = Path.Combine(assets, "lib/jquery/jquery.min.js") });
        await page.AddScriptTagAsync(new() { Path = Path.Combine(assets, "js/fw.js") });
        await page.EvaluateAsync("() => { $('#pwd').on('input', fw.renderPwdBar); fw.process_form_errors(); }");
        Assert.AreEqual("Password too weak. Use more characters, mixed case, numbers or symbols.", await page.Locator(".err-BAD.invalid-feedback").TextContentAsync());
        Assert.IsTrue(await page.Locator(".err-BAD.invalid-feedback").IsVisibleAsync());
        await page.EvaluateAsync("() => fw.process_form_errors($('form'), {})");
        Assert.IsFalse(await page.Locator(".err-BAD").IsVisibleAsync());
        Assert.AreEqual(0, await page.Locator("#pwd + .form-text").CountAsync());
        var users = new Users();
        foreach (var password in new[] { "password", "Abcdef1234!", "Abcdef12345!", STRONG_PASSWORD,
            new string('a', 32) + STRONG_PASSWORD, "  " + STRONG_PASSWORD + "  ",
            "\u0085Abcdef1234!\u0085", "\ufeffAbcdef1234!\ufeff", "Mixed١٢٣Émoji😀!" })
        {
            var score = await page.EvaluateAsync<double>("password => fw.scorePwd(fw.cleanPwd(password))", password);
            Assert.AreEqual(users.scorePwd(users.cleanPwd(password)), score, 0.00000001);
            await page.EvaluateAsync("password => $('#pwd').val(password).trigger('input')", password);
            var label = await page.Locator(".progress-bar").TextContentAsync();
            Assert.AreEqual(users.isAcceptablePwd(password), label == "good" || label == "strong");
        }
    }

    private static FwTestScope createScope()
    {
        var scope = new FwTestScope(_ => new PasswordDb(), new Dictionary<string, string?>
        {
            ["appSettings:template"] = TestHelpers.TemplateRoot(),
            ["appSettings:IS_SIGNUP"] = "true"
        }, TestHelpers.CreateHttpContext());
        scope.Fw.config()["IS_SIGNUP"] = true;
        scope.Fw.Session("user_id", "7");
        scope.Fw.request.Method = "POST";
        var users = new PasswordUsers();
        users.init(scope.Fw);
        TestHelpers.RegisterModel(scope.Fw, (Users)users);
        return scope;
    }

    private static FwController createController(FW fw, string flow)
    {
        FwController controller = flow switch
        {
            "personal" => new PersonalController(),
            "admin" => new AdminUsersController(),
            "signup" => new SignupController(),
            "reset" => new ResetController(),
            _ => throw new ArgumentException(nameof(flow))
        };
        controller.init(fw);
        if (controller is PersonalController personal)
            personal.UseModel(fw.model<Users>());
        if (controller is ResetController reset)
            reset.UseModel(fw.model<Users>());
        return controller;
    }

    private static FwDict input(string password) => new()
    {
        ["email"] = "person@example.test", ["ehack"] = "person@example.test",
        ["access_level"] = Users.ACL_MEMBER, ["old_pwd"] = "old", ["pwd"] = password, ["pwd2"] = password
    };

    private static void validate(FwController controller, FwDict item)
    {
        switch (controller)
        {
            case MyPasswordController personal: personal.Validate(7, item); break;
            case AdminUsersController admin: admin.Validate(7, item); break;
            case SignupController signup: signup.Validate(item); break;
            case PasswordResetController reset: reset.Validate(7, item); break;
        }
    }
}
