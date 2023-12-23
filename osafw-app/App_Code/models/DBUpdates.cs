// DBUpdates model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace osafw;

public class DBUpdates : FwModel
{
    public const int STATUS_APPLIED = 50;
    public DBUpdates() : base()
    {
        db_config = "";
        table_name = "db_updates";
        
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
                var row = model.oneByIcode(filename);
                if (row.Count == 0)
                {
                    var filepath = updates_root + @"\" + filename;
                    var content = FW.getFileContent(filepath);
                    model.add(new Hashtable() {
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
        var where = new Hashtable();
        where["id"] = id;

        var fields = new Hashtable();
        fields["is_applied"] = 1;
        fields["status"] = STATUS_APPLIED;
        fields["applied_time"] = DB.NOW;
        
        db.update(table_name, fields, where);
    }

    public int getNotAppliedCount()
    {
        return Utils.f2int(db.valuep("SELECT COUNT(*) FROM " + db.qid(table_name) + " WHERE status=@status", DB.h("@status", STATUS_ACTIVE)));
    }
}