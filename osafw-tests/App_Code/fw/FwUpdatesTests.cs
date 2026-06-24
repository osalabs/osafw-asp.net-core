using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class FwUpdatesTests
{
    [TestMethod]
    public void SqlScriptRoot_UsesProviderSpecificSqlFolders()
    {
        var fw = TestHelpers.CreateFw();
        var settings = FwConfig.GetCurrentSettings();
        var oldSiteRoot = settings["site_root"];

        try
        {
            settings["site_root"] = @"C:\site";
            var updates = new FwUpdates();

            fw.db = new DB("", DB.DBTYPE_SQLSRV);
            updates.init(fw);
            Assert.AreEqual(Path.Combine(@"C:\site", "App_Data", "sql"), updates.sqlScriptRoot());
            CollectionAssert.AreEqual(
                new[] { Path.Combine(@"C:\site", "App_Data", "sql", "updates") },
                updates.sqlUpdateRoots());

            fw.db = new DB("", DB.DBTYPE_MYSQL);
            updates.init(fw);
            Assert.AreEqual(Path.Combine(@"C:\site", "App_Data", "sql", "mysql"), updates.sqlScriptRoot());
            Assert.AreEqual(Path.Combine(@"C:\site", "App_Data", "sql", "mysql", "updates"), updates.sqlUpdatesRoot());
            CollectionAssert.AreEqual(
                new[]
                {
                    Path.Combine(@"C:\site", "App_Data", "sql", "updates"),
                    Path.Combine(@"C:\site", "App_Data", "sql", "mysql", "updates")
                },
                updates.sqlUpdateRoots());

            fw.db = new DB("", DB.DBTYPE_SQLITE);
            updates.init(fw);
            Assert.AreEqual(Path.Combine(@"C:\site", "App_Data", "sql", "sqlite"), updates.sqlScriptRoot());
            CollectionAssert.AreEqual(
                new[] { Path.Combine(@"C:\site", "App_Data", "sql", "sqlite", "updates") },
                updates.sqlUpdateRoots());
        }
        finally
        {
            settings["site_root"] = oldSiteRoot;
        }
    }

    [TestMethod]
    public void FileNameWithoutExtComparer_SortsByDatePart()
    {
        var files = new List<string>
        {
            "update2025-03-03-001.sql",
            "update2025-02-20.sql",
            "update2025-03-03.sql",
            "update2025-02-30.sql"
        };

        files.Sort(new FwUpdates.FileNameWithoutExtComparer());

        CollectionAssert.AreEqual(new[]
        {
            "update2025-02-20.sql",
            "update2025-02-30.sql",
            "update2025-03-03.sql",
            "update2025-03-03-001.sql"
        }, files);
    }

    [TestMethod]
    public void IsAutoApplyEnabledForDev_ReturnsFalseWhenDeveloperFlagDisabled()
    {
        var updates = CreateUpdatesModel();
        var settings = FwConfig.GetCurrentSettings();
        var oldIsDev = settings["IS_DEV"];
        var oldAutoApply = settings["is_fwupdates_auto_apply"];

        try
        {
            settings["IS_DEV"] = true;
            settings["is_fwupdates_auto_apply"] = false;

            Assert.IsFalse(updates.isAutoApplyEnabledForDev());
        }
        finally
        {
            settings["IS_DEV"] = oldIsDev;
            settings["is_fwupdates_auto_apply"] = oldAutoApply;
        }
    }

    [TestMethod]
    public void IsAutoApplyEnabledForDev_ReturnsTrueWhenDevAndFlagEnabled()
    {
        var updates = CreateUpdatesModel();
        var settings = FwConfig.GetCurrentSettings();
        var oldIsDev = settings["IS_DEV"];
        var oldAutoApply = settings["is_fwupdates_auto_apply"];

        try
        {
            settings["IS_DEV"] = true;
            settings["is_fwupdates_auto_apply"] = true;

            Assert.IsTrue(updates.isAutoApplyEnabledForDev());
        }
        finally
        {
            settings["IS_DEV"] = oldIsDev;
            settings["is_fwupdates_auto_apply"] = oldAutoApply;
        }
    }

    [TestMethod]
    public void CheckApplyIfDev_LoadsUpdatesButSkipsRedirectWhenAutoApplyDisabled()
    {
        var fw = TestHelpers.CreateFw();
        var updates = new SpyFwUpdates();
        updates.init(fw);
        var settings = FwConfig.GetCurrentSettings();
        var oldIsDev = settings["IS_DEV"];
        var oldAutoApply = settings["is_fwupdates_auto_apply"];

        try
        {
            settings["IS_DEV"] = true;
            settings["is_fwupdates_auto_apply"] = false;

            updates.checkApplyIfDev();

            Assert.IsTrue(updates.LoadUpdatesCalled);
            Assert.IsFalse(updates.CountPendingCalled);
        }
        finally
        {
            settings["IS_DEV"] = oldIsDev;
            settings["is_fwupdates_auto_apply"] = oldAutoApply;
        }
    }

    [TestMethod]
    public void CheckApplyIfDev_RedirectsToPendingNoticeWhenDevUpdatesPending()
    {
        var fw = TestHelpers.CreateFw();
        var updates = new SpyFwUpdates();
        updates.init(fw);
        var settings = FwConfig.GetCurrentSettings();
        var oldIsDev = settings["IS_DEV"];
        var oldAutoApply = settings["is_fwupdates_auto_apply"];

        try
        {
            settings["IS_DEV"] = true;
            settings["is_fwupdates_auto_apply"] = true;

            Assert.ThrowsExactly<RedirectException>(() => updates.checkApplyIfDev());

            Assert.IsTrue(updates.LoadUpdatesCalled);
            Assert.IsTrue(updates.CountPendingCalled);
            var location = fw.response.Headers["Location"].ToString();
            Assert.AreEqual("/Dev/Configure/(PendingUpdates)", location);
            Assert.IsFalse(location.Contains("ApplyUpdates"));
        }
        finally
        {
            settings["IS_DEV"] = oldIsDev;
            settings["is_fwupdates_auto_apply"] = oldAutoApply;
        }
    }

    private static FwUpdates CreateUpdatesModel()
    {
        var fw = TestHelpers.CreateFw();
        var updates = new FwUpdates();
        updates.init(fw);
        return updates;
    }

    private sealed class SpyFwUpdates : FwUpdates
    {
        public bool LoadUpdatesCalled { get; private set; }
        public bool CountPendingCalled { get; private set; }

        public override void loadUpdates()
        {
            LoadUpdatesCalled = true;
        }

        public override long getCountPending()
        {
            CountPendingCalled = true;
            return 1;
        }
    }
}
