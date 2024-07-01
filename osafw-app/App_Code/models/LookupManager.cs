// LookupManager model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Linq;
using System.Text;

namespace osafw;

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

        return db.rowp(db.limit("select * from " + db.qid(tname), top_number));
    }

    public virtual int maxIdByTname(string tname)
    {
        if (tname == "")
            throw new ApplicationException("Wrong maxIdByTname params");

        Hashtable defs = fw.model<LookupManagerTables>().oneByTname(tname);
        if (defs.Count == 0)
            throw new ApplicationException("Wrong lookup table name");

        var id_field = fw.model<LookupManagerTables>().getColumnId(defs);
        var value = db.valuep("SELECT MAX(" + db.qid(id_field) + ") from " + db.qid(tname));
        return Utils.f2int(value);
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

        if (Utils.isEmpty(defs["list_columns"]))
        {
            // if no list cols - it's std table - add std fields
            if (!item.ContainsKey("add_users_id") && fw.isLogged)
                item["add_users_id"] = Utils.f2str(fw.userId);
        }

        var field_prio = fw.model<LookupManagerTables>().getColumnPrio(defs);
        var prio = !Utils.isEmpty(field_prio) ? (!Utils.isEmpty(item[field_prio]) ? item[field_prio] : null) : null;

        //prevent exception if prio column doesn't allow NULLs
        if (!Utils.isEmpty(field_prio) && prio == null)
            item[field_prio] = 0;

        int id = db.insert(tname, item);
        fw.logActivity(FwLogTypes.ICODE_ADDED, tname, id);

        //if priority field defined and its value is not passed - update it with newly added id to allow proper re/ordering
        if (!Utils.isEmpty(field_prio) && prio == null)
        {
            var field_id = fw.model<LookupManagerTables>().getColumnId(defs);
            db.update(tname, DB.h(field_prio, id), DB.h(field_id, id));
        }

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

        //set nullable fields to NULL value if empty
        Hashtable table_schema = db.tableSchemaFull(tname);
        foreach (string fld_name in item.Keys.Cast<string>().ToArray())
            if (Utils.isEmpty(item[fld_name]) && (string)((Hashtable)(table_schema[fld_name]))["is_nullable"] == "1")
                item[fld_name] = null;

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
            // additional null and old value empty string check to avoid SET to NULL constantly as we get empty string from DB for DBNull values
            if ((item[key] == null && itemold[key].ToString() != "")
                || (item[key] != null && itemold[key].ToString() != item[key].ToString()))
                item_save[key] = item[key];
        }
        // logger("NEW SAVE")
        // logger(item_save)

        if (item_save.Count > 0)
        {
            Hashtable where = new();
            where[id_fname] = id;

            if (Utils.isEmpty(defs["list_columns"]))
            {
                // if no list cols - it's std table - add std fields
                if (!item_save.ContainsKey("upd_time"))
                    item_save["upd_time"] = DB.NOW;
                if (!item_save.ContainsKey("upd_users_id") && fw.isLogged)
                    item_save["upd_users_id"] = fw.userId;
            }

            db.update(tname, item_save, where);

            fw.logActivity(FwLogTypes.ICODE_UPDATED, tname, id);
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

        fw.logActivity(FwLogTypes.ICODE_DELETED, tname, id);
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

    public int updatePrioRange(int inc_value, int from_prio, int to_prio, string tname, string field_prio)
    {
        var field_prioq = db.qid(field_prio);
        var p = DB.h("inc_value", inc_value, "from_prio", from_prio, "to_prio", to_prio);
        return db.exec("UPDATE " + db.qid(tname) +
            " SET " + field_prioq + "=" + field_prioq + "+(@inc_value)" +
            " WHERE " + field_prioq + " BETWEEN @from_prio AND @to_prio", p);
    }

    public int updatePrio(int id, int prio, string tname, string field_id, string field_prio)
    {
        return db.update(tname, DB.h(field_prio, prio), DB.h(field_id, id));
    }

    // reorder prio column
    public bool reorderPrio(Hashtable defs, string sortdir, int id, int under_id, int above_id)
    {
        if (sortdir != "asc" && sortdir != "desc")
            throw new ApplicationException("Wrong sort directrion");

        var field_id = fw.model<LookupManagerTables>().getColumnId(defs);
        var field_prio = fw.model<LookupManagerTables>().getColumnPrio(defs);
        if (string.IsNullOrEmpty(field_prio))
            return false;

        var field_prioq = db.qid(field_prio);

        var tname = (string)defs["tname"];
        int id_prio = Utils.f2int(oneByTname(tname, id)[field_prio]);

        // detect reorder
        if (under_id > 0)
        {
            // under id present
            int under_prio = Utils.f2int(oneByTname(tname, under_id)[field_prio]);
            if (sortdir == "asc")
            {
                if (id_prio < under_prio)
                {
                    // if my prio less than under_prio - make all records between old prio and under_prio as -1
                    updatePrioRange(-1, id_prio, under_prio, tname, field_prio);
                    // and set new id prio as under_prio
                    updatePrio(id, under_prio, tname, field_id, field_prio);
                }
                else
                {
                    // if my prio more than under_prio - make all records between old prio and under_prio as +1
                    updatePrioRange(+1, (under_prio + 1), id_prio, tname, field_prio);
                    // and set new id prio as under_prio+1
                    updatePrio(id, under_prio + 1, tname, field_id, field_prio);
                }
            }
            else
                // desc
                if (id_prio < under_prio)
            {
                // if my prio less than under_prio - make all records between old prio and under_prio-1 as -1
                updatePrioRange(-1, id_prio, under_prio - 1, tname, field_prio);
                // and set new id prio as under_prio-1
                updatePrio(id, under_prio - 1, tname, field_id, field_prio);
            }
            else
            {
                // if my prio more than under_prio - make all records between under_prio and old prio as +1
                updatePrioRange(+1, under_prio, id_prio, tname, field_prio);
                // and set new id prio as under_prio
                updatePrio(id, under_prio, tname, field_id, field_prio);
            }
        }
        else if (above_id > 0)
        {
            // above id present
            int above_prio = Utils.f2int(oneByTname(tname, above_id)[field_prio]);
            if (sortdir == "asc")
            {
                if (id_prio < above_prio)
                {
                    // if my prio less than under_prio - make all records between old prio and above_prio-1 as -1
                    updatePrioRange(-1, id_prio, above_prio - 1, tname, field_prio);
                    // and set new id prio as under_prio
                    updatePrio(id, above_prio - 1, tname, field_id, field_prio);
                }
                else
                {
                    // if my prio more than under_prio - make all records between above_prio and old prio as +1
                    updatePrioRange(+1, above_prio, id_prio, tname, field_prio);
                    // and set new id prio as under_prio+1
                    updatePrio(id, above_prio, tname, field_id, field_prio);
                }
            }
            else
                // desc
                if (id_prio < above_prio)
            {
                // if my prio less than under_prio - make all records between old prio and above_prio as -1
                updatePrioRange(-1, id_prio, above_prio, tname, field_prio);
                // and set new id prio as above_prio
                updatePrio(id, above_prio, tname, field_id, field_prio);
            }
            else
            {
                // if my prio more than under_prio - make all records between above_prio+1 and old prio as +1
                updatePrioRange(+1, above_prio + 1, id_prio, tname, field_prio);
                // and set new id prio as under_prio+1
                updatePrio(id, above_prio + 1, tname, field_id, field_prio);
            }
        }
        else
            // bad reorder call - ignore
            return false;

        return true;
    }

}
