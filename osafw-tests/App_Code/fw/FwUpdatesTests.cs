using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class FwUpdatesTests
{
    [TestMethod]
    [DataRow(DB.DBTYPE_SQLSRV, "", false)]
    [DataRow(DB.DBTYPE_MYSQL, "mysql", true)]
    [DataRow(DB.DBTYPE_SQLITE, "sqlite", false)]
    public void SqlScriptRoot_UsesProviderSpecificSqlFolders(string provider, string subfolder, bool includesSharedUpdates)
    {
        var siteRoot = Path.GetFullPath("synthetic-site");
        using var scope = new FwTestScope(_ => new RejectingDb { dbtype = provider }, new Dictionary<string, string?>
        {
            ["appSettings:site_root"] = siteRoot,
        });
        var updates = scope.Fw.model<FwUpdates>();
        var sqlRoot = Path.Combine(siteRoot, "App_Data", "sql");
        var providerRoot = subfolder.Length == 0 ? sqlRoot : Path.Combine(sqlRoot, subfolder);
        var updateRoot = Path.Combine(providerRoot, "updates");

        Assert.AreEqual(providerRoot, updates.sqlScriptRoot());
        Assert.AreEqual(updateRoot, updates.sqlUpdatesRoot());
        CollectionAssert.AreEqual(
            includesSharedUpdates ? new[] { Path.Combine(sqlRoot, "updates"), updateRoot } : new[] { updateRoot },
            updates.sqlUpdateRoots());
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
        using var scope = CreateScope(autoApply: false);
        Assert.IsFalse(scope.Fw.model<FwUpdates>().isAutoApplyEnabledForDev());
    }

    [TestMethod]
    public void IsAutoApplyEnabledForDev_ReturnsTrueWhenDevAndFlagEnabled()
    {
        using var scope = CreateScope(autoApply: true);
        Assert.IsTrue(scope.Fw.model<FwUpdates>().isAutoApplyEnabledForDev());
    }

    [TestMethod]
    public void CheckApplyIfDev_LoadsUpdatesButSkipsRedirectWhenAutoApplyDisabled()
    {
        using var scope = CreateScope(autoApply: false);
        var updates = new SpyFwUpdates();
        updates.init(scope.Fw);

        updates.checkApplyIfDev();

        Assert.IsTrue(updates.LoadUpdatesCalled);
        Assert.IsFalse(updates.CountPendingCalled);
    }

    [TestMethod]
    public void CheckApplyIfDev_RedirectsToPendingNoticeWhenDevUpdatesPending()
    {
        using var scope = CreateScope(autoApply: true);
        var updates = new SpyFwUpdates();
        updates.init(scope.Fw);

        Assert.ThrowsExactly<RedirectException>(() => updates.checkApplyIfDev());

        Assert.IsTrue(updates.LoadUpdatesCalled);
        Assert.IsTrue(updates.CountPendingCalled);
        var location = scope.Fw.response.Headers["Location"].ToString();
        Assert.AreEqual("/Dev/Configure/(PendingUpdates)", location);
        Assert.IsFalse(location.Contains("ApplyUpdates"));
    }

    private static FwTestScope CreateScope(bool autoApply) => new(_ => new RejectingDb(), new Dictionary<string, string?>
    {
        ["appSettings:IS_DEV"] = "true",
        ["appSettings:is_fwupdates_auto_apply"] = autoApply.ToString(),
    });

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
