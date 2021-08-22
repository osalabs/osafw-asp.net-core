// UserLists model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw
{
    public class UserLists : FwModel
    {
        public string table_items = "user_lists_items";

        public UserLists() : base()
        {
            table_name = "user_lists";
        }

        public int countItems(int id)
        {
            return (int)db.value(table_items, new Hashtable() { { "user_lists_id", id } }, "count(*)");
        }

        // list for select by entity and for only logged user
        public ArrayList listSelectByEntity(string entity)
        {
            return db.array("select id, iname from " + table_name + " where status=0 and entity=" + db.q(entity) + " and add_users_id=" + db.qi(Users.id) + " order by iname");
        }

        public ArrayList listItemsById(int id)
        {
            return db.array("select id, item_id from " + table_items + " where status=0 and user_lists_id=" + db.qi(id) + " order by id desc");
        }

        public ArrayList listForItem(string entity, int item_id)
        {
            return db.array("select t.id, t.iname, " + item_id + " as item_id, ti.id as is_checked from " + table_name + " t" + " LEFT OUTER JOIN " + table_items + " ti ON (ti.user_lists_id=t.id and ti.item_id=" + item_id + " )" + " where t.status=0 and t.entity=" + db.q(entity) + " and t.add_users_id=" + db.qi(Users.id) + " order by t.iname");
        }

        public override void delete(int id, bool is_perm = false)
        {
            if (is_perm)
            {
                // delete list items first
                Hashtable where = new();
                where["user_lists_id"] = id;
                db.del(table_items, where);
            }

            base.delete(id, is_perm);
        }

        public Hashtable oneItemsByUK(int user_lists_id, int item_id)
        {
            return db.row(table_items, new Hashtable() { { "user_lists_id", user_lists_id }, { "item_id", item_id } });
        }

        public virtual void deleteItems(int id)
        {
            Hashtable where = new();
            where["id"] = id;
            db.del(table_items, where);
            fw.logEvent(table_items + "_del", id);
        }

        // add new record and return new record id
        public virtual int addItems(int user_lists_id, int item_id)
        {
            Hashtable item = new();
            item["user_lists_id"] = user_lists_id;
            item["item_id"] = item_id;
            item["add_users_id"] = Users.id;

            int id = db.insert(table_items, item);
            fw.logEvent(table_items + "_add", id);
            return id;
        }

        // add or remove item from the list
        public bool toggleItemList(int user_lists_id, int item_id)
        {
            var result = false;
            var litem = oneItemsByUK(user_lists_id, item_id);
            if (litem.Count > 0)
                // remove 
                deleteItems((int)litem["id"]);
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
            var litem = oneItemsByUK(user_lists_id, item_id);
            if (litem.Count > 0)
            {
                deleteItems((int)litem["id"]);
                result = true;
            }

            return result;
        }
    }
}