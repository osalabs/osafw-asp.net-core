// UserFilters model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;

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

    /// <summary>
    /// Returns one saved filter owned by the logged-in user so direct id paths cannot load another user's private filter.
    /// </summary>
    /// <param name="id">The `user_filters.id` value to load.</param>
    /// <returns>The matching saved filter row, or an empty row when unavailable.</returns>
    public virtual DBRow oneMine(int id)
    {
        return oneByOwner(id, "mine", false);
    }

    /// <summary>
    /// Returns one saved filter available to the logged-in user, including system filters that may be applied by everyone.
    /// </summary>
    /// <param name="id">The `user_filters.id` value to load.</param>
    /// <returns>The matching owner or system saved filter row, or an empty row when unavailable.</returns>
    public virtual DBRow oneAvail(int id)
    {
        return oneByOwner(id, "avail", true);
    }

    /// <summary>
    /// Loads saved filters through the owner-or-system predicate used by generic CRUD id paths.
    /// </summary>
    /// <param name="id">The `user_filters.id` value to load.</param>
    /// <returns>The matching available row, or an empty row when unavailable.</returns>
    public override DBRow one(int id)
    {
        return oneAvail(id);
    }

    /// <summary>
    /// Adds a saved filter only when the submitted system flag is allowed for the current user.
    /// </summary>
    /// <param name="item">Filtered `user_filters` field values to insert.</param>
    /// <returns>The new `user_filters.id` value.</returns>
    public override int add(FwDict item)
    {
        checkSystemInput(item);
        return base.add(item);
    }

    /// <summary>
    /// Updates a saved filter only when the current user owns it or can manage system filters.
    /// </summary>
    /// <param name="id">The `user_filters.id` value to update.</param>
    /// <param name="item">Filtered field values to persist.</param>
    /// <returns>True when the update was issued.</returns>
    public override bool update(int id, FwDict item)
    {
        checkEdit(oneAvail(id));
        checkSystemInput(item);
        return base.update(id, item);
    }

    /// <summary>
    /// Deletes a saved filter only when the current user owns it or can manage system filters.
    /// </summary>
    /// <param name="id">The `user_filters.id` value to delete.</param>
    /// <param name="is_perm">True to permanently delete the row instead of using status soft-delete.</param>
    public override void delete(int id, bool is_perm = false)
    {
        checkEdit(oneAvail(id));
        base.delete(id, is_perm);
    }

    // list for select by icode and only for logged user OR active system filters
    public FwList listSelectByIcode(string icode)
    {
        return db.arrayp("select id, iname from " + db.qid(table_name) +
            @" where status=0 and icode=@icode
                     and (is_system=1 OR add_users_id=@users_id)
                   order by is_system desc, iname", DB.h("@icode", icode, "@users_id", fw.userId));
    }

    private DBRow oneByOwner(int id, string scope, bool is_include_system)
    {
        if (id <= 0)
            return [];

        var cacheKey = $"{cache_prefix}{scope}:{fw.userId}:{id}";
        if (fw.cache.getRequestValue(cacheKey) is FwDict cached)
            return (DBRow)cached;

        var sql = $@"select *
  from {qTable()}
 where id=@id
   and add_users_id=@users_id";
        if (is_include_system)
            sql = $@"select *
  from {qTable()}
 where id=@id
   and (is_system=1 OR add_users_id=@users_id)";

        var item = db.rowp(sql, DB.h("@id", id, "@users_id", fw.userId));
        normalizeNames(item);
        fw.cache.setRequestValue(cacheKey, item);
        return item;
    }

    private bool isSystemManager()
    {
        return fw.model<Users>().isAccessLevel(Users.ACL_ADMIN);
    }

    private void checkEdit(DBRow item)
    {
        if (item.Count == 0)
            throw new NotFoundException();
        if (item["is_system"].toBool() && !isSystemManager())
            throw new AuthException();
    }

    private void checkSystemInput(FwDict item)
    {
        if (item["is_system"].toBool() && !isSystemManager())
            throw new AuthException();
    }
}
