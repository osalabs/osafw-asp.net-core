// DemosDemoDicts model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class DemosDemoDicts : FwModel
{
    public DemosDemoDicts() : base()
    {
        db_config = "";
        table_name = "demos_demo_dicts";        
    }

    public override ArrayList listByRelatedId(int demos_id, Hashtable def = null)
    {
        var rows = (ArrayList)db.array(table_name, DB.h("demos_id", demos_id));
        var select_demo_dicts = fw.model<DemoDicts>().listSelectOptions();
        foreach (Hashtable row in rows)
        {
            row["select_demo_dicts"] = select_demo_dicts;
        }
        return rows;
    }
}