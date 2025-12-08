// Resources model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Generic;

namespace osafw;

public class Resources : FwModel<Resources.Row>
{
    public class Row
    {
        public int id { get; set; }
    public string icode { get; set; } = string.Empty;
    public string iname { get; set; } = string.Empty;
    public string idesc { get; set; } = string.Empty;
        public int prio { get; set; }
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

    public Resources() : base()
    {
        db_config = "";
        table_name = "resources";
        field_prio = "prio";
    }

    //list all non-deleted resource icodes
    public List<string> colIcodes(IList<int> ids = null)
    {
        var where = new Hashtable
        {
            [field_status] = db.opNOT(STATUS_DELETED)
        };
        if (ids != null && ids.Count > 0)
            where["id"] = db.opIN(ids);

        return db.col(table_name, where, field_icode, field_icode);
    }
}