// UserFilters model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw
{
    public class UserFilters : FwModel
    {
        public UserFilters() : base()
        {
            table_name = "user_filters";
        }

        // list for select by icode and only for logged user OR active system filters
        public ArrayList listSelectByIcode(string icode)
        {
            return db.arrayp("select id, iname from " + db.q_ident(table_name) +
                @" where status=0 and icode=@icode
                     and (is_system=1 OR add_users_id=@users_id)
                   order by is_system desc, iname", DB.h("@icode", icode, "@users_id", fw.userId));
        }
    }
}