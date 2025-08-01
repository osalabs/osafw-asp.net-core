// Fw Model base class

// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

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

    protected FW fw;
    protected DB db;
    protected string db_config = ""; // if empty(default) - fw.db used, otherwise - new db connection created based on this config name

    public string table_name = ""; // must be assigned in child class
    protected Hashtable table_schema; // table schema cache (fields, types, etc) - filled on demand
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
    public FwModel junction_model_main;   // main model (first entity), initialize in init(), ex fw.model<Users>()
    public string junction_field_main_id; // id field name for main, ex users_id
    public FwModel junction_model_linked;   // linked model (second entity), initialize in init()
    public string junction_field_linked_id; // id field name for linked, ex companies_id
    public string junction_field_status; // custom junction status field name, using this.field_status if not set

    protected string cache_prefix = "fwmodel.one."; // default cache prefix for caching items
    protected string cache_prefix_byicode = "fwmodel.onebyicode."; // default cache prefix for caching items by icode

    protected FwModel(FW fw = null)
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
        var item = (DBRow)(Hashtable)fw.cache.getRequestValue(cache_key);
        if (item == null)
        {
            Hashtable where = [];
            where[this.field_id] = id;
            item = db.row(table_name, where);
            normalizeNames(item);
            fw.cache.setRequestValue(cache_key, item);
        }
        return item;
    }

    //overload of one() to accept id of any type, so no need to explicitly convert by caller
    public virtual DBRow one(object id)
    {
        var iid = id.toInt();
        if (iid > 0)
            return one(iid);
        else
            return [];
    }

    // return one specific field for the row, uncached
    public virtual object oneField(int id, string field_name)
    {
        Hashtable where = [];
        where[this.field_id] = id;
        return db.value(table_name, where, field_name);
    }

    public virtual DBList multi(ICollection ids)
    {
        if (ids.Count == 0)
            return [];

        object[] arr = new object[ids.Count - 1 + 1];
        ids.CopyTo(arr, 0);
        return db.array(table_name, new Hashtable() { { "id", db.opIN(arr) } });
    }

    // add renamed fields For template engine - spaces and special chars replaced With "_" and other normalizations
    public void normalizeNames(Hashtable row)
    {
        if (!is_normalize_names || row.Count == 0)
            return;

        foreach (string key in new ArrayList(row.Keys)) // static copy of row keys to avoid loop issues
            row[Utils.name2fw(key)] = row[key];

        if (!string.IsNullOrEmpty(field_id) && row[field_id] != null && !row.ContainsKey("id"))
            row["id"] = row[field_id];
    }

    public void normalizeNames(ArrayList rows)
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

        foreach (string key in new ArrayList(row.Keys)) // static copy of row keys to avoid loop issues
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
    public virtual string iname(object id)
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
        Hashtable item = this.oneByIname(iname);
        if (item.ContainsKey(this.field_id))
            // exists
            result = item[this.field_id].toInt();
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
    public virtual DBList list(IList statuses = null)
    {
        Hashtable where = [];
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
    public virtual DBList listByWhere(Hashtable where = null, int limit = -1, int offset = 0, string orderby = "")
    {
        where ??= [];
        return db.array(table_name, where, orderby != "" ? orderby : getOrderBy());
    }

    // return count of all non-deleted or with specified statuses
    public virtual long getCount(IList statuses = null, int? since_days = null)
    {
        Hashtable where = [];
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

        Hashtable where = [];
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
        var item = (DBRow)(Hashtable)fw.cache.getRequestValue(cache_key);
        if (item == null)
        {
            Hashtable where = [];
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
        Hashtable where = [];
        where[field] = uniq_key;
        if (!string.IsNullOrEmpty(field_id))
            where[field_id] = db.opNOT(not_id);
        return db.value(table_name, where, "1").toBool();
    }

    // check if item exists for a given fields and their values, commonly used in junction tables
    public bool isExistsByFields(Hashtable fields, int not_id)
    {
        Hashtable where = [];
        foreach (DictionaryEntry de in fields)
            where[de.Key] = de.Value;

        if (!string.IsNullOrEmpty(field_id))
            where[field_id] = db.opNOT(not_id);
        return db.value(table_name, where, "1").toBool();
    }

    // check if item exists for a given iname
    public virtual bool isExists(object uniq_key, int not_id)
    {
        return isExistsByField(uniq_key, not_id, field_iname);
    }

    // add new record and return new record id
    public virtual int add(Hashtable item)
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
    public virtual bool update(int id, Hashtable item)
    {
        Hashtable item_changes = [];
        if (is_log_changes)
        {
            var item_old = this.one(id);
            Hashtable item_compare = item;
            if (is_under_bulk_update)
            {
                // when under bulk update - as existing items updated from status=1 to 0
                // so we only need to check if other fields changed, not status
                item_compare = (Hashtable)item.Clone();
                item_compare.Remove(field_status);
            }
            item_changes = FormUtils.changesOnly(item_compare, item_old);
        }

        if (!string.IsNullOrEmpty(field_upd_time))
            item[field_upd_time] = DB.NOW;
        if (!string.IsNullOrEmpty(field_upd_users_id) && !item.ContainsKey(field_upd_users_id) && fw.isLogged)
            item[field_upd_users_id] = fw.userId;

        Hashtable where = [];
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
        Hashtable where = [];
        where[this.field_id] = id;

        if (is_perm || string.IsNullOrEmpty(field_status))
        {
            // place here code that remove related data
            db.del(table_name, where);
            this.removeCache(id);
        }
        else
        {
            Hashtable vars = [];
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

    public Hashtable getTableSchema()
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
    // override if id/iname differs in table or to process custom lookup_params and filter_for/filter_field
    // def - in dynamic controller - field definition (also contains "i" and "ps", "lookup_params", ...) or you could use it to pass additional params
    public virtual ArrayList listSelectOptions(Hashtable def = null)
    {
        Hashtable where = [];
        if (!string.IsNullOrEmpty(field_status))
            where[field_status] = db.opNOT(STATUS_DELETED);

        // Support filter_by/filter_field from config
        if (def != null && def.ContainsKey("filter_by") && def.ContainsKey("filter_field"))
        {
            var item = def["i"] as Hashtable ?? [];
            var filter_by = def["filter_by"].toStr();
            var filter_field = def["filter_field"].toStr();
            if (item.ContainsKey(filter_by))
                where[filter_field] = item[filter_by];
        }

        ArrayList select_fields =
        [
            new Hashtable() { { "field", field_id }, { "alias", "id" } },
            new Hashtable() { { "field", field_iname }, { "alias", "iname" } }
        ];
        return db.array(table_name, where, getOrderBy(), select_fields);
    }

    // similar to listSelectOptions but returns iname/iname
    public virtual ArrayList listSelectOptionsName(Hashtable def = null)
    {
        Hashtable where = [];
        if (!string.IsNullOrEmpty(field_status))
            where[field_status] = db.opNOT(STATUS_DELETED);

        ArrayList select_fields =
        [
            new Hashtable() { { "field", field_iname }, { "alias", "id" } },
            new Hashtable() { { "field", field_iname }, { "alias", "iname" } }
        ];
        return db.array(table_name, where, getOrderBy(), select_fields);
    }

    // like listSelectOptions, but for autocomplete by search string q
    public virtual ArrayList listSelectOptionsAutocomplete(string q, Hashtable def = null)
    {
        Hashtable where = [];
        where[field_iname] = db.opLIKE("%" + q + "%");

        if (!string.IsNullOrEmpty(field_status))
            where[field_status] = db.opNOT(STATUS_DELETED);

        ArrayList select_fields =
        [
            new Hashtable() { { "field", field_id }, { "alias", "id" } },
            new Hashtable() { { "field", field_iname }, { "alias", "iname" } }
        ];
        return db.array(table_name, where, getOrderBy(), select_fields);
    }


    [ObsoleteAttribute("This method is deprecated. Use listSelectOptions instead.", true)]
    public virtual string getSelectOptions(string sel_id)
    {
        return FormUtils.selectOptions(this.listSelectOptions(), sel_id);
    }

    public virtual List<string> listAutocomplete(string q, int limit = 5)
    {
        Hashtable where = [];
        where[field_iname] = db.opLIKE("%" + q + "%");
        if (!string.IsNullOrEmpty(field_status))
            where[field_status] = db.opNOT(STATUS_DELETED);
        return db.col(table_name, where, field_iname, field_iname, limit);
    }

    [ObsoleteAttribute("This method is deprecated. Use listAutocomplete instead.", true)]
    public virtual List<string> getAutocompleteList(string q)
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
    public virtual DBList listByMainId(int main_id, Hashtable def = null)
    {
        if (string.IsNullOrEmpty(junction_field_main_id))
            throw new NotImplementedException();
        return db.array(table_name, DB.h(junction_field_main_id, main_id));
    }

    //similar to listByMainId but by linked_id
    public virtual DBList listByLinkedId(int linked_id, Hashtable def = null)
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
    public virtual ArrayList sortByCheckedPrio(ArrayList lookup_rows)
    {
        ArrayList result = [];
        if (!string.IsNullOrEmpty(field_prio))
            result.AddRange((from Hashtable h in lookup_rows
                             orderby ((Hashtable)h["_link"])[field_prio], h["is_checked"] descending
                             select h).ToList());
        else
            result.AddRange((from Hashtable h in lookup_rows
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
    public virtual ArrayList listLinkedByMainId(int main_id, Hashtable def = null)
    {
        var linked_rows = listByMainId(main_id, def);

        ArrayList lookup_rows = junction_model_linked.list();
        if (linked_rows != null && linked_rows.Count > 0)
        {
            foreach (Hashtable row in lookup_rows)
            {
                // check if linked_rows contain main id
                row["is_checked"] = false;
                row["_link"] = new Hashtable();
                foreach (var lrow in linked_rows)
                {
                    // compare LINKED ids
                    if (row[junction_model_linked.field_id].toStr() == lrow[junction_field_linked_id])
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
    public virtual ArrayList listMainByLinkedId(int linked_id, Hashtable def = null)
    {
        var linked_rows = listByLinkedId(linked_id, def);

        ArrayList lookup_rows = junction_model_main.list();
        if (linked_rows != null && linked_rows.Count > 0)
        {
            foreach (Hashtable row in lookup_rows)
            {
                // check if linked_rows contain main id
                row["is_checked"] = false;
                row["_link"] = new Hashtable();
                foreach (var lrow in linked_rows)
                {
                    // compare MAIN ids
                    if (row[junction_model_main.field_id].toStr() == lrow[junction_field_main_id])
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

    protected ArrayList setMultiListChecked(ArrayList rows, List<string> ids, Hashtable def = null)
    {
        var result = rows;

        var is_checked_only = def?["lookup_checked_only"].toBool() ?? false;

        if (ids != null && ids.Count > 0)
        {
            foreach (Hashtable row in rows)
                row["is_checked"] = ids.Contains(row[this.field_id]);

            // now sort so checked values will be at the top - using LINQ
            result = filterAndSortChecked(rows, def);
        }
        else if (is_checked_only)
            // return no items if no checked
            result = [];
        return result;
    }

    protected ArrayList filterAndSortChecked(ArrayList rows, Hashtable def = null)
    {
        var is_checked_only = def?["lookup_checked_only"].toBool() ?? false;
        if (is_checked_only)
        {
            var result = new ArrayList();
            result.AddRange((from Hashtable h in rows
                             where (bool)h["is_checked"]
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
    public virtual ArrayList listWithChecked(List<string> ids, Hashtable def = null)
    {
        var rows = setMultiListChecked(this.list(), ids, def);
        return rows;
    }

    /// <summary>
    /// list rows and add is_checked=True flag for selected ids, sort by is_checked desc
    /// </summary>
    /// <param name="sel_ids">comma-separated selected ids from the list()</param>
    /// <param name="def">def - in dynamic controller - field definition (also contains "i" and "ps", "lookup_params", ...) or you could use it to pass additional params</param>
    /// <returns></returns>
    public virtual ArrayList listWithChecked(string sel_ids, Hashtable def = null)
    {
        List<string> ids = new(sel_ids.Split(","));
        return this.listWithChecked(ids, def);
    }

    /// <summary>
    ///     return array of LINKED ids for the MAIN id in junction table
    /// </summary>
    /// <param name="main_id">main id</param>
    /// <returns></returns>
    public virtual List<string> colLinkedIdsByMainId(int main_id)
    {
        return db.col(table_name, DB.h(junction_field_main_id, main_id), db.qid(junction_field_linked_id));
    }
    /// <summary>
    ///     return array of MAIN ids for the LINKED id in junction table
    /// </summary>
    /// <param name="linked_id">linked id</param>
    /// <returns></returns>
    public virtual List<string> colMainIdsByLinkedId(int linked_id)
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

        var where = new Hashtable()
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

        var where = new Hashtable() { { junction_field_main_id, main_id } };
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
    public virtual void updateJunction(string junction_table_name, int main_id, string main_id_name, string linked_id_name, Hashtable linked_keys)
    {
        Hashtable fields = [];
        Hashtable where = [];
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
    public virtual void updateJunctionByMainIdAdditional(Hashtable linked_keys, string link_id, Hashtable fields)
    {
        if (!string.IsNullOrEmpty(field_prio) && linked_keys.Contains(field_prio + "_" + link_id))
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
    public virtual void updateJunctionByMainId(int main_id, Hashtable linked_keys)
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

                Hashtable fields = [];
                fields[junction_field_main_id] = main_id;
                fields[junction_field_linked_id] = link_id;
                fields[link_table_field_status] = STATUS_ACTIVE;

                // additional fields here
                updateJunctionByMainIdAdditional(linked_keys, link_id, fields);

                Hashtable where = [];
                where[junction_field_main_id] = main_id;
                where[junction_field_linked_id] = link_id;
                db.updateOrInsert(table_name, fields, where);
            }
        }

        // remove those who still not updated (so removed)
        deleteUnderUpdateByMainId(main_id);
    }

    // override to add set more additional fields
    public virtual void updateJunctionByLinkedIdAdditional(Hashtable linked_keys, string main_id, Hashtable fields)
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
    public virtual void updateJunctionByLinkedId(int linked_id, Hashtable main_keys)
    {
        Hashtable fields = [];
        Hashtable where = [];
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
    public virtual void prepareSubtable(ArrayList list_rows, int related_id, Hashtable def = null)
    {
        foreach (Hashtable row in list_rows)
        {
            //if row_id starts with "new-" - set flag is_new
            row["is_new"] = row["id"].ToString().StartsWith("new-");

            // for non-Vue - add def to each row
            if (!fw.isJsonExpected())
            {
                row["def"] = def;
            }
        }
    }

    // override in your specific models when necessary, add defaults for new record
    public virtual void prepareSubtableAddNew(ArrayList list_rows, int related_id, Hashtable def = null)
    {
        var id = "new-" + DateTimeOffset.Now.ToUnixTimeMilliseconds(); //generate unique id based on time for sequental adding
        var item = new Hashtable()
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
    public virtual void filterForJson(Hashtable item)
    {
        //first, remove sensitive fields
        foreach (string fieldname in Utils.qw(json_fields_exclude))
            if (item.ContainsKey(fieldname))
                item.Remove(fieldname);

        //then perform necessary transformations
        var table_schema = getTableSchema();
        //iterate over item.Keys, but make it static array to avoid "collection was modified" error
        var keys = item.Keys.Cast<string>().ToArray();
        foreach (string fieldname in keys)
        {
            var fieldname_lc = fieldname.ToLower();
            if (!table_schema.ContainsKey(fieldname_lc)) continue;

            var field_schema = (Hashtable)table_schema[fieldname_lc];

            var fw_type = (string)field_schema["fw_type"];
            var fw_subtype = (string)field_schema["fw_subtype"];
            if (fw_subtype == "date")
            {
                //if field is exactly DATE - show only date part without time - in YYYY-MM-DD format
                item[fieldname] = DateUtils.Str2SQL((string)item[fieldname]);
            }
            else if (fw_type == "datetime")
            {
                //if field is exactly DATETIME - show in YYYY-MM-DD HH:MM:SS format
                item[fieldname] = DateUtils.Str2SQL((string)item[fieldname], true);
            }
            else if (fw_subtype == "bit")
            {
                //if field is exactly BIT - convert from True/False to 1/0
                item[fieldname] = item[fieldname].toBool() ? 1 : 0;
            }
            // ADD OTHER CONVERSIONS HERE if necessary
        }
    }

    /// <summary>
    /// filter list of items for json output
    /// </summary>
    /// <param name="rows"></param>
    /// <returns></returns>
    public virtual ArrayList filterListForJson(IList rows)
    {
        ArrayList result = [];
        foreach (Hashtable row in rows)
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
    /// <param name="rows">list of Hashtables</param>
    /// <returns></returns>
    public virtual ArrayList filterListOptionsForJson(IList rows)
    {
        ArrayList result = [];
        foreach (Hashtable row in rows)
        {
            Hashtable item = [];
            item[field_id] = row[field_id];
            item[field_iname] = row[field_iname];
            if (row.ContainsKey("is_checked"))
                item["is_checked"] = row["is_checked"];
            if (row.ContainsKey(field_prio))
                item[field_prio] = row[field_prio];
            result.Add(item);
        }
        return result;
    }

    public virtual StringBuilder getCSVExport()
    {
        Hashtable where = [];
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