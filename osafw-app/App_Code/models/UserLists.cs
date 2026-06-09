// UserLists model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;

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

    /// <summary>
    /// Returns one list owned by the logged-in user so direct id paths cannot load another user's list.
    /// </summary>
    /// <param name="id">The `user_lists.id` value to load.</param>
    /// <returns>The matching owned list row, or an empty row when unavailable.</returns>
    public virtual DBRow oneMine(int id)
    {
        if (id <= 0)
            return [];

        var cacheKey = $"{cache_prefix}mine:{fw.userId}:{id}";
        if (fw.cache.getRequestValue(cacheKey) is FwDict cached)
            return (DBRow)cached;

        var sql = $@"select *
  from {qTable()}
 where id=@id
   and add_users_id=@users_id";
        var item = db.rowp(sql, DB.h("@id", id, "@users_id", fw.userId));
        normalizeNames(item);
        fw.cache.setRequestValue(cacheKey, item);
        return item;
    }

    /// <summary>
    /// Loads lists through the owner predicate used by generic CRUD id paths.
    /// </summary>
    /// <param name="id">The `user_lists.id` value to load.</param>
    /// <returns>The matching owned list row, or an empty row when unavailable.</returns>
    public override DBRow one(int id)
    {
        return oneMine(id);
    }

    /// <summary>
    /// Adds a list as owned by the current user, ignoring any submitted owner value.
    /// </summary>
    /// <param name="item">Filtered `user_lists` field values to insert.</param>
    /// <returns>The new `user_lists.id` value.</returns>
    public override int add(FwDict item)
    {
        if (fw.isLogged)
            item[field_add_users_id] = fw.userId;
        return base.add(item);
    }

    /// <summary>
    /// Updates a list only when the current user owns it.
    /// </summary>
    /// <param name="id">The `user_lists.id` value to update.</param>
    /// <param name="item">Filtered field values to persist.</param>
    /// <returns>True when the update was issued.</returns>
    public override bool update(int id, FwDict item)
    {
        checkMine(id);
        return base.update(id, item);
    }

    /// <summary>
    /// Deletes a list only when the current user owns it.
    /// </summary>
    /// <param name="id">The `user_lists.id` value to delete.</param>
    /// <param name="is_perm">True to permanently delete the row and its linked items.</param>
    public override void delete(int id, bool is_perm = false)
    {
        checkMine(id);
        if (is_perm)
        {
            // delete list items first
            FwDict where = [];
            where["user_lists_id"] = id;
            db.del(table_items, where);
        }

        base.delete(id, is_perm);
    }

    public int countItems(int id)
    {
        return db.value(table_items, new FwDict() { { "user_lists_id", id } }, "count(*)").toInt();
    }

    public FwList listSelectOptionsEntities()
    {
        return db.arrayp(@" SELECT DISTINCT entity AS id, entity AS iname
                                  FROM " + db.qid(table_name) +
                         @"  WHERE add_users_id = @users_id 
                              ORDER BY entity "
                        , DB.h("@users_id", fw.userId)
        );
    }

    // list for select by entity and for only logged user
    public FwList listSelectByEntity(string entity)
    {
        FwDict where = [];
        where["status"] = STATUS_ACTIVE;
        where["entity"] = entity;
        where["add_users_id"] = fw.userId;
        return db.array(table_name, where, "iname", Utils.qw("id iname"));
    }

    /// <summary>
    /// Lists items for a user list only after proving the current user owns the parent list.
    /// </summary>
    /// <param name="id">The parent `user_lists.id` value.</param>
    /// <returns>Active linked item rows for the owned list.</returns>
    public FwList listItemsById(int id)
    {
        checkMine(id);

        FwDict where = [];
        where["status"] = STATUS_ACTIVE;
        where["user_lists_id"] = id;
        return db.array(table_items, where, "id desc", Utils.qw("id item_id"));
    }

    public FwList listForItem(string entity, int item_id)
    {
        return db.arrayp(@"select t.id, t.iname, @item_id as item_id, ti.id as is_checked
                                 from " + db.qid(table_name) + " t" +
                         "        LEFT OUTER JOIN " + db.qid(table_items) + @" ti ON (ti.user_lists_id=t.id and ti.item_id=@item_id )
                                where t.status=0 and t.entity=@entity
                                  and t.add_users_id=@users_id
                             order by t.iname", DB.h("@item_id", item_id, "@entity", entity, "@users_id", fw.userId));
    }

    /// <summary>
    /// Returns one linked item only after confirming the current user owns the parent list.
    /// </summary>
    /// <param name="user_lists_id">The parent `user_lists.id` value.</param>
    /// <param name="item_id">The linked application record id stored in `user_lists_items.item_id`.</param>
    /// <returns>The matching list-item row, or an empty row when it is not present.</returns>
    public FwDict oneItemsByUK(int user_lists_id, int item_id)
    {
        checkMine(user_lists_id);
        return db.row(table_items, DB.h("user_lists_id", user_lists_id, "item_id", item_id));
    }

    /// <summary>
    /// Deletes one linked list item only when it belongs to a list owned by the current user.
    /// </summary>
    /// <param name="id">The `user_lists_items.id` value to delete.</param>
    public virtual void deleteItems(int id)
    {
        if (oneItemMine(id).Count == 0)
            throw new NotFoundException();

        FwDict where = [];
        where["id"] = id;
        db.del(table_items, where);

        if (is_log_changes)
            fw.logActivity(FwLogTypes.ICODE_DELETED, table_items, id);
    }

    /// <summary>
    /// Adds one linked item only when the parent list is owned by the current user.
    /// </summary>
    /// <param name="user_lists_id">The parent `user_lists.id` value.</param>
    /// <param name="item_id">The linked application record id to store.</param>
    /// <returns>The new `user_lists_items.id` value.</returns>
    public virtual int addItems(int user_lists_id, int item_id)
    {
        checkMine(user_lists_id);

        FwDict item = [];
        item["user_lists_id"] = user_lists_id;
        item["item_id"] = item_id;
        item["add_users_id"] = fw.userId;

        int id = db.insert(table_items, item);

        if (is_log_changes)
            fw.logActivity(FwLogTypes.ICODE_ADDED, table_items, id);

        return id;
    }

    /// <summary>
    /// Adds or removes one item only when the parent list is owned by the current user.
    /// </summary>
    /// <param name="user_lists_id">The parent `user_lists.id` value.</param>
    /// <param name="item_id">The linked application record id to toggle.</param>
    /// <returns>True when the item was added; false when it was removed.</returns>
    public bool toggleItemList(int user_lists_id, int item_id)
    {
        checkMine(user_lists_id);

        var result = false;
        FwDict litem = oneItemsByUK(user_lists_id, item_id);
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

    /// <summary>
    /// Adds one item only when the parent list is owned by the current user.
    /// </summary>
    /// <param name="user_lists_id">The parent `user_lists.id` value.</param>
    /// <param name="item_id">The linked application record id to add.</param>
    /// <returns>True when a new link was created; false when the link already existed.</returns>
    public bool addItemList(int user_lists_id, int item_id)
    {
        checkMine(user_lists_id);

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

    /// <summary>
    /// Removes one item only when the parent list is owned by the current user.
    /// </summary>
    /// <param name="user_lists_id">The parent `user_lists.id` value.</param>
    /// <param name="item_id">The linked application record id to remove.</param>
    /// <returns>True when an existing link was removed; false when the link was absent.</returns>
    public bool delItemList(int user_lists_id, int item_id)
    {
        checkMine(user_lists_id);

        var result = false;
        FwDict litem = oneItemsByUK(user_lists_id, item_id);
        if (litem.Count > 0)
        {
            deleteItems(litem["id"].toInt());
            result = true;
        }

        return result;
    }

    /// <summary>
    /// Returns one linked item only when its parent list is owned by the current user.
    /// </summary>
    /// <param name="id">The `user_lists_items.id` value to load.</param>
    /// <returns>The matching linked item row, or an empty row when unavailable.</returns>
    protected virtual DBRow oneItemMine(int id)
    {
        if (id <= 0)
            return [];

        var cacheKey = $"{cache_prefix}itemmine:{fw.userId}:{id}";
        if (fw.cache.getRequestValue(cacheKey) is FwDict cached)
            return (DBRow)cached;

        var item = db.rowp(@"select ti.* from " + db.qid(table_items) + @" ti
                              inner join " + db.qid(table_name) + @" t on t.id=ti.user_lists_id
                              where ti.id=@id
                                and t.add_users_id=@users_id", DB.h("@id", id, "@users_id", fw.userId));
        fw.cache.setRequestValue(cacheKey, item);
        return item;
    }

    private DBRow checkMine(int id)
    {
        var item = oneMine(id);
        if (item.Count == 0)
            throw new NotFoundException();
        return item;
    }
}
