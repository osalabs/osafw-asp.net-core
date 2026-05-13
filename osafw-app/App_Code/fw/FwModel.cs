// Fw Model base class

// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using Microsoft.AspNetCore.Http;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace osafw;

public abstract class FwModel : IDisposable
{
    public const int STATUS_ACTIVE = 0;
    public const int STATUS_UNDER_UPDATE = 1;
    public const int STATUS_INACTIVE = 10;
    public const int STATUS_DELETED = 127;

    protected FW fw = null!;
    protected DB db = null!;
    protected string db_config = ""; // if empty(default) - fw.db used, otherwise - new db connection created based on this config name

    public string table_name = ""; // must be assigned in child class
    protected FwDict? table_schema; // table schema cache (fields, types, etc) - filled on demand
    public string csv_export_fields = ""; // all or Utils.qw format
    public string csv_export_headers = ""; // comma-separated format

    public string field_id = "id"; // default primary key name
    public string field_iname = "iname";
    public string field_icode = "icode";

    // default field names. If you override it and make empty - automatic processing disabled
    public string field_status = "status";
    public string field_add_users_id = "add_users_id";
    public string field_upd_users_id = "upd_users_id";
    public string field_add_time = "add_time";
    public string field_upd_time = "upd_time";
    public string field_prio = "";
    public bool is_normalize_names = false; // if true - Utils.name2fw() will be called for all fetched rows to normalize names (no spaces or special chars)

    // default list of sensitive fields to filter out from json output
    public string json_fields_exclude = "pwd password pwd_reset mfa_secret mfa_recovery";

    public bool is_log_changes = true; // if true - event_log record added on add/update/delete
    public bool is_log_fields_changed = true; // if true - event_log.fields filled with changes
    public bool is_under_bulk_update = false; // true when perform bulk updates like modelAddOrUpdateSubtableDynamic (disables log changes for status)

    // for junction models like UsersCompanies that link 2 tables via junction table, ex users_companies
    public FwModel? junction_model_main;   // main model (first entity), initialize in init(), ex fw.model<Users>()
    public string junction_field_main_id = string.Empty; // id field name for main, ex users_id
    public FwModel? junction_model_linked;   // linked model (second entity), initialize in init()
    public string junction_field_linked_id = string.Empty; // id field name for linked, ex companies_id
    public string junction_field_status = string.Empty; // custom junction status field name, using this.field_status if not set

    protected string cache_prefix = "fwmodel.one."; // default cache prefix for caching items
    protected string cache_prefix_byicode = "fwmodel.onebyicode."; // default cache prefix for caching items by icode

    protected FwModel(FW? fw = null)
    {
        if (fw != null)
        {
            this.fw = fw;
            this.db = fw.db;
        }

        cache_prefix = cache_prefix + this.GetType().Name + "*"; // setup cache prefix for this model only
    }

    public virtual void init(FW fw)
    {
        this.fw = fw;
        if (!string.IsNullOrEmpty(this.db_config))
        {
            this.db = fw.getDB(this.db_config);
        }
        else
            this.db = fw.db;
    }

    public virtual DB getDB()
    {
        return db;
    }

    /// <summary>
    /// return table name as quoted identifier to use in SQL queries
    /// </summary>
    /// <returns></returns>
    public virtual string qTable()
    {
        return db.qid(this.table_name);
    }

    /// <summary>
    /// standard stub for check access for particular record
    /// </summary>
    /// <param name="id"></param>
    /// <param name="action">specific action code to check like view or edit</param>
    /// <exception cref="NotImplementedException"></exception>
    public virtual bool isAccess(int id = 0, string action = "")
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// shortcut for isAccess with throwing AuthException if no access
    /// </summary>
    /// <param name="id"></param>
    /// <param name="action"></param>
    /// <exception cref="AuthException"></exception>
    public virtual void checkAccess(int id = 0, string action = "")
    {
        if (!isAccess(id, action))
        {
            throw new AuthException();
        }
    }

    #region basic CRUD one, list, multi, add, update, delete and related helpers
    public virtual DBRow one(int id)
    {
        var cache_key = this.cache_prefix + id;
        var itemObj = fw.cache.getRequestValue(cache_key);
        DBRow? item = itemObj is FwDict ht ? (DBRow)ht : null;
        if (item == null)
        {
            FwDict where = [];
            where[this.field_id] = id;
            item = db.row(table_name, where);
            normalizeNames(item);
            fw.cache.setRequestValue(cache_key, item);
        }
        return item ?? [];
    }

    //overload of one() to accept id of any type, so no need to explicitly convert by caller
    public virtual DBRow one(object? id)
    {
        var iid = id.toInt();
        if (iid > 0)
            return one(iid);
        else
            return [];
    }

    // return one specific field for the row, uncached
    public virtual object? oneField(int id, string field_name)
    {
        FwDict where = [];
        where[this.field_id] = id;
        return db.value(table_name, where, field_name);
    }

    public virtual DBList multi(ICollection ids)
    {
        if (ids.Count == 0)
            return [];

        object[] arr = new object[ids.Count - 1 + 1];
        ids.CopyTo(arr, 0);
        return db.array(table_name, new FwDict() { { "id", db.opIN(arr) } });
    }

    // Return the previous or next ID from the ordered ID list, wrapping to the end/start if needed.
    public virtual int getAdjacentId(StrList ids, int current_id, bool is_prev)
    {
        if (ids.Count == 0)
            return 0;

        var current_id_str = current_id.ToString();
        int go_id;

        if (is_prev)
        {
            // Scan backwards to find the current id and return the previous item in the list.
            var index_prev = -1;
            for (var index = ids.Count - 1; index >= 0; index += -1)
            {
                if (ids[index] == current_id_str)
                {
                    index_prev = index - 1;
                    break;
                }
            }

            if (index_prev > -1 && index_prev <= ids.Count - 1)
                go_id = ids[index_prev].toInt();
            else
                go_id = ids[ids.Count - 1].toInt();
        }
        else
        {
            // Scan forwards to find the current id and return the next item in the list.
            var index_next = -1;
            for (var index = 0; index <= ids.Count - 1; index++)
            {
                if (ids[index] == current_id_str)
                {
                    index_next = index + 1;
                    break;
                }
            }

            if (index_next > -1 && index_next <= ids.Count - 1)
                go_id = ids[index_next].toInt();
            else
                go_id = ids[0].toInt();
        }

        return go_id;
    }

    // add renamed fields For template engine - spaces and special chars replaced With "_" and other normalizations
    public void normalizeNames(FwDict row)
    {
        if (!is_normalize_names || row.Count == 0)
            return;

        foreach (string key in new StrList(row.Keys)) // static copy of row keys to avoid loop issues
            row[Utils.name2fw(key)] = row[key];

        if (!string.IsNullOrEmpty(field_id) && row[field_id] != null && !row.ContainsKey("id"))
            row["id"] = row[field_id];
    }

    public void normalizeNames(FwList rows)
    {
        if (!is_normalize_names)
            return;

        foreach (DBRow row in rows)
            normalizeNames(row);
    }

    public void normalizeNames(DBRow row)
    {
        if (!is_normalize_names || row.Count == 0)
            return;

        foreach (string key in new StrList(row.Keys)) // static copy of row keys to avoid loop issues
            row[Utils.name2fw(key)] = row[key];

        if (!string.IsNullOrEmpty(field_id) && row[field_id] != null && !row.ContainsKey("id"))
            row["id"] = row[field_id];
    }

    public void normalizeNames(DBList rows)
    {
        if (!is_normalize_names)
            return;

        foreach (var row in rows)
            normalizeNames(row);
    }

    public virtual string iname(int id)
    {
        if (field_iname == "")
            return "";

        var row = one(id);
        return row[field_iname];
    }
    public virtual string iname(object? id)
    {
        var result = "";
        var iid = id.toInt();
        if (iid > 0)
            result = iname(iid);
        return result;
    }

    //find record by iname, if not exists - add, return id (existing or newly added)
    public virtual int idByInameOrAdd(string iname)
    {
        var row = oneByIname(iname);
        var id = row[field_id].toInt();
        if (id == 0)
            id = add(DB.h(field_iname, iname));
        return id;
    }

    public virtual int findOrAddByIname(string iname, out bool is_added)
    {
        is_added = false;
        iname = iname.Trim();
        if (iname.Length == 0)
            return 0;
        int result;
        FwDict item = this.oneByIname(iname);
        if (item.TryGetValue(this.field_id, out object? value))
            // exists
            result = value.toInt();
        else
        {
            // not exists - add new
            item = [];
            item[field_iname] = iname;
            result = this.add(item);
            is_added = true;
        }
        return result;
    }

    //default order is iname asc
    //or if prio column exists - prio asc, iname asc
    protected virtual string getOrderBy()
    {
        var result = field_iname;
        if (!string.IsNullOrEmpty(field_prio))
            result = db.qid(field_prio) + ", " + db.qid(field_iname);
        return result;
    }

    // return standard list of id,iname for all non-deleted OR wtih specified statuses order by by getOrderBy
    public virtual DBList list(IList? statuses = null)
    {
        FwDict where = [];
        if (!string.IsNullOrEmpty(field_status))
        {
            if (statuses != null && statuses.Count > 0)
                where[field_status] = db.opIN(statuses);
            else
                where[field_status] = db.opNOT(STATUS_DELETED);
        }
        return db.array(table_name, where, getOrderBy());
    }

    /// <summary>
    /// list records by where condition optionally limited and ordered
    /// </summary>
    /// <param name="where"></param>
    /// <param name="limit">TODO</param>
    /// <param name="offset">TODO</param>
    /// <param name="orderby"></param>
    /// <returns></returns>
    public virtual DBList listByWhere(FwDict? where = null, int limit = -1, int offset = 0, string orderby = "")
    {
        where ??= [];
        return db.array(table_name, where, orderby != "" ? orderby : getOrderBy());
    }

    // return count of all non-deleted or with specified statuses
    public virtual long getCount(IList? statuses = null, int? since_days = null)
    {
        FwDict where = [];
        if (!string.IsNullOrEmpty(field_status))
        {
            if (statuses != null && statuses.Count > 0)
                where[field_status] = db.opIN(statuses);
            else
                where[field_status] = db.opNOT(STATUS_DELETED);
        }
        if (!string.IsNullOrEmpty(field_add_time) && since_days != null)
        {
            where[field_add_time] = db.opGT(DateTime.Now.AddDays((int)since_days));
        }
        return db.value(table_name, where, "count(*)").toLong();
    }

    // just return first row by iname field (you may want to make it unique)
    public virtual DBRow oneByIname(string iname)
    {
        if (field_iname == "")
            return [];

        FwDict where = [];
        where[field_iname] = iname;
        var item = db.row(table_name, where);
        normalizeNames(item);
        return item;
    }

    public virtual DBRow oneByIcode(string icode)
    {
        if (field_icode == "")
            return [];

        var cache_key = this.cache_prefix_byicode + icode;
        var itemObj = fw.cache.getRequestValue(cache_key);
        DBRow? item = itemObj is FwDict ht ? (DBRow)ht : null;
        if (item == null)
        {
            FwDict where = [];
            where[field_icode] = icode;
            item = db.row(table_name, where);
            normalizeNames(item);
            fw.cache.setRequestValue(cache_key, item);

            // if found by icode - cache by id too
            if (!string.IsNullOrEmpty(field_id) && item.Count > 0)
                fw.cache.setRequestValue(this.cache_prefix + item[field_id], item);
        }
        return item;
    }

    public virtual DBRow oneByIcodeOrFail(string icode)
    {
        var item = oneByIcode(icode);
        if (item.Count == 0)
            throw new NotFoundException();
        return item;
    }

    // check if item exists for a given field
    public virtual bool isExistsByField(object uniq_key, int not_id, string field)
    {
        FwDict where = [];
        where[field] = uniq_key;
        if (!string.IsNullOrEmpty(field_id))
            where[field_id] = db.opNOT(not_id);
        return db.value(table_name, where, "1").toBool();
    }

    // check if item exists for a given fields and their values, commonly used in junction tables
    public bool isExistsByFields(FwDict fields, int not_id)
    {
        FwDict where = [];
        foreach(var kv in fields)
            where[kv.Key] = kv.Value;

        if (!string.IsNullOrEmpty(field_id))
            where[field_id] = db.opNOT(not_id);
        return db.value(table_name, where, "1").toBool();
    }

    // check if item exists for a given iname
    public virtual bool isExists(object uniq_key, int not_id)
    {
        return isExistsByField(uniq_key, not_id, field_iname);
    }

    // convert user input fields to proper database format before add/update
    // including timezone conversion of datetime values to internal UTC
    public virtual void convertUserInput(FwDict item)
    {
        var tschema = getTableSchema();
        var keys = item.Keys.ToArray();
        foreach (string fieldname in keys)
        {
            var fieldname_lc = fieldname.ToLower();
            if (!tschema.ContainsKey(fieldname_lc)) continue;

            var field_schema = tschema[fieldname_lc] as FwDict ?? [];
            var fw_type = field_schema["fw_type"].toStr();
            //var fw_subtype = field_schema["fw_subtype"].toStr();

            if (fw_type == "date")
            {
                // skip if value is DB.NOW object or DateTime object
                if (item[fieldname] is DateTime || item[fieldname] == DB.NOW)
                    continue;

                //if field is exactly DATE - convert only date part without time - in YYYY-MM-DD format
                item[fieldname] = DateUtils.Str2SQL(item[fieldname].toStr(), fw.userDateFormat);
            }
            else if (fw_type == "datetime")
            {
                if (item[fieldname] is DateTime dt)
                {
                    // normalize DateTime inputs from UI or other sources to UTC
                    if (dt.Kind == DateTimeKind.Unspecified)
                    {
                        var utc = DateUtils.convertTimezone(dt, fw.userTimezone, DateUtils.TZ_UTC);
                        item[fieldname] = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
                    }
                    else if (dt.Kind == DateTimeKind.Local)
                        item[fieldname] = dt.ToUniversalTime();
                    else
                        item[fieldname] = dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                }
                else if (item[fieldname] == DB.NOW)
                {
                    // leave NOW untouched, DB will set current timestamp
                }
                else if (string.IsNullOrEmpty(item[fieldname].toStr()))
                {
                    // empty strings become NULL
                    item[fieldname] = DBNull.Value;
                }
                else
                {
                    // parse strings and convert from user's timezone to UTC
                    var str = item[fieldname].toStr();
                    DateTime? parsed = null;
                    if (DateUtils.isDateSQL(str))
                    {
                        parsed = DateUtils.SQL2Date(str);
                    }
                    else
                    {
                        var format = DateUtils.mapDateFormat(fw.userDateFormat) + " " + DateUtils.mapTimeFormat(fw.userTimeFormat);
                        parsed = str.toDateOrNull(format);
                        if (parsed != null)
                            parsed = DateUtils.convertTimezone((DateTime)parsed, fw.userTimezone, DateUtils.TZ_UTC);
                    }

                    item[fieldname] = parsed == null ? DBNull.Value : DateTime.SpecifyKind((DateTime)parsed, DateTimeKind.Utc);
                }
            }

            // ADD OTHER CONVERSIONS HERE if necessary
        }
    }


    // add new record and return new record id
    public virtual int add(FwDict item)
    {
        // item("add_time") = Now() 'not necessary because add_time field in db should have default value now() or getdate()
        if (!string.IsNullOrEmpty(field_add_users_id) && !item.ContainsKey(field_add_users_id) && fw.isLogged)
            item[field_add_users_id] = fw.userId;
        int id = db.insert(table_name, item);

        if (is_log_changes)
        {
            if (is_log_fields_changed)
                fw.logActivity(FwLogTypes.ICODE_ADDED, table_name, id, "", item);
            else
                fw.logActivity(FwLogTypes.ICODE_ADDED, table_name, id);
        }

        this.removeCache(id);

        if (!string.IsNullOrEmpty(field_prio) && !item.ContainsKey(field_prio))
        {
            //if priority field defined - update it with newly added id to allow proper re/ordering
            db.update(table_name, DB.h(field_prio, id), DB.h(field_id, id));
        }

        return id;
    }

    // update exising record
    public virtual bool update(int id, FwDict item)
    {
        FwDict item_changes = [];
        if (is_log_changes)
        {
            var item_old = this.one(id);
            FwDict item_compare = item;
            if (is_under_bulk_update)
            {
                // when under bulk update - as existing items updated from status=1 to 0
                // so we only need to check if other fields changed, not status
                item_compare = new(item);
                item_compare.Remove(field_status);
            }
            item_changes = FormUtils.changesOnly(item_compare, item_old);
        }

        if (!string.IsNullOrEmpty(field_upd_time))
            item[field_upd_time] = DB.NOW;
        if (!string.IsNullOrEmpty(field_upd_users_id) && !item.ContainsKey(field_upd_users_id) && fw.isLogged)
            item[field_upd_users_id] = fw.userId;

        FwDict where = [];
        where[this.field_id] = id;
        db.update(table_name, item, where);

        this.removeCache(id); // cleanup cache, so next one read will read new value

        if (is_log_changes && item_changes.Count > 0)
        {
            if (is_log_fields_changed)
                fw.logActivity(FwLogTypes.ICODE_UPDATED, table_name, id, "", item_changes);
            else
                fw.logActivity(FwLogTypes.ICODE_UPDATED, table_name, id);
        }

        return true;
    }

    // mark record as deleted (status=127) OR actually delete from db (if is_perm or status field not defined for this model table)
    public virtual void delete(int id, bool is_perm = false)
    {
        FwDict where = [];
        where[this.field_id] = id;

        if (is_perm || string.IsNullOrEmpty(field_status))
        {
            // place here code that remove related data
            db.del(table_name, where);
            this.removeCache(id);
        }
        else
        {
            FwDict vars = [];
            vars[field_status] = STATUS_DELETED;
            if (!string.IsNullOrEmpty(field_upd_time))
                vars[field_upd_time] = DB.NOW;
            if (!string.IsNullOrEmpty(field_upd_users_id) && fw.isLogged)
                vars[field_upd_users_id] = fw.userId;

            db.update(table_name, vars, where);
        }
        if (is_log_changes)
            fw.logActivity(FwLogTypes.ICODE_DELETED, table_name, id);
    }

    public virtual void deleteWithPermanentCheck(int id)
    {
        // if record already deleted and we are admin - perform permanent delete
        if (fw.model<Users>().isAccessLevel(Users.ACL_ADMIN)
            && !string.IsNullOrEmpty(field_status)
            && one(id)[field_status].toInt() == FwModel.STATUS_DELETED)
            delete(id, true);
        else
            delete(id);
    }
    #endregion

    #region cache
    public virtual void removeCache(int id)
    {
        var cache_key = this.cache_prefix + id;
        fw.cache.requestRemove(cache_key);
        //also remove all by icode caches as they may contain this id
        fw.cache.requestRemoveWithPrefix(this.cache_prefix_byicode);
    }

    public virtual void removeCacheAll()
    {
        fw.cache.requestRemoveWithPrefix(this.cache_prefix);
        fw.cache.requestRemoveWithPrefix(this.cache_prefix_byicode);
    }

    public FwDict getTableSchema()
    {
        table_schema ??= db.tableSchemaFull(table_name);
        return table_schema;
    }
    #endregion

    #region upload utils
    public virtual bool uploadFile(int id, out string filepath, string input_name = "file1", bool is_skip_check = false)
    {
        return UploadUtils.uploadFile(fw, table_name, id, out filepath, input_name, is_skip_check);
    }
    public virtual bool uploadFile(int id, out string filepath, int file_index = 0, bool is_skip_check = false)
    {
        return UploadUtils.uploadFile(fw, table_name, id, out filepath, file_index, is_skip_check);
    }
    public virtual bool uploadFile(int id, out string filepath, IFormFile file, bool is_skip_check = false)
    {
        return UploadUtils.uploadFile(fw, table_name, id, out filepath, file, is_skip_check);
    }

    // return upload dir for the module name and id related to FW.config("site_root")/upload
    // id splitted to 1000
    public virtual string getUploadDir(long id)
    {
        return UploadUtils.getUploadDir(fw, table_name, id);
    }

    public virtual string getUploadUrl(long id, string ext, string size = "")
    {
        return UploadUtils.getUploadUrl(fw, table_name, id, ext, size);
    }

    // removes all type of image files uploaded with thumbnails
    public virtual bool removeUpload(long id, string ext)
    {
        string dir = getUploadDir(id);

        if (UploadUtils.isUploadImgExtAllowed(ext))
        {
            // if this is image - remove possibly created thumbs
            File.Delete(dir + "/" + id + "_l" + ext);
            File.Delete(dir + "/" + id + "_m" + ext);
            File.Delete(dir + "/" + id + "_s" + ext);
        }

        // delete main file
        File.Delete(dir + "/" + id + ext);
        return true;
    }

    public virtual string getUploadImgPath(long id, string size, string ext = "")
    {
        return UploadUtils.getUploadImgPath(fw, table_name, id, size, ext);
    }
    #endregion

    #region logger
    // methods from fw - just for a covenience, so no need to use "fw.", as they are used quite frequently
    public void logger(params object[] args)
    {
        fw.logger(args);
    }
    public void logger(LogLevel level, params object[] args)
    {
        fw.logger(level, args);
    }
    #endregion

    #region select options and autocomplete
    /// <summary>
    /// Builds lightweight id/name rows for select controls while keeping inactive records out of
    /// new assignments and preserving the current inactive value on edit forms.
    /// </summary>
    /// <param name="def">Dynamic field definition, including optional `i`, `record_id`, filtering, and lookup flags.</param>
    /// <param name="selected_id">Explicit selected id or ids for hand-written forms and multi-select helpers.</param>
    /// <returns>Rows with `id` and `iname`, plus inactive exception metadata when needed.</returns>
    public virtual FwList listSelectOptions(FwDict? def = null, object? selected_id = null)
    {
        return listSelectOptionsByWhere(def, selected_id);
    }

    /// <summary>
    /// Similar to <see cref="listSelectOptions(FwDict?, object?)"/> but uses the display name as
    /// the option value for legacy controls that store names instead of ids.
    /// </summary>
    /// <param name="def">Dynamic field definition or lookup parameters.</param>
    /// <param name="selected_id">Selected value or values to preserve when the row is inactive.</param>
    /// <returns>Rows whose `id` and `iname` are both based on <see cref="field_iname"/>.</returns>
    public virtual FwList listSelectOptionsName(FwDict? def = null, object? selected_id = null)
    {
        return listSelectOptionsByNameWhere(def, selected_id);
    }

    /// <summary>
    /// Builds autocomplete option rows using the same active-record rule as dropdowns so inactive
    /// records are not offered as new selections.
    /// </summary>
    /// <param name="q">User-entered search prefix or numeric id.</param>
    /// <param name="def">Dynamic field definition or lookup parameters.</param>
    /// <param name="limit">Maximum number of rows to return; values less than one disable limiting.</param>
    /// <param name="selected_id">Selected id or ids allowed as inactive exceptions.</param>
    /// <returns>Autocomplete rows with `id` and `iname`.</returns>
    public virtual FwList listSelectOptionsAutocomplete(string q, FwDict? def = null, int limit = 5, object? selected_id = null)
    {
        var table = db.qid(table_name);
        var qfield_id = db.qid(field_id);
        var qfield_iname = db.qid(field_iname);
        var searchWhere = $"{qfield_iname} LIKE @iname OR {qfield_id} = @id";

        var id = q.toInt();
        FwDict where_params = new()
        {
            { "iname", q + "%" },
            { "id", id },
        };

        var statusWhere = lookupStatusSql(def, selected_id, where_params);
        var where = string.IsNullOrEmpty(statusWhere)
            ? searchWhere
            : $"({searchWhere}) AND ({statusWhere})";

        var sql = $"SELECT {qfield_id} AS id, {qfield_iname} AS iname FROM {table} WHERE {where} ORDER BY {getOrderBy()}";
        if (limit > 0)
            sql = db.limit(sql, limit);
        return markInactiveLookupExceptions(db.arrayp(sql, where_params), listSelectedLookupIds(def, selected_id));
    }

    /// <summary>
    /// Builds select option rows for the current model with an optional base filter. Derived models
    /// use this when they need the standard inactive-record behavior plus model-specific predicates.
    /// </summary>
    /// <param name="def">Dynamic field definition or lookup parameters.</param>
    /// <param name="selected_id">Selected id or ids that should remain available on edit forms.</param>
    /// <param name="valueFromIname">When true, uses <see cref="field_iname"/> as the option value.</param>
    /// <param name="baseWhere">Additional predicates applied before lookup status and dynamic filters.</param>
    /// <returns>Select option rows, active by default plus selected inactive exceptions on edit.</returns>
    protected virtual FwList listSelectOptionsByWhere(FwDict? def = null, object? selected_id = null, bool valueFromIname = false, FwDict? baseWhere = null)
    {
        var selectedIds = listSelectedLookupIds(def, selected_id);
        var includeInactive = isLookupIncludeInactive(def);
        var explicitStatuses = listLookupStatuses(def);
        var hasStatus = !string.IsNullOrEmpty(field_status);

        FwDict where = baseWhere != null ? new FwDict(baseWhere) : [];
        applyLookupFilter(where, def);

        if (hasStatus)
        {
            if (explicitStatuses.Count > 0)
                where[field_status] = db.opIN(explicitStatuses);
            else if (includeInactive)
                where[field_status] = db.opNOT(STATUS_DELETED);
            else
                where[field_status] = STATUS_ACTIVE;
        }

        var selectFields = listSelectOptionFields(valueFromIname);
        FwList rows = db.array(table_name, where, getOrderBy(), selectFields);

        if (!hasStatus || includeInactive || explicitStatuses.Count > 0 || selectedIds.Count == 0)
            return rows;

        FwDict selectedWhere = baseWhere != null ? new FwDict(baseWhere) : [];
        applyLookupFilter(selectedWhere, def);
        selectedWhere[field_status] = db.opNOT(STATUS_DELETED);
        selectedWhere[field_id] = db.opIN(selectedIds);

        FwList selectedRows = db.array(table_name, selectedWhere, getOrderBy(), selectFields);
        markInactiveLookupExceptions(selectedRows, selectedIds);
        return mergeSelectOptionRows(rows, selectedRows);
    }

    /// <summary>
    /// Builds name-valued option rows with the same active-plus-selected inactive rule as id-valued
    /// lookups.
    /// </summary>
    /// <param name="def">Dynamic field definition or lookup parameters.</param>
    /// <param name="selected_id">Selected name or names that should remain available on edit forms.</param>
    /// <param name="baseWhere">Additional predicates applied before lookup status and dynamic filters.</param>
    /// <returns>Name-valued option rows.</returns>
    protected virtual FwList listSelectOptionsByNameWhere(FwDict? def = null, object? selected_id = null, FwDict? baseWhere = null)
    {
        var selectedValues = listSelectedLookupValues(def, selected_id);
        var includeInactive = isLookupIncludeInactive(def);
        var explicitStatuses = listLookupStatuses(def);
        var hasStatus = !string.IsNullOrEmpty(field_status);

        FwDict where = baseWhere != null ? new FwDict(baseWhere) : [];
        applyLookupFilter(where, def);

        if (hasStatus)
        {
            if (explicitStatuses.Count > 0)
                where[field_status] = db.opIN(explicitStatuses);
            else if (includeInactive)
                where[field_status] = db.opNOT(STATUS_DELETED);
            else
                where[field_status] = STATUS_ACTIVE;
        }

        var selectFields = listSelectOptionFields(valueFromIname: true);
        FwList rows = db.array(table_name, where, getOrderBy(), selectFields);

        if (!hasStatus || includeInactive || explicitStatuses.Count > 0 || selectedValues.Count == 0)
            return rows;

        FwDict selectedWhere = baseWhere != null ? new FwDict(baseWhere) : [];
        applyLookupFilter(selectedWhere, def);
        selectedWhere[field_status] = db.opNOT(STATUS_DELETED);
        selectedWhere[field_iname] = db.opIN(selectedValues);

        FwList selectedRows = db.array(table_name, selectedWhere, getOrderBy(), selectFields);
        markInactiveLookupExceptionsByValue(selectedRows, selectedValues);
        return mergeSelectOptionRows(rows, selectedRows);
    }

    /// <summary>
    /// Returns selected lookup ids from an explicit argument, dynamic `selected_id`, or the current
    /// record field value. The ids are used only when the caller has edit context.
    /// </summary>
    /// <param name="def">Dynamic field definition that may contain `i`, `field`, `record_id`, or `selected_id`.</param>
    /// <param name="selected_id">Explicit id, comma-separated ids, or id collection supplied by the caller.</param>
    /// <returns>Distinct positive integer ids to preserve as inactive edit-form exceptions.</returns>
    public virtual IntList listSelectedLookupIds(FwDict? def = null, object? selected_id = null)
    {
        if (!isLookupEditContext(def, selected_id))
            return [];

        object? source = selected_id;
        if (source == null && def != null)
        {
            if (def.TryGetValue("selected_id", out object? defSelected))
                source = defSelected;
            else if (def["i"] is FwDict item)
            {
                var field = def["field"].toStr();
                if (!string.IsNullOrEmpty(field))
                    source = item[field];
            }
        }

        return listPositiveInts(source);
    }

    /// <summary>
    /// Returns selected lookup display values from an explicit argument, dynamic `selected_id`, or
    /// the current record field value.
    /// </summary>
    /// <param name="def">Dynamic field definition that may contain `i`, `field`, `record_id`, or `selected_id`.</param>
    /// <param name="selected_id">Explicit selected value or values supplied by the caller.</param>
    /// <returns>Distinct non-empty selected values.</returns>
    protected virtual StrList listSelectedLookupValues(FwDict? def = null, object? selected_id = null)
    {
        if (!isLookupEditContext(def, selected_id))
            return [];

        object? source = selected_id;
        if (source == null && def != null)
        {
            if (def.TryGetValue("selected_id", out object? defSelected))
                source = defSelected;
            else if (def["i"] is FwDict item)
            {
                var field = def["field"].toStr();
                if (!string.IsNullOrEmpty(field))
                    source = item[field];
            }
        }

        return listNonEmptyStrings(source);
    }

    /// <summary>
    /// Applies `filter_by`/`filter_field` from dynamic config to a lookup query.
    /// </summary>
    /// <param name="where">Mutable where dictionary used by DB helpers.</param>
    /// <param name="def">Dynamic field definition with filter metadata and current item values.</param>
    protected virtual void applyLookupFilter(FwDict where, FwDict? def)
    {
        if (def != null && def.TryGetValue("filter_by", out object? fby) && def.TryGetValue("filter_field", out object? ff))
        {
            var item = def["i"] as FwDict ?? [];
            var filter_by = fby.toStr();
            var filter_field = ff.toStr();
            if (item.TryGetValue(filter_by, out object? value))
                where[filter_field] = value;
        }
    }

    /// <summary>
    /// Builds select-list field definitions for DB array helpers.
    /// </summary>
    /// <param name="valueFromIname">When true, uses the display name as option value.</param>
    /// <returns>Field/alias definitions including status and priority metadata when available.</returns>
    protected virtual FwList listSelectOptionFields(bool valueFromIname = false)
    {
        FwList selectFields =
        [
            new FwDict() { { "field", valueFromIname ? field_iname : field_id }, { "alias", "id" } },
            new FwDict() { { "field", field_iname }, { "alias", "iname" } }
        ];
        if (!string.IsNullOrEmpty(field_status))
            selectFields.Add(new FwDict() { { "field", field_status }, { "alias", field_status } });
        if (!string.IsNullOrEmpty(field_prio))
            selectFields.Add(new FwDict() { { "field", field_prio }, { "alias", field_prio } });
        return selectFields;
    }

    /// <summary>
    /// Determines whether selected ids should be honored. Add-new forms deliberately ignore selected
    /// inactive defaults so duplicate/add screens cannot offer inactive assignments.
    /// </summary>
    /// <param name="def">Dynamic field definition containing edit context.</param>
    /// <param name="selected_id">Explicit selected id argument.</param>
    /// <returns>True when the call represents an edit context or an explicit hand-written selection.</returns>
    protected virtual bool isLookupEditContext(FwDict? def, object? selected_id = null)
    {
        if (def != null)
        {
            if (def.TryGetValue("record_id", out object? recordId))
                return recordId.toInt() > 0;
            if (def["i"] is FwDict item)
                return item["id"].toInt() > 0;
        }
        return def == null && selected_id != null;
    }

    /// <summary>
    /// Checks whether a lookup explicitly asks to include inactive rows.
    /// </summary>
    /// <param name="def">Dynamic field definition or lookup parameters.</param>
    /// <returns>True when all non-deleted rows should be returned.</returns>
    protected virtual bool isLookupIncludeInactive(FwDict? def)
    {
        return def?["lookup_include_inactive"].toBool() == true || def?["show_all"].toBool() == true;
    }

    /// <summary>
    /// Parses explicit lookup statuses from config when a dropdown needs custom status coverage.
    /// </summary>
    /// <param name="def">Dynamic field definition or lookup parameters.</param>
    /// <returns>Status values excluding deleted status.</returns>
    protected virtual IntList listLookupStatuses(FwDict? def)
    {
        var statuses = listInts(def?["lookup_statuses"], allowZero: true);
        statuses.RemoveAll(status => status == STATUS_DELETED);
        return statuses;
    }

    /// <summary>
    /// Builds a SQL status predicate for autocomplete queries.
    /// </summary>
    /// <param name="def">Dynamic field definition or lookup parameters.</param>
    /// <param name="selected_id">Selected id or ids that can be inactive exceptions.</param>
    /// <param name="where_params">Parameter bag to receive status parameters.</param>
    /// <returns>SQL predicate for status filtering, or an empty string when the model has no status field.</returns>
    protected virtual string lookupStatusSql(FwDict? def, object? selected_id, FwDict where_params)
    {
        if (string.IsNullOrEmpty(field_status))
            return "";

        var qfield_status = db.qid(field_status);
        where_params["status_active"] = STATUS_ACTIVE;
        where_params["status_deleted"] = STATUS_DELETED;

        var explicitStatuses = listLookupStatuses(def);
        if (explicitStatuses.Count > 0)
            return $"{qfield_status}{db.insqli(explicitStatuses)}";

        if (isLookupIncludeInactive(def))
            return $"{qfield_status} <> @status_deleted";

        var selectedIds = listSelectedLookupIds(def, selected_id);
        if (selectedIds.Count > 0)
            return $"({qfield_status} = @status_active OR {db.qid(field_id)}{db.insqli(selectedIds)}) AND {qfield_status} <> @status_deleted";

        return $"{qfield_status} = @status_active";
    }

    /// <summary>
    /// Labels selected inactive exception rows so users can see why the option is retained.
    /// </summary>
    /// <param name="rows">Lookup rows to mutate in place.</param>
    /// <param name="selectedIds">Selected ids that are allowed inactive exceptions.</param>
    /// <returns>The same row list for fluent use.</returns>
    protected virtual FwList markInactiveLookupExceptions(FwList rows, IntList selectedIds)
    {
        if (string.IsNullOrEmpty(field_status) || selectedIds.Count == 0)
            return rows;

        foreach (FwDict row in rows)
        {
            if (!selectedIds.Contains(row["id"].toInt()))
                continue;

            var status = row[field_status].toInt();
            if (status == STATUS_ACTIVE || status == STATUS_DELETED)
                continue;

            var name = row["iname"].toStr();
            if (!name.EndsWith(" (inactive)", StringComparison.Ordinal))
                row["iname"] = name + " (inactive)";
            row["class"] = "text-muted";
        }
        return rows;
    }

    /// <summary>
    /// Labels selected inactive exception rows for controls whose option value is the display name.
    /// </summary>
    /// <param name="rows">Lookup rows to mutate in place.</param>
    /// <param name="selectedValues">Selected values that are allowed inactive exceptions.</param>
    /// <returns>The same row list for fluent use.</returns>
    protected virtual FwList markInactiveLookupExceptionsByValue(FwList rows, StrList selectedValues)
    {
        if (string.IsNullOrEmpty(field_status) || selectedValues.Count == 0)
            return rows;

        var selected = new HashSet<string>(selectedValues, StringComparer.Ordinal);
        foreach (FwDict row in rows)
        {
            if (!selected.Contains(row["id"].toStr()))
                continue;

            var status = row[field_status].toInt();
            if (status == STATUS_ACTIVE || status == STATUS_DELETED)
                continue;

            var name = row["iname"].toStr();
            if (!name.EndsWith(" (inactive)", StringComparison.Ordinal))
                row["iname"] = name + " (inactive)";
            row["class"] = "text-muted";
        }
        return rows;
    }

    /// <summary>
    /// Merges active lookup rows with selected exception rows while avoiding duplicate option values.
    /// </summary>
    /// <param name="rows">Active option rows.</param>
    /// <param name="selectedRows">Selected non-deleted exception rows.</param>
    /// <returns>Merged option list.</returns>
    protected virtual FwList mergeSelectOptionRows(FwList rows, FwList selectedRows)
    {
        var result = new FwList(rows);
        var seen = new HashSet<string>(rows.Select(row => row["id"].toStr()), StringComparer.Ordinal);
        foreach (FwDict row in selectedRows)
        {
            var key = row["id"].toStr();
            if (seen.Add(key))
                result.Add(row);
        }
        return result;
    }

    /// <summary>
    /// Normalizes a loose id source into distinct positive integers.
    /// </summary>
    /// <param name="value">Integer, string, comma-separated string, dictionary, or enumerable of ids.</param>
    /// <returns>Distinct positive ids.</returns>
    protected virtual IntList listPositiveInts(object? value)
    {
        return listInts(value, allowZero: false);
    }

    /// <summary>
    /// Normalizes a loose integer source into distinct integers.
    /// </summary>
    /// <param name="value">Integer, string, comma-separated string, dictionary, or enumerable of values.</param>
    /// <param name="allowZero">Whether zero is retained in the result.</param>
    /// <returns>Distinct integers in first-seen order.</returns>
    protected virtual IntList listInts(object? value, bool allowZero)
    {
        var seen = new HashSet<int>();
        var result = new IntList();
        addIntsFromObject(value, allowZero, seen, result);
        return result;
    }

    /// <summary>
    /// Adds integers from a loose source to an existing distinct result set.
    /// </summary>
    /// <param name="value">Value to parse.</param>
    /// <param name="allowZero">Whether zero is retained in the result.</param>
    /// <param name="seen">Set used to prevent duplicates.</param>
    /// <param name="result">Output list preserving first-seen order.</param>
    protected virtual void addIntsFromObject(object? value, bool allowZero, HashSet<int> seen, IntList result)
    {
        if (value == null)
            return;

        if (value is FwDict dict)
        {
            foreach (var key in dict.Keys)
            {
                if (dict[key].toBool())
                    addIntValue(key, allowZero, seen, result);
            }
            return;
        }

        if (value is IDictionary legacyDict)
        {
            foreach (DictionaryEntry entry in legacyDict)
            {
                if (entry.Value.toBool())
                    addIntValue(entry.Key, allowZero, seen, result);
            }
            return;
        }

        if (value is string str)
        {
            foreach (var part in str.Replace(';', ',').Replace('|', ',').Split([',', ' '], StringSplitOptions.RemoveEmptyEntries))
                addIntValue(part, allowZero, seen, result);
            return;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
                addIntValue(item, allowZero, seen, result);
            return;
        }

        addIntValue(value, allowZero, seen, result);
    }

    /// <summary>
    /// Adds one integer value to an existing distinct result set.
    /// </summary>
    /// <param name="value">Value to parse as an integer.</param>
    /// <param name="allowZero">Whether zero is retained in the result.</param>
    /// <param name="seen">Set used to prevent duplicates.</param>
    /// <param name="result">Output list preserving first-seen order.</param>
    protected virtual void addIntValue(object? value, bool allowZero, HashSet<int> seen, IntList result)
    {
        var id = value.toInt();
        if (id < 0 || (!allowZero && id == 0))
            return;
        if (seen.Add(id))
            result.Add(id);
    }

    /// <summary>
    /// Normalizes a loose string source into distinct non-empty values.
    /// </summary>
    /// <param name="value">String, dictionary, or enumerable of string-like values.</param>
    /// <returns>Distinct non-empty strings in first-seen order.</returns>
    protected virtual StrList listNonEmptyStrings(object? value)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new StrList();
        addStringsFromObject(value, seen, result);
        return result;
    }

    /// <summary>
    /// Adds strings from a loose source to an existing distinct result set.
    /// </summary>
    /// <param name="value">Value to parse.</param>
    /// <param name="seen">Set used to prevent duplicates.</param>
    /// <param name="result">Output list preserving first-seen order.</param>
    protected virtual void addStringsFromObject(object? value, HashSet<string> seen, StrList result)
    {
        if (value == null)
            return;

        if (value is FwDict dict)
        {
            foreach (var key in dict.Keys)
            {
                if (dict[key].toBool())
                    addStringValue(key, seen, result);
            }
            return;
        }

        if (value is IDictionary legacyDict)
        {
            foreach (DictionaryEntry entry in legacyDict)
            {
                if (entry.Value.toBool())
                    addStringValue(entry.Key, seen, result);
            }
            return;
        }

        if (value is string)
        {
            addStringValue(value, seen, result);
            return;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
                addStringValue(item, seen, result);
            return;
        }

        addStringValue(value, seen, result);
    }

    /// <summary>
    /// Adds one string value to an existing distinct result set.
    /// </summary>
    /// <param name="value">Value to parse as a string.</param>
    /// <param name="seen">Set used to prevent duplicates.</param>
    /// <param name="result">Output list preserving first-seen order.</param>
    protected virtual void addStringValue(object? value, HashSet<string> seen, StrList result)
    {
        var text = value.toStr().Trim();
        if (string.IsNullOrEmpty(text))
            return;
        if (seen.Add(text))
            result.Add(text);
    }


    [ObsoleteAttribute("This method is deprecated. Use listSelectOptions instead.", true)]
    public virtual string getSelectOptions(string sel_id)
    {
        return FormUtils.selectOptions(this.listSelectOptions(null, sel_id), sel_id);
    }

    /// <summary>
    /// Returns active autocomplete labels for free-text lookup widgets.
    /// </summary>
    /// <param name="q">Search text matched against <see cref="field_iname"/>.</param>
    /// <param name="limit">Maximum number of labels to return.</param>
    /// <returns>Matching active labels.</returns>
    public virtual StrList listAutocomplete(string q, int limit = 5)
    {
        FwDict where = [];
        where[field_iname] = db.opLIKE("%" + q + "%");
        if (!string.IsNullOrEmpty(field_status))
            where[field_status] = STATUS_ACTIVE;
        return db.col(table_name, where, field_iname, field_iname, limit);
    }

    [ObsoleteAttribute("This method is deprecated. Use listAutocomplete instead.", true)]
    public virtual StrList getAutocompleteList(string q)
    {
        return listAutocomplete(q, 5);
    }

    #endregion

    #region support for junction models/tables
    // override in your specific models when necessary

    /// <summary>
    /// list records from junction table by main_id
    /// </summary>
    /// <param name="main_id"></param>
    /// <param name="def"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public virtual DBList listByMainId(int main_id, FwDict? def = null)
    {
        if (string.IsNullOrEmpty(junction_field_main_id))
            throw new NotImplementedException();
        return db.array(table_name, DB.h(junction_field_main_id, main_id));
    }

    //similar to listByMainId but by linked_id
    public virtual DBList listByLinkedId(int linked_id, FwDict? def = null)
    {
        if (string.IsNullOrEmpty(junction_field_linked_id))
            throw new NotImplementedException();
        return db.array(table_name, DB.h(junction_field_linked_id, linked_id));
    }

    /// <summary>
    /// sort lookup rows so checked values will be at the top (is_checked desc)
    ///   AND then by [_link]prio field (if junction table has any) - using LINQ
    /// </summary>
    /// <returns></returns>
    public virtual FwList sortByCheckedPrio(FwList lookup_rows)
    {
        FwList result = [];
        if (!string.IsNullOrEmpty(field_prio))
            result.AddRange((from FwDict h in lookup_rows
                             orderby (h?["_link"] as FwDict)?[field_prio] ?? "", h["is_checked"] descending
                             select h).ToList());
        else
            result.AddRange((from FwDict h in lookup_rows
                             orderby h["is_checked"] descending
                             select h).ToList());
        return result;
    }

    /// <summary>
    /// list LINKED (from junction_model_linked model) records by main id
    /// called from withing junction model like UsersCompanies that links 2 tables
    /// </summary>
    /// <param name="id">main table id</param>
    /// <param name="def">in dynamic controller - field definition (also contains "i" and "ps", "lookup_params", ...) or you could use it to pass additional params</param>
    /// <returns></returns>
    public virtual FwList listLinkedByMainId(int main_id, FwDict? def = null)
    {
        if (junction_model_linked == null)
            throw new ApplicationException("junction_model_linked not defined in model " + this.GetType().Name);            

        var linked_rows = listByMainId(main_id, def);
        var selectedIds = new StrList();
        foreach (var lrow in linked_rows)
            selectedIds.Add(lrow[junction_field_linked_id].toStr());

        FwList lookup_rows = junction_model_linked.listSelectOptions(def, selectedIds);
        if (linked_rows != null)
        {
            foreach (FwDict row in lookup_rows)
            {
                // check if linked_rows contain main id
                row["is_checked"] = false;
                row["_link"] = new FwDict();
                foreach (var lrow in linked_rows)
                {
                    // compare LINKED ids
                    var rowId = row["id"].toStr(row[junction_model_linked.field_id].toStr());
                    if (rowId == lrow[junction_field_linked_id].toStr())
                    {
                        row["is_checked"] = true;
                        row["_link"] = lrow;
                        break;
                    }
                }
            }

            lookup_rows = filterAndSortChecked(lookup_rows, def);
        }
        return lookup_rows;
    }

    /// <summary>
    /// list MAIN (from junction_model_main model) records by linked id
    /// called from withing junction model like UsersCompanies that links 2 tables
    /// </summary>
    /// <param name="linked_id">linked table id</param>
    /// <param name="def">in dynamic controller - field definition (also contains "i" and "ps", "lookup_params", ...) or you could use it to pass additional params</param>
    /// <returns></returns>
    public virtual FwList listMainByLinkedId(int linked_id, FwDict? def = null)
    {
        if (junction_model_main == null)
            throw new ApplicationException("junction_model_main not defined in model " + this.GetType().Name);

        var linked_rows = listByLinkedId(linked_id, def);
        var selectedIds = new StrList();
        foreach (var lrow in linked_rows)
            selectedIds.Add(lrow[junction_field_main_id].toStr());

        FwList lookup_rows = junction_model_main.listSelectOptions(def, selectedIds);
        if (linked_rows != null)
        {
            foreach (FwDict row in lookup_rows)
            {
                // check if linked_rows contain main id
                row["is_checked"] = false;
                row["_link"] = new FwDict();
                foreach (var lrow in linked_rows)
                {
                    // compare MAIN ids
                    var rowId = row["id"].toStr(row[junction_model_main.field_id].toStr());
                    if (rowId == lrow[junction_field_main_id].toStr())
                    {
                        row["is_checked"] = true;
                        row["_link"] = lrow;
                        break;
                    }
                }
            }

            lookup_rows = filterAndSortChecked(lookup_rows, def);
        }
        return lookup_rows;
    }

    protected FwList setMultiListChecked(FwList rows, IList<string>? ids, FwDict? def = null)
    {
        var result = rows;

        var is_checked_only = def?["lookup_checked_only"].toBool() ?? false;

        if (ids != null && ids.Count > 0)
        {
            foreach (FwDict row in rows)
                row["is_checked"] = ids.Contains(row["id"].toStr(row[this.field_id].toStr()));

            // now sort so checked values will be at the top - using LINQ
            result = filterAndSortChecked(rows, def);
        }
        else if (is_checked_only)
            // return no items if no checked
            result = [];
        return result;
    }

    protected FwList filterAndSortChecked(FwList rows, FwDict? def = null)
    {
        var is_checked_only = def?["lookup_checked_only"].toBool() ?? false;
        if (is_checked_only)
        {
            var result = new FwList();
            result.AddRange((from FwDict h in rows
                             where h["is_checked"].toBool()
                             select h).ToList());
            return result;
        }
        else
            return sortByCheckedPrio(rows);

    }

    /// <summary>
    /// list rows and add is_checked=True flag for selected ids, sort by is_checked desc
    /// </summary>
    /// <param name="ids">selected ids from the list()</param>
    /// <param name="def">def - in dynamic controller - field definition (also contains "i" and "ps", "lookup_params", ...) or you could use it to pass additional params</param>
    /// <returns></returns>
    public virtual FwList listWithChecked(StrList? ids, FwDict? def = null)
    {
        var rows = setMultiListChecked(this.listSelectOptions(def, ids), ids, def);
        return rows;
    }

    /// <summary>
    /// list rows and add is_checked=True flag for selected ids, sort by is_checked desc
    /// </summary>
    /// <param name="sel_ids">comma-separated selected ids from the list()</param>
    /// <param name="def">def - in dynamic controller - field definition (also contains "i" and "ps", "lookup_params", ...) or you could use it to pass additional params</param>
    /// <returns></returns>
    public virtual FwList listWithChecked(string sel_ids, FwDict? def = null)
    {
        StrList ids = Utils.isEmpty(sel_ids) ? [] : new(sel_ids.Split(","));
        return this.listWithChecked(ids, def);
    }

    /// <summary>
    ///     return array of LINKED ids for the MAIN id in junction table
    /// </summary>
    /// <param name="main_id">main id</param>
    /// <returns></returns>
    public virtual StrList colLinkedIdsByMainId(int main_id)
    {
        return db.col(table_name, DB.h(junction_field_main_id, main_id), db.qid(junction_field_linked_id));
    }
    /// <summary>
    ///     return array of MAIN ids for the LINKED id in junction table
    /// </summary>
    /// <param name="linked_id">linked id</param>
    /// <returns></returns>
    public virtual StrList colMainIdsByLinkedId(int linked_id)
    {
        return db.col(table_name, DB.h(junction_field_linked_id, linked_id), db.qid(junction_field_main_id));
    }

    public virtual string getJunctionFieldStatus()
    {
        return !string.IsNullOrEmpty(junction_field_status) ? junction_field_status : field_status;
    }

    public virtual void setUnderUpdateByMainId(int main_id)
    {
        var junction_field_status = getJunctionFieldStatus();

        if (string.IsNullOrEmpty(junction_field_status) || string.IsNullOrEmpty(junction_field_main_id)) return; //if no status or linked field - do nothing

        is_under_bulk_update = true;

        db.update(table_name, DB.h(junction_field_status, STATUS_UNDER_UPDATE), DB.h(junction_field_main_id, main_id));
    }

    public virtual void deleteUnderUpdateByMainId(int main_id)
    {
        var junction_field_status = getJunctionFieldStatus();

        if (string.IsNullOrEmpty(junction_field_status) || string.IsNullOrEmpty(junction_field_main_id)) return; //if no status or linked field - do nothing

        var where = new FwDict()
        {
            {junction_field_main_id, main_id},
            {junction_field_status, STATUS_UNDER_UPDATE},
        };
        db.del(table_name, where);

        is_under_bulk_update = false;
    }

    // used when the main record must be permanently deleted
    public virtual void deleteByMainId(int main_id)
    {
        if (string.IsNullOrEmpty(junction_field_main_id)) return; //if no linked field - do nothing

        var where = new FwDict() { { junction_field_main_id, main_id } };
        db.del(table_name, where);
    }

    /// <summary>
    ///  generic update (and add/del) for junction table
    /// </summary>
    /// <param name="junction_table_name">junction table name that contains id_name and link_id_name fields</param>
    /// <param name="main_id">main id</param>
    /// <param name="main_id_name">field name for main id</param>
    /// <param name="linked_id_name">field name for linked id</param>
    /// <param name="linked_keys">hashtable with keys as link id (as passed from web)</param>
    public virtual void updateJunction(string junction_table_name, int main_id, string main_id_name, string linked_id_name, FwDict linked_keys)
    {
        FwDict fields = [];
        FwDict where = [];
        var link_table_field_status = getJunctionFieldStatus();

        // set all fields as under update
        fields[link_table_field_status] = STATUS_UNDER_UPDATE;
        where[main_id_name] = main_id;
        db.update(junction_table_name, fields, where);

        if (linked_keys != null)
        {
            foreach (string linked_id in linked_keys.Keys)
            {
                if (!linked_keys[linked_id].toBool())
                    continue;

                fields = [];
                fields[main_id_name] = main_id;
                fields[linked_id_name] = linked_id;
                fields[link_table_field_status] = STATUS_ACTIVE;

                where = [];
                where[main_id_name] = main_id;
                where[linked_id_name] = linked_id;
                db.updateOrInsert(junction_table_name, fields, where);
            }
        }

        // remove those who still not updated (so removed)
        where = [];
        where[main_id_name] = main_id;
        where[link_table_field_status] = STATUS_UNDER_UPDATE;
        db.del(junction_table_name, where);
    }

    // override to add set more additional fields
    public virtual void updateJunctionByMainIdAdditional(FwDict linked_keys, string link_id, FwDict fields)
    {
        if (!string.IsNullOrEmpty(field_prio) && linked_keys.ContainsKey(field_prio + "_" + link_id))
            fields[field_prio] = linked_keys[field_prio + "_" + link_id].toInt();// get value from prio_ID
    }

    /// <summary>
    /// updates junction table by MAIN id and linked keys (existing in db, but not present keys will be removed)
    /// called from withing junction model like UsersCompanies that links 2 tables
    /// usage example: fw.model<UsersCompanies>().updateJunctionByMainId(id, reqh("companies"));
    /// html: <input type="checkbox" name="companies[123]" value="1" checked>
    /// </summary>
    /// <param name="main_id">main id</param>
    /// <param name="linked_keys">hashtable with keys as linked_id (as passed from web)</param>
    public virtual void updateJunctionByMainId(int main_id, FwDict linked_keys)
    {
        var link_table_field_status = getJunctionFieldStatus();

        // set all rows as under update
        setUnderUpdateByMainId(main_id);

        if (linked_keys != null)
        {
            foreach (string link_id in linked_keys.Keys)
            {
                if (link_id.toInt() == 0 || !linked_keys[link_id].toBool())
                    continue; // skip non-id, ex prio_ID

                FwDict fields = [];
                fields[junction_field_main_id] = main_id;
                fields[junction_field_linked_id] = link_id;
                fields[link_table_field_status] = STATUS_ACTIVE;

                // additional fields here
                updateJunctionByMainIdAdditional(linked_keys, link_id, fields);

                FwDict where = [];
                where[junction_field_main_id] = main_id;
                where[junction_field_linked_id] = link_id;
                db.updateOrInsert(table_name, fields, where);
            }
        }

        // remove those who still not updated (so removed)
        deleteUnderUpdateByMainId(main_id);
    }

    // override to add set more additional fields
    public virtual void updateJunctionByLinkedIdAdditional(FwDict linked_keys, string main_id, FwDict fields)
    {
        if (string.IsNullOrEmpty(field_prio) && linked_keys.ContainsKey(field_prio + "_" + main_id))
            fields[field_prio] = linked_keys[field_prio + "_" + main_id].toInt();// get value from prio_ID
    }

    /// <summary>
    /// updates junction table by LINKED id and linked keys (existing in db, but not present keys will be removed)
    /// called from withing junction model like UsersCompanies that links 2 tables
    /// usage example: fw.model<UsersCompanies>().updateJunctionByLinkedId(id, reqh("users"));
    /// html: <input type="checkbox" name="users[123]" value="1" checked>
    /// </summary>
    /// <param name="linked_id">linked id</param>
    /// <param name="main_keys">hashtable with keys as main_id (as passed from web)</param>
    public virtual void updateJunctionByLinkedId(int linked_id, FwDict main_keys)
    {
        FwDict fields = [];
        FwDict where = [];
        var link_table_field_status = getJunctionFieldStatus();

        // set all fields as under update
        fields[link_table_field_status] = STATUS_UNDER_UPDATE;
        where[junction_field_linked_id] = linked_id;
        db.update(table_name, fields, where);

        if (main_keys != null)
        {
            foreach (string main_id in main_keys.Keys)
            {
                if (main_id.toInt() == 0 || !main_keys[main_id].toBool())
                    continue; // skip non-id, ex prio_ID

                fields = [];
                fields[junction_field_linked_id] = linked_id;
                fields[junction_field_main_id] = main_id;
                fields[link_table_field_status] = STATUS_ACTIVE;

                // additional fields here
                updateJunctionByLinkedIdAdditional(main_keys, main_id, fields);

                where = [];
                where[junction_field_linked_id] = linked_id;
                where[junction_field_main_id] = main_id;
                //logger(fields);
                db.updateOrInsert(table_name, fields, where);
            }
        }

        // remove those who still not updated (so removed)
        where = [];
        where[junction_field_linked_id] = linked_id;
        where[link_table_field_status] = STATUS_UNDER_UPDATE;
        db.del(table_name, where);
    }
    #endregion

    #region dynamic subtable component
    // override in your specific models when necessary
    public virtual void prepareSubtable(FwList list_rows, int related_id, FwDict? def = null)
    {
        foreach (FwDict row in list_rows)
        {
            //if row_id starts with "new-" - set flag is_new
            row["is_new"] = row["id"].toStr().StartsWith("new-");

            // for non-Vue - add def to each row
            if (!fw.isJsonExpected())
            {
                row["def"] = def;
            }
        }
    }

    // override in your specific models when necessary, add defaults for new record
    public virtual void prepareSubtableAddNew(FwList list_rows, int related_id, FwDict? def = null)
    {
        var id = "new-" + DateTimeOffset.Now.ToUnixTimeMilliseconds(); //generate unique id based on time for sequental adding
        var item = new FwDict()
        {
            { "id", id }
        };
        list_rows.Add(item);
    }
    #endregion

    #region support for sortable records
    public int updatePrioRange(int inc_value, int from_prio, int to_prio)
    {
        var field_prioq = db.qid(field_prio);
        var p = DB.h("inc_value", inc_value, "from_prio", from_prio, "to_prio", to_prio);
        return db.exec("UPDATE " + db.qid(table_name) +
            " SET " + field_prioq + "=" + field_prioq + "+(@inc_value)" +
            " WHERE " + field_prioq + " BETWEEN @from_prio AND @to_prio", p);
    }

    public int updatePrio(int id, int prio)
    {
        return db.update(table_name, DB.h(field_prio, prio), DB.h(field_id, id));
    }

    // reorder prio column
    public bool reorderPrio(string sortdir, int id, int under_id, int above_id)
    {
        if (sortdir != "asc" && sortdir != "desc")
            throw new ApplicationException("Wrong sort directrion");

        if (string.IsNullOrEmpty(field_prio))
            return false;

        int id_prio = one(id)[field_prio].toInt();

        // detect reorder
        if (under_id > 0)
        {
            // under id present
            int under_prio = one(under_id)[field_prio].toInt();
            if (sortdir == "asc")
            {
                if (id_prio < under_prio)
                {
                    // if my prio less than under_prio - make all records between old prio and under_prio as -1
                    updatePrioRange(-1, id_prio, under_prio);
                    // and set new id prio as under_prio
                    updatePrio(id, under_prio);
                }
                else
                {
                    // if my prio more than under_prio - make all records between old prio and under_prio as +1
                    updatePrioRange(+1, (under_prio + 1), id_prio);
                    // and set new id prio as under_prio+1
                    updatePrio(id, under_prio + 1);
                }
            }
            else
                // desc
                if (id_prio < under_prio)
            {
                // if my prio less than under_prio - make all records between old prio and under_prio-1 as -1
                updatePrioRange(-1, id_prio, under_prio - 1);
                // and set new id prio as under_prio-1
                updatePrio(id, under_prio - 1);
            }
            else
            {
                // if my prio more than under_prio - make all records between under_prio and old prio as +1
                updatePrioRange(+1, under_prio, id_prio);
                // and set new id prio as under_prio
                updatePrio(id, under_prio);
            }
        }
        else if (above_id > 0)
        {
            // above id present
            int above_prio = one(above_id)[field_prio].toInt();
            if (sortdir == "asc")
            {
                if (id_prio < above_prio)
                {
                    // if my prio less than under_prio - make all records between old prio and above_prio-1 as -1
                    updatePrioRange(-1, id_prio, above_prio - 1);
                    // and set new id prio as under_prio
                    updatePrio(id, above_prio - 1);
                }
                else
                {
                    // if my prio more than under_prio - make all records between above_prio and old prio as +1
                    updatePrioRange(+1, above_prio, id_prio);
                    // and set new id prio as under_prio+1
                    updatePrio(id, above_prio);
                }
            }
            else
                // desc
                if (id_prio < above_prio)
            {
                // if my prio less than under_prio - make all records between old prio and above_prio as -1
                updatePrioRange(-1, id_prio, above_prio);
                // and set new id prio as above_prio
                updatePrio(id, above_prio);
            }
            else
            {
                // if my prio more than under_prio - make all records between above_prio+1 and old prio as +1
                updatePrioRange(+1, above_prio + 1, id_prio);
                // and set new id prio as under_prio+1
                updatePrio(id, above_prio + 1);
            }
        }
        else
            // bad reorder call - ignore
            return false;

        return true;
    }
    #endregion


    #region frontend(json) output support and export
    /// <summary>
    /// to filter item for json output - remove sensitive fields, add calculated fields, etc
    /// </summary>
    /// <param name="item"></param>
    public virtual void filterForJson(FwDict item)
    {
        //first, remove sensitive fields
        foreach (string fieldname in Utils.qw(json_fields_exclude))
            if (item.ContainsKey(fieldname))
                item.Remove(fieldname);

        //then perform necessary transformations
        var table_schema = getTableSchema();
        //iterate over item.Keys, but make it static array to avoid "collection was modified" error
        var keys = item.Keys.ToArray();
        foreach (string fieldname in keys)
        {
            var fieldname_lc = fieldname.ToLower();
            if (!table_schema.ContainsKey(fieldname_lc)) continue;

            var field_schema = table_schema[fieldname_lc] as FwDict ?? [];

            var fw_type = field_schema["fw_type"].toStr();
            var fw_subtype = field_schema["fw_subtype"].toStr();

            if (fw_subtype == "bit")
            {
                //if field is exactly BIT - convert from True/False to 1/0
                item[fieldname] = item[fieldname].toBool() ? 1 : 0;
            }
            else if (fw_type == "date")
            {
                DateTime? dt = item[fieldname] switch
                {
                    DateTime d => d,
                    _ => DateUtils.SQL2Date(item[fieldname].toStr()),
                };
                item[fieldname] = dt == null ? "" : DateUtils.Date2SQL((DateTime)dt);
            }
            else if (fw_type == "datetime")
            {
                item[fieldname] = fw.formatUserDateTime(item[fieldname], true); //ISO format
            }
            // ADD OTHER CONVERSIONS HERE if necessary
        }
    }

    /// <summary>
    /// filter list of items for json output
    /// </summary>
    /// <param name="rows"></param>
    /// <returns></returns>
    public virtual FwList filterListForJson(IList rows)
    {
        FwList result = [];
        foreach (FwDict row in rows)
        {
            filterForJson(row);
            result.Add(row);
        }
        return result;
    }

    /// <summary>
    /// filter list of items for json output for list options, leave only keys:
    ///   id, iname, is_checked (if exists), prio (if exists)
    /// </summary>
    /// <param name="rows">list of <see cref="FwDict"/> entries</param>
    /// <returns></returns>
    public virtual FwList filterListOptionsForJson(IList rows)
    {
        FwList result = [];
        foreach (FwDict row in rows)
        {
            FwDict item = [];
            item["id"] = row["id"] ?? row[field_id];
            item["iname"] = row["iname"] ?? row[field_iname];
            if (row.TryGetValue("is_checked", out object? ic))
                item["is_checked"] = ic;
            if (row.TryGetValue("class", out object? classValue))
                item["class"] = classValue;
            if (row.TryGetValue(field_prio, out object? fp))
                item[field_prio] = fp;
            result.Add(item);
        }
        return result;
    }

    public virtual StringBuilder getCSVExport()
    {
        FwDict where = [];
        if (!string.IsNullOrEmpty(field_status))
            where[field_status] = STATUS_ACTIVE;

        string[] aselect_fields = [];
        if (!string.IsNullOrEmpty(csv_export_fields))
            aselect_fields = Utils.qw(csv_export_fields);

        var rows = db.array(table_name, where, "", aselect_fields);
        return Utils.getCSVExport(csv_export_headers, csv_export_fields, rows);
    }
    #endregion

    public void Dispose()
    {
        fw.Dispose();
        GC.SuppressFinalize(this);
    }
}
