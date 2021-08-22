// LookupManager model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Linq;
using System.Text;

namespace osafw
{
    public class LookupManager : FwModel
    {

        // system columns that not need to be shown to user as is
        public Hashtable SYS_COLS = new()
        {
            { "add_time", true },
            { "add_users_id", true },
            { "upd_time", true },
            { "upd_users_id", true }
        };

        public LookupManager() : base()
        {
            table_name = "xxx";
        }

        // return top X rows (default 1) from table tname
        public virtual Hashtable topByTname(string tname, int top_number = 1)
        {
            if (tname == "")
                throw new ApplicationException("Wrong topByTname params");

            return db.row("select TOP " + db.qi(top_number) + " * from " + db.q_ident(tname));
        }

        public virtual int maxIdByTname(string tname)
        {
            if (tname == "")
                throw new ApplicationException("Wrong maxIdByTname params");

            Hashtable defs = fw.model<LookupManagerTables>().oneByTname(tname);
            if (defs.Count == 0)
                throw new ApplicationException("Wrong lookup table name");

            var id_field = fw.model<LookupManagerTables>().getColumnId(defs);
            return (int)db.value("SELECT MAX(" + db.q_ident(id_field) + ") from " + db.q_ident(tname));
        }

        public virtual Hashtable oneByTname(string tname, int id)
        {
            if (tname == "" || id == 0)
                throw new ApplicationException("Wrong oneByTname params");

            Hashtable defs = fw.model<LookupManagerTables>().oneByTname(tname);
            if (defs.Count == 0)
                throw new ApplicationException("Wrong lookup table name");

            Hashtable where = new();
            where[fw.model<LookupManagerTables>().getColumnId(defs)] = id;
            return db.row(tname, where);
        }


        // add new record and return new record id
        public virtual int addByTname(string tname, Hashtable item)
        {
            if (tname == "")
                throw new ApplicationException("Wrong update_by_tname params");
            Hashtable defs = fw.model<LookupManagerTables>().oneByTname(tname);
            if (defs.Count == 0)
                throw new ApplicationException("Wrong lookup table name");

            if (string.IsNullOrEmpty((string)defs["list_columns"]))
            {
                // if no list cols - it's std table - add std fields
                if (!item.ContainsKey("add_users_id") && Users.isLogged)
                    item["add_users_id"] = Users.id;
            }

            int id = db.insert(tname, item);
            fw.logEvent(tname + "_add", id);
            return id;
        }

        // update exising record
        public virtual bool updateByTname(string tname, int id, Hashtable item, string md5 = "")
        {
            if (tname == "" || id == 0)
                throw new ApplicationException("Wrong update_by_tname params");
            Hashtable defs = fw.model<LookupManagerTables>().oneByTname(tname);
            if (defs.Count == 0)
                throw new ApplicationException("Wrong lookup table name");
            string id_fname = fw.model<LookupManagerTables>().getColumnId(defs);

            // also we need include old fields into where just because id by sort is not robust enough
            var itemold = oneByTname(tname, id);
            // If Not defs["list_columns"] > "" Then
            // 'remove syscols
            // Dim itemold2 As New Hashtable
            // For Each key In itemold.Keys
            // If SYS_COLS.ContainsKey(key) Then Continue For
            // itemold2(key) = itemold[key]
            // Next
            // itemold = itemold2
            // End If
            if (!string.IsNullOrEmpty(md5))
            {
                // additionally check we got right record by comparing md5
                if (md5 != getRowMD5(itemold))
                    throw new ApplicationException("Cannot update database. Wrong checksum. Probably someone else already updated data you are trying to edit.");
            }

            itemold.Remove(id_fname);
            itemold.Remove("SSMA_TimeStamp"); // remove timestamp fields, it was created during migration from Access

            // now compare new values with old values and save only values that are different
            // so if nothing changed - no db update performed
            // logger("OLD")
            // logger(itemold)
            // logger("NEW")
            // logger(item)
            Hashtable item_save = new();
            foreach (string key in item.Keys)
            {
                if (itemold[key].ToString() != item[key].ToString())
                    item_save[key] = item[key];
            }
            // logger("NEW SAVE")
            // logger(item_save)

            if (item_save.Count > 0)
            {
                Hashtable where = new();
                where[id_fname] = id;

                if (string.IsNullOrEmpty((string)defs["list_columns"]))
                {
                    // if no list cols - it's std table - add std fields
                    if (!item_save.ContainsKey("upd_time"))
                        item_save["upd_time"] = DateTime.Now;
                    if (!item_save.ContainsKey("upd_users_id") && Users.isLogged)
                        item_save["upd_users_id"] = Users.id;
                }

                db.update(tname, item_save, where);

                fw.logEvent(tname + "_upd", id);
                return true;
            }
            else
                return false;
        }

        // delete from db
        public virtual void deleteByTname(string tname, int id, string md5 = "")
        {
            if (tname == "" || id == 0)
                throw new ApplicationException("Wrong update_by_tname params");
            Hashtable defs = fw.model<LookupManagerTables>().oneByTname(tname);
            if (defs.Count == 0)
                throw new ApplicationException("Wrong lookup table name");
            string id_fname = fw.model<LookupManagerTables>().getColumnId(defs);

            // also we need include old fields into where just because id by sort is not robust enough
            var itemold = oneByTname(tname, id);
            if (!string.IsNullOrEmpty(md5))
            {
                // additionally check we got right record by comparing md5
                if (md5 != getRowMD5(itemold))
                    throw new ApplicationException("Cannot delete from database. Wrong checksum. Probably someone else already updated data you are trying to edit.");
            }

            Hashtable where = new();
            where[id_fname] = id;
            db.del(tname, where);

            fw.logEvent(tname + "_del", id);
        }

        // calculate md5 for all values from hashtable
        // values sorted by keyname before calculating
        internal string getRowMD5(Hashtable row)
        {
            logger(row);
            // sort with LINQ
            var sorted_keys = from string k in row.Keys
                              orderby k
                              select k;

            StringBuilder str = new();
            // logger("calc id for: " & row("_RowNumber"))
            foreach (string fieldname in sorted_keys)
            {
                // logger(fieldname)
                if (fieldname == "_RowNumber")
                    continue;
                str.AppendLine(row[fieldname].ToString());
            }
            // logger(row("id"))
            // logger(str.ToString())
            // logger(Utils.md5(str.ToString()))
            return Utils.md5(str.ToString());
        }

        public ArrayList filterOutSysCols(ArrayList cols)
        {
            ArrayList result = new();

            foreach (Hashtable col in cols)
            {
                if (SYS_COLS.ContainsKey(col["name"]))
                    continue;
                result.Add(col);
            }

            return result;
        }
    }
}