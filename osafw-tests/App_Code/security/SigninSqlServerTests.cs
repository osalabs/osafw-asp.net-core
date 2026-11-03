using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace osafw.Tests;

[TestClass, TestCategory("SqlServer")]
public class SigninSqlServerTests
{
    [TestMethod]
    public void NativeSqlRememberTokensExpireRenewAndRejectDisabledOrMfaAccounts()
    {
        if (Environment.GetEnvironmentVariable("FW_TEST_SQLSERVER") != "1")
            Assert.Inconclusive("Run the SqlServer profile.");
        using var database = new SqlServerTestDatabase();
        using var scope = new FwTestScope(name => new DB(database.ConnectionString, DB.DBTYPE_SQLSRV, name),
            context: TestHelpers.CreateHttpContext());
        var fw = scope.Fw;
        fw.is_log_events = false;
        var users = fw.model<Users>();
        users.is_log_changes = false;
        var id = fw.db.insert("users", DB.h("email", "sql-member@example.test", "access_level", Users.ACL_MEMBER));
        fw.db.insert("users_cookies", DB.h("users_id", id, "cookie_id", Utils.sha256("expired-token"),
            "add_time", new DateTime(2000, 1, 1)));
        fw.request.Headers.Cookie = Users.PERM_COOKIE_NAME + "=expired-token";
        Assert.IsFalse(users.checkPermanentLogin());

        var token = users.createPermCookie(id);
        fw.request.Headers.Cookie = Users.PERM_COOKIE_NAME + "=" + token;
        Assert.IsTrue(users.checkPermanentLogin());
        Assert.AreEqual(id, fw.userId);
        users.update(id, DB.h("status", Users.STATUS_ACTIVE, "fname", "Profile only"));
        Assert.AreEqual(1, fw.db.value("users_cookies", DB.h("users_id", id), "count(*)").toInt());

        fw.context!.Session.Clear();
        // Model an administrative change between requests that leaves an older cookie present.
        fw.db.update("users", DB.h("status", Users.STATUS_INACTIVE), DB.h("id", id));
        users.removeCache(id);
        Assert.IsFalse(users.checkPermanentLogin());
        Assert.AreEqual(0, fw.userId);
        Assert.AreEqual(0, fw.db.value("users_cookies", DB.h("users_id", id), "count(*)").toInt());

        users.update(id, DB.h("status", Users.STATUS_ACTIVE));
        token = users.createPermCookie(id);
        fw.request.Headers.Cookie = Users.PERM_COOKIE_NAME + "=" + token;
        fw.db.update("users", DB.h("mfa_secret", "synthetic-enrollment"), DB.h("id", id));
        users.removeCache(id);
        Assert.IsFalse(users.checkPermanentLogin());
        Assert.AreEqual(0, fw.userId);
        Assert.AreEqual(string.Empty, users.createPermCookie(id));
    }
}
