using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace osafw_asp_net_core.fw
{
    public enum DBOps : int {
        EQ,            // =
        NOT,           // <>
        LE,            // <=
        LT,            // <
        GE,            // >=
        GT,            // >
        ISNULL,        // IS NULL
        ISNOTNULL,     // IS NOT NULL
        IN,            // IN
        NOTIN,         // NOT IN
        LIKE,          // LIKE
        NOTLIKE        // NOT LIKE
    }
    // describes DB operation
    public class DBOperation {
        public DBOps op;
        public string opstr; // string value for op
        public bool is_value = true; // if false - operation is unary (no value)
        public object value; // can be array for IN, NOT IN, OR
        public string quoted_value;
        public DBOperation(DBOps op, object value = null) {
            op = op;
            setOpStr();
            value = value;
        }
        public void setOpStr() {
            switch (op) {
                case DBOps.ISNULL:
                    opstr = "IS NULL";
                    is_value = false;
                    break;
                case DBOps.ISNOTNULL:
                    opstr = "IS NOT NULL";
                    is_value = false;
                    break;
                case DBOps.EQ:
                    opstr = "=";
                    break;
                case DBOps.NOT:
                    opstr = "<>";
                    break;
                case DBOps.LE:
                    opstr = "<=";
                    break;
                case DBOps.LT:
                    opstr = "<";
                    break;
                case DBOps.GE:
                    opstr = ">=";
                    break;
                case DBOps.GT:
                    opstr = ">";
                    break;
                case DBOps.IN:
                    opstr = "IN";
                    break;
                case DBOps.NOTIN:
                    opstr = "NOT IN";
                    break;
                case DBOps.LIKE:
                    opstr = "LIKE";
                    break;
                case DBOps.NOTLIKE:
                    opstr = "NOT LIKE";
                    break;
                default:
                    //Throw New ApplicationException("Wrong DB OP")
                    break;
            }
        }
    }
    public class DB
    {
        private static Hashtable schemafull_cache;  // cache for the full schema, lifetime = app lifetime
        private static Hashtable schema_cache; // cache for the schema, lifetime = app lifetime

        public static int SQL_QUERY_CTR = 0; // counter for SQL queries during request

        private FW fw; // for now only used for: fw.logger and fw.cache (for request-level cacheing of multi-db connections)

        public string db_name = "";
        public string dbtype = "SQL";
        private readonly Hashtable conf = new Hashtable(); // config contains: connection_string, type
        private readonly string connstr = "";

        private Hashtable schema = null; // schema for currently connected db
        private DbConnection conn = null; // actual db connection - SqlConnection or OleDbConnection

        private bool is_check_ole_types = false; // if true - checks for unsupported OLE types during readRow
        private Hashtable UNSUPPORTED_OLE_TYPES = null;
        // <summary>
        // construct new DB object with
        // </summary>
        // <param name="fw">framework reference</param>
        // <param name="conf">config hashtable with "connection_string" and "type" keys. If none - fw.config("db")("main") used</param>
        // <param name="db_name">database human name, only used for logger</param>
        public DB(FW fw, Hashtable _conf = null, string db_name = "main") {
            this.fw = fw;
            if (_conf != null) {
                this.conf = _conf;
            } else {
                Hashtable db = (Hashtable)FW.config("db");
                conf = (Hashtable)db["main"];
            }
            dbtype = (String)conf["type"];
            connstr = (String)conf["connection_string"];

            this.db_name = db_name;

            //UNSUPPORTED_OLE_TYPES = Utils.qh("DBTYPE_IDISPATCH DBTYPE_IUNKNOWN") 'also? DBTYPE_ARRAY DBTYPE_VECTOR DBTYPE_BYTES

        }

        public void logger(FwLogger.LogLevel level, params object[] args) {
            if (args.Length == 0) return;
            fw.logger(level, args);
        }

        // <summary>
        // connect to DB server using connection string defined in appsettings.json appSettings, key db:main:connection_string (by default)
        // </summary>
        // <returns></returns>
        public DbConnection connect() {
            string cache_key = "DB#" + connstr;

            //first, try to get connection from request cache (so we will use only one connection per db server - TBD make configurable?)
            if (conn == null) {
                //conn = fw.cache.getRequestValue(cache_key)
            }

            // if still no connection - re-make it
            if (conn == null) {
                schema = new Hashtable(); // reset schema cache
                conn = createConnection(connstr, dbtype);
                //fw.cache.setRequestValue(cache_key, conn)
            }

            // if it's disconnected - re-connect
            if (conn.State != ConnectionState.Open) {
                conn.Open();
            }

            if (dbtype == "OLE") {
                is_check_ole_types = true;
            } else {
                is_check_ole_types = false;
            }

            return conn;
        }

        public void disconnect() {
            if (conn != null) {
                conn.Close();
            }
        }

        public DbConnection createConnection(string connstr, string dbtype = "SQL") {
            if (dbtype == "SQL")
            {
                DbConnection result = new SqlConnection(connstr);
                result.Open();
                return result;
            }
            else if (dbtype == "OLE")
            {
                DbConnection result = new OleDbConnection(connstr);
                result.Open();
                return result;
            }
            else
            {
                String msg = "Unknown type [" + dbtype + "]";
                logger(FwLogger.LogLevel.FATAL, msg);
                throw new ApplicationException(msg);
            }
        }


        public void check_create_mdb(String filepath) {
            /*if (File.Exists(filepath)) return;
            String connstr = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" + filepath;
            Object cat = CreateObject("ADOX.Catalog");
            cat.Create(connstr);*/
        }

        //<Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")>
        public DbDataReader query(String sql) {
            connect();
            logger(FwLogger.LogLevel.INFO, "DB:", db_name, " ", sql);

            SQL_QUERY_CTR += 1;

            DbCommand dbcomm = null;
            if (dbtype == "SQL" && conn is SqlConnection)
            {
                dbcomm = new SqlCommand(sql, conn as SqlConnection);
            }
            else if (dbtype == "OLE" && conn is OleDbConnection)
            {
                dbcomm = new OleDbCommand(sql, conn as OleDbConnection);
            }
            DbDataReader dbread = dbcomm.ExecuteReader();
            return dbread;
        }

        // exectute without results (so db reader will be closed), return number of rows affected.
        // <Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")>
        public int exec(String sql)
        {
            connect();
            logger(FwLogger.LogLevel.INFO, "DB:", db_name, ", SQL QUERY: ", sql);

            SQL_QUERY_CTR += 1;

            DbCommand dbcomm = null;
            if (dbtype == "SQL" && conn is SqlConnection) {
                dbcomm = new SqlCommand(sql, conn as SqlConnection);
            }
            else if (dbtype == "OLE" && conn is OleDbConnection)
            {
                dbcomm = new OleDbCommand(sql, conn as OleDbConnection);
            }
            return dbcomm.ExecuteNonQuery();
        }

        private Hashtable readRow(DbDataReader dbread)
        {
            Hashtable result = new Hashtable();
            for (int i = 0; i < dbread.FieldCount - 1; i++)
            {
                try
                {
                    if (is_check_ole_types && UNSUPPORTED_OLE_TYPES.ContainsKey(dbread.GetDataTypeName(i)))
                    {
                        continue;
                    }
                    String value = dbread[i].ToString();
                    String name = dbread.GetName(i).ToString();
                    result.Add(name, value);
                }
                catch (Exception Ex)
                {
                    return null;
                }
            }
            return result;
        }

        public Hashtable row(String sql)
        {
            DbDataReader dbread = query(sql);
            dbread.Read();

            Hashtable h = new Hashtable();
            if (dbread.HasRows)
            {
                h = readRow(dbread);
            }
            dbread.Close();
            return h;
        }

        public Hashtable row(String table, Hashtable where, String order_by = "")
        {
            return row(hash2sql_select(table, where, order_by));
        }

        public ArrayList array(String sql)
        {
            DbDataReader dbread = query(sql);
            ArrayList a = new ArrayList();

            while (dbread.Read()) {
                a.Add(readRow(dbread));
            }

            dbread.Close();
            return a;
        }

        // <summary>
        // return all rows with all fields from the table based on coditions/order
        // array("table", where, "id asc", Utils.qh("field1|id field2|iname"))
        // </summary>
        // <param name="table">table name</param>
        // <param name="where">where conditions</param>
        // <param name="order_by">optional order by, MUST BE QUOTED</param>
        // <param name="aselect_fields">optional select fields array or hashtable(for aliases) or arraylist of hashtable("field"=>,"alias"=> for cases if there could be several same fields with diff aliases), if not set * returned</param>
        // <returns></returns>
        public ArrayList array(String table, Hashtable where, String order_by = "", ICollection aselect_fields = null)
        {
            String select_fields = "*";
            if (aselect_fields != null)
            {
                ArrayList quoted = new ArrayList();
                if (aselect_fields is ArrayList)
                {
                    // arraylist of hashtables with "field","alias" keys - usable for the case when we need same field to be selected more than once with different aliases
                    foreach (Hashtable asf in aselect_fields)
                    {
                        quoted.Add(q_ident((String)asf["field"]) + " as " + q_ident((String)asf["alias"]));
                    }
                }
                else if (aselect_fields is IDictionary)
                {
                    IDictionary _dict = (IDictionary)aselect_fields;
                    foreach (String field in _dict.Keys)
                    {
                        quoted.Add(q_ident(field) + " as " + q_ident((String)_dict[field])); // field as alias
                    }
                }
                else // IList
                {
                    foreach (String field in aselect_fields)
                    {
                        quoted.Add(q_ident(field));
                    }
                }
                select_fields = quoted.Count > 0 ? String.Join(", ", quoted.ToArray()) : "*";
            }

            return array(hash2sql_select(table, where, order_by, select_fields));
        }


        // quote identifier: table => [table]
        public String q_ident(String str)
        {
            if (str == null) str = "";
            str = str.Replace("[", "");
            str = str.Replace("]", "");
            return "[" + str + "]";
        }


        // join key/values with quoting values according to table
        // h - already quoted! values
        // kv_delim = pass "" to autodetect " = " or " IS " (for NULL values)
        public String _join_hash(Hashtable h, String kv_delim, String pairs_delim)
        {
            String res = "";
            if (h.Count < 1) return res;

            String[] ar = new String[h.Count - 1];

            int i = 0;
            foreach (String k in h.Keys)
            {
                var vv = h[k];
                var v = "";
                String delim = kv_delim;
                if (delim == null || delim == "")
                {
                    if (vv is DBOperation)
                    {
                        DBOperation dbop = (DBOperation)vv;
                        delim = " " + dbop.opstr + " ";
                        if (dbop.is_value)
                        {
                            v = dbop.quoted_value;
                        }
                    }
                    else
                    {
                        v = (String)vv;
                        if (v == "NULL")
                        {
                            delim = " IS ";
                        }
                        else
                        {
                            delim = "=";
                        }
                    }
                }
                else
                {
                    v = (String)vv;
                }
                ar[i] = k + delim + v;
                i += 1;
            }
            res = String.Join(pairs_delim, ar);
            return res;
        }

        // <summary>
        // build SELECT sql string
        // </summary>
        // <param name="table">table name</param>
        // <param name="where">where conditions</param>
        // <param name="order_by">optional order by string</param>
        // <param name="select_fields">MUST already be quoted!</param>
        // <returns></returns>
        private String hash2sql_select(String table, Hashtable where, String order_by = "", String select_fields = "*")
        {
            // where = quote(table, where); !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            // FW.logger(where)
            String where_string = _join_hash(where, "", " AND ");
            if (where_string.Length > 0)
            {
                where_string = " WHERE " + where_string;
            }

            String sql = "SELECT " + select_fields + " FROM " + q_ident(table) + " " + where_string;
            if (order_by.Length > 0)
            {
                sql = sql + " ORDER BY " + order_by;
            }
            return sql;
        }

        /*public Hashtable quote(String table, Hashtable fields)
        {
            connect();
            load_table_schema(table);
            if (!schema.ContainsKey(table))
            {
                throw new ApplicationException("table [" + table + "] does not defined in FW.config(\"schema\")");
            }

            Hashtable fieldsq = new Hashtable();
            String k;

            foreach (String k In fields.Keys)
            {
                Dim q = qone(table, k, fields(k));
                // quote field name too
                if (q != null) {
                    fieldsq(q_ident(k)) = q;
                }
            }

            return fieldsq;
        }*/
    }
}
