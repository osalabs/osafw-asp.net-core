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
        junction_model_main = fw.model<Demos>();
        junction_field_main_id = "demos_id";
        junction_model_linked = fw.model<DemoDicts>();
        junction_field_linked_id = "demo_dicts_id";
    }

}