using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace osafw_asp.net_core.fw
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
        public String opstr; // String value for op
        public bool is_value = true; // if false - operation is unary (no value)
        public Object value; // can be array for IN, NOT IN, OR
        public String quoted_value;
        public DBOperation(DBOps op, Object value = null) {
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
    public class DB : IDisposable
    {
        private Hashtable schemafull_cache;  // cache for the full schema, lifetime = app lifetime
        private Hashtable schema_cache; // cache for the schema, lifetime = app lifetime

        public static int SQL_QUERY_CTR = 0; // counter for SQL queries during request

        private FW fw; // for now only used for: fw.logger and fw.cache (for request-level cacheing of multi-db connections)

        public String db_name = "";
        public String dbtype = "SQL";
        private readonly Hashtable conf = new Hashtable(); // config contains: connection_String, type
        private readonly String connstr = "";

        private Hashtable schema = null; // schema for currently connected db
        private DbConnection conn = null; // actual db connection - SqlConnection or OleDbConnection

        private bool is_check_ole_types = false; // if true - checks for unsupported OLE types during readRow
        private Hashtable UNSUPPORTED_OLE_TYPES = null;
        // <summary>
        // construct new DB Object with
        // </summary>
        // <param name="fw">framework reference</param>
        // <param name="conf">config hashtable with "connection_String" and "type" keys. If none - fw.config("db")("main") used</param>
        // <param name="db_name">database human name, only used for logger</param>
        public DB(FW fw, Hashtable _conf = null, String db_name = "main") {
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

        public void logger(FwLogger.LogLevel level, params Object[] args) {
            if (args.Length == 0) return;
            fw.logger(level, args);
        }

        // <summary>
        // connect to DB server using connection String defined in appsettings.json appSettings, key db:main:connection_String (by default)
        // </summary>
        // <returns></returns>
        public DbConnection connect() {
            String cache_key = "DB#" + connstr;

            //first, try to get connection from request cache (so we will use only one connection per db server - TBD make configurable?)
            if (conn == null) {
                conn = (DbConnection)fw.cache.getRequestValue(cache_key);
            }

            // if still no connection - re-make it
            if (conn == null) {
                schema = new Hashtable(); // reset schema cache
                conn = createConnection(connstr, dbtype);
                fw.cache.setRequestValue(cache_key, conn);
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

        public DbConnection createConnection(String connstr, String dbtype = "SQL") {
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
            for (int i = 0; i < dbread.FieldCount; i++)
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

        // return just first column values as arraylist
        public virtual ArrayList col(String sql)
        {
            DbDataReader dbread = query(sql);
            ArrayList a = new ArrayList();
            while (dbread.Read())
            {
                a.Add(dbread[0].ToString());
            }
            dbread.Close();
            return a;
        }

        // <summary>
        // return just one column values as arraylist
        // </summary>
        // <param name="table">table name</param>
        // <param name="where">where conditions</param>
        // <param name="field_name">optional field name, if empty - first field returned</param>
        // <param name="order_by">optional order by (MUST be quoted)</param>
        // <returns></returns>
        public virtual ArrayList col(String table, Hashtable where, String field_name = "", String order_by = "")
        {
            if (String.IsNullOrEmpty(field_name))
            {
                field_name = "*";
            }
            else
            {
                field_name = q_ident(field_name);
            }
            return col(hash2sql_select(table, where, order_by, field_name));
        }

        // return just first value from column
        public virtual Object value(String sql)
        {
            DbDataReader dbread = query(sql);
            Object result = null;

            while (dbread.Read())
            {
                result = dbread[0];
                break; // just return first row
            }

            dbread.Close();
            return result;
        }

        // <summary>
        // return just one field value:
        // value("table", where)
        // value("table", where, "field1")
        // value("table", where, "1") 'just return 1, useful for exists queries
        // value("table", where, "count(*)", "id asc")
        // </summary>
        // <param name="table"></param>
        // <param name="where"></param>
        // <param name="field_name">field name, special cases: "1", "count(*)"</param>
        // <param name="order_by"></param>
        // <returns></returns>
        public virtual Object value(String table, Hashtable where, String field_name = "", String order_by = "")
        {
            if (String.IsNullOrEmpty(field_name))
            {
                field_name = "*";
            }
            else if (field_name == "count(*)" || field_name == "1")
            {
                // no changes
            }
            else
            {
                field_name = q_ident(field_name);
            }
            return value(hash2sql_select(table, where, order_by, field_name));
        }

        // String will be Left(RTrim(str),length)
        public String left(String str, int length)
        {
            if (String.IsNullOrEmpty(str)) return "";
            return new String(str).TrimEnd().Substring(0, length);
        }

        // create "IN (1,2,3)" sql or IN (NULL) if empty params passed
        // examples:
        //  where = " field "& db.insql("a,b,c,d")
        //  where = " field "& db.insql(String())
        //  where = " field "& db.insql(ArrayList)
        public String insql(String parameters)
        {
            return insql(new String(parameters).Split(","));
        }
        public String insql(IList parameters)
        {
            ArrayList result = new ArrayList();
            foreach (String param in parameters)
            {
                result.Add(q(param));
            }
            return " IN (" + (result.Count > 0 ? String.Join(", ", result.ToArray()) : "NULL") + ")";
        }

        // quote identifier: table => [table]
        public String q_ident(String str)
        {
            if (str == null) str = "";
            str = str.Replace("[", "");
            str = str.Replace("]", "");
            return "[" + str + "]";
        }


        // if length defined - String will be Left(Trim(str),length) before quoted
        public String q(String str, int length = 0)
        {
            if (str == null) str = "";
            if (length > 0) str = this.left(str, length);
            return "'" + new String(str).Replace("'", "''") + "'";
        }

        // simple just replace quotes, don't add start/end single quote - for example, for use with LIKE
        public String qq(String str)
        {
            if (str == null) str = "";
            return new String(str).Replace("'", "''");
        }

        // simple quote as Integer Value
        public int qi(String str)
        {
            return Utils.f2int(str);
        }

        // simple quote as Float Value
        public double qf(String str)
        {
            return Utils.f2float(str);
        }

        // simple quote as Date Value
        public String qd(String str)
        {
            String result = "";
            if (dbtype == "SQL") {
                DateTime tmpdate;
                if (DateTime.TryParse(str, out tmpdate))
                {
                    result = "convert(DATETIME2, '" + tmpdate.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.DateTimeFormatInfo.InvariantInfo) + "', 120)";
                }
                else
                {
                    result = "NULL";
                }
            }
            else
            {
                result = Regex.Replace(str, @"['""\]\[]", "");
                if (Regex.IsMatch(result, @"\D"))
                {
                    result = "'" + str + "'";
                }
                else
                {
                    result = "NULL";
                }
            }
            return result;
        }

        public Hashtable quote(String table, Hashtable fields)
        {
            connect();
            load_table_schema(table);
            if (!schema.ContainsKey(table))
            {
                throw new ApplicationException("table [" + table + "] does not defined in FW.config(\"schema\")");
            }

            Hashtable fieldsq = new Hashtable();

            foreach (String k in fields.Keys)
            {
                var q = qone(table, k, fields[k]);
                // quote field name too
                if (q != null)
                {
                    fieldsq[q_ident(k)] = q;
                }
            }

            return fieldsq;
        }

        // can return String or DBOperation class
        public Object qone(String table, String field_name, Object field_value_or_op)
        {
            connect();
            load_table_schema(table);
            field_name = field_name.ToLower();
            if (!(schema[table] as Hashtable).ContainsKey(field_name))
            {
                throw new ApplicationException("field " + table + "." + field_name + " does not defined in FW.config(\"schema\") ");
            }

            Object field_value;
            DBOperation dbop = null;
            if (field_value_or_op is DBOperation)
            {
                dbop = (DBOperation)field_value_or_op;
                field_value = dbop.value;
            }
            else
            {
                field_value = field_value_or_op;
            }

            String field_type = (this.schema[table] as Hashtable)[field_name] as String;
            String quoted;
            if (dbop != null)
            {
                if (dbop.op == DBOps.IN || dbop.op == DBOps.NOTIN)
                {
                    if (dbop.value != null && dbop.value is IList)
                    {
                        ArrayList result = new ArrayList();
                        foreach (var param in dbop.value as IEnumerable)
                        {
                            result.Add(qone_by_type(field_type, param));
                        }
                        quoted = "(" + (result.Count > 0 ? String.Join(", ", result.ToArray()) : "NULL") + ")";
                    }
                    else
                    {
                        quoted = qone_by_type(field_type, field_value);
                    }
                }
                else
                {
                    quoted = qone_by_type(field_type, field_value);
                }
            }
            else
            {
                quoted = qone_by_type(field_type, field_value);
            }

            if (dbop != null)
            {
                dbop.quoted_value = quoted;
                return field_value_or_op;
            }
            else
            {
                return quoted;
            }
        }

        public String qone_by_type(String field_type, Object field_value)
        {
            String quoted;

            // if value set to Nothing or DBNull - assume it's NULL in db
            if (field_value == null || System.Convert.IsDBNull(field_value))
            {
                quoted = "NULL";
            }
            else
            {
                // fw.logger(table & "." & field_name & " => " & field_type & ", value=[" & field_value & "]")
                if (Regex.IsMatch(field_type, "int"))
                {
                    if (field_value != null && Regex.IsMatch(field_value as String, "true", RegexOptions.IgnoreCase))
                    {
                        quoted = "1";
                    }
                    else if (field_value != null && Regex.IsMatch(field_value as String, "false", RegexOptions.IgnoreCase))
                    {
                        quoted = "0";
                    }
                    else if (field_value != null && field_value is String && field_value == "")
                    {
                        // if empty String for numerical field - assume NULL
                        quoted = "NULL";
                    }
                    else
                    {
                        quoted = Utils.f2int(field_value).ToString();
                    }
                }
                else if (field_type == "datetime")
                {
                    quoted = qd(field_value.ToString());
                }
                else if (field_type == "float")
                {
                    quoted = Utils.f2float(field_value).ToString();
                }
                else
                {
                    // fieldsq(k) = "'" & Regex.Replace(fields(k), "(['""])", "\\$1") & "'"
                    if (field_value == null)
                    {
                        quoted = "''";
                    }
                    else
                    {
                        // escape backslash following by carriage return char(13) with doubling backslash and carriage return
                        // because of https://msdn.microsoft.com/en-us/library/dd207007.aspx
                        quoted = Regex.Replace(field_value.ToString(), "\\(\r\n?)", "\\$1$1");
                        quoted = Regex.Replace(quoted, "'", "''"); // escape single quotes
                        quoted = "'" + quoted + "'";
                    }
                }
            }
            return quoted;
        }

        // operations support for non-raw sql methods

        /// <summary>
        /// NOT EQUAL operation 
        /// Example: Dim rows = db.array("users", New Hashtable From {{"status", db.opNOT(127)}})
        /// <![CDATA[ select * from users where status<>127 ]]>
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public DBOperation opNOT(Object value)
        {
            return new DBOperation(DBOps.NOT, value);
        }

        /// <summary>
        /// LESS or EQUAL than operation
        /// Example: Dim rows = db.array("users", New Hashtable From {{"access_level", db.opLE(50)}})
        /// <![CDATA[ select * from users where access_level<=50 ]]>
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public DBOperation opLE(Object value)
        {
            return new DBOperation(DBOps.LE, value);
        }

        /// <summary>
        /// LESS THAN operation
        /// Example: Dim rows = db.array("users", New Hashtable From {{"access_level", db.opLT(50)}})
        /// <![CDATA[ select * from users where access_level<50 ]]>
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public DBOperation opLT(Object value)
        {
            return new DBOperation(DBOps.LT, value);
        }

        /// <summary>
        /// GREATER or EQUAL than operation
        /// Example: Dim rows = db.array("users", New Hashtable From {{"access_level", db.opGE(50)}})
        /// <![CDATA[ select * from users where access_level>=50 ]]>
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public DBOperation opGE(Object value)
        {
            return new DBOperation(DBOps.GE, value);
        }

        /// <summary>
        /// GREATER THAN operation
        /// Example: Dim rows = db.array("users", New Hashtable From {{"access_level", db.opGT(50)}})
        /// <![CDATA[ select * from users where access_level>50 ]]>
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public DBOperation opGT(Object value)
        {
            return new DBOperation(DBOps.GT, value);
        }

        /// <summary>
        /// Example: Dim rows = db.array("users", New Hashtable From {{"field", db.opISNULL()}})
        /// select * from users where field IS NULL
        /// </summary>
        /// <returns></returns>
        public DBOperation opISNULL()
        {
            return new DBOperation(DBOps.ISNULL);
        }
        /// <summary>
        /// Example: Dim rows = db.array("users", New Hashtable From {{"field", db.opISNOTNULL()}})
        /// select * from users where field IS NOT NULL
        /// </summary>
        /// <returns></returns>
        public DBOperation opISNOTNULL()
        {
            return new DBOperation(DBOps.ISNOTNULL);
        }
        /// <summary>
        /// Example: Dim rows = DB.array("users", New Hashtable From {{"address1", db.opLIKE("%Orlean%")}})
        /// select * from users where address1 LIKE '%Orlean%'
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public DBOperation opLIKE(Object value)
        {
            return new DBOperation(DBOps.LIKE, value);
        }
        /// <summary>
        /// Example: Dim rows = DB.array("users", New Hashtable From {{"address1", db.opNOTLIKE("%Orlean%")}})
        /// select * from users where address1 NOT LIKE '%Orlean%'
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public DBOperation opNOTLIKE(Object value)
        {
            return new DBOperation(DBOps.NOTLIKE, value);
        }

        /// <summary>
        /// 2 ways to call:
        /// opIN(1,2,4) - as multiple arguments
        /// opIN(array) - as one array of values
        /// 
        /// Example: Dim rows = db.array("users", New Hashtable From {{"id", db.opIN(1, 2)}})
        /// select * from users where id IN (1,2)
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public DBOperation opIN(params Object[] args)
        {
            Object values;
            if (args.Length == 1 && args[0].GetType().IsArray)
            {
                values = args[0];
            }
            else
            {
                values = args;
            }
            return new DBOperation(DBOps.IN, values);
        }

        /// <summary>
        /// 2 ways to call:
        /// opIN(1,2,4) - as multiple arguments
        /// opIN(array) - as one array of values
        /// 
        /// Example: Dim rows = db.array("users", New Hashtable From {{"id", db.opNOTIN(1, 2)}})
        /// select * from users where id NOT IN (1,2)
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public DBOperation opNOTIN(params Object[] args)
        {
            Object values;
            if (args.Length == 1 && args[0].GetType().IsArray)
            {
                values = args[0];
            }
            else
            {
                values = args;
            }
            return new DBOperation(DBOps.NOTIN, values);
        }

        // return last inserted id
        public int insert(String table, Hashtable fields)
        {
            int insert_id = -1;

            if (fields.Count < 1) return insert_id;

            exec(hash2sql_i(table, fields));

            if (dbtype == "SQL")
            {
                insert_id = (int)value("SELECT SCOPE_IDENTITY() AS [SCOPE_IDENTITY] ");
            }
            else if (dbtype == "OLE")
            {
                insert_id = (int)value("SELECT @@identity");
            }
            else
            {
                throw new ApplicationException("Get last insert ID for DB type [" + dbtype + "] not implemented");
            }

            // if table doesn't have identity insert_id would be DBNull
            if (System.Convert.IsDBNull(insert_id)) insert_id = 0;

            return insert_id;
        }

        public int update(String sql) 
        {
            return exec(sql);
        }

        public int update(String table, Hashtable fields, Hashtable where)
        {
            return exec(hash2sql_u(table, fields, where));
        }

        // retrun number of affected rows
        public int update_or_insert(String table, Hashtable fields, Hashtable where)
        {
            // merge fields and where
            Hashtable allfields = new Hashtable();
            
            foreach (String k in fields.Keys)
            {
                allfields[k] = fields[k];
            }

            foreach (String k in where.Keys)
            {
                allfields[k] = where[k];
            }

            String update_sql = hash2sql_u(table, fields, where);
            String insert_sql = hash2sql_i(table, allfields);
            String full_sql = update_sql + "  IF @@ROWCOUNT = 0 " + insert_sql;

            return exec(full_sql);
        }

        // retrun number of affected rows
        public int del(String table, Hashtable where) 
        {
            return exec(hash2sql_d(table, where));
        }

        // join key/values with quoting values according to table
        // h - already quoted! values
        // kv_delim = pass "" to autodetect " = " or " IS " (for NULL values)
        private String _join_hash(Hashtable h, String kv_delim, String pairs_delim)
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
        // build SELECT sql String
        // </summary>
        // <param name="table">table name</param>
        // <param name="where">where conditions</param>
        // <param name="order_by">optional order by String</param>
        // <param name="select_fields">MUST already be quoted!</param>
        // <returns></returns>
        private String hash2sql_select (String table, Hashtable where, String order_by = "", String select_fields = "*")
        {
            where = quote(table, where);
            // FW.logger(where)
            String where_String = _join_hash(where, "", " AND ");
            if (where_String.Length > 0)
            {
                where_String = " WHERE " + where_String;
            }

            String sql = "SELECT " + select_fields + " FROM " + q_ident(table) + " " + where_String;
            if (order_by.Length > 0)
            {
                sql = sql + " ORDER BY " + order_by;
            }
            return sql;
        }

        public String hash2sql_u(String table, Hashtable fields, Hashtable where)
        {
            fields = quote(table, fields);
            where = quote(table, where);

            String update_string = _join_hash(fields, "=", ", ");
            String where_string = _join_hash(where, "", " AND ");

            if (where_string.Length > 0) where_string = " WHERE " + where_string;

            String sql = "UPDATE " + q_ident(table) + " " + " SET " + update_string + where_string;

            return sql;
        }

        private String hash2sql_i(String table, Hashtable fields)
        {
            fields = quote(table, fields);

            String[] ar = new String[fields.Count];

            fields.Keys.CopyTo(ar, 0);
            String names_string = String.Join(", ", ar);

            fields.Values.CopyTo(ar, 0);
            String values_string = String.Join(", ", ar);
            String sql = "INSERT INTO " + q_ident(table) + " (" + names_string + ") VALUES (" + values_string + ")";
            return sql;
        }

        private String hash2sql_d(String table, Hashtable where)
        {
            where = quote(table, where);
            String where_string = _join_hash(where, "", " AND ");
            if (where_string.Length > 0) where_string = " WHERE " + where_string;

            String sql = "DELETE FROM " + q_ident(table) + " " + where_string;
            return sql;
        }

        public ArrayList load_table_schema_full(String table)
        {
            // check if full schema already there
            if (schemafull_cache == null)
            {
                schemafull_cache = new Hashtable();
            }

            if (!schemafull_cache.ContainsKey(connstr))
            {
                schemafull_cache[connstr] = new Hashtable();
            }

            if ((schemafull_cache[connstr] as Hashtable).ContainsKey(table))
            {
                return (schemafull_cache[connstr] as Hashtable)[table] as ArrayList;
            }

            // cache miss
            ArrayList result = new ArrayList();
            if (dbtype == "SQL")
            {
                // fw.logger("cache MISS " & current_db & "." & table)
                // get information about all columns in the table
                // default = ((0)) ('') (getdate())
                // maxlen = -1 for nvarchar(MAX)
                String sql = "SELECT c.column_name as 'name'," +
                    " c.data_type as 'type'," +
                    " CASE c.is_nullable WHEN 'YES' THEN 1 ELSE 0 END AS 'is_nullable'," +
                    " c.column_default as 'default'," +
                    " c.character_maximum_length as 'maxlen'," +
                    " c.numeric_precision," +
                    " c.numeric_scale," +
                    " c.character_set_name as 'charset'," +
                    " c.collation_name as 'collation'," +
                    " c.ORDINAL_POSITION as 'pos'," +
                    " COLUMNPROPERTY(Object_id(c.table_name), c.column_name, 'IsIdentity') as is_identity" +
                    " FROM INFORMATION_SCHEMA.TABLES t," +
                    "   INFORMATION_SCHEMA.COLUMNS c" +
                    " WHERE t.table_name = c.table_name" +
                    "   AND t.table_name = " + q(table) +
                    " order by c.ORDINAL_POSITION";
                result = array(sql);
                foreach (Hashtable row in result)
                {
                    row["fw_type"] = map_mssqltype2fwtype(row["type"] as String); //meta type
                    row["fw_subtype"] = row["type"].ToString().ToLower();
                }
            }
            else
            {
               /* // OLE DB (Access)
                DataTable schemaTable =
                    DirectCast(conn, OleDbConnection).GetOleDbSchemaTable(OleDb.OleDbSchemaGuid.Columns, New Object() { Nothing, Nothing, table, Nothing})

                Dim fieldslist = New List(Of Hashtable)
                For Each row As DataRow In schemaTable.Rows
                    'unused:
                    'COLUMN_HASDEFAULT True False
                    'COLUMN_FLAGS   74 86 90(auto) 102 106 114 122(date) 130 226 230 234
                    'CHARACTER_OCTET_LENGTH
                    'DATETIME_PRECISION=0
                    'DESCRIPTION
                    Dim h = New Hashtable
                    h("name") = row("COLUMN_NAME").ToString()
                    h("type") = row("DATA_TYPE")
                    h("fw_type") = map_oletype2fwtype(row("DATA_TYPE")) 'meta type
                    h("fw_subtype") = LCase([Enum].GetName(GetType(OleDbType), row("DATA_TYPE"))) 'exact type as String
                    h("is_nullable") = IIf(row("IS_NULLABLE"), 1, 0)
                    h("default") = row("COLUMN_DEFAULT") '"=Now()" "0" "No"
                    h("maxlen") = row("CHARACTER_MAXIMUM_LENGTH")
                    h("numeric_precision") = row("NUMERIC_PRECISION")
                    h("numeric_scale") = row("NUMERIC_SCALE")
                    h("charset") = row("CHARACTER_SET_NAME")
                    h("collation") = row("COLLATION_NAME")
                    h("pos") = row("ORDINAL_POSITION")
                    h("is_identity") = 0
                    h("desc") = row("DESCRIPTION")
                    h("column_flags") = row("COLUMN_FLAGS")
                    fieldslist.Add(h)
                Next
                'order by ORDINAL_POSITION
                result.AddRange(fieldslist.OrderBy(Function(h) h("pos")).ToList())

                'now detect identity (because order is important)
                For Each h As Hashtable In result
                    'actually this also triggers for Long Integers, so for now - only first field that match conditions will be an identity
                    If h("type") = OleDbType.Integer AndAlso h("column_flags") = 90 Then
                        h("is_identity") = 1
                        Exit For
                    End If
                Next*/
            }

            // save to cache
            (this.schemafull_cache[connstr] as Hashtable)[table] = result;

            return result;
        }

        // load table schema from db
        public Hashtable load_table_schema(String table)
        {
            // for non-MSSQL schemas - just use config schema for now - TODO
            if (dbtype != "SQL" && dbtype != "OLE")
            {
                if (this.schema.Count == 0)
                {
                    this.schema = this.conf["schema"] as Hashtable;
                }
                return null;
            }

            // check if schema already there
            if (this.schema.ContainsKey(table)) return this.schema[table] as Hashtable;

            if (this.schema_cache == null) this.schema_cache = new Hashtable();
            if (!this.schema_cache.ContainsKey(connstr)) this.schema_cache[connstr] = new Hashtable();
            if (!(this.schema_cache[connstr] as Hashtable).ContainsKey(table))
            {
                Hashtable h = new Hashtable();

                ArrayList fields = load_table_schema_full(table);
                foreach (Hashtable row in fields)
                {
                    h[row["name"].ToString().ToLower()] = row["fw_type"];
                }

                this.schema[table] = h;
                (this.schema_cache[connstr] as Hashtable)[table] = h;
            }
            else
            {
                // fw.logger("schema_cache HIT " & current_db & "." & table)
                this.schema[table] = (this.schema_cache[connstr] as Hashtable)[table];
            }

            return this.schema[table] as Hashtable;
        }

        public void clear_schema_cache() 
        {
            if (schemafull_cache != null) schemafull_cache.Clear();
            if (schema_cache != null) schema_cache.Clear();
            if (schema != null) schema.Clear();
        }

        private String map_mssqltype2fwtype(String mstype)
        {
            String result = "";
            switch (mstype.ToLower())
            {
                // TODO - unsupported: image, varbinary, timestamp
                case "tinyint":
                case "smallint":
                case "int":
                case "bigint":
                case "bit":
                    result = "int";
                    break;
                case "real":
                case "numeric":
                case "decimal":
                case "money":
                case "smallmoney":
                case "float":
                    result = "float";
                    break;
                case "datetime":
                case "datetime2":
                case "date":
                case "smalldatetime":
                    result = "datetime";
                    break;
                case "text":
                case "ntext":
                case "varchar":
                case "nvarchar":
                case "char":
                case "nchar":
                    result = "varchar";
                    break;
                default:
                    result = "varchar";
                    break;
            }

            return result;
        }

        private String map_oletype2fwtype(int mstype)
        {
            String result = "";
            switch (mstype)
            {
                // TODO - unsupported: image, varbinary, longvarbinary, dbtime, timestamp
                // NOTE: Boolean here is: True=-1 (vbTrue), False=0 (vbFalse)
                case (int)OleDbType.Boolean:
                case (int)OleDbType.TinyInt:
                case (int)OleDbType.UnsignedTinyInt:
                case (int)OleDbType.SmallInt:
                case (int)OleDbType.UnsignedSmallInt:
                case (int)OleDbType.Integer:
                case (int)OleDbType.UnsignedInt:
                case (int)OleDbType.BigInt:
                case (int)OleDbType.UnsignedBigInt:
                    result = "int";
                    break;
                case (int)OleDbType.Double:
                case (int)OleDbType.Numeric:
                case (int)OleDbType.VarNumeric:
                case (int)OleDbType.Single:
                case (int)OleDbType.Decimal:
                case (int)OleDbType.Currency:
                    result = "float";
                    break;
                case (int)OleDbType.Date:
                case (int)OleDbType.DBDate:
                case (int)OleDbType.DBTimeStamp:
                    result = "datetime";
                    break;
                default:
                    result = "varchar";
                    break;
            }

            return result;
        }


        #region "IDisposable Support"
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    disconnect();
                }
            }
            disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
