// FwUpdates model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class FwUpdates : FwModel
{
    public const int STATUS_FAILED_DB_UPDATE = 20;
    public const int STATUS_APPLIED = 30;
    public FwUpdates() : base()
    {
        db_config = "";
        table_name = "fwupdates";
    }

    public void parseUpdates()
    {
        var updates_root = fw.config("site_root") + @"\App_Data\sql\updates";
        if (System.IO.Directory.Exists(updates_root))
        {
            string[] files = System.IO.Directory.GetFiles(updates_root);

            ArrayList rows = new();
            foreach (string file in files)
            {
                var filename = System.IO.Path.GetFileName(file);
                var row = oneByIcode(filename);
                if (row.Count == 0)
                {
                    var filepath = updates_root + @"\" + filename;
                    var content = FW.getFileContent(filepath);
                    add(new Hashtable() {
                        { "icode", filename },
                        { "iname", filename },
                        { "idesc", content }
                    });
                }
            }
        }
    }

    public void markAsApplied(int id)
    {
        var fields = new Hashtable();
        fields["status"] = STATUS_APPLIED;
        fields["applied_time"] = DB.NOW;

        this.update(id, fields);
    }

    public void markAsFailed(int id, string error)
    {
        var fields = new Hashtable();
        fields["status"] = STATUS_FAILED_DB_UPDATE;
        fields["idesc"] = error;

        this.update(id, fields);
    }

    public int getNotAppliedCount()
    {
        var value = db.value(table_name, DB.h("status", STATUS_ACTIVE), "count(*)");
        return Utils.f2int(value);
    }
}