// FwUpdates model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Generic;

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
    private class FileNameWithoutExtComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            string xname = System.IO.Path.GetFileNameWithoutExtension(x);
            string yname = System.IO.Path.GetFileNameWithoutExtension(y);
            return xname.CompareTo(yname);
        }
    }

    /// <summary>
    /// Load new updates from the updates directory and add them to the database.
    /// </summary>
    public void loadUpdates()
    {
        string updates_root = fw.config("site_root") + @"\App_Data\sql\updates";
        logger("checking " + updates_root);
        if (!System.IO.Directory.Exists(updates_root))
            return;

        string[] files = System.IO.Directory.GetFiles(updates_root);

        //sort files by name, so it will appear for example as:
        // update2025-02-20.sql
        // update2025-02-30.sql
        // update2025-03-03.sql
        // update2025-03-03-001.sql   (any additional updates for the day going with suffix -NNN)
        Array.Sort(files, new FileNameWithoutExtComparer());
        //logger("SORTED FILES:", files);

        foreach (string file in files)
        {
            logger("checking " + file);
            if (file == "." || file == ".." || !file.EndsWith(".sql"))
                continue;
            if (isExists(file, 0))
                continue;

            string content = System.IO.File.ReadAllText(file);
            add(new Hashtable() {
                { "iname", file },
                { "idesc", content }
            });
        }
    }

    public DBList listPending()
    {
        return db.array(table_name, new Hashtable() { { "status", STATUS_ACTIVE } }, "id");
    }

    public void applyPending(bool is_echo = false)
    {
        DBList rows = listPending();
        foreach (Hashtable row in rows)
        {
            applyOne(row["id"].toInt(), is_echo);
        }
        fw.Session("FW_UPDATES_CTR", "0");
    }

    public void applyOne(int id, bool is_echo = false)
    {
        DBRow row = one(id);
        if (is_echo)
            fw.rw("<b>" + row["iname"] + " applying</b>");

        Hashtable uitem = new() {
            { "status", STATUS_APPLIED },
            { "applied_time", DB.NOW }
        };
        try
        {
            db.exec("BEGIN TRANSACTION");
            db.execMultipleSQL(row["idesc"]);
            db.exec("COMMIT");

            update(id, uitem);
        }
        catch (Exception e)
        {
            try
            {
                db.exec("ROLLBACK");
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
    }

    public long getCountPending()
    {
        return getCount(new int[] { STATUS_ACTIVE });
    }

    public void checkApplyIfDev()
    {
        if (!fw.config("IS_DEV").toBool())
            return; // only auto-apply in dev
        try
        {
            loadUpdates();

            if (getCountPending() > 0)
                fw.redirect("/Dev/Configure/(ApplyUpdates)");
        }
        catch (Exception e)
        {
            logger("ERROR", e.Message);
        }
    }
}