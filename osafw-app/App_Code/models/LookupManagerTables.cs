// LookupManager Tables model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;
using System.Linq;

namespace osafw
{
    public class LookupManagerTables : FwModel
    {
        public LookupManagerTables() : base()
        {
            table_name = "lookup_manager_tables";
        }

        // just return first row by tname field (you may want to make it unique)
        // CACHED
        public virtual Hashtable oneByTname(string tname)
        {
            Hashtable item = (Hashtable)fw.cache.getRequestValue("LookupManagerTables_one_by_tname_" + table_name + "#" + tname);
            if (item == null)
            {
                Hashtable where = new();
                where["tname"] = tname;
                item = db.row(table_name, where);
                fw.cache.setRequestValue("LookupManagerTables_one_by_tname_" + table_name + "#" + tname, item);
            }
            return item;
        }

        // return table columns from database
        // no identity column returned
        // no timestamp, image, varbinary returned (as not supported by UI)
        // filtered by defs(columns)
        // added custom names, types and grouping info - if defined
        public ArrayList getColumns(Hashtable defs)
        {
            ArrayList result = new();

            Hashtable custom_columns = new();
            Hashtable ix_custom_columns = new();
            if (!string.IsNullOrEmpty((string)defs["columns"]))
            {
                var custom_cols = Utils.commastr2hash((string)defs["columns"], "123...");
                foreach (string key in custom_cols.Keys)
                {
                    Hashtable h = new();
                    h["index"] = custom_cols[key];
                    h["iname"] = key; // default display name is column name
                    h["itype"] = ""; // no default input type
                    h["igroup"] = ""; // no default group
                    custom_columns[key] = h;

                    ix_custom_columns[h["index"]] = h; // build inverted index
                }
                // custom names
                if (!string.IsNullOrEmpty((string)defs["column_names"]))
                {
                    ArrayList custom_names = new(defs["column_names"].ToString().Split(","));
                    for (var i = 0; i <= custom_names.Count - 1; i++)
                        ((Hashtable)ix_custom_columns[i])["iname"] = custom_names[i];
                }
                // custom types
                if (!string.IsNullOrEmpty((string)defs["column_types"]))
                {
                    ArrayList custom_types = new(defs["column_types"].ToString().Split(","));
                    for (var i = 0; i <= custom_types.Count - 1; i++)
                        ((Hashtable)ix_custom_columns[i])["itype"] = custom_types[i].ToString().Trim();
                }

                // groups
                if (!string.IsNullOrEmpty((string)defs["column_groups"]))
                {
                    // Dim groups As Hashtable = Utils.commastr2hash(defs["groups"), ]123...")
                    ArrayList custom_groups = new(defs["column_groups"].ToString().Split(","));
                    for (var i = 0; i <= custom_groups.Count - 1; i++)
                        ((Hashtable)ix_custom_columns[i])["igroup"] = custom_groups[i];
                }
            }

            ArrayList cols = db.loadTableSchemaFull((string)defs["tname"]);
            foreach (Hashtable col in cols)
            {
                var colname = (string)col["name"];
                var coltype = (string)col["type"];
                // skip unsupported (binary) fields
                // identity field also skipped as not updateable
                if ((string)col["is_identity"] == "1" || coltype == "timestamp" || coltype == "image" || coltype == "varbinary")
                    continue;

                // add/override custom info
                if (custom_columns.Count > 0)
                {
                    Hashtable cc = (Hashtable)custom_columns[colname];
                    if (cc == null)
                        // skip this field as not custom defined
                        continue;
                    else
                        Utils.mergeHash(col, cc);
                }
                else
                {
                    // defaults
                    // col["index") ]= 0
                    col["iname"] = Utils.name2human(colname); // default display name is column name
                    col["itype"] = ""; // no default input type
                    col["igroup"] = ""; // no default group
                }

                //if prio column detected - set it as first for easy sort UI
                if (colname == "prio")
                {
                    result.Insert(0, col);
                }
                else
                {
                    result.Add(col);
                }
            }

            if (custom_columns.Count > 0)
            {
                // if custom columns - return columns sorted according to custom list
                // sort with LINQ
                var query = from Hashtable col in result
                            orderby col["index"]
                            select col;

                ArrayList sorted_result = new();
                foreach (Hashtable h in query)
                    sorted_result.Add(h);
                return sorted_result;
            }
            else
                return result;
        }

        //convert cols into hashtable
        public Hashtable hColumns(ArrayList cols)
        {
            var result = new Hashtable();
            foreach (Hashtable col in cols)
                result[col["name"]] = col;
            return result;
        }

        // return "id" or customer column_id defined in defs
        public string getColumnId(Hashtable defs)
        {
            if (!string.IsNullOrEmpty((string)defs["column_id"]))
                return (string)defs["column_id"];
            else
                return "id";
        }

        public string getColumnPrio(Hashtable defs)
        {
            var cols = getColumns(defs);
            var hCols = hColumns(cols);

            if (hCols.ContainsKey("prio"))
                return "prio";
            else
                return "";
        }

        public string getLookupSelectOptions(string itype_lookup, object sel_id)
        {
            string lutable = "";
            string lufields = "";
            Utils.split2(@"\.", itype_lookup, ref lutable, ref lufields);
            var idfield = "";
            var inamefield = "";
            Utils.split2(":", lufields, ref idfield, ref inamefield);

            ArrayList fields = new();
            fields.Add(new Hashtable { { "field", idfield }, { "alias", "id" } });
            fields.Add(new Hashtable { { "field", inamefield }, { "alias", "iname" } });
            var rows = db.array(lutable, new Hashtable(), "1", fields);

            return FormUtils.selectOptions(rows, (string)sel_id);
        }

        public string getLookupValue(string itype_lookup, object sel_id)
        {
            string lutable = "";
            string lufields = "";
            Utils.split2(@"\.", itype_lookup, ref lutable, ref lufields);
            var idfield = "";
            var inamefield = "";
            Utils.split2(":", lufields, ref idfield, ref inamefield);

            Hashtable where = new() { { idfield, sel_id } };
            return (string)db.value(lutable, where, inamefield);
        }
    }
}