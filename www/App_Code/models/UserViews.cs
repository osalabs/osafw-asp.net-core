// User Custom List Views model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw
{
    public class UserViews : FwModel
    {
        public UserViews() : base()
        {
            table_name = "user_views";
        }

        // return screen record for logged user
        public override Hashtable oneByIcode(string screen)
        {
            return db.row(table_name, new Hashtable() { { field_add_users_id, fw.model<Users>().meId() }, { field_icode, screen } });
        }

        // update screen fields for logged user
        // return user_views.id
        public int updateByIcode(string screen, string fields)
        {
            var result = 0;
            var item = oneByIcode(screen);
            if (item.Count > 0)
            {
                // exists
                result = Utils.f2int(item[field_id]);
                update(Utils.f2int(item[field_id]), new Hashtable() { { "fields", fields } });
            }
            else
                // new
                result = add(new Hashtable()
            {
                {
                    field_icode, screen
                },
                {
                    "fields", fields
                },
                {
                    field_add_users_id, fw.model<Users>().meId()
                }
            });
            return result;
        }

        // list for select by entity and only for logged user OR active system views
        public ArrayList listSelectByIcode(string entity)
        {
            return db.array("select id, iname from " + table_name + " where status=0 and icode=" + db.q(entity) + " and (is_system=1 OR add_users_id=" + fw.model<Users>().meId() + ") order by is_system desc, iname");
        }
    }

}