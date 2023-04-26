// DemosDemoDicts model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class DemosDemoDicts : FwModel
{
    public DemosDemoDicts() : base()
    {
        db_config = "";
        table_name = "demos_demo_dicts";
    }

    public override void init(FW fw)
    {
        base.init(fw);
        linked_model_main = fw.model<Demos>();
        linked_field_main_id = "demos_id";
        linked_model_link = fw.model<DemoDicts>();
        linked_field_link_id = "demo_dicts_id";
    }

    public override ArrayList listByRelatedId(int demos_id, Hashtable def = null)
    {
        return db.array(table_name, DB.h("demos_id", demos_id));
    }

    public override void prepareSubtable(ArrayList list_rows, int related_id, Hashtable def = null)
    {
        var model_name = def != null ? (string)def["model"] : this.GetType().Name;
        var select_demo_dicts = fw.model<DemoDicts>().listSelectOptions();
        foreach (Hashtable row in list_rows)
        {
            row["model"] = model_name;            
            //if row_id starts with "new-" - set flag is_new
            row["is_new"] = row["id"].ToString().StartsWith("new-");

            row["select_demo_dicts"] = select_demo_dicts;
        }
    }

    public override void prepareSubtableAddNew(ArrayList list_rows, int related_id, Hashtable def = null)
    {
        var id = "new-" + DateTimeOffset.Now.ToUnixTimeMilliseconds(); // new item not in db yet - mark it with sequental id starting with "new-"
        var item = new Hashtable()
        {
            {"id", id},
        };
        list_rows.Add(item);
    }
}