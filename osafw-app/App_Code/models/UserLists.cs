// UserLists model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class UserLists : FwModel<UserLists.Row>
{
    public class Row
    {
        public int id { get; set; }
        public string entity { get; set; } = string.Empty;
        public string iname { get; set; } = string.Empty;
        public string idesc { get; set; } = string.Empty;
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

    public string table_items = "user_lists_items";

    public UserLists() : base()
    {
        table_name = "user_lists";
        is_log_changes = false; // no need to log changes here
    }

    public int countItems(int id)
    {
        return db.value(table_items, new Hashtable() { { "user_lists_id", id } }, "count(*)").toInt();
    }

    public ArrayList listSelectOptionsEntities()
    {
        return db.arrayp(@" SELECT DISTINCT entity AS id, entity AS iname
                                  FROM " + db.qid(table_name) +
                         @"  WHERE add_users_id = @users_id 
                              ORDER BY entity "
                        , DB.h("@users_id", fw.userId)
        );
    }

    // list for select by entity and for only logged user
    public ArrayList listSelectByEntity(string entity)
    {
        Hashtable where = [];
        where["status"] = STATUS_ACTIVE;
        where["entity"] = entity;
        where["add_users_id"] = fw.userId;
        return db.array(table_name, where, "iname", Utils.qw("id iname"));
    }

    public ArrayList listItemsById(int id)
    {
        Hashtable where = [];
        where["status"] = STATUS_ACTIVE;
        where["user_lists_id"] = id;
        return db.array(table_items, where, "id desc", Utils.qw("id item_id"));
    }

    public ArrayList listForItem(string entity, int item_id)
    {
        return db.arrayp(@"select t.id, t.iname, @item_id as item_id, ti.id as is_checked
                                 from " + db.qid(table_name) + " t" +
                         "        LEFT OUTER JOIN " + db.qid(table_items) + @" ti ON (ti.user_lists_id=t.id and ti.item_id=@item_id )
                                where t.status=0 and t.entity=@entity
                                  and t.add_users_id=@users_id
                             order by t.iname", DB.h("@item_id", item_id, "@entity", entity, "@users_id", fw.userId));
    }

    public override void delete(int id, bool is_perm = false)
    {
        if (is_perm)
        {
            // delete list items first
            Hashtable where = [];
            where["user_lists_id"] = id;
            db.del(table_items, where);
        }

        base.delete(id, is_perm);
    }

    public Hashtable oneItemsByUK(int user_lists_id, int item_id)
    {
        return db.row(table_items, DB.h("user_lists_id", user_lists_id, "item_id", item_id));
    }

    public virtual void deleteItems(int id)
    {
        Hashtable where = [];
        where["id"] = id;
        db.del(table_items, where);

        if (is_log_changes)
            fw.logActivity(FwLogTypes.ICODE_DELETED, table_items, id);
    }

    // add new record and return new record id
    public virtual int addItems(int user_lists_id, int item_id)
    {
        Hashtable item = [];
        item["user_lists_id"] = user_lists_id;
        item["item_id"] = item_id;
        item["add_users_id"] = fw.userId;

        int id = db.insert(table_items, item);

        if (is_log_changes)
            fw.logActivity(FwLogTypes.ICODE_ADDED, table_items, id);

        return id;
    }

    // add or remove item from the list
    public bool toggleItemList(int user_lists_id, int item_id)
    {
        var result = false;
        Hashtable litem = oneItemsByUK(user_lists_id, item_id);
        if (litem.Count > 0)
            // remove
            deleteItems(litem["id"].toInt());
        else
        {
            // add new
            addItems(user_lists_id, item_id);
            result = true;
        }

        return result;
    }

    // add item to the list, if item not yet in the list
    public bool addItemList(int user_lists_id, int item_id)
    {
        var result = false;
        var litem = oneItemsByUK(user_lists_id, item_id);
        if (litem.Count > 0)
        {
        }
        else
        {
            // add new
            addItems(user_lists_id, item_id);
            result = true;
        }

        return result;
    }

    // delete item from the list
    public bool delItemList(int user_lists_id, int item_id)
    {
        var result = false;
        Hashtable litem = oneItemsByUK(user_lists_id, item_id);
        if (litem.Count > 0)
        {
            deleteItems(litem["id"].toInt());
            result = true;
        }

        return result;
    }
}