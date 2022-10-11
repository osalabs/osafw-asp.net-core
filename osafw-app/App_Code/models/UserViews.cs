// User Custom List Views model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class UserViews : FwModel
{
    public UserViews() : base()
    {
        table_name = "user_views";
    }

    // return screen record for logged user
    public override DBRow oneByIcode(string screen)
    {
        return db.row(table_name, DB.h(field_add_users_id, fw.userId, field_icode, screen));
    }

    // additionally by id
    public DBRow oneByIcodeId(string icode, int id)
    {
        return db.row(table_name, DB.h(field_add_users_id, fw.userId, field_icode, icode, field_id, id));
    }

    // by icode/iname/loggeduser
    public DBRow oneByUK(string icode, string iname) {
        var where = new Hashtable();
        where["icode"] = icode;
        where["iname"] = iname;
        where["add_users_id"] = fw.userId;
        return db.row(table_name, where);
    }

    public int addSimple(string icode, string fields , string iname) {
        return add(new Hashtable {
            { field_icode, icode},
            { "fields", fields},
            { "iname", iname},
            { field_add_users_id, fw.userId}
        });
    }

    public int addOrUpdateByUK(string icode, string fields, string iname) {
        int id;
        var item = oneByUK(icode, iname);
        if (item.Count > 0) {
            id = Utils.f2int(item["id"]);
            update(id, DB.h("fields", fields));
        }
        else {
            id = addSimple(icode, fields, iname);
        }
        return id;
    }


    // update default screen fields for logged user
    // return user_views.id
    public int updateByIcode(string icode, string fields)
    {
        var item = oneByIcode(icode);
        int result;
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
                {field_icode, icode},
                {"fields", fields},
                {field_add_users_id, Utils.f2str(fw.userId)}
            });
        return result;
    }

    // list for select by entity and only for logged user OR active system views
    public ArrayList listSelectByIcode(string entity)
    {
        return db.arrayp("select id, iname from " + db.qid(table_name) +
                        @" where status=0 and entity=@entity
                                 and (is_system=1 OR add_users_id=@users_id)
                            order by is_system desc, iname", DB.h("@entity", entity, "@users_id", fw.userId));
    }

    public void setViewForIcode(string icode, int id)
    {
        var item = oneByIcodeId(icode, id);
        if (item.Count == 0) return;

        updateByIcode(icode, item["fields"]);
    }

}