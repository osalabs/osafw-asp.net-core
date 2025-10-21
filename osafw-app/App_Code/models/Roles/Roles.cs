// Roles model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System;

namespace osafw;

public class Roles : FwModel<Roles.Row>
{
    public class Row
    {
        public int id { get; set; }
        public string iname { get; set; }
        public string idesc { get; set; }
        public int prio { get; set; }
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

    public const string INAME_VISITOR = "visitor";

    public Roles() : base()
    {
        db_config = "";
        table_name = "roles";
        field_prio = "prio";
    }

    public int idVisitor()
    {
        return oneByIname(INAME_VISITOR)[field_id].toInt();
    }

}
