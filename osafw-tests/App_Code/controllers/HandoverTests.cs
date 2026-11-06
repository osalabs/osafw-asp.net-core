using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class HandoverTests
{
    internal static FwTestScope createScope(DB database, DefaultHttpContext context, string version = "2.4", string? support = "help@example.org")
    {
        var config = new Dictionary<string, string?>
        {
            ["appSettings:template"] = TestHelpers.TemplateRoot(),
            ["appSettings:site_root"] = Path.Combine(TestHelpers.RepoRoot(), "osafw-app"),
            ["appSettings:SITE_NAME"] = "Operations Portal",
            ["appSettings:SITE_VERSION"] = version,
            ["appSettings:is_mfa_enforced"] = "true",
        };
        if (support != null)
            config["appSettings:support_email"] = support;
        var scope = new FwTestScope(_ => database, config, context);
        scope.Fw.Session("access_level", Users.ACL_SITEADMIN.ToString());
        return scope;
    }

    [TestMethod]
    [DataRow(DB.DBTYPE_SQLSRV, "SQL Server")]
    [DataRow(DB.DBTYPE_MYSQL, "MySQL")]
    [DataRow(DB.DBTYPE_SQLITE, "SQLite")]
    [DataRow(DB.DBTYPE_OLE, "OLE DB")]
    [DataRow(DB.DBTYPE_ODBC, "ODBC")]
    public void DocsActionShowsConfiguredFactsAndOnlyDisplayGrants(string provider, string label)
    {
        var db = new HandoverDb { dbtype = provider };
        using var scope = createScope(db, TestHelpers.CreateHttpContext(), version: "");
        var controller = new DevManageController();
        controller.init(scope.Fw);
        var ps = controller.DocsAction();
        Assert.IsNotNull(ps);
        var html = scope.Fw.parsePage("/dev/manage/docs", "/dev/manage/docs/main.html", ps);

        StringAssert.Contains(html, "<dd>" + label + "</dd>");
        StringAssert.Contains(html, "Operations Portal");
        StringAssert.Contains(html, "User and Administrator Guide");
        StringAssert.Contains(html, "<strong>For the commissioning manager.</strong>");
        StringAssert.Contains(html, "<pre><code class=\"language-powershell\">");
        StringAssert.Contains(html, "powershell -NoProfile -File scripts\\deploy_production.ps1 -Check");
        StringAssert.Contains(html, "help@example.org");
        StringAssert.Contains(html, "Enforced by application policy");
        StringAssert.Contains(html, "Site Administrator");
        Assert.IsFalse(html.Contains("<p>Release "));
        Assert.IsFalse(html.Contains("%%"));
        Assert.AreEqual(provider is DB.DBTYPE_SQLSRV or DB.DBTYPE_SQLITE, html.Contains("-- database-init"));
        if (provider is DB.DBTYPE_SQLSRV or DB.DBTYPE_SQLITE)
        {
            StringAssert.Contains(html, "<pre><code class=\"language-powershell\">$env:DOTNET_ENVIRONMENT");
            StringAssert.Contains(html, "\ndotnet run --no-launch-profile --project osafw-app -- database-init");
        }
        Assert.IsFalse(html.Contains("internal-account-marker"));
        Assert.IsFalse(html.Contains("credential-test-marker"));

        if (scope.Fw.model<Users>().isRoles())
        {
            Assert.AreEqual(1, db.GrantReads, "Read the grants in one bulk query.");
            StringAssert.Contains(html, "Readers &lt;team&gt;");
            StringAssert.Contains(html, "Tickets &amp; reviews");
            StringAssert.Contains(html, "View &lt;own&gt;");
            StringAssert.Contains(html, "No active grants.");
            Assert.IsFalse(html.Contains("Inactive grant label"));
        }
        else
        {
            Assert.AreEqual(0, db.GrantReads);
            Assert.IsFalse(html.Contains("Configured role grants"));
        }
    }

    [TestMethod]
    [DataRow(DB.DBTYPE_SQLSRV, "Server=intranet-sql\\REPORTS;Application Name=decoy.database.windows.net", true, false, false)]
    [DataRow(DB.DBTYPE_SQLSRV, "Address=TCP:synthetic-db.token.us-east-1.RDS.AMAZONAWS.COM,1433", false, true, false)]
    [DataRow(DB.DBTYPE_SQLSRV, "Server=synthetic-db.token.cn-north-1.rds.amazonaws.com.cn", false, true, false)]
    [DataRow(DB.DBTYPE_SQLSRV, "Data Source=tcp:synthetic-db.database.windows.net,1433", false, false, true)]
    [DataRow(DB.DBTYPE_SQLSRV, "Server=synthetic-db.zone.database.windows.net", false, false, true)]
    [DataRow(DB.DBTYPE_SQLSRV, "Server=synthetic-db.database.usgovcloudapi.net.", false, false, true)]
    [DataRow(DB.DBTYPE_SQLSRV, "Server=synthetic-db.database.windows.net.internal", true, false, false)]
    [DataRow(DB.DBTYPE_SQLITE, "Data Source=synthetic-db.database.windows.net", false, false, false)]
    [DataRow(DB.DBTYPE_MYSQL, "Server=synthetic-db.token.us-east-1.rds.amazonaws.com;Uid=test", false, false, false)]
    [DataRow("DB2", "Server=synthetic-db.database.windows.net:50000;Protocol=TCPIP", false, false, false)]
    public void DocsActionPrintsRecoveryForTheConfiguredEndpointAndOnlyPublicValues(
        string provider, string connectionString, bool isNative, bool isRds, bool isAzure)
    {
        var db = new HandoverDb(connectionString + ";Password=secret-test-marker.database.windows.net") { dbtype = provider };
        using var scope = createScope(db, TestHelpers.CreateHttpContext(), support: null);
        var controller = new DevManageController();
        controller.init(scope.Fw);
        var html = scope.Fw.parsePage("/dev/manage/docs", "/dev/manage/docs/main.html", controller.DocsAction()!);

        Assert.AreEqual(isNative, html.Contains("SQL Server Management Studio"));
        Assert.AreEqual(isRds, html.Contains("RDS snapshot"));
        Assert.AreEqual(isRds, html.Contains("RDS backup retention"));
        Assert.AreEqual(isAzure, html.Contains("Azure SQL point-in-time restore"));
        Assert.AreEqual(isAzure, html.Contains("Azure SQL backup retention"));
        Assert.IsFalse(html.Contains("synthetic-db"));
        Assert.IsFalse(html.Contains("secret-test-marker"));
        StringAssert.Contains(html, "admin@website.com");
        StringAssert.Contains(html, "support@website.com");
    }

    internal sealed class HandoverDb(string connectionString = "") : RejectingDb
    {
        public int GrantReads;

        public override DbConnection connect()
        {
            if (dbtype != DBTYPE_SQLSRV)
                throw Unexpected("connect for non-SQL Server provider");
            return new SqlConnection(connectionString);
        }

        public override DBList array(string table, FwDict where, string order_by = "", ICollection? aselect_fields = null, int offset = 0, int limit = -1)
        {
            switch (table)
            {
                case "settings":
                    return [new DBRow { ["icode"] = "smtp_pwd", ["ivalue"] = "credential-test-marker" }];
                case "roles":
                    return [new DBRow { ["id"] = "1", ["iname"] = "Readers <team>", ["add_users_id"] = "internal-account-marker" },
                        new DBRow { ["id"] = "2", ["iname"] = "Observers" }];
                case "resources":
                    return [new DBRow { ["id"] = "10", ["iname"] = "Tickets & reviews" }];
                case "permissions":
                    return [new DBRow { ["id"] = "20", ["iname"] = "View <own>", ["icode"] = "view" },
                        new DBRow { ["id"] = "21", ["iname"] = "Inactive grant label", ["icode"] = "edit" }];
                case "roles_resources_permissions":
                    GrantReads++;
                    return [new DBRow { ["roles_id"] = "1", ["resources_id"] = "10", ["permissions_id"] = "20", ["status"] = "0" },
                        new DBRow { ["roles_id"] = "1", ["resources_id"] = "10", ["permissions_id"] = "21", ["status"] = "1" }];
                default:
                    throw Unexpected(table);
            }
        }
    }
}
