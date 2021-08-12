// Fw Model base class

// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualBasic;

namespace osafw
{
    public abstract class FwModel : IDisposable
    {
        public const int STATUS_ACTIVE = 0;
        public const int STATUS_INACTIVE = 10;
        public const int STATUS_DELETED = 127;

        protected FW fw;
        protected DB db;
        protected string db_config = ""; // if empty(default) - fw.db used, otherwise - new db connection created based on this config name

        public string table_name = ""; // must be assigned in child class
        public string csv_export_fields = ""; // all or Utils.qw format
        public string csv_export_headers = ""; // comma-separated format

        public string field_id = "id"; // default primary key name
        public string field_iname = "iname";
        public string field_icode = "icode";

        // default field names. If you override it and make empty - automatic processing disabled
        public string field_status = "status";
        public string field_add_users_id = "add_users_id";
        public string field_upd_users_id = "upd_users_id";
        public string field_upd_time = "upd_time";
        public string field_prio = "";
        public bool is_normalize_names = false; // if true - Utils.name2fw() will be called for all fetched rows to normalize names (no spaces or special chars)

        public bool is_log_changes = true; // if true - event_log record added on add/update/delete
        public bool is_log_fields_changed = true; // if true - event_log.fields filled with changes

        // for linked models ex UsersCompanies that link 2 tables
        public FwModel linked_model_main;
        public string linked_field_main_id; // ex users_id
        public FwModel linked_model_link;
        public string linked_field_link_id; // ex companies_id

        protected string cache_prefix = "fwmodel.one."; // default cache prefix for caching items

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
                Hashtable dbconfig = (Hashtable)fw.config("db");
                Hashtable config_details = (Hashtable)dbconfig[this.db_config];
                this.db = new DB(fw, config_details, this.db_config);
            }
            else
                this.db = fw.db;
        }

        public virtual DB getDB()
        {
            return db;
        }

        public virtual Hashtable one(int id)
        {
            var cache_key = this.cache_prefix + id;
            Hashtable item = (Hashtable)fw.cache.getRequestValue(cache_key);
            if (item == null)
            {
                Hashtable where = new Hashtable();
                where[this.field_id] = id;
                item = db.row(table_name, where);
                normalizeNames(item);
                fw.cache.setRequestValue(cache_key, item);
            }
            return item;
        }

        public virtual ArrayList multi(ICollection ids)
        {
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

            foreach (Hashtable row in rows)
                normalizeNames(row);
        }

        public virtual string iname(int id)
        {
            if (field_iname == "")
                return "";

            Hashtable row = one(id);
            return (string)row[field_iname];
        }
        public virtual string iname(object id)
        {
            var result = "";
            if (Utils.f2int(id) > 0)
                result = iname(Utils.f2int(id));
            return result;
        }

        protected virtual string getOrderBy()
        {
            var result = field_iname;
            if (!string.IsNullOrEmpty(field_prio))
                result = db.q_ident(field_prio) + " desc, " + db.q_ident(field_iname);
            return result;
        }

        // return standard list of id,iname where status=0 order by iname
        public virtual ArrayList list()
        {
            Hashtable where = new Hashtable();
            if (!string.IsNullOrEmpty(field_status))
                where[field_status] = db.opNOT(STATUS_DELETED);
            return db.array(table_name, where, getOrderBy());
        }

        // override if id/iname differs in table
        // def - in dynamic controller - field definition (also contains "i" and "ps", "lookup_params", ...) or you could use it to pass additional params
        public virtual ArrayList listSelectOptions(Hashtable def = null)
        {
            Hashtable where = new Hashtable();
            if (!string.IsNullOrEmpty(field_status))
                where[field_status] = db.opNOT(STATUS_DELETED);

            ArrayList select_fields = new ArrayList()
        {
            new Hashtable() { { "field", field_id }, { "alias", "id" } },
            new Hashtable() { { "field", field_iname }, { "alias", "iname" } }
        };
            return db.array(table_name, where, getOrderBy(), select_fields);
        }

        // similar to listSelectOptions but returns iname/iname
        public virtual ArrayList listSelectOptionsName(Hashtable def = null)
        {
            Hashtable where = new Hashtable();
            if (!string.IsNullOrEmpty(field_status))
                where[field_status] = db.opNOT(STATUS_DELETED);

            ArrayList select_fields = new ArrayList()
        {
            new Hashtable() { { "field", field_iname }, { "alias", "id" } },
            new Hashtable() { { "field", field_iname }, { "alias", "iname" } }
        };
            return db.array(table_name, where, getOrderBy(), select_fields);
        }

        // return count of all non-deleted
        public int getCount()
        {
            Hashtable where = new Hashtable();
            if (!string.IsNullOrEmpty(field_status))
                where[field_status] = db.opNOT(STATUS_DELETED);
            return (int)db.value(table_name, where, "count(*)");
        }

        // just return first row by iname field (you may want to make it unique)
        public virtual Hashtable oneByIname(string iname)
        {
            if (field_iname == "")
                return new Hashtable();

            Hashtable where = new();
            where[field_iname] = iname;
            return db.row(table_name, where);
        }

        public virtual Hashtable oneByIcode(string icode)
        {
            if (field_icode == "")
                return new Hashtable();

            Hashtable where = new();
            where[field_icode] = icode;
            return db.row(table_name, where);
        }

        // check if item exists for a given field
        public virtual bool isExistsByField(object uniq_key, int not_id, string field)
        {
            Hashtable where = new();
            where[field] = uniq_key;
            if (!string.IsNullOrEmpty(field_id))
                where[field_id] = db.opNOT(not_id);
            string val = (string)db.value(table_name, where, "1");
            if (val == "1")
                return true;
            else
                return false;
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
            if (!string.IsNullOrEmpty(field_add_users_id) && !item.ContainsKey(field_add_users_id) && fw.SessionBool("is_logged"))
                item[field_add_users_id] = fw.SessionInt("user_id");
            int id = db.insert(table_name, item);

            if (is_log_changes)
            {
                if (is_log_fields_changed)
                    fw.logEvent(table_name + "_add", id, 0, "", 0, item);
                else
                    fw.logEvent(table_name + "_add", id);
            }

            this.removeCache(id);

            return id;
        }

        // update exising record
        public virtual bool update(int id, Hashtable item)
        {
            Hashtable item_changes = new Hashtable();
            if (is_log_changes)
            {
                var item_old = this.one(id);
                item_changes = fw.model<FwEvents>().changes_only(item, item_old);
            }

            if (!string.IsNullOrEmpty(field_upd_time))
                item[field_upd_time] = DateTime.Now;
            if (!string.IsNullOrEmpty(field_upd_users_id) && !item.ContainsKey(field_upd_users_id) && fw.SessionBool("is_logged"))
                item[field_upd_users_id] = fw.SessionInt("user_id");

            Hashtable where = new();
            where[this.field_id] = id;
            db.update(table_name, item, where);

            this.removeCache(id); // cleanup cache, so next one read will read new value

            if (is_log_changes && item_changes.Count > 0)
            {
                if (is_log_fields_changed)
                    fw.logEvent(table_name + "_upd", id, 0, "", 0, item_changes);
                else
                    fw.logEvent(table_name + "_upd", id);
            }

            return true;
        }

        // mark record as deleted (status=127) OR actually delete from db (if is_perm or status field not defined for this model table)
        public virtual void delete(int id, bool is_perm = false)
        {
            Hashtable where = new();
            where[this.field_id] = id;

            if (is_perm || string.IsNullOrEmpty(field_status))
            {
                // place here code that remove related data
                db.del(table_name, where);
                this.removeCache(id);
            }
            else
            {
                Hashtable vars = new();
                vars[field_status] = STATUS_DELETED;
                if (!string.IsNullOrEmpty(field_upd_time))
                    vars[field_upd_time] = DateTime.Now;
                if (!string.IsNullOrEmpty(field_upd_users_id) && fw.SessionBool("is_logged"))
                    vars[field_upd_users_id] = fw.SessionInt("user_id");

                db.update(table_name, vars, where);
            }
            if (is_log_changes)
                fw.logEvent(table_name + "_del", id);
        }

        public virtual void removeCache(int id)
        {
            var cache_key = this.cache_prefix + id;
            fw.cache.requestRemove(cache_key);
        }

        public virtual void removeCacheAll()
        {
            fw.cache.requestRemoveWithPrefix(this.cache_prefix);
        }

        // upload utils
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

        // methods from fw - just for a covenience, so no need to use "fw.", as they are used quite frequently
        public void logger(params object[] args)
        {
            if (args.Length == 0)
                return;
            fw._logger(LogLevel.DEBUG, ref args);
        }
        public void logger(LogLevel level, params object[] args)
        {
            if (args.Length == 0)
                return;
            fw._logger(level, ref args);
        }


        public virtual string getSelectOptions(string sel_id)
        {
            return FormUtils.selectOptions(this.listSelectOptions(), sel_id);
        }

        public virtual ArrayList getAutocompleteList(string q)
        {
            Hashtable where = new();
            where[field_iname] = db.opLIKE("%" + q + "%");
            if (!string.IsNullOrEmpty(field_status))
                where[field_status] = db.opNOT(STATUS_DELETED);
            return db.col(table_name, where, field_iname);
        }

        // called from withing link model like UsersCompanies that links 2 tables By Main ID (like users_id)
        // id - main table id
        // def - in dynamic controller - field definition (also contains "i" and "ps", "lookup_params", ...) or you could use it to pass additional params
        public virtual ArrayList getMultiListLinkedRows(object id, Hashtable def = null)
        {
            var linked_rows = db.array(table_name, new Hashtable() { { linked_field_main_id, id } });

            ArrayList lookup_rows = linked_model_link.list();
            if (linked_rows != null && linked_rows.Count > 0)
            {
                foreach (Hashtable row in lookup_rows)
                {
                    // check if linked_rows contain main id
                    row["is_checked"] = false;
                    row["_link"] = new Hashtable();
                    foreach (Hashtable lrow in linked_rows)
                    {
                        if (row[linked_model_link.field_id] == lrow[linked_field_link_id])
                        {
                            row["is_checked"] = true;
                            row["_link"] = lrow;
                            break;
                        }
                    }
                }

                // now sort so checked values will be at the top AND then by prio field (if any) - using LINQ
                ArrayList result = new();
                if (!string.IsNullOrEmpty(field_prio))
                    result.AddRange((from Hashtable h in lookup_rows
                                     orderby ((Hashtable)h["_link"])[field_prio], h["is_checked"] descending
                                     select h).ToList());
                else
                    result.AddRange((from Hashtable h in lookup_rows
                                     orderby h["is_checked"] descending
                                     select h).ToList());
                lookup_rows = result;
            }
            return lookup_rows;
        }

        // called from withing link model like UsersCompanies that links 2 tables By Linked ID (like companies_id)
        // id - linked table id
        // def - in dynamic controller - field definition (also contains "i" and "ps", "lookup_params", ...) or you could use it to pass additional params
        public virtual ArrayList getMultiListLinkedRowsByLinkedId(object id, Hashtable def = null)
        {
            var linked_rows = db.array(table_name, new Hashtable() { { linked_field_link_id, id } });

            ArrayList lookup_rows = linked_model_main.list();
            if (linked_rows != null && linked_rows.Count > 0)
            {
                foreach (Hashtable row in lookup_rows)
                {
                    // check if linked_rows contain main id
                    row["is_checked"] = false;
                    row["_link"] = new Hashtable();
                    foreach (Hashtable lrow in linked_rows)
                    {
                        if (row[linked_model_main.field_id] == lrow[linked_field_main_id])
                        {
                            row["is_checked"] = true;
                            row["_link"] = lrow;
                            break;
                        }
                    }
                }

                // now sort so checked values will be at the top AND then by prio field (if any) - using LINQ
                ArrayList result = new();
                if (!string.IsNullOrEmpty(field_prio))
                    result.AddRange((from Hashtable h in lookup_rows
                                     orderby ((Hashtable)h["_link"])[field_prio], h["is_checked"] descending
                                     select h).ToList());
                else
                    result.AddRange((from Hashtable h in lookup_rows
                                     orderby h["is_checked"] descending
                                     select h).ToList());
                lookup_rows = result;
            }
            return lookup_rows;
        }

        protected void setMultiListChecked(ref ArrayList rows, ArrayList ids, Hashtable def = null)
        {
            var is_checked_only = (def != null && Utils.f2bool(def["lookup_checked_only"]));

            if (ids != null && ids.Count > 0)
            {
                foreach (Hashtable row in rows)
                    row["is_checked"] = ids.Contains(row[this.field_id]);
                // now sort so checked values will be at the top - using LINQ
                ArrayList result = new();
                if (is_checked_only)
                    result.AddRange((from Hashtable h in rows
                                     where (bool)h["is_checked"]
                                     select h).ToList());
                else
                    result.AddRange((from Hashtable h in rows
                                     orderby h["is_checked"] descending
                                     select h).ToList());
                rows = result;
            }
            else if (is_checked_only)
                // return no items if no checked
                rows = new ArrayList();
        }

        // sel_ids - selected ids in the list()
        // def - in dynamic controller - field definition (also contains "i" and "ps", "lookup_params", ...) or you could use it to pass additional params
        public virtual ArrayList getMultiListAL(ArrayList ids, Hashtable def = null)
        {
            ArrayList rows = this.list();
            setMultiListChecked(ref rows, ids, def);
            return rows;
        }

        // overloaded version for string comma-separated ids
        // sel_ids - comma-separated ids
        // def - in dynamic controller - field definition (also contains "i" and "ps", "lookup_params", ...) or you could use it to pass additional params
        public virtual ArrayList getMultiList(string sel_ids, Hashtable def = null)
        {
            ArrayList ids = new(Strings.Split(sel_ids, ","));
            return this.getMultiListAL(ids, def);
        }

        /// <summary>
        ///     return array of ids of linked elements
        /// </summary>
        /// <param name="link_table_name">link table name that contains id_name and link_id_name fields</param>
        /// <param name="id">main id</param>
        /// <param name="id_name">field name for main id</param>
        /// <param name="link_id_name">field name for linked id</param>
        /// <returns></returns>
        public virtual ArrayList getLinkedIds(string link_table_name, int id, string id_name, string link_id_name)
        {
            Hashtable where = new();
            where[id_name] = id;
            ArrayList rows = db.array(link_table_name, where);
            ArrayList result = new();
            foreach (Hashtable row in rows)
                result.Add(row[link_id_name]);

            return result;
        }

        // shortcut for getLinkedIds based on dynamic controller definition
        public virtual ArrayList getLinkedIdsByDef(int id, Hashtable def)
        {
            return getLinkedIds((string)def["table_link"], id, (string)def["table_link_id_name"], (string)def["table_link_linked_id_name"]);
        }

        /// <summary>
        ///  update (and add/del) linked table
        /// </summary>
        /// <param name="link_table_name">link table name that contains id_name and link_id_name fields</param>
        /// <param name="id">main id</param>
        /// <param name="id_name">field name for main id</param>
        /// <param name="link_id_name">field name for linked id</param>
        /// <param name="linked_keys">hashtable with keys as link id (as passed from web)</param>
        public virtual void updateLinked(string link_table_name, int id, string id_name, string link_id_name, Hashtable linked_keys)
        {
            Hashtable fields = new();
            Hashtable where = new();
            var link_table_field_status = "status";

            // set all fields as under update
            fields[link_table_field_status] = 1;
            where[id_name] = id;
            db.update(link_table_name, fields, where);

            if (linked_keys != null)
            {
                foreach (string link_id in linked_keys.Keys)
                {
                    fields = new Hashtable();
                    fields[id_name] = id;
                    fields[link_id_name] = link_id;
                    fields[link_table_field_status] = 0;

                    where = new Hashtable();
                    where[id_name] = id;
                    where[link_id_name] = link_id;
                    db.update_or_insert(link_table_name, fields, where);
                }
            }

            // remove those who still not updated (so removed)
            where = new Hashtable();
            where[id_name] = id;
            where[link_table_field_status] = 1;
            db.del(link_table_name, where);
        }

        // override to add set more additional fields
        public virtual void updateLinkedRowsAdditional(Hashtable linked_keys, string link_id, Hashtable fields)
        {
            if (!string.IsNullOrEmpty(field_prio) && linked_keys.Contains(field_prio + "_" + link_id))
                fields[field_prio] = Utils.f2int(linked_keys[field_prio + "_" + link_id]);// get value from prio_ID
        }
        // called from withing link model like UsersCompanies that links 2 tables
        public virtual void updateLinkedRows(int main_id, Hashtable linked_keys)
        {
            Hashtable fields = new Hashtable();
            Hashtable where = new Hashtable();
            var link_table_field_status = this.field_status;

            // set all fields as under update
            fields[link_table_field_status] = 1;
            where[linked_field_main_id] = main_id;
            db.update(table_name, fields, where);

            if (linked_keys != null)
            {
                foreach (string link_id in linked_keys.Keys)
                {
                    if (Utils.f2int(link_id) == 0)
                        continue; // skip non-id, ex prio_ID

                    fields = new Hashtable();
                    fields[linked_field_main_id] = main_id;
                    fields[linked_field_link_id] = link_id;
                    fields[link_table_field_status] = 0;

                    // additional fields here
                    updateLinkedRowsAdditional(linked_keys, link_id, fields);

                    where = new Hashtable();
                    where[linked_field_main_id] = main_id;
                    where[linked_field_link_id] = link_id;
                    db.update_or_insert(table_name, fields, where);
                }
            }

            // remove those who still not updated (so removed)
            where = new Hashtable();
            where[linked_field_main_id] = main_id;
            where[link_table_field_status] = 1;
            db.del(table_name, where);
        }

        // override to add set more additional fields
        public virtual void updateLinkedRowsByLinkedIdAdditional(Hashtable linked_keys, string main_id, Hashtable fields)
        {
            if (string.IsNullOrEmpty(field_prio) && linked_keys.ContainsKey(field_prio + "_" + main_id))
                fields[field_prio] = Utils.f2int(linked_keys[field_prio + "_" + main_id]);// get value from prio_ID
        }
        // called from withing link model like UsersCompanies that links 2 tables
        public virtual void updateLinkedRowsByLinkedId(int linked_id, Hashtable linked_keys)
        {
            Hashtable fields = new Hashtable();
            Hashtable where = new Hashtable();
            var link_table_field_status = this.field_status;

            // set all fields as under update
            fields[link_table_field_status] = 1;
            where[linked_field_link_id] = linked_id;
            db.update(table_name, fields, where);

            if (linked_keys != null)
            {
                foreach (string main_id in linked_keys.Keys)
                {
                    if (Utils.f2int(main_id) == 0)
                        continue; // skip non-id, ex prio_ID

                    fields = new Hashtable();
                    fields[linked_field_link_id] = linked_id;
                    fields[linked_field_main_id] = main_id;
                    fields[link_table_field_status] = 0;

                    // additional fields here
                    updateLinkedRowsByLinkedIdAdditional(linked_keys, main_id, fields);

                    where = new Hashtable();
                    where[linked_field_link_id] = linked_id;
                    where[linked_field_main_id] = main_id;
                    logger(fields);
                    db.update_or_insert(table_name, fields, where);
                }
            }

            // remove those who still not updated (so removed)
            where = new Hashtable();
            where[linked_field_link_id] = linked_id;
            where[link_table_field_status] = 1;
            db.del(table_name, where);
        }

        public virtual int findOrAddByIname(string iname, out bool is_added)
        {
            is_added = false;
            iname = Strings.Trim(iname);
            if (iname.Length == 0)
                return 0;
            int result;
            Hashtable item = this.oneByIname(iname);
            if (item.ContainsKey(this.field_id))
                // exists
                result = (int)item[this.field_id];
            else
            {
                // not exists - add new
                item = new();
                item[field_iname] = iname;
                result = this.add(item);
                is_added = true;
            }
            return result;
        }

        public virtual StringBuilder getCSVExport()
        {
            Hashtable where = new Hashtable();
            if (!string.IsNullOrEmpty(field_status))
                where[field_status] = STATUS_ACTIVE;

            string[] aselect_fields = Array.Empty<string>();
            if (!string.IsNullOrEmpty(csv_export_fields))
                aselect_fields = Utils.qw(csv_export_fields);

            var rows = db.array(table_name, where, "", aselect_fields);
            return Utils.getCSVExport(csv_export_headers, csv_export_fields, rows);
        }

        public void Dispose()
        {
            fw.Dispose();
        }
    }

}