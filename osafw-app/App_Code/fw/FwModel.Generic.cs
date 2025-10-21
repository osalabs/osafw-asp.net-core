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

    protected FwModel(FW fw = null) : base(fw)
    {
        typedCachePrefix = cache_prefix + "typed.";
        typedCacheByIcodePrefix = cache_prefix_byicode + "typed.";
    }

    protected virtual bool IsRowEmpty(TRow row)
    {
        return row == null;
    }

    protected virtual Hashtable BuildStatusesWhere(IList statuses)
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

    protected virtual TRow CacheTypedOne(int id, Func<TRow> loader)
    {
        if (fw?.cache == null || id <= 0)
            return loader();

        var cacheKey = typedCachePrefix + id;
        if (fw.cache.getRequestValue(cacheKey) is TRow cached)
            return cached;

        var row = loader();
        if (!IsRowEmpty(row))
            fw.cache.setRequestValue(cacheKey, row);

        return row;
    }

    public virtual TRow oneT(int id)
    {
        if (id <= 0)
            return null;

        return CacheTypedOne(id, () =>
        {
            var where = DB.h(field_id, id);
            return db.row<TRow>(table_name, where);
        });
    }

    public virtual TRow oneT(object id)
    {
        var iid = id.toInt();
        return iid > 0 ? oneT(iid) : null;
    }

    public virtual TRow oneOrFailT(int id)
    {
        var row = oneT(id);
        if (IsRowEmpty(row))
            throw new NotFoundException();
        return row;
    }

    public virtual TRow oneByInameT(string iname)
    {
        if (string.IsNullOrEmpty(field_iname))
            return null;

        var where = DB.h(field_iname, iname);
        return db.row<TRow>(table_name, where);
    }

    public virtual TRow oneByIcodeT(string icode)
    {
        if (string.IsNullOrEmpty(field_icode))
            return null;

        if (fw?.cache == null)
            return LoadByIcode(icode);

        var cacheKey = typedCacheByIcodePrefix + icode;
        if (fw.cache.getRequestValue(cacheKey) is TRow cached)
            return cached;

        var row = LoadByIcode(icode);
        if (!IsRowEmpty(row))
        {
            fw.cache.setRequestValue(cacheKey, row);
            CacheByIdFromTypedRow(row);
        }

        return row;
    }

    public virtual TRow oneByIcodeOrFailT(string icode)
    {
        var row = oneByIcodeT(icode);
        if (IsRowEmpty(row))
            throw new NotFoundException();
        return row;
    }

    protected virtual TRow LoadByIcode(string icode)
    {
        var where = DB.h(field_icode, icode);
        return db.row<TRow>(table_name, where);
    }

    protected virtual void CacheByIdFromTypedRow(TRow row)
    {
        if (fw?.cache == null || string.IsNullOrEmpty(field_id) || IsRowEmpty(row))
            return;

        var idValue = row.valueByMemberName(field_id);
        var iid = idValue.toInt();
        if (iid > 0)
            fw.cache.setRequestValue(typedCachePrefix + iid, row);
    }

    public virtual List<TRow> listT(IList statuses = null)
    {
        var where = BuildStatusesWhere(statuses);
        return db.array<TRow>(table_name, where, getOrderBy());
    }

    public virtual List<TRow> listByWhereT(Hashtable where = null, int limit = -1, int offset = 0, string orderby = "")
    {
        where ??= [];
        var order = orderby != "" ? orderby : getOrderBy();
        return db.array<TRow>(table_name, where, order);
    }

    public virtual List<TRow> multiT(ICollection ids)
    {
        if (ids == null || ids.Count == 0)
            return new List<TRow>();

        object[] arr = new object[ids.Count];
        ids.CopyTo(arr, 0);
        return db.array<TRow>(table_name, DB.h("id", db.opIN(arr)));
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

        var fields = dto.toHashtable();
        PrepareFields(fields, forInsert: true);

        int id = add(fields);

        var props = typeof(TRow).GetWritableProperties();
        if (!string.IsNullOrEmpty(field_id))
            dto.SetPropertyValue(props, field_id, id);

        if (!string.IsNullOrEmpty(field_add_users_id) && fw?.isLogged == true)
            dto.SetPropertyValue(props, field_add_users_id, fw.userId);

        return id;
    }

    public virtual bool update(int id, TRow dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var fields = dto.toHashtable();
        PrepareFields(fields, forInsert: false);

        var result = update(id, fields);

        if (!string.IsNullOrEmpty(field_upd_users_id) && fw?.isLogged == true)
        {
            var props = typeof(TRow).GetWritableProperties();
            dto.SetPropertyValue(props, field_upd_users_id, fw.userId);
        }

        return result;
    }

    protected virtual void PrepareFields(Hashtable fields, bool forInsert)
    {
        if (!string.IsNullOrEmpty(field_id) && fields.ContainsKey(field_id))
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
            RemoveIfDefault(fields, field_add_users_id);
            RemoveIfDefault(fields, field_prio);
        }
        else
        {
            RemoveIfDefault(fields, field_upd_users_id);
        }
    }

    protected virtual void RemoveIfDefault(Hashtable fields, string field)
    {
        if (string.IsNullOrEmpty(field) || !fields.ContainsKey(field))
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
