// Resources model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System.Collections;
using System.Collections.Generic;

namespace osafw;

public class Resources : FwModel
{
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