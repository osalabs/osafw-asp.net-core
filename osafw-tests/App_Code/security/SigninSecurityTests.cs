using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace osafw.Tests;

[TestClass]
public class SigninSecurityTests
{
    private sealed class LoginDb : RejectingDb
    {
        internal DBRow User = new(DB.h("id", 9, "status", 0, "access_level", Users.ACL_MEMBER));
        internal string Lookup = "";
        internal bool HasCookie = true;
        internal int Revoked;
        public override DBRow row(string table, FwDict where, string order_by = "")
        {
            if (table == "users_cookies")
                return HasCookie ? new DBRow(DB.h("users_id", 9)) : [];
            if (table == "users")
            {
                if (where.ContainsKey("login"))
                {
                    Lookup = where["login"].toStr();
                    return Lookup == @"DOMAIN\alex" ? User : [];
                }
                return User;
            }
            throw Unexpected("row " + table);
        }
        public override int del(string table, FwDict? where = null)
        {
            Assert.AreEqual("users_cookies", table);
            Revoked++;
            return 1;
        }
        public override int update(string table, FwDict fields, FwDict where) => 1;
    }

    private sealed class LoginSettings : Settings
    {
        internal bool Enforced;
        public override bool readBool(string code, bool defaultValue = false) => code == "is_mfa_enforced" && Enforced;
        public override string read(string code) => "/Main";
    }

    [TestMethod]
    [DataRow("GET", "token", "token", false)]
    [DataRow("POST", "token", "wrong", false)]
    [DataRow("POST", "", "", false)]
    [DataRow("POST", "token", "token", true)]
    public void LoginRedisplaysFormWithoutAuthenticatingForUnsafeOrEmptyRequests(string method, string sessionToken, string token, bool query)
    {
        using var scope = new FwTestScope(_ => new RejectingDb(), context: TestHelpers.CreateHttpContext());
        var fw = scope.Fw;
        fw.route.controller = "Login";
        fw.route.method = method;
        fw.config()["template"] = TestHelpers.TemplateRoot();
        fw.config()["is_lang_update"] = false;
        fw.is_log_events = false;
        fw.Session("XSS", sessionToken);
        fw.FORM["XSS"] = token;
        if (query)
            fw.request.QueryString = new QueryString("?item[login]=attacker&item[pwdh]=secret");
        var users = new WindowsUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        var controller = new LoginController();
        // Guard runs before model use, so no real database can be contacted.
        controller.init(fw);
        controller.SaveAction();
        Assert.AreEqual(FW.ACTION_INDEX, fw.route.action);
        Assert.AreEqual(0, fw.userId);
        if (method == "POST")
            Assert.AreEqual("", fw.G["err_msg"].toStr());
    }

    [TestMethod]
    [DataRow(Users.STATUS_INACTIVE, false, false, false)]
    [DataRow(Users.STATUS_DELETED, false, false, false)]
    [DataRow(Users.STATUS_ACTIVE, true, false, false)]
    [DataRow(Users.STATUS_ACTIVE, false, true, false)]
    [DataRow(Users.STATUS_ACTIVE, false, false, true)]
    public void RememberLoginRequiresActiveAccountAndInteractiveMfa(int status, bool enrolled, bool enforced, bool allowed)
    {
        var db = new LoginDb();
        db.User["status"] = status.toStr();
        db.User["mfa_secret"] = enrolled ? "test-enrollment" : "";
        using var scope = new FwTestScope(_ => db, context: TestHelpers.CreateHttpContext());
        var fw = scope.Fw;
        fw.request.Headers.Cookie = Users.PERM_COOKIE_NAME + "=test-cookie";
        fw.is_log_events = false;
        var settings = new LoginSettings { Enforced = enforced };
        settings.init(fw);
        TestHelpers.RegisterModel(fw, (Settings)settings);
        var users = fw.model<Users>();
        users.is_log_changes = false;
        Assert.AreEqual(allowed, users.checkPermanentLogin());
        Assert.AreEqual(allowed ? 9 : 0, fw.userId);
        Assert.AreEqual(allowed ? 0 : 2, db.Revoked);
    }

    [TestMethod]
    [DataRow(@"DOMAIN\alex", "/Main")]
    [DataRow(@"OTHER\alex", "/Login")]
    public void WindowsLoginUsesFullPrincipalAndTrustsWindowsAuthentication(string principal, string destination)
    {
        var db = new LoginDb();
        db.User["mfa_secret"] = "test-enrollment";
        using var scope = new FwTestScope(_ => db, context: TestHelpers.CreateHttpContext());
        var fw = scope.Fw;
        fw.is_log_events = false;
        var settings = new LoginSettings { Enforced = true };
        settings.init(fw);
        TestHelpers.RegisterModel(fw, (Settings)settings);
        // Initializing a controller needs only these non-DB permission hooks.
        var users = new WindowsUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        fw.context!.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, principal) }, "Negotiate"));
        var controller = new WinLoginController();
        controller.init(fw);
        Assert.ThrowsExactly<RedirectException>(() => controller.IndexAction());
        Assert.AreEqual(principal, db.Lookup);
        Assert.AreEqual(destination, fw.response.Headers.Location.ToString());
        Assert.AreEqual(0, fw.Session("mfa_login_users_id").toInt());
        Assert.AreEqual(destination == "/Main" ? 9 : 0, fw.userId);
    }

#if isSQLite
    [TestMethod]
    [DataRow("pwd", "Replacement12!", true)]
    [DataRow("status", "10", true)]
    [DataRow("mfa_secret", "new-enrollment", true)]
    [DataRow("fname", "New name", false)]
    [DataRow("status", "0", false)]
    public void AccountSecurityUpdatesRevokePersistentTokens(string field, string value, bool revoked)
    {
        using var database = new SqliteFile();
        using var scope = new FwTestScope(database.CreateDb, context: TestHelpers.CreateHttpContext());
        var fw = scope.Fw;
        fw.db.exec("CREATE TABLE users (id INTEGER PRIMARY KEY, pwd TEXT, status INT, mfa_secret TEXT, mfa_recovery TEXT, fname TEXT, upd_time TEXT, login_time TEXT)");
        fw.db.exec("CREATE TABLE users_cookies (users_id INT, cookie_id TEXT, add_time TEXT)");
        fw.db.exec("INSERT INTO users(id,status) VALUES (9,0)");
        fw.db.exec("INSERT INTO users_cookies(users_id,cookie_id) VALUES (9,'old-token')");
        var users = fw.model<Users>();
        users.is_log_changes = false;
        Assert.IsTrue(users.update(9, DB.h(field, value, "fname", "Profile edit")));
        Assert.AreEqual(revoked ? 0 : 1, fw.db.value("users_cookies", [], "count(*)").toInt());
    }
#endif

    [TestMethod]
    [DataRow("challenge", true)]
    [DataRow("setup", true)]
    [DataRow("challenge", false)]
    public void AnonymousMfaSetupCannotReplaceExistingOrBypassChallenge(string purpose, bool enrolled)
    {
        var db = new LoginDb();
        db.User["mfa_secret"] = enrolled ? "existing-factor" : "";
        using var scope = new FwTestScope(_ => db, context: TestHelpers.CreateHttpContext());
        var fw = scope.Fw;
        fw.Session("mfa_login_users_id", "9");
        fw.Session("mfa_login_purpose", purpose);
        fw.Session("mfa_login_time", DateUtils.UnixTimestamp().ToString());
        var users = new WindowsUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        var controller = new MyMFAController();
        controller.init(fw);
        Assert.ThrowsExactly<AuthException>(() => controller.ShowFormAction());
        fw.route.method = "POST";
        fw.Session("XSS", "token");
        fw.FORM["XSS"] = "token";
        fw.Session("mfa_secret", "attacker-factor");
        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction());
        Assert.AreEqual(0, fw.userId);
    }

#if isSQLite
    [TestMethod]
    public void RememberTokenRenewalRefreshesExpiredIssuanceDate()
    {
        using var database = new SqliteFile();
        using var scope = new FwTestScope(database.CreateDb, context: TestHelpers.CreateHttpContext());
        var fw = scope.Fw;
        fw.is_log_events = false;
        fw.db.exec("CREATE TABLE users (id INTEGER PRIMARY KEY, status INT, mfa_secret TEXT, upd_time TEXT, upd_users_id INT, login_time TEXT)");
        fw.db.exec("CREATE TABLE users_cookies (users_id INT, cookie_id TEXT, add_time TEXT)");
        fw.db.exec("INSERT INTO users(id,status) VALUES (9,0)");
        fw.db.insert("users_cookies", DB.h("users_id", 9, "cookie_id", Utils.sha256("expired-cookie"), "add_time", new DateTime(2000,1,1)));
        var settings = new LoginSettings();
        settings.init(fw);
        TestHelpers.RegisterModel(fw, (Settings)settings);
        var users = fw.model<Users>();
        users.is_log_changes = false;
        fw.request.Headers.Cookie = Users.PERM_COOKIE_NAME + "=expired-cookie";
        Assert.IsFalse(users.checkPermanentLogin());
        var token = users.createPermCookie(9);
        Assert.IsFalse(string.IsNullOrEmpty(token));
        fw.request.Headers.Cookie = Users.PERM_COOKIE_NAME + "=" + token;
        Assert.IsTrue(users.checkPermanentLogin());
        Assert.AreEqual(9, fw.userId);
    }
#endif

    private sealed class WindowsUsers : Users
    {
        public override bool isReadOnly(int id = -1) => false;
        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }
}
