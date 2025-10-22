// DemosItems model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class DemosItems : FwModel<DemosItems.Row>
{
    public class Row
    {
        public int id { get; set; }
        public int demos_id { get; set; }
        public int? demo_dicts_id { get; set; }
        public string iname { get; set; }
        public string idesc { get; set; }
        public int is_checkbox { get; set; }
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

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