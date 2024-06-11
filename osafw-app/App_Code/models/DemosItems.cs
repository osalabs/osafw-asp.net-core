// DemosItems model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class DemosItems : FwModel
{
    public DemosItems() : base()
    {
        db_config = "";
        table_name = "demos_items";
    }

    public override void init(FW fw)
    {
        base.init(fw);
        junction_model_main = fw.model<Demos>();
        junction_field_main_id = "demos_id";
    }

    public override void prepareSubtable(ArrayList list_rows, int related_id, Hashtable def = null)
    {
        base.prepareSubtable(list_rows, related_id, def);

        // add select options
        var select_demo_dicts = fw.model<DemoDicts>().listSelectOptions();
        foreach (Hashtable row in list_rows)
        {
            row["select_demo_dicts"] = select_demo_dicts;
        }
    }
}