// FwUpdates model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

using System;

namespace osafw;


public class FwUpdates : FwModel
{
    public const int STATUS_FAILED = 20;
    public const int STATUS_APPLIED = 30;

    public FwUpdates() : base()
    {
        db_config = "";
        table_name = "fwupdates";
    }

    //comparer for sorting files by name without extension
    internal class FileNameWithoutExtComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            string xname = System.IO.Path.GetFileNameWithoutExtension(x ?? string.Empty);
            string yname = System.IO.Path.GetFileNameWithoutExtension(y ?? string.Empty);
            return xname.CompareTo(yname);
        }
    }

    /// <summary>
    /// Resolves the provider-specific SQL script root used by database initialization, update loading, and view refreshes.
    /// </summary>
    /// <returns>
    /// Absolute path to the active provider SQL folder, such as <c>App_Data/sql</c>, <c>App_Data/sql/mysql</c>,
    /// or <c>App_Data/sql/sqlite</c>.
    /// </returns>
    public virtual string sqlScriptRoot()
    {
        var result = sqlBaseRoot();
        var provider_subdir = db.dbtype switch
        {
            DB.DBTYPE_SQLITE => "sqlite",
            DB.DBTYPE_MYSQL => "mysql",
            _ => "",
        };

        return string.IsNullOrEmpty(provider_subdir) ? result : System.IO.Path.Combine(result, provider_subdir);
    }

    private string sqlBaseRoot()
    {
        return System.IO.Path.Combine(fw.config("site_root").toStr(), "App_Data", "sql");
    }

    /// <summary>
    /// Resolves the provider-specific update folder where generated update scripts should be written.
    /// </summary>
    /// <returns>Absolute path to the active provider update folder.</returns>
    public virtual string sqlUpdatesRoot()
    {
        return System.IO.Path.Combine(sqlScriptRoot(), "updates");
    }

    /// <summary>
    /// Returns update folders to scan, preserving legacy MySQL root updates while allowing provider overrides.
    /// </summary>
    /// <returns>Ordered update folders; later folders override files with the same name from earlier folders.</returns>
    public virtual string[] sqlUpdateRoots()
    {
        var root = System.IO.Path.Combine(sqlBaseRoot(), "updates");
        return db.dbtype switch
        {
            DB.DBTYPE_SQLITE => [sqlUpdatesRoot()],
            DB.DBTYPE_MYSQL => [root, sqlUpdatesRoot()],
            _ => [root],
        };
    }

    /// <summary>
    /// Load new updates from the updates directory and add them to the database.
    /// </summary>
    public virtual void loadUpdates()
    {
        var update_roots = sqlUpdateRoots();
        foreach (var updates_root in update_roots)
            logger("checking " + updates_root);

        Dictionary<string, string> filesByName = new(StringComparer.OrdinalIgnoreCase);
        foreach (var updates_root in update_roots)
        {
            if (!System.IO.Directory.Exists(updates_root))
                continue;

            foreach (string file in System.IO.Directory.GetFiles(updates_root))
            {
                var filename = System.IO.Path.GetFileName(file);
                if (!string.IsNullOrEmpty(filename))
                    filesByName[filename] = file;
            }
        }

        if (filesByName.Count == 0)
            return;

        string[] files = new List<string>(filesByName.Values).ToArray();

        //sort files by name, so it will appear for example as:
        // update2025-02-20.sql
        // update2025-02-30.sql
        // update2025-03-03.sql
        // update2025-03-03-001.sql   (any additional updates for the day going with suffix -NNN)
        Array.Sort(files, new FileNameWithoutExtComparer());
        //logger("SORTED FILES:", files);

        var dbfiles = db.array(table_name, [], "", new[] { "iname" });
        var hdbfiles = Utils.array2hashtable(dbfiles, "iname");
        foreach (string file in files)
        {
            if (file == "." || file == ".." || !file.EndsWith(".sql"))
                continue;

            var filename = System.IO.Path.GetFileName(file);
            logger("checking " + filename);
            if (hdbfiles.ContainsKey(filename))
                continue; // already exists in db

            string content = System.IO.File.ReadAllText(file);
            add(new FwDict() {
                { "iname", filename },
                { "idesc", content }
            });
        }
    }

    public virtual DBList listPending()
    {
        return db.array(table_name, new FwDict() { { "status", STATUS_ACTIVE } }, "id");
    }

    public virtual void applyPending(bool is_echo = false)
    {
        DBList rows = listPending();
        foreach (FwDict row in rows)
        {
            applyOne(row["id"].toInt(), is_echo);
        }
    }

    public void applyOne(int id, bool is_echo = false)
    {
        DBRow row = one(id);
        if (is_echo)
            fw.rw("<b>" + row["iname"] + " applying</b>");

        FwDict uitem = new() {
            { "status", STATUS_APPLIED },
            { "applied_time", DB.NOW }
        };

        var conn = db.getConnection();
        try
        {
            db.begin();
            db.execMultipleSQL(row["idesc"]);
            db.commit();

            update(id, uitem);
        }
        catch (Exception e)
        {
            try
            {
                db.rollback();
            }
            catch (Exception e2)
            {
                // rollback can fail if SET XACT_ABORT ON or rollback already happened
                logger("ERROR", "ROLLBACK failed: " + e2.Message);
            }

            uitem["status"] = STATUS_FAILED;
            uitem["last_error"] = e.Message;
            update(id, uitem);

            if (is_echo)
                fw.rw("<b style='color:red'>" + row["iname"] + " failed</b>");
            throw; // re-throw
        }

        fw.Session("FW_UPDATES_CTR", fw.model<FwUpdates>().getCountPending().toStr());

        //clear caches as db structure might be changed
        FwCache.clear();
        db.clearSchemaCache();
    }

    /// <summary>
    /// Counts framework update records that are still pending.
    /// </summary>
    /// <returns>Number of active update rows waiting to be applied.</returns>
    public virtual long getCountPending()
    {
        return getCount(new int[] { STATUS_ACTIVE });
    }

    public void markAllPendingApplied()
    {
        db.update(table_name,
            DB.h(
                "status", STATUS_APPLIED,
                "applied_time", DB.NOW
            ),
            DB.h("status", STATUS_ACTIVE)
        );
        fw.Session("FW_UPDATES_CTR", "0");
    }

    public virtual void applyList(IntList ids, bool is_echo = false)
    {
        foreach (var id in ids)
        {
            applyOne(id, is_echo);
        }
    }

    public void refreshViews(bool is_echo = false)
    {
        var views_file = System.IO.Path.Combine(sqlScriptRoot(), "views.sql");
        if (is_echo)
            fw.rw("Applying views file: " + views_file);

        // ignore errors for views
        db.execMultipleSQL(Utils.getFileContent(views_file), true);

        FwCache.clear();
        db.clearSchemaCache();
    }

    /// <summary>
    /// Determines whether a developer Home page request should trigger the automatic FwUpdates redirect.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the app is running with <c>IS_DEV</c> and <c>is_fwupdates_auto_apply</c> enabled; otherwise <c>false</c>.
    /// </returns>
    public bool isAutoApplyEnabledForDev()
    {
        return fw.config("IS_DEV").toBool() && fw.config("is_fwupdates_auto_apply").toBool();
    }

    /// <summary>
    /// Checks for pending framework SQL updates during developer Home page visits and redirects to the pending notice.
    /// </summary>
    /// <remarks>
    /// The <c>is_fwupdates_auto_apply</c> setting lets local developers keep pending updates visible in `/Admin/FwUpdates`
    /// without automatically entering the apply flow when they visit Home.
    /// </remarks>
    public void checkApplyIfDev()
    {
        if (!fw.config("IS_DEV").toBool())
            return; // only check update files in dev
        try
        {
            loadUpdates();

            if (!isAutoApplyEnabledForDev())
                return; // keep pending updates visible without entering the apply flow

            if (getCountPending() > 0)
                fw.redirect("/Dev/Configure/(PendingUpdates)");
        }
        catch (Exception e)
        {
            //except RedirectException
            if (e is RedirectException)
                throw; // re-throw

            logger("ERROR", e.Message);
        }
    }
}
