using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace osafw
{
    public class FwModel : IDisposable
    {
        const int STATUS_ACTIVE = 0;
        const int STATUS_DELETED = 127;

        protected FW fw;
        protected DB db;
        protected string db_config = ""; // if empty(default) - fw.db used, otherwise - new db connection created based on this config name

        public string table_name = ""; // must be assigned in child class
        public string csv_export_fields = ""; // all or Utils.qw format
        public string csv_export_headers = ""; // comma-separated format

        public string field_id = "id"; // default primary key name
        public string field_iname = "iname";

        // default field names. If you override it and make empty - automatic processing disabled
        public string field_status = "status";
        public string field_add_users_id = "add_users_id";
        public string field_upd_users_id = "upd_users_id";
        public string field_upd_time = "upd_time";
        public bool is_normalize_names = false; // if true - Utils.name2fw() will be called for all fetched rows to normalize names (no spaces or special chars)

        protected FwModel(FW fw = null) {
            if (fw != null) {
                this.fw = fw;
                this.db = fw.db;
            }
        }

        public virtual void init(FW fw) {
            this.fw = fw;
            if (db_config != "") {
                //db = new DB(fw, fw.config("db")(db_config), db_config);
            } else {
                db = fw.db;
            }
        }

        public virtual DB getDB()
        {
            return db;
        }

        public virtual Hashtable one(int id)
        {
            Hashtable item = (Hashtable)fw.cache.getRequestValue("fwmodel_one_" + table_name + "#" + id);
            if (item == null)
            {
                Hashtable where = new Hashtable();
                where[field_id] = id;
                item = db.row(table_name, where);
                normalizeNames(item); // TODO uncomment after port method
                fw.cache.setRequestValue("fwmodel_one_" + table_name + "#" + id, item);
            }
            return item;
        }

        // add renamed fields For template engine - spaces and special chars replaced With "_" and other normalizations
        public void normalizeNames(Hashtable row)
        {
            if (!is_normalize_names) return;
            foreach (String key in new ArrayList(row.Keys)) // static copy of row keys to avoid loop issues
            {
                row[Utils.name2fw(key)] = row[key];
            }
            if (field_id != "" && !row.ContainsKey("id")) row["id"] = row[field_id];
        }

        public void normalizeNames(ArrayList rows)
        {
            if (!is_normalize_names) return;
            foreach (Hashtable row in rows)
            {
                normalizeNames(row);
            }
        }

        public virtual String iname(int id)
        {
            Hashtable row = one(id);
            return (String)row[field_iname];
        }
        public virtual String iname(Object id)
        {
            String result = "";
            if (Utils.f2int(id) > 0)
            {
                result = iname(Utils.f2int(id));
            }
            return result;
        }

        // return standard list of id,iname where status=0 order by iname
        public virtual ArrayList list()
        {
            Hashtable where = new Hashtable();
            if (field_status != "")
            {
                where[field_status] = STATUS_ACTIVE;
            }
            return db.array(table_name, where, field_iname);
        }

        // override if id/iname differs in table
        // parameter - to use - override in your model
        public virtual ArrayList listSelectOptions(Object parameter = null)
        {
            Hashtable where = new Hashtable();
            if (field_status.Length > 0) where[field_status] = STATUS_ACTIVE;

            ArrayList select_fields = new ArrayList() {
                new Hashtable() { { "field", field_id}, { "alias", "id"} },
                new Hashtable() { { "field", field_iname}, { "alias", "iname"} }
            };
            return db.array(table_name, where, db.q_ident(field_iname), select_fields);
        }

        // return count of all non-deleted
        public long getCount()
        {
            Hashtable where = new Hashtable();
            if (field_status.Length > 0)
            {
                where[field_status] = db.opNOT(STATUS_DELETED);
            }
            return (long)db.value(table_name, where, "count(*)");
        }

        // just return first row by iname field (you may want to make it unique)
        public virtual Hashtable oneByIname(String iname)
        {
            Hashtable where = new Hashtable();
            where[field_iname] = iname;
            return db.row(table_name, where);
        }

        // check if item exists for a given field
        public virtual bool isExistsByField(Object uniq_key, int not_id, String field)
        {
            Hashtable where = new Hashtable();
            where[field] = uniq_key;
            where[field_id] = db.opNOT(not_id);
            String val = (String)db.value(table_name, where, "1");
            if (val == "1")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // check if item exists for a given iname
        public virtual bool isExists(Object uniq_key, int not_id)
        {
            return isExistsByField(uniq_key, not_id, field_iname);
        }

        // add new record and return new record id
        public virtual int add(Hashtable item)
        {
            // item("add_time") = Now() // not necessary because add_time field in db should have default value now() or getdate()
            if (field_add_users_id != "" && !item.ContainsKey(field_add_users_id) && fw.getSessionInt("is_logged") == 1)
            {
                item[field_add_users_id] = fw.getSessionInt("user_id");
            }
            int id = db.insert(table_name, item);
            fw.logEvent(table_name + "_add", id);
            return id;
        }

        // update exising record
        public virtual bool update(int id, Hashtable item)
        {
            if (field_upd_time != String.Empty) item[field_upd_time] = DateTime.Now;
            if (field_upd_users_id != String.Empty && !item.ContainsKey(field_upd_users_id) && fw.getSessionInt("is_logged") == 1)
            {
                item[field_upd_users_id] = fw.getSessionInt("user_id");
            }

            Hashtable where = new Hashtable();
            where[field_id] = id;
            db.update(table_name, item, where);

            fw.logEvent(table_name + "_upd", id);

            fw.cache.requestRemove("fwmodel_one_" + table_name + "#" + id); // cleanup cache, so next one read will read new value
            return true;
        }

        // mark record as deleted (status=127) OR actually delete from db (if is_perm or status field not defined for this model table)
        public virtual void delete(int id, bool is_perm = false)
        {
            Hashtable where = new Hashtable();
            where[this.field_id] = id;

            if (is_perm || String.IsNullOrEmpty(field_status))
            {
                // place here code that remove related data
                db.del(table_name, where);
                fw.cache.requestRemove("fwmodel_one_" + table_name + "#" + id); // cleanup cache, so next one read will read new value
            }
            else
            {
                Hashtable vars = new Hashtable();
                vars[field_status] = STATUS_DELETED;
                if (field_upd_time != String.Empty) vars[field_upd_time] = DateTime.Now;
                if (field_add_users_id != String.Empty && fw.getSessionInt("is_logged") == 1)
                {
                    vars[field_add_users_id] = fw.getSessionInt("user_id");
                }

                db.update(table_name, vars, where);
            }
            fw.logEvent(table_name + "_del", id);
        }

        // upload utils
        public virtual bool uploadFile(int id, ref String filepath, String input_name = "file1", bool is_skip_check = false)
        {
            return UploadUtils.uploadFile(fw, table_name, id, ref filepath, input_name, is_skip_check);
        }

        // return upload dir for the module name and id related to FW.config("site_root")/upload
        // id splitted to 1000
        public virtual String getUploadDir(long id)
        {
            return UploadUtils.getUploadDir(fw, table_name, id);
        }

        public virtual String getUploadUrl(long id, String ext, String size = "")
        {
            return UploadUtils.getUploadUrl(fw, table_name, id, ext, size);
        }

        // removes all type of image files uploaded with thumbnails
        public virtual bool removeUpload(long id, String ext)
        {
            String dir = getUploadDir(id);

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

        public virtual String getUploadImgPath(long id, String size, String ext = "")
        {
            return UploadUtils.getUploadImgPath(fw, table_name, id, size, ext);
        }

        // methods from fw - just for a covenience, so no need to use "fw.", as they are used quite frequently
        public void logger(params Object[] args)
        {
            if (args.Length == 0) return;
            fw.logger(LogLevel.DEBUG, args);
        }
        public void logger(LogLevel level, params Object[] args)
        {
            if (args.Length == 0) return;
            fw.logger(level, args);
        }


        public virtual String getSelectOptions(String sel_id)
        {
            return FormUtils.selectOptions(listSelectOptions(), sel_id);
        }

        public virtual ArrayList getAutocompleteList(String q)
        {
            Hashtable where = new Hashtable();
            where[this.field_iname] = db.opLIKE("%" + q + "%");
            if (!String.IsNullOrEmpty(this.field_status))
            {
                where[this.field_status] = db.opNOT(STATUS_DELETED);
            }
            return db.col(this.table_name, where, this.field_iname);
        }

        // sel_ids - selected ids in the list()
        // params - to use - override in your model
        public virtual ArrayList getMultiListAL(ArrayList ids, Object parameters = null)
        {
            ArrayList rows = list();
            foreach (Hashtable row in rows)
            {
                row["is_checked"] = ids.Contains(row[this.field_id]);
            }
            return rows;
        }

        // overloaded version for string comma-separated ids
        // sel_ids - comma-separated ids
        // params - to use - override in your model
        public virtual ArrayList getMultiList(String sel_ids, Object parameters = null)
        {
            ArrayList ids = new ArrayList(sel_ids.Split(","));
            return this.getMultiListAL(ids, parameters);
        }

        /// <summary>
        ///     return comma-separated ids of linked elements - TODO refactor to use arrays, not comma-separated string
        /// </summary>
        /// <param name="link_table_name">link table name that contains id_name and link_id_name fields</param>
        /// <param name="id">main id</param>
        /// <param name="id_name">field name for main id</param>
        /// <param name="link_id_name">field name for linked id</param>
        /// <returns></returns>
        public virtual ArrayList getLinkedIds(String link_table_name, int id, String id_name, String link_id_name)
        {
            Hashtable where = new Hashtable();
            where[id_name] = id;
            ArrayList rows = db.array(link_table_name, where);
            ArrayList result = new ArrayList();
            foreach (Hashtable row in rows)
            {
                result.Add(row[link_id_name]);
            }

            return result;
        }

        /// <summary>
        ///  update (and add/del) linked table
        /// </summary>
        /// <param name="link_table_name">link table name that contains id_name and link_id_name fields</param>
        /// <param name="id">main id</param>
        /// <param name="id_name">field name for main id</param>
        /// <param name="link_id_name">field name for linked id</param>
        /// <param name="linked_keys">hashtable with keys as link id (as passed from web)</param>
        public virtual void updateLinked(String link_table_name, int id, String id_name, String link_id_name, Hashtable linked_keys)
        {
            Hashtable fields = new Hashtable();
            Hashtable where = new Hashtable();
            String link_table_field_status = "status";

            // set all fields as under update
            fields[link_table_field_status] = 1;
            where[id_name] = id;
            db.update(link_table_name, fields, where);

            if (linked_keys != null)
            {
                foreach (String link_id in linked_keys.Keys)
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

        public virtual int findOrAddByIname(String iname, ref bool is_added)
        {
            is_added = false;
            iname = iname.Trim();
            if (iname.Length == 0) return 0;
            int result = 0;
            Hashtable item = this.oneByIname(iname);
            if (item.ContainsKey(this.field_id))
            {
                // exists
                result = (int)item[this.field_id];
            }
            else
            {
                // not exists - add new
                item = new Hashtable();
                item[field_iname] = iname;
                result = this.add(item);
                is_added = true;
            }
            return result;
        }

        public virtual StringBuilder getCSVExport()
        {
            Hashtable where = new Hashtable();
            if (!String.IsNullOrEmpty(field_status))
            {
                where[field_status] = STATUS_ACTIVE;
            }

            String[] aselect_fields = Array.Empty<String>();
            if (!String.IsNullOrEmpty(csv_export_fields))
            {
                aselect_fields = Utils.qw(csv_export_fields);
            }

            ArrayList rows = db.array(table_name, where, "", aselect_fields);
            return Utils.getCSVExport(csv_export_headers, csv_export_fields, rows);
        }
        
        public void Dispose()
        {
            fw.Dispose();
        }
    }
}
