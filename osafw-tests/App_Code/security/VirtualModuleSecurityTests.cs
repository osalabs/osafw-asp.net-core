#if isSQLite
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace osafw.Tests;

[TestClass]
public class VirtualModuleSecurityTests
{
    private sealed class ModuleUsers : Users
    {
        public override bool isReadOnly(int id = -1) => false;
        public override bool isAccessLevel(int min_acl) => true;
        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    [TestMethod]
    public void VirtualControllerLookupRequiresActiveStatusAndRefreshesAfterUpdates()
    {
        using var database = new SqliteFile();
        using var scope = new FwTestScope(database.CreateDb, context: TestHelpers.CreateHttpContext());
        var fw = scope.Fw;
        fw.db.exec("CREATE TABLE fwcontrollers (id INTEGER PRIMARY KEY, icode TEXT, status INT, upd_time TEXT, upd_users_id INT, url TEXT, model TEXT, config TEXT, iname TEXT, igroup TEXT, access_level INT, access_level_edit INT)");
        fw.Session("user_id", "1");
        fw.Session("access_level", Users.ACL_SITEADMIN.toStr());
        var users = new ModuleUsers();
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        var code = "Virtual" + Guid.NewGuid().ToString("N");
        var config = Utils.jsonEncode(DB.h("model", "Demos", "save_fields", "iname", "controller", DB.h("url", "/VirtualModule", "type", "vue")));
        fw.db.insert("fwcontrollers", DB.h("id", 1, "icode", code, "status", FwModel.STATUS_ACTIVE,
            "url", "/VirtualModule", "model", "Demos", "config", config, "access_level", Users.ACL_MEMBER, "access_level_edit", Users.ACL_SITEADMIN));
        var model = fw.model<FwControllers>();
        model.is_log_changes = false;
        try
        {
            Assert.AreEqual(1, model.oneByIcode(code.ToLowerInvariant())["id"].toInt());
            model.update(1, DB.h("status", FwModel.STATUS_DELETED));
            Assert.AreEqual(1, model.oneByIcode(code)["id"].toInt()); // Management can still find disabled definitions.
            Assert.IsNull(fw.controller(code));
            Assert.IsEmpty(model.listGrouped());
            model.update(1, DB.h("status", FwModel.STATUS_INACTIVE));
            Assert.AreEqual(1, model.oneByIcode(code)["id"].toInt()); // Management can still find disabled definitions.
            Assert.IsNull(fw.controller(code));
            Assert.IsEmpty(model.listGrouped());
            model.update(1, DB.h("status", FwModel.STATUS_ACTIVE));
            Assert.AreEqual(1, model.oneByIcode(code)["id"].toInt());
            Assert.HasCount(1, model.listGrouped());
            Assert.IsNotNull(fw.controller(code));
        }
        finally
        {
            model.removeCacheAll();
        }
    }
}
#endif
