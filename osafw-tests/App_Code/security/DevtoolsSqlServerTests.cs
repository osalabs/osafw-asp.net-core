using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace osafw.Tests;

[TestClass, TestCategory("SqlServer")]
public class DevtoolsSqlServerTests
{
    private static FwTestScope Scope(SqlServerTestDatabase database)
    {
        return new FwTestScope(name => new DB(database.ConnectionString, DB.DBTYPE_SQLSRV, name),
            new Dictionary<string, string?>
            {
                ["appSettings:IS_DEV"] = "true",
                ["appSettings:site_root"] = Path.Combine(TestHelpers.RepoRoot(), "osafw-app")
            }, TestHelpers.CreateHttpContext());
    }

    [TestMethod]
    public void NativeSqlBootstrapAllowsEmptyDatabaseAndRefusesReinitialization()
    {
        if (Environment.GetEnvironmentVariable("FW_TEST_SQLSERVER") != "1")
            Assert.Inconclusive("Run the SqlServer profile.");
        using var database = new SqlServerTestDatabase(isInitializeSchema: false);
        using var scope = Scope(database);
        var fw = scope.Fw;
        var state = DevCli.initializeDatabase(fw);
        var password = state["pwd"].toStr();
        Assert.IsFalse(string.IsNullOrEmpty(password));
        Assert.IsTrue(fw.model<Users>().checkPwd(password, fw.db.value("users", DB.h("id", 1), "pwd").toStr()));
        Assert.IsGreaterThan(0, fw.db.value("settings", [], "count(*)").toInt());
        Assert.ThrowsExactly<UserException>(() => DevCli.initializeDatabase(fw));
        Assert.AreEqual(1, fw.db.value("users", [], "count(*)").toInt());
    }

    [TestMethod]
    public void NativeSqlConsoleReauthenticatesAndWebBootstrapCannotResetExistingData()
    {
        if (Environment.GetEnvironmentVariable("FW_TEST_SQLSERVER") != "1")
            Assert.Inconclusive("Run the SqlServer profile.");
        using var database = new SqlServerTestDatabase();
        using var scope = Scope(database);
        var fw = scope.Fw;
        fw.route.method = "POST";
        fw.Session("XSS", "test-token");
        fw.FORM["XSS"] = "test-token";
        var bootstrap = new DevConfigureController();
        bootstrap.init(fw);
        Assert.ThrowsExactly<AuthException>(() => bootstrap.InitDBAction());
        fw.Session("user_id", "1");
        fw.Session("access_level", Users.ACL_SITEADMIN.toStr());
        Assert.ThrowsExactly<UserException>(() => bootstrap.InitDBAction());

        var users = fw.model<Users>();
        users.is_log_changes = false;
        users.update(1, DB.h("pwd", "Console regression12!"));
        fw.FORM["sql"] = "SELECT 42 AS answer";
        fw.FORM["db"] = "";
        var console = new AdminDBController();
        console.init(fw);
        fw.FORM["pwd"] = "wrong-password";
        Assert.ThrowsExactly<AuthException>(() => console.IndexAction());
        fw.FORM["pwd"] = "Console regression12!";
        Assert.AreEqual(1, console.IndexAction()["sql_ctr"].toInt());
        users.update(1, DB.h("is_readonly", 1));
        Assert.ThrowsExactly<AuthException>(() => console.IndexAction());
    }
}
