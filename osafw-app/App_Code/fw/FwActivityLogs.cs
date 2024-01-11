// Activity Logs model class
// can be used for:
// - log user activity
// - log comments per entity
// - log changes per entity
// - log related events per entity
// - log custom user events per entity
//
// Can be used as a base class for custom log models
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2024 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class FwActivityLogs : FwModel
{
    public const string TAB_ALL = "all";
    public const string TAB_COMMENTS = "comments";
    public const string TAB_HISTORY = "history";

    public FwActivityLogs() : base()
    {
        table_name = "activity_logs";
        is_log_changes = false; // disable logging of changes in this table as this is log table itself
    }

    /// <summary>
    /// add new log record by icodes
    /// </summary>
    /// <param name="log_types_icode">required, must be predefined constant from FwLogTypes</param>
    /// <param name="entity_icode">required, fwentity, basically table name - if not exists - autocreated</param>
    /// <param name="item_id">related item id, if 0 - NULL will be stored in db</param>
    /// <param name="idesc">optional title/description</param>
    /// <param name="payload">optional payload (will be serialized as json)</param>
    /// <returns></returns>
    /// <exception cref="ApplicationException"></exception>
    public int addSimple(string log_types_icode, string entity_icode, int item_id = 0, string idesc = "", Hashtable payload = null)
    {
        var lt = fw.model<FwLogTypes>().oneByIcode(log_types_icode);
        if (lt.Count == 0)
            throw new ApplicationException("Log type not found for icode=[" + log_types_icode + "]");
        var et_id = fw.model<FwEntities>().idByIcodeOrAdd(entity_icode);
        var fields = new Hashtable
        {
            ["log_types_id"] = lt["id"],
            ["fwentities_id"] = et_id,
            ["idesc"] = idesc,
            ["users_id"] = fw.userId
        };
        if (item_id != 0)
            fields["item_id"] = item_id;
        if (payload != null)
            fields["payload"] = Utils.jsonEncode(payload);
        return add(fields);
    }


    /// <summary>
    /// return activity for given entity
    /// </summary>
    /// <param name="entity_icode">entity table name</param>
    /// <param name="id">entity item id</param>
    /// <param name="log_types_icodes">optional list of log types(by icode) to filter on</param>
    /// <returns></returns>
    public DBList listByEntity(string entity_icode, int id, IList log_types_icodes = null)
    {
        var fwentities_id = fw.model<FwEntities>().idByIcodeOrAdd(entity_icode);
        var where = new Hashtable
        {
            {"fwentities_id", fwentities_id },
            {"item_id", id }
        };
        if (log_types_icodes != null && log_types_icodes.Count > 0)
        {
            var log_types_ids = new ArrayList();
            foreach (string icode in log_types_icodes)
            {
                var log_type = fw.model<FwLogTypes>().oneByIcode(icode);
                log_types_ids.Add(log_type["id"]);
            }
            where["log_types_id"] = db.opIN(log_types_ids);
        }

        return db.array(table_name, where, "idate desc, id desc");
    }

    /// <summary>
    /// return activity for given entity for UI
    /// </summary>
    /// <param name="entity_icode"></param>
    /// <param name="id"></param>
    /// <param name="tab">"all", "comments" or "history"</param>
    /// <returns></returns>
    public ArrayList listByEntityForUI(string entity_icode, int id, string tab = "")
    {
        // convert tab to log_types_icodes
        var log_types_icodes = new ArrayList();
        switch (tab)
        {
            case TAB_COMMENTS:
                log_types_icodes.Add(FwLogTypes.ICODE_COMMENT);
                break;
            case TAB_HISTORY:
                log_types_icodes.Add(FwLogTypes.ICODE_ADDED);
                log_types_icodes.Add(FwLogTypes.ICODE_UPDATED);
                log_types_icodes.Add(FwLogTypes.ICODE_DELETED);
                break;
        }

        // prepare list of activity records for UI
        // group system consequential changes from the same user within 10 minutes into one fields row
        Hashtable last_fields = null;
        var last_add_time = DateTime.MinValue;
        var last_users_id = -1;
        var last_log_types_id = -1;

        var result = new ArrayList();
        var rows = listByEntity(entity_icode, id, log_types_icodes);
        foreach (DBRow row in rows)
        {
            var add_time = (DateTime)Utils.f2date(row["add_time"]);
            var users_id = Utils.f2int(row["users_id"]);
            var log_types_id = Utils.f2int(row["log_types_id"]);
            var log_type = fw.model<FwLogTypes>().one(log_types_id);

            //for system types - fill fields from payload
            var is_merged = false;
            if (Utils.f2int(log_type["itype"]) == FwLogTypes.ITYPE_SYSTEM)
            {
                if (last_fields != null
                    && last_log_types_id == log_types_id
                    && last_users_id == users_id
                    && add_time.Subtract(last_add_time).TotalMinutes < 10)
                {
                    // same user and short time between updates - merge with last_fields
                    is_merged = true;
                }
                else
                {
                    // new row
                    last_fields = [];
                }

                Hashtable payload = (Hashtable)Utils.jsonDecode(row["payload"]);
                Hashtable fields = (Hashtable)payload["fields"] ?? null;
                if (fields != null)
                {
                    foreach (string key in fields.Keys)
                    {
                        //if key is password, pass, pwd - hide value
                        var value = fields[key];
                        if (key.Contains("pass") || key.Contains("pwd"))
                            value = "********";

                        // deduplicate - if key already exists - skip, because we merging older row into newer
                        if (!last_fields.ContainsKey(key))
                            last_fields[key] = value;
                    }
                }
            }
            else
            {
                last_fields = null; // reset fields for user types
            }

            last_users_id = users_id;
            last_add_time = add_time;
            last_log_types_id = log_types_id;

            if (is_merged)
                continue; // skip this row as it's merged with previous

            var new_row = new Hashtable();
            new_row["idesc"] = row["idesc"];
            new_row["idate"] = row["idate"];
            new_row["add_time"] = row["add_time"];
            new_row["upd_time"] = row["upd_time"];
            new_row["tab"] = tab;
            new_row["log_type"] = log_type;
            var user = fw.model<Users>().one(users_id);
            new_row["user"] = user;
            if (Utils.f2int(user["att_id"]) > 0)
                new_row["avatar_link"] = fw.model<Att>().getUrl(Utils.f2int(user["att_id"]), "s");
            if (!Utils.isEmpty(row["upd_users_id"]))
                new_row["upd_user"] = fw.model<Users>().one(row["upd_users_id"]);
            if (last_fields != null)
                new_row["fields"] = last_fields;

            result.Add(new_row);
        }

        //and now for each result row with fields - convert fields from Hashtable to ArrayList for ParsePage
        foreach (Hashtable row in result)
        {
            if (!row.ContainsKey("fields"))
                continue;

            var fields = (Hashtable)row["fields"];
            var fields_list = new ArrayList();
            foreach (string key in fields.Keys)
            {
                fields_list.Add(new Hashtable()
                    {
                        {"key",key},
                        {"value",fields[key]}
                    });
            }
            row["fields"] = fields_list;
        }

        return result;
    }

    public long getCountByLogIType(int log_itype, IList statuses = null, int? since_days = null)
    {
        var sql = $@"SELECT count(*) 
                    from {db.qid(table_name)} al 
                        INNER JOIN {fw.model<FwLogTypes>().table_name} lt on (lt.id=al.log_types_id)
                    where lt.itype=@itype
                     and al.status IN (@statuses)
            ";
        var p = new Hashtable()
        {
            {"itype", log_itype},
            {"statuses", statuses}
        };
        if (since_days != null)
        {
            sql += " and al.add_time > DATEADD(day, @since_days, GETDATE())";
            p["since_days"] = since_days;
        }

        return Utils.f2long(db.valuep(sql, p));
    }

    /// ****** helpers to detect changes

    /// <summary>
    /// leave in only those item keys, which are apsent/different from itemold
    /// </summary>
    /// <param name="item"></param>
    /// <param name="itemold"></param>
    public Hashtable changesOnly(Hashtable item, Hashtable itemold)
    {
        Hashtable result = new();
        object datenew;
        object dateold;
        object vnew;
        object vold;
        foreach (var key in item.Keys)
        {
            vnew = item[key];
            vold = itemold[key];

            datenew = Utils.f2date(vnew);
            dateold = Utils.f2date(vold);
            if (datenew != null && dateold != null)
            {
                // it's dates - only compare DATE part, not time as all form inputs are dates without times
                vnew = System.Convert.ToDateTime(datenew).ToShortDateString();
                vold = System.Convert.ToDateTime(dateold).ToShortDateString();
            }

            // If Not itemold.ContainsKey(key) _
            // OrElse vnew Is Nothing AndAlso vold IsNot Nothing _
            // OrElse vnew IsNot Nothing AndAlso vold Is Nothing _
            // OrElse vnew IsNot Nothing AndAlso vold IsNot Nothing _
            // AndAlso vnew.ToString() <> vold.ToString() _
            // Then
            if (!itemold.ContainsKey(key) || Utils.f2str(vnew) != Utils.f2str(vold))
                // logger("****:" & key)
                // logger(TypeName(vnew) & " - " & vnew & " - " & datenew)
                // logger(TypeName(vold) & " - " & vold & " - " & dateold)
                result[key] = item[key];
        }
        return result;
    }

    /// <summary>
    /// return true if any of passed fields changed
    /// </summary>
    /// <param name="item1"></param>
    /// <param name="item2"></param>
    /// <param name="fields">qw-list of fields</param>
    /// <returns>false if no chagnes in passed fields or fields are empty</returns>
    public bool isChanged(Hashtable item1, Hashtable item2, string fields)
    {
        var result = false;
        var afields = Utils.qw(fields);
        foreach (var fld in afields)
        {
            if (item1.ContainsKey(fld) && item2.ContainsKey(fld) && Utils.f2str(item1[fld]) != Utils.f2str(item2[fld]))
            {
                result = true;
                break;
            }
        }

        return result;
    }

    // check if 2 dates (without time) chagned
    public bool isChangedDate(object date1, object date2)
    {
        var dt1 = Utils.f2date(date1);
        var dt2 = Utils.f2date(date2);

        if (dt1 != null || dt2 != null)
        {
            if (dt1 != null && dt2 != null)
            {
                // both set - compare dates
                if (DateUtils.Date2SQL((DateTime)dt1) != DateUtils.Date2SQL((DateTime)dt2))
                    return true;
            }
            else
                // one set, one no - chagned
                return true;
        }
        else
        {
        }

        return false;
    }
}