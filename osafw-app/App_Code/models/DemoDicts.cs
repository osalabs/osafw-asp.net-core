// DemoDicts model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System;

namespace osafw;

public class DemoDicts : FwModel<DemoDicts.Row>
{
    public class Row
    {
        public int id { get; set; }
        public string iname { get; set; } = "";
        public string idesc { get; set; } = "";
        public int prio { get; set; }
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

    public DemoDicts() : base()
    {
        db_config = "";
        table_name = "demo_dicts";
        //###CODEGEN
    }
}