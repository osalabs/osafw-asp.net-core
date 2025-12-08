// Fw Model generic base class extensions

// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2024 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Generic;
using osafw;

namespace osafw;

public abstract class FwModel<TRow> : FwModel where TRow : class, new()
{
    private readonly string typedCachePrefix;
    private readonly string typedCacheByIcodePrefix;

    protected FwModel(FW? fw = null) : base(fw)
    {
        typedCachePrefix = cache_prefix + "typed.";
        typedCacheByIcodePrefix = cache_prefix_byicode + "typed.";
    }

    protected virtual bool isRowEmpty(TRow? row)
    {
        return row == null;
    }

    protected virtual Hashtable buildStatusesWhere(IList? statuses)
    {
        Hashtable where = [];
        if (!string.IsNullOrEmpty(field_status))
        {
            if (statuses != null && statuses.Count > 0)
                where[field_status] = db.opIN(statuses);
            else
                where[field_status] = db.opNOT(STATUS_DELETED);
        }
        return where;
    }

    public virtual TRow? oneT(int id)
    {
        if (id <= 0)
            return null;

        var where = DB.h(field_id, id);

        if (fw?.cache == null)
            return db.row<TRow>(table_name, where);

        var cacheKey = typedCachePrefix + id;
        if (fw.cache.getRequestValue(cacheKey) is TRow cached)
            return cached;

        var row = db.row<TRow>(table_name, where);
        if (!isRowEmpty(row))
            fw.cache.setRequestValue(cacheKey, row);

        return row;
    }

    public virtual TRow? oneT(object id)
    {
        var iid = id.toInt();
        return iid > 0 ? oneT(iid) : null;
    }

    public virtual TRow oneTOrFail(int id)
    {
        var row = oneT(id);
        if (isRowEmpty(row))
            throw new NotFoundException();
        return row!;
    }

    public virtual TRow? oneTByIname(string iname)
    {
        if (string.IsNullOrEmpty(field_iname))
            return null;

        var where = DB.h(field_iname, iname);
        return db.row<TRow>(table_name, where);
    }

    public virtual TRow? oneTByIcode(string icode)
    {
        if (string.IsNullOrEmpty(field_icode))
            return null;

        var where = DB.h(field_icode, icode);

        if (fw?.cache == null)
            return db.row<TRow>(table_name, where);

        var cacheKey = typedCacheByIcodePrefix + icode;
        if (fw.cache.getRequestValue(cacheKey) is TRow cached)
            return cached;

        var row = db.row<TRow>(table_name, where);
        if (!isRowEmpty(row))
        {
            fw.cache.setRequestValue(cacheKey, row);
            if (!string.IsNullOrEmpty(field_id))
            {
                var idValue = row.valueByMemberName(field_id);
                var iid = idValue.toInt();
                if (iid > 0)
                    fw.cache.setRequestValue(typedCachePrefix + iid, row);
            }
        }

        return row;
    }

    public virtual TRow oneTByIcodeOrFail(string icode)
    {
        var row = oneTByIcode(icode);
        if (isRowEmpty(row))
            throw new NotFoundException();
        return row!;
    }

    public virtual List<TRow> listT(IList? statuses = null)
    {
        var where = buildStatusesWhere(statuses);
        return db.array<TRow>(table_name, where, getOrderBy());
    }

    public virtual List<TRow> listTByWhere(Hashtable? where = null, int limit = -1, int offset = 0, string orderby = "")
    {
        where ??= [];
        var order = orderby != "" ? orderby : getOrderBy();
        return db.array<TRow>(table_name, where, order);
    }

    public virtual List<TRow> multiT(ICollection? ids)
    {
        if (ids == null || ids.Count == 0)
            return new List<TRow>();

        var key = string.IsNullOrEmpty(field_id) ? "id" : field_id;
        return db.array<TRow>(table_name, DB.h(key, db.opIN(ids)));
    }

    public virtual TRow convertUserInput(TRow dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var fields = dto.toKeyValue();
        Hashtable htFields = new(fields);
        convertUserInput(htFields);
        htFields.applyTo(dto);
        return dto;
    }

    public virtual int add(TRow dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var fields = dto.toKeyValue();
        prepareFields(fields, forInsert: true);

        if (!string.IsNullOrEmpty(field_add_users_id) && fw?.isLogged == true && !fields.ContainsKey(field_add_users_id))
            fields[field_add_users_id] = fw.userId;

        int id = db.insert(table_name, fields);

        if (is_log_changes)
        {
            if (is_log_fields_changed)
                fw.logActivity(FwLogTypes.ICODE_ADDED, table_name, id, "", new Hashtable(fields));
            else
                fw.logActivity(FwLogTypes.ICODE_ADDED, table_name, id);
        }

        removeCache(id);

        if (!string.IsNullOrEmpty(field_prio) && !fields.ContainsKey(field_prio))
            db.update(table_name, DB.h(field_prio, id), DB.h(field_id, id));

        var props = typeof(TRow).getWritableProperties();
        if (!string.IsNullOrEmpty(field_id))
            dto.setPropertyValue(props, field_id, id);

        if (!string.IsNullOrEmpty(field_add_users_id) && fw?.isLogged == true)
            dto.setPropertyValue(props, field_add_users_id, fw.userId);

        return id;
    }

    public virtual bool update(int id, TRow dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var fields = dto.toHashtable();
        prepareFields(fields, forInsert: false);

        var updated = base.update(id, fields);

        if (updated && !string.IsNullOrEmpty(field_upd_users_id) && fw?.isLogged == true)
        {
            var props = typeof(TRow).getWritableProperties();
            dto.setPropertyValue(props, field_upd_users_id, fw.userId);
        }

        return updated;
    }

    protected virtual void prepareFields(IDictionary? fields, bool forInsert)
    {
        if (fields == null)
            return;

        if (!string.IsNullOrEmpty(field_id) && fields.Contains(field_id))
        {
            var idValue = fields[field_id];
            var iid = idValue.toInt();
            if (forInsert)
            {
                if (iid <= 0)
                    fields.Remove(field_id);
            }
            else
            {
                fields.Remove(field_id);
            }
        }

        if (forInsert)
        {
            removeIfDefault(fields, field_add_users_id);
            removeIfDefault(fields, field_prio);
        }
        else
        {
            removeIfDefault(fields, field_upd_users_id);
        }
    }

    protected virtual void removeIfDefault(IDictionary fields, string field)
    {
        if (fields == null || string.IsNullOrEmpty(field) || !fields.Contains(field))
            return;

        var value = fields[field];
        if (value == null)
        {
            fields.Remove(field);
            return;
        }

        switch (value)
        {
            case string str when string.IsNullOrEmpty(str):
                fields.Remove(field);
                break;
            case IConvertible _ when value.toInt() == 0:
                fields.Remove(field);
                break;
        }
    }

    public override void removeCache(int id)
    {
        base.removeCache(id);
        if (fw?.cache == null)
            return;

        fw.cache.requestRemove(typedCachePrefix + id);
        fw.cache.requestRemoveWithPrefix(typedCacheByIcodePrefix);
    }

    public override void removeCacheAll()
    {
        base.removeCacheAll();
        if (fw?.cache == null)
            return;

        fw.cache.requestRemoveWithPrefix(typedCachePrefix);
        fw.cache.requestRemoveWithPrefix(typedCacheByIcodePrefix);
    }
}
