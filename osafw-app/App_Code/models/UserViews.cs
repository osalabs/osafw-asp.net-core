// User Custom List Views model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections.Generic;

namespace osafw;

public class UserViews : FwModel<UserViews.Row>
{
    public class Row
    {
        public int id { get; set; }
        public string icode { get; set; } = string.Empty;
        public string fields { get; set; } = string.Empty;
        public string iname { get; set; } = string.Empty;
        public int is_system { get; set; }
        public int is_shared { get; set; }
        public string density { get; set; } = string.Empty;
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

    private const int CacheSeconds = 300;
    private string CacheRegistryKey => $"fw:userviews:registry:{fw.userId}";

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
        // prefer request cache first
        var reqCacheKey = cache_prefix_byicode + icode;
        if (fw.cache.getRequestValue(reqCacheKey) is DBRow requestCached)
            return requestCached;

        // then try shared cache to bypass reloading same view across requests
        var appCacheKey = cacheKeyDefault(icode);
        if (FwCache.getValue(appCacheKey) is DBRow cached)
        {
            var cloned = cloneRow(cached);
            fw.cache.setRequestValue(reqCacheKey, cloned);
            return cloned;
        }

        // fall back to default implementation (includes request cache + normalization)
        var item = base.oneByIcode(icode);

        var sharedCopy = cloneRow(item);
        FwCache.setValue(appCacheKey, sharedCopy, CacheSeconds);
        registerCachedIcode(icode);

        return item;
    }

    // return screen record for logged user by id
    public DBRow oneByIcodeId(string icode, int id)
    {
        var p = new FwDict()
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
        return db.row(table_name, new FwDict() {
            {field_icode, icode },
            {field_iname, iname },
            {field_add_users_id, fw.userId },
        });
    }

    // add view for logged user with icode, fields, iname
    public int addSimple(string icode, string fields, string iname, string density = "")
    {
        var result = add(new FwDict()
        {
            { field_icode, icode },
            { field_iname, iname },
            { "fields", fields },
            { "density", density },
            { field_add_users_id, fw.userId },
        });
        return result;
    }

    // add or update view for logged user
    public int addOrUpdateByUK(string icode, string fields, string iname)
    {
        int id;
        var item = oneByUK(icode, iname);
        if (item.Count > 0)
        {
            id = item["id"].toInt();
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
    public int updateByIcode(string icode, FwDict itemdb)
    {
        var item = oneByIcode(icode);
        int result;
        if (item.Count > 0)
        {
            // exists
            result = item[field_id].toInt();
            update(result, itemdb);
        }
        else
        {
            // new - add key fields
            FwDict itemdb_add = new(itemdb);
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
    public FwList listSelectByIcode(string icode)
    {
        var cacheKey = cacheKeySelect(icode);
        if (FwCache.getValue(cacheKey) is FwList cached)
            return cloneList(cached);

        var rows = db.arrayp("select id, iname from " + db.qid(table_name) +
                        @" where status=0
                                 and iname>''
                                 and icode=@icode
                                 and (is_system=1 OR add_users_id=@users_id)
                            order by is_system desc, iname", DB.h("@icode", icode, "@users_id", fw.userId));
        var toCache = cloneList(rows);
        FwCache.setValue(cacheKey, toCache, CacheSeconds);
        registerCachedIcode(icode);
        return rows;
    }

    /// <summary>
    /// list all icodes available for the user
    /// </summary>
    /// <returns></returns>
    public FwList listSelectIcodes()
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

    private string cacheKeyDefault(string icode)
    {
        return $"fw:userviews:default:{fw.userId}:{icode}";
    }

    private string cacheKeySelect(string icode)
    {
        return $"fw:userviews:select:{fw.userId}:{icode}";
    }

    public override void removeCache(int id)
    {
        var icode = id > 0 ? db.value(table_name, DB.h(field_id, id), field_icode).toStr() : string.Empty;
        base.removeCache(id);
        if (!string.IsNullOrEmpty(icode))
            removeAppCache(icode);
    }

    public override void removeCacheAll()
    {
        base.removeCacheAll();
        removeAppCacheAll();
    }

    private static DBRow cloneRow(DBRow row)
    {
        return (DBRow)(Utils.cloneHashDeep(row) ?? []);
    }

    private static FwList cloneList(FwList rows)
    {
        FwList cloned = [];
        foreach (FwDict row in rows)
            cloned.Add(Utils.cloneHashDeep(row) ?? []);
        return cloned;
    }

    private void registerCachedIcode(string icode)
    {
        if (string.IsNullOrEmpty(icode))
            return;

        var set = (HashSet<string>?)FwCache.getValue(CacheRegistryKey) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        set.Add(icode);
        FwCache.setValue(CacheRegistryKey, set, CacheSeconds);
    }

    private void removeAppCache(string icode)
    {
        if (string.IsNullOrEmpty(icode))
            return;

        FwCache.remove(cacheKeyDefault(icode));
        FwCache.remove(cacheKeySelect(icode));

        if (FwCache.getValue(CacheRegistryKey) is HashSet<string> set && set.Remove(icode))
            FwCache.setValue(CacheRegistryKey, set, CacheSeconds);
    }

    private void removeAppCacheAll()
    {
        if (FwCache.getValue(CacheRegistryKey) is not HashSet<string> set || set.Count == 0)
            return;

        foreach (var icode in set)
        {
            FwCache.remove(cacheKeyDefault(icode));
            FwCache.remove(cacheKeySelect(icode));
        }

        FwCache.remove(CacheRegistryKey);
    }
}
