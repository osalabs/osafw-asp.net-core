#if isSQLite && isRoles
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class AdminSettingsRbacTests
{
    [TestMethod]
    public void OrdinaryAdmin_WithViewAndEditGrantsCanReadAndEditLevel90Setting()
    {
        using var fixture = new RbacFixture(Users.ACL_ADMIN, isGranted: true);

        fixture.SetRoute(FW.ACTION_SHOW);
        fixture.Controller.checkAccess();
        var pageState = fixture.Controller.ShowAction(fixture.OrdinarySettingId)!;
        Assert.AreEqual("before", ((FwDict)pageState["i"]!)["ivalue"]);

        fixture.SetRoute(FW.ACTION_SAVE, "POST", fixture.OrdinarySettingId);
        fixture.Fw.FORM = new FwDict
        {
            ["XSS"] = "token",
            ["item"] = new FwDict { ["basis"] = Settings.BASIS_VALUE, ["ivalue"] = "after" },
        };
        fixture.Controller.checkAccess();
        fixture.Controller.SaveAction(fixture.OrdinarySettingId);

        Assert.AreEqual("after", fixture.Db.value("settings", DB.h("id", fixture.OrdinarySettingId), "ivalue").toStr());
    }

    [TestMethod]
    public void OrdinaryAdmin_WithoutRoleGrantIsDeniedByControllerCheckAccess()
    {
        using var fixture = new RbacFixture(Users.ACL_ADMIN, isGranted: false);
        fixture.SetRoute(FW.ACTION_SHOW);

        Assert.ThrowsExactly<AuthException>(() => fixture.Controller.checkAccess());
    }

    [TestMethod]
    public void OrdinaryAdmin_WithViewGrantStillCannotReadLevel100Setting()
    {
        using var fixture = new RbacFixture(Users.ACL_ADMIN, isGranted: true);
        fixture.SetRoute(FW.ACTION_SHOW);

        fixture.Controller.checkAccess();
        Assert.ThrowsExactly<AuthException>(() => fixture.Controller.ShowAction(fixture.RestrictedSettingId));
    }

    [TestMethod]
    public void SiteAdmin_CanReadLevel100SettingWithoutRoleGrant()
    {
        using var fixture = new RbacFixture(Users.ACL_SITEADMIN, isGranted: false);
        fixture.SetRoute(FW.ACTION_SHOW);

        fixture.Controller.checkAccess();
        var pageState = fixture.Controller.ShowAction(fixture.RestrictedSettingId)!;

        Assert.AreEqual("restricted", ((FwDict)pageState["i"]!)["ivalue"]);
    }

    private sealed class RbacFixture : IDisposable
    {
        private readonly string path;
        private readonly ServiceProvider services;
        private readonly FwTestScope scope;

        public DB Db { get; }
        public FW Fw => scope.Fw;
        public AdminSettingsController Controller { get; }
        public int OrdinarySettingId { get; }
        public int RestrictedSettingId { get; }

        public RbacFixture(int accessLevel, bool isGranted)
        {
            path = Path.Combine(Path.GetTempPath(), "osafw-settings-rbac-" + Guid.NewGuid().ToString("N") + ".sqlite");
            string connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Pooling = false,
                ForeignKeys = true,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();
            Db = new DB(DB.h(
                "type", DB.DBTYPE_SQLITE,
                "connection_string", connectionString,
                "timezone", "UTC"), "main");
            Db.exec("""
                CREATE TABLE users (
                  id INTEGER PRIMARY KEY,
                  access_level INTEGER NOT NULL,
                  is_readonly INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE settings (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  icat TEXT NOT NULL DEFAULT '',
                  icode TEXT NOT NULL DEFAULT '',
                  ivalue TEXT NOT NULL DEFAULT '',
                  iname TEXT NOT NULL DEFAULT '',
                  idesc TEXT,
                  input INTEGER NOT NULL DEFAULT 0,
                  allowed_values TEXT,
                  is_user_edit INTEGER DEFAULT 0,
                  access_level INTEGER NOT NULL DEFAULT 100,
                  mask INTEGER NOT NULL DEFAULT 0,
                  basis INTEGER NOT NULL DEFAULT 0,
                  add_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                  add_users_id INTEGER DEFAULT 0,
                  upd_time DATETIME,
                  upd_users_id INTEGER DEFAULT 0
                );
                CREATE UNIQUE INDEX UX_settings_icode ON settings (icode);
                CREATE TABLE resources (
                  id INTEGER PRIMARY KEY,
                  icode TEXT NOT NULL,
                  iname TEXT NOT NULL DEFAULT '',
                  status INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE permissions (
                  id INTEGER PRIMARY KEY,
                  icode TEXT NOT NULL,
                  iname TEXT NOT NULL DEFAULT '',
                  prio INTEGER NOT NULL DEFAULT 0,
                  status INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE roles (
                  id INTEGER PRIMARY KEY,
                  icode TEXT NOT NULL,
                  iname TEXT NOT NULL DEFAULT '',
                  status INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE users_roles (
                  users_id INTEGER NOT NULL,
                  roles_id INTEGER NOT NULL,
                  status INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE roles_resources_permissions (
                  roles_id INTEGER NOT NULL,
                  resources_id INTEGER NOT NULL,
                  permissions_id INTEGER NOT NULL,
                  status INTEGER NOT NULL DEFAULT 0
                );
                """);
            seedRbac(accessLevel, isGranted);
            OrdinarySettingId = seedSetting("ordinary", "before", Users.ACL_ADMIN);
            RestrictedSettingId = seedSetting("restricted", "restricted", Users.ACL_SITEADMIN);

            services = new ServiceCollection().BuildServiceProvider();
            var context = TestHelpers.CreateHttpContext("settings-rbac-tests");
            context.RequestServices = services;
            context.Request.Headers.Accept = "application/json";
            scope = new FwTestScope(
                name => name == "main" ? Db : throw new InvalidOperationException("Unknown DB name."),
                new Dictionary<string, string?> { ["appSettings:ROOT_DOMAIN"] = "https://example.test" },
                context);
            Fw.Session("user_id", "10");
            Fw.Session("access_level", accessLevel.ToString());
            Fw.Session("XSS", "token");
            Fw.FORM = new FwDict { ["XSS"] = "token" };
            SetRoute(FW.ACTION_SHOW);

            Fw.model<Settings>().is_log_changes = false;

            Controller = new AdminSettingsController();
            Controller.init(Fw);
        }

        public void SetRoute(string action, string method = "GET", int id = 0)
        {
            Fw.route.controller = "AdminSettings";
            Fw.route.action = action;
            Fw.route.method = method;
            Fw.route.id = id > 0 ? id.ToString() : "";
        }

        private void seedRbac(int accessLevel, bool isGranted)
        {
            Db.insert("users", DB.h("id", 10, "access_level", accessLevel, "is_readonly", 0));
            Db.insert("resources", DB.h("id", 1, "icode", "AdminSettings", "iname", "Settings", "status", 0));
            Db.insert("permissions", DB.h("id", 1, "icode", Permissions.PERMISSION_LIST, "iname", "List", "status", 0));
            Db.insert("permissions", DB.h("id", 2, "icode", Permissions.PERMISSION_VIEW, "iname", "View", "status", 0));
            Db.insert("permissions", DB.h("id", 3, "icode", Permissions.PERMISSION_ADD, "iname", "Add", "status", 0));
            Db.insert("permissions", DB.h("id", 4, "icode", Permissions.PERMISSION_EDIT, "iname", "Edit", "status", 0));
            Db.insert("permissions", DB.h("id", 5, "icode", Permissions.PERMISSION_DELETE, "iname", "Delete", "status", 0));
            Db.insert("roles", DB.h("id", 1, "icode", "settings_admin", "iname", "Settings Admin", "status", 0));
            Db.insert("users_roles", DB.h("users_id", 10, "roles_id", 1, "status", 0));
            if (isGranted)
            {
                Db.insert("roles_resources_permissions", DB.h("roles_id", 1, "resources_id", 1, "permissions_id", 2, "status", 0));
                Db.insert("roles_resources_permissions", DB.h("roles_id", 1, "resources_id", 1, "permissions_id", 4, "status", 0));
            }
        }

        private int seedSetting(string code, string value, int accessLevel)
        {
            return Db.insert("settings", DB.h(
                "icode", code,
                "ivalue", value,
                "iname", code,
                "input", Settings.INPUT_TEXT,
                "is_user_edit", 1,
                "access_level", accessLevel,
                "mask", Settings.MASK_NONE,
                "basis", Settings.BASIS_VALUE));
        }

        public void Dispose()
        {
            scope.Dispose();
            services.Dispose();
            Db.disconnect();
            foreach (string suffix in new[] { "", "-wal", "-shm", "-journal" })
                File.Delete(path + suffix);
        }
    }
}
#endif
