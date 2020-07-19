using osafw_asp.net_core.fw;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace osafw_asp.net_core.fw
{
    public class Users : FwModel
    {
        // ACL constants
        public const int ACL_VISITOR = -1;
        public const int ACL_MEMBER = 0;
        public const int ACL_ADMIN = 100;

        private const String table_menu_items = "menu_items";

        public Users() : base()
        {
            table_name = "users";
            csv_export_fields = "id fname lname email add_time";
            csv_export_headers = "id,First Name,Last Name,Email,Registered";
        }

        // return standard list of id,iname where status=0 order by iname
        public override ArrayList list()
        {
            String sql = "select id, CONCAT(fname,' ',lname) as iname from " + table_name + " where status=0 order by fname, lname";
            return db.array(sql);
        }
    }
}
