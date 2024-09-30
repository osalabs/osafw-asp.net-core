// User Custom List Views model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class UserViews : FwModel
{
    public static string icodeByUrl(string url, bool is_list_edit = false)
    {
        return url + (is_list_edit ? "/edit" : "");
    }

    public UserViews() : base()
    {
        table_name = "user_views";
        is_log_changes = false; //no need to log changes for user views
    }

    // return default screen record for logged user
    public override DBRow oneByIcode(string icode)
    {
        return db.row(table_name, DB.h(field_add_users_id, fw.userId, field_icode, icode, field_iname, ""));
    }

    // return screen record for logged user by id
    public DBRow oneByIcodeId(string icode, int id)
    {
        var p = new Hashtable()
        {
            { field_icode, icode },
            { field_id, id },
            { "meId", fw.userId},
        };

        return db.rowp(@"select * from " + db.qid(table_name) +
             @" where icode=@icode
                      and id=@id
                      and (is_system=1 OR add_users_id=@meId)", p);
    }

    // by icode/iname/loggeduser
    public DBRow oneByUK(string icode, string iname)
    {
        return db.row(table_name, new Hashtable() {
            {field_icode, icode },
            {field_iname, iname },
            {field_add_users_id, fw.userId },
        });
    }

    // add view for logged user with icode, fields, iname
    public int addSimple(string icode, string fields, string iname, string density = "")
    {
        return add(new Hashtable()
        {
            { field_icode, icode },
            { field_iname, iname },
            { "fields", fields },
            { "density", density },
            { field_add_users_id, fw.userId },
        });
    }

    // add or update view for logged user
    public int addOrUpdateByUK(string icode, string fields, string iname)
    {
        int id;
        var item = oneByUK(icode, iname);
        if (item.Count > 0)
        {
            id = Utils.toInt(item["id"]);
            update(id, DB.h("fields", fields));
        }
        else
        {
            id = addSimple(icode, fields, iname);
        }
        return id;
    }

    /// <summary>
    /// update default screen fields for logged user
    /// </summary>
    /// <param name="icode">screen url</param>
    /// <param name="fields">comma-separated fields</param>
    /// <param name="iname">view title (for save new view)</param>
    /// <returns>user_views.id</returns>
    public int updateByIcode(string icode, Hashtable itemdb)
    {
        var item = oneByIcode(icode);
        int result;
        if (item.Count > 0)
        {
            // exists
            result = Utils.toInt(item[field_id]);
            update(result, itemdb);
        }
        else
        {
            // new - add key fields
            var itemdb_add = (Hashtable)itemdb.Clone();
            itemdb_add[field_icode] = icode;
            itemdb_add[field_add_users_id] = fw.userId;
            result = add(itemdb_add);
        }
        return result;
    }

    /// <summary>
    /// update default screen fields for logged user
    /// </summary>
    /// <param name="icode">screen url</param>
    /// <param name="fields">comma-separated fields</param>
    /// <param name="iname">view title (for save new view)</param>
    /// <returns>user_views.id</returns>
    public int updateByIcodeFields(string icode, string fields)
    {
        return updateByIcode(icode, DB.h("fields", fields));
    }

    /// <summary>
    /// list for select by icode(basically controller's base_url) and only for logged user OR active system views
    /// iname>'' - because empty name is for default view, it's not visible in the list (use "Reset to Defaults" instead)
    /// </summary>
    /// <param name="icode"></param>
    /// <returns></returns>
    public ArrayList listSelectByIcode(string icode)
    {
        return db.arrayp("select id, iname from " + db.qid(table_name) +
                        @" where status=0
                                 and iname>''
                                 and icode=@icode
                                 and (is_system=1 OR add_users_id=@users_id)
                            order by is_system desc, iname", DB.h("@icode", icode, "@users_id", fw.userId));
    }

    /// <summary>
    /// list all icodes available for the user
    /// </summary>
    /// <returns></returns>
    public ArrayList listSelectIcodes()
    {
        return db.arrayp("select distinct icode as id, icode as iname from " + db.qid(table_name) +
                        @" where status=0 
                                 and iname>''
                                 and (is_system=1 OR add_users_id=@users_id)
                            order by icode", DB.h("@users_id", fw.userId));
    }

    /// <summary>
    /// replace current default view for icode using view in id
    /// </summary>
    /// <param name="icode"></param>
    /// <param name="id"></param>
    public void setViewForIcode(string icode, int id)
    {
        var item = oneByIcodeId(icode, id);
        if (item.Count == 0) return;

        updateByIcodeFields(icode, item["fields"]);
    }
}