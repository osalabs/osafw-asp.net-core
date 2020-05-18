using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace osafw_asp_net_core.fw
{
    public class FwModel
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
            if (fw == null) {
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

        /*public virtual Hashtable one(id As Integer)
        {
            Hashtable item = fw.cache.getRequestValue("fwmodel_one_" & table_name & "#" & id);
            if (item == null)
            {
                Hashtable where = new Hashtable();
                where[field_id] = id;
                item = db.row(table_name, where);
                normalizeNames(item);
                fw.cache.setRequestValue("fwmodel_one_" & table_name & "#" & id, item);
            }
            return item;
        }*/

    }
}
