// UserFilters model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class UserFilters : FwModel<UserFilters.Row>
{
    public class Row
    {
        public int id { get; set; }
        public string icode { get; set; } = string.Empty;
        public string iname { get; set; } = string.Empty;
        public string idesc { get; set; } = string.Empty;
        public int is_system { get; set; }
        public int is_shared { get; set; }
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

    public UserFilters() : base()
    {
        table_name = "user_filters";
        is_log_changes = false; // no need to log changes here
    }

    // list for select by icode and only for logged user OR active system filters
    public FwList listSelectByIcode(string icode)
    {
        return db.arrayp("select id, iname from " + db.qid(table_name) +
            @" where status=0 and icode=@icode
                     and (is_system=1 OR add_users_id=@users_id)
                   order by is_system desc, iname", DB.h("@icode", icode, "@users_id", fw.userId));
    }
}