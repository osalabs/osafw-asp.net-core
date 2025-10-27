// DemosDemoDicts model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System;

namespace osafw;

public class DemosDemoDicts : FwModel<DemosDemoDicts.Row>
{
    public class Row
    {
        public int demos_id { get; set; }
        public int? demo_dicts_id { get; set; }
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

    public DemosDemoDicts() : base()
    {
        db_config = "";
        table_name = "demos_demo_dicts";
    }

    public override void init(FW fw)
    {
        base.init(fw);
        junction_model_main = fw.model<Demos>();
        junction_field_main_id = "demos_id";
        junction_model_linked = fw.model<DemoDicts>();
        junction_field_linked_id = "demo_dicts_id";
    }

}