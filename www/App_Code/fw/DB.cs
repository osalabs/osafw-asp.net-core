using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace osafw
{

    public class DBRow : Dictionary<string, string> { }
    public class DBList : List<DBRow> { }

    public enum DBOps : int
    {
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
        NOTLIKE,       // NOT LIKE
        BETWEEN        // BETWEEN
    }
    // describes DB operation
    public class DBOperation
    {
        public DBOps op;
        public string opstr; // string value for op
        public bool is_value = true; // if false - operation is unary (no value)
        public object value; // can be array for IN, NOT IN, OR
        public string quoted_value;
        public DBOperation(DBOps op, object value = null)
        {
            this.op = op;
            setOpStr();
            this.value = value;
        }
        public void setOpStr()
        {
            switch (op)
            {
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
        private static Hashtable schemafull_cache; // cache for the full schema, lifetime = app lifetime
        private static Hashtable schema_cache; // cache for the schema, lifetime = app lifetime

        public static int SQL_QUERY_CTR = 0; // counter for SQL queries during request

        private FW fw; // for now only used for: fw.logger and fw.cache (for request-level cacheing of multi-db connections)

        public string db_name = "";
        public string dbtype = "SQL";
        private Hashtable conf = new();  // config contains: connection_string, type
        private string connstr = "";

        private Hashtable schema = new(); // schema for currently connected db
        private DbConnection conn; // actual db connection - SqlConnection or OleDbConnection

        private bool is_check_ole_types = false; // if true - checks for unsupported OLE types during readRow
        private Hashtable UNSUPPORTED_OLE_TYPES = new();

        /// <summary>
        ///  "synax sugar" helper to build Hashtable from list of arguments instead more complex New Hashtable from {...}
        ///  Example: db.row("table", h("id", 123)) => "select * from table where id=123"
        ///  </summary>
        ///  <param name="args">even number of args required</param>
        ///  <returns></returns>
        public static Hashtable h(params object[] args)
        {
            Hashtable result = new();
            if (args.Length == 0) return result;
            if (args.Length % 2 != 0)
            {
                throw new ArgumentException("h() accepts even number of arguments");
            }
            
            for (var i = 0; i <= args.Length - 1; i += 2)
            {
                result[args[i]] = args[i + 1];
            }
            return result;
        }

        /// <summary>
        ///  construct new DB object with
        ///  </summary>
        ///  <param name="fw">framework reference</param>
        ///  <param name="conf">config hashtable with "connection_string" and "type" keys. If none - fw.config("db")("main") used</param>
        ///  <param name="db_name">database human name, only used for logger</param>
        public DB(FW fw, Hashtable conf = null, string db_name = "main")
        {
            this.fw = fw;
            if (conf != null)
            {
                this.conf = conf;
            }
            else
            {
                this.conf = (Hashtable)((Hashtable)fw.config("db"))["main"];
            }
            this.dbtype = (string)this.conf["type"];
            this.connstr = (string)this.conf["connection_string"];

            this.db_name = db_name;

            this.UNSUPPORTED_OLE_TYPES = Utils.qh("DBTYPE_IDISPATCH DBTYPE_IUNKNOWN"); // also? DBTYPE_ARRAY DBTYPE_VECTOR DBTYPE_BYTES
        }

        public void logger(LogLevel level, params object[] args)
        {
            if (args.Length == 0)
                return;
            fw.logger(level, args);
        }

        /// <summary>
        /// connect to DB server using connection string defined in web.config appSettings, key db|main|connection_string (by default)
        /// </summary>
        /// <returns></returns>
        public DbConnection connect()
        {
            var cache_key = "DB#" + connstr;

            // first, try to get connection from request cache (so we will use only one connection per db server - TBD make configurable?)
            if (conn == null)
            {
                conn = (DbConnection)fw.cache.getRequestValue(cache_key);
            }

            // if still no connection - re-make it
            if (conn == null)
            {
                schema = new Hashtable(); // reset schema cache
                conn = createConnection(connstr, (string)conf["type"]);
                fw.cache.setRequestValue(cache_key, conn);
            }

            // if it's disconnected - re-connect
            if (conn.State != ConnectionState.Open)
            {
                conn.Open();
            }

            if (this.dbtype == "OLE")
            {
                is_check_ole_types = true;
            }
            else
            {
                is_check_ole_types = false;
            }

            return conn;
        }

        public void disconnect()
        {
            if (this.conn != null)
            {
                this.conn.Close();
            }
        }

        /// <summary>
        /// return internal connection object
        /// </summary>
        /// <returns></returns>
        public DbConnection getConnection()
        {
            return conn;
        }

        public DbConnection createConnection(string connstr, string dbtype = "SQL")
        {
            DbConnection result;

            if (dbtype == "SQL")
            {
                result = new SqlConnection(connstr);
            }
            else if (dbtype == "OLE" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result = new OleDbConnection(connstr);
            }
            else if (dbtype == "ODBC")
            {
                result = new OdbcConnection(connstr);
            }
            else
            {
                string msg = "Unknown type [" + dbtype + "]";
                logger(LogLevel.FATAL, msg);
                throw new ApplicationException(msg);
            }

            result.Open();
            return result;
        }

        [SupportedOSPlatform("windows")]
        public void check_create_mdb(string filepath)
        {
            if (File.Exists(filepath)) return;

            string connstr = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" + filepath;

            OleDbConnection conn = new();
            conn.ConnectionString = connstr;
            // Exception must be checked in method there check_create_mdb is called.
            conn.Open();
            conn.Close();
        }

        public DbDataReader query(string sql, Hashtable @params = null)
        {
            connect();
            if (@params!=null && @params.Count>0)
                logger(LogLevel.INFO, "DB:", db_name, " ", sql, @params);
            else
                logger(LogLevel.INFO, "DB:", db_name, " ", sql);
          

            SQL_QUERY_CTR += 1;

            DbCommand dbcomm = null;
            if (dbtype == "SQL")
            {
                dbcomm = new SqlCommand(sql, (SqlConnection)conn);
                if (@params != null)
                    foreach (string p in @params.Keys)
                        dbcomm.Parameters.Add(new SqlParameter(p, @params[p]));
            }
            else if (dbtype == "OLE" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                dbcomm = new OleDbCommand(sql, (OleDbConnection)conn);
                if (@params != null)
                    foreach (string p in @params.Keys)
                        dbcomm.Parameters.Add(new OleDbParameter(p, @params[p]));
            }

            DbDataReader dbread = dbcomm.ExecuteReader();
            return dbread;
        }

        // exectute without results (so db reader will be closed), return number of rows affected.
        public int exec(string sql)
        {
            connect();
            logger(LogLevel.INFO, "DB:", db_name, ", SQL QUERY: ", sql);

            SQL_QUERY_CTR += 1;

            DbCommand dbcomm = null;
            if (dbtype == "SQL")
            {
                dbcomm = new SqlCommand(sql, (SqlConnection)conn);
            }
            else if (dbtype == "OLE" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                dbcomm = new OleDbCommand(sql, (OleDbConnection)conn);
            }

            return dbcomm.ExecuteNonQuery();
        }

        //read row values as a strings
        private Hashtable readRow(DbDataReader dbread)
        {
            Hashtable result = new();

            if (dbread.HasRows)
            {
                for (int i = 0; i <= dbread.FieldCount - 1; i++)
                {
                    try
                    {
                        if (is_check_ole_types && UNSUPPORTED_OLE_TYPES.ContainsKey(dbread.GetDataTypeName(i))) continue;

                        string value = dbread[i].ToString();
                        string name = dbread.GetName(i).ToString();
                        result.Add(name, value);
                    }
                    catch (Exception Ex)
                    {
                        break;
                    }
                }
            }

            return result;
        }

        private DBRow readRow2(DbDataReader dbread)
        {
            DBRow result = new();

            for (int i = 0; i <= dbread.FieldCount - 1; i++)
            {
                try
                {
                    if (is_check_ole_types && UNSUPPORTED_OLE_TYPES.ContainsKey(dbread.GetDataTypeName(i))) continue;

                    string value = dbread[i].ToString();
                    string name = dbread.GetName(i).ToString();
                    result.Add(name, value);
                }
                catch (Exception Ex)
                {
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// read single first row using raw sql query
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public Hashtable row(string sql)
        {
            DbDataReader dbread = query(sql);
            dbread.Read();
            var result = readRow(dbread);
            dbread.Close();
            return result;
        }

        /// <summary>
        /// read signle irst row using table/where/orderby
        /// </summary>
        /// <param name="table"></param>
        /// <param name="where"></param>
        /// <param name="order_by"></param>
        /// <returns></returns>
        public Hashtable row(string table, Hashtable where, string order_by = "")
        {
            return row(hash2sql_select(table, where, order_by, "TOP 1 *"));
        }

        /// <summary>
        /// read single first row using parametrized sql query
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="params"></param>
        /// <returns></returns>
        public Hashtable rowp(string sql, Hashtable @params)
        {
            DbDataReader dbread = query(sql, @params);
            dbread.Read();
            var result = readRow(dbread);
            dbread.Close();
            return result;
        }

        public ArrayList readArray(DbDataReader dbread)
        {
            ArrayList result = new();

            while (dbread.Read())
                result.Add(readRow(dbread));

            dbread.Close();
            return result;
        }

        /// <summary>
        /// read all rows using raw query
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public ArrayList array(string sql)
        {
            DbDataReader dbread = query(sql);
            return readArray(dbread);
        }

        public DBList array2(string sql)
        {
            DbDataReader dbread = query(sql);
            DBList a = new();

            while (dbread.Read())
                a.Add(readRow2(dbread));

            dbread.Close();
            return a;
        }

        /// <summary>
        /// read all rows using parametrized query
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="params"></param>
        /// <returns></returns>
        public ArrayList arrayp(string sql, Hashtable @params)
        {
            DbDataReader dbread = query(sql, @params);
            return readArray(dbread);
        }

        /// <summary>
        /// return all rows with all fields from the table based on coditions/order
        /// array("table", where, "id asc", Utils.qh("field1|id field2|iname"))
        /// </summary>
        /// <param name="table">table name</param>
        /// <param name="where">where conditions</param>
        /// <param name="order_by">optional order by, MUST BE QUOTED</param>
        /// <param name="aselect_fields">optional select fields array or hashtable(for aliases) or arraylist of hashtable("field"=>,"alias"=> for cases if there could be several same fields with diff aliases), if not set * returned</param>
        /// <returns></returns>
        public ArrayList array(string table, Hashtable where, string order_by = "", ICollection aselect_fields = null)
        {
            string select_fields = "*";
            if (aselect_fields != null)
            {
                ArrayList quoted = new();
                if (aselect_fields is ArrayList)
                {
                    // arraylist of hashtables with "field","alias" keys - usable for the case when we need same field to be selected more than once with different aliases
                    foreach (Hashtable asf in aselect_fields)
                    {
                        quoted.Add(this.q_ident((string)asf["field"]) + " as " + this.q_ident((string)asf["alias"]));
                    }
                }
                else if (aselect_fields is IDictionary)
                {
                    foreach (string field in (aselect_fields as IDictionary).Keys)
                    {
                        quoted.Add(this.q_ident(field) + " as " + this.q_ident((string)(aselect_fields as IDictionary)[field]));// field as alias
                    }
                }
                else
                {
                    foreach (string field in aselect_fields)
                    {
                        quoted.Add(this.q_ident(field));
                    }
                    select_fields = quoted.Count > 0 ? string.Join(", ", quoted.ToArray()) : "*";
                }
            }

            return array(hash2sql_select(table, where, order_by, select_fields));
        }

        /// <summary>
        /// read column helper
        /// </summary>
        /// <param name="dbread"></param>
        /// <returns></returns>
        public ArrayList readCol(DbDataReader dbread)
        {
            ArrayList result = new();
            while (dbread.Read())
                result.Add(dbread[0].ToString());

            dbread.Close();
            return result;
        }

        /// <summary>
        /// return just first column values (strings!) as arraylist using raw sql query
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public ArrayList col(string sql)
        {
            DbDataReader dbread = query(sql);
            return readCol(dbread);
        }

        /// <summary>
        /// read first column using parametrized query
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="params"></param>
        /// <returns></returns>
        public ArrayList colp(string sql, Hashtable @params)
        {
            DbDataReader dbread = query(sql, @params);
            return readCol(dbread);
        }

        /// <summary>
        /// return just one column values as arraylist
        /// </summary>
        /// <param name="table">table name</param>
        /// <param name="where">where conditions</param>
        /// <param name="field_name">optional field name, if empty - first field returned</param>
        /// <param name="order_by">optional order by (MUST be quoted)</param>
        /// <returns></returns>
        public ArrayList col(string table, Hashtable where, string field_name, string order_by = "")
        {
            if (field_name == null) field_name = "";

            if (string.IsNullOrEmpty(field_name))
            {
                field_name = "*";
            }
            else
            {
                field_name = q_ident(field_name);
            }
            return col(hash2sql_select(table, where, order_by, field_name));
        }

        public object readValue(DbDataReader dbread)
        {
            object result = null;

            while (dbread.Read())
            {
                result = dbread[0]; //read first
                break; // just return first row
            }

            dbread.Close();
            return result;
        }

        // return just first value from column
        // NOTE, not string, but db type
        public object value(string sql)
        {
            DbDataReader dbread = query(sql);
            return readValue(dbread);
        }

        // return just first value from column
        public object valuep(string sql, Hashtable @params)
        {
            DbDataReader dbread = query(sql, @params);
            return readValue(dbread);
        }

        /// <summary>
        /// Return just one field value:
        /// value("table", where)
        /// value("table", where, "field1")
        /// value("table", where, "1") 'just return 1, useful for exists queries
        /// value("table", where, "count(*)", "id asc")
        /// </summary>
        /// <param name="table"></param>
        /// <param name="where"></param>
        /// <param name="field_name">field name, special cases: "1", "count(*)"</param>
        /// <param name="order_by"></param>
        /// <returns></returns>
        public object value(string table, Hashtable where, string field_name, string order_by = "")
        {
            if (field_name == null) field_name = "";

            if (string.IsNullOrEmpty(field_name))
            {
                field_name = "*";
            }
            else if (field_name == "count(*)" || field_name == "1")
            {
            }
            else
            {
                field_name = q_ident(field_name);
            }
            return value(hash2sql_select(table, where, order_by, field_name));
        }

        // string will be Left(RTrim(str),length)
        public string left(string str, int length)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.TrimStart().Substring(0, length);
        }

        // create "IN (1,2,3)" sql or IN (NULL) if empty params passed
        // examples:
        // where = " field "& db.insql("a,b,c,d")
        // where = " field "& db.insql(string())
        // where = " field "& db.insql(ArrayList)
        public string insql(string parameters)
        {
            return insql(parameters.Split(","));
        }
        public string insql(IList parameters)
        {
            ArrayList result = new();
            foreach (string param in parameters)
            {
                result.Add(this.q(param));
            }
            return " IN (" + (result.Count > 0 ? string.Join(", ", result.ToArray()) : "NULL") + ")";
        }

        // same as insql, but for quoting numbers - uses qi() 
        public string insqli(string parameters)
        {
            return insqli(parameters.Split(","));
        }

        public string insqli(IList parameters)
        {
            ArrayList result = new();
            foreach (string param in parameters)
            {
                result.Add(this.qi(param));
            }
            return " IN (" + (result.Count > 0 ? string.Join(", ", result.ToArray()) : "NULL") + ")";
        }

        // quote identifier: table => [table]
        public string q_ident(string str)
        {
            if (str == null) str = "";

            str = str.Replace("[", "");
            str = str.Replace("]", "");
            return "[" + str + "]";
        }

        public string q(object str, int length = 0)
        {
            return q((string)str, length);
        }

        // if length defined - string will be Left(Trim(str),length) before quoted
        public string q(string str, int length = 0)
        {
            if (str == null) str = "";

            if (length > 0)
            {
                str = this.left(str, length);
            }
            return "'" + str.Replace("'", "''") + "'";
        }

        // simple just replace quotes, don't add start/end single quote - for example, for use with LIKE
        public string qq(string str)
        {
            if (str == null) str = "";

            return str.Replace("'", "''");
        }

        // simple quote as Integer Value
        public int qi(object str)
        {
            return Utils.f2int(str);
        }

        // simple quote as Float Value
        public double qf(object str)
        {
            return Utils.f2float(str);
        }

        // simple quote as Date Value
        public string qd(object str)
        {
            string result;
            if (dbtype == "SQL")
            {
                DateTime tmpdate;
                if (DateTime.TryParse(str.ToString(), out tmpdate))
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
                result = Regex.Replace(str.ToString(), @"['""\]\[]", "");
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

        public Hashtable quote(string table, Hashtable fields)
        {
            connect();
            load_table_schema(table);
            if (!schema.ContainsKey(table))
            {
                throw new ApplicationException("table [" + table + "] does not defined in FW.config(\"schema\")");
            }

            Hashtable fieldsq = new();

            foreach (string k in fields.Keys)
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

        // can return string or DBOperation class
        public object qone(string table, string field_name, object field_value_or_op)
        {
            connect();
            load_table_schema(table);
            field_name = field_name.ToLower();
            Hashtable schema_table = (Hashtable)schema[table];
            if (!schema_table.ContainsKey(field_name))
            {
                throw new ApplicationException("field " + table + "." + field_name + " does not defined in FW.config(\"schema\") ");
            }

            object field_value;
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

            string field_type = (string)schema_table[field_name];
            string quoted;
            if (dbop != null)
            {
                if (dbop.op == DBOps.IN || dbop.op == DBOps.NOTIN)
                {
                    if (dbop.value != null && (dbop.value) is IList)
                    {
                        ArrayList result = new();
                        foreach (object param in (ArrayList)dbop.value)
                        {
                            result.Add(qone_by_type(field_type, param));
                        }
                        quoted = "(" + (result.Count > 0 ? string.Join(", ", result.ToArray()) : "NULL") + ")";
                    }
                    else
                    {
                        quoted = qone_by_type(field_type, field_value);
                    }
                }
                else if (dbop.op == DBOps.BETWEEN)
                {
                    ArrayList values = (ArrayList)dbop.value;
                    quoted = qone_by_type(field_type, (string)values[0]) + " AND " + qone_by_type(field_type, (string)values[1]);
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

        public string qone_by_type(string field_type, object field_value)
        {
            string quoted;

            // if value set to Nothing or DBNull - assume it's NULL in db
            if (field_value == null || field_value == DBNull.Value)
            {
                quoted = "NULL";
            }
            else
            {
                // fw.logger(table & "." & field_name & " => " & field_type & ", value=[" & field_value & "]")
                if (Regex.IsMatch(field_type, "int"))
                {
                    if (field_value != null && field_value is string @string)
                    {
                        if (Regex.IsMatch(@string, "true", RegexOptions.IgnoreCase))
                        {
                            quoted = "1";
                        }
                        else if (Regex.IsMatch(@string, "false", RegexOptions.IgnoreCase))
                        {
                            quoted = "0";
                        }
                        else if (@string == "")
                        {
                            // if empty string for numerical field - assume NULL
                            quoted = "NULL";
                        }
                        else
                        {
                            quoted = Utils.f2int(field_value).ToString();
                        }
                    }
                    else
                    {
                        quoted = Utils.f2int(field_value).ToString();
                    }
                }
                else if (field_type == "datetime")
                {
                    quoted = this.qd(field_value);
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
                        quoted = Regex.Replace((string)field_value, @"\\(\r\n?)", @"\\$1$1");
                        quoted = Regex.Replace(quoted, "'", "''"); // escape single quotes
                        quoted = "'" + quoted + "'";
                    }
                }
            }
            return quoted;
        }

        // operations support for non-raw sql methods

        /// <summary>
        ///  NOT EQUAL operation 
        ///  Example: Dim rows = db.array("users", New Hashtable From {{"status", db.opNOT(127)}})
        ///  <![CDATA[ select * from users where status<>127 ]]>
        ///  </summary>
        ///  <param name="value"></param>
        ///  <returns></returns>
        public DBOperation opNOT(object value)
        {
            return new DBOperation(DBOps.NOT, value);
        }

        /// <summary>
        ///  LESS or EQUAL than operation
        ///  Example: Dim rows = db.array("users", New Hashtable From {{"access_level", db.opLE(50)}})
        ///  <![CDATA[ select * from users where access_level<=50 ]]>
        ///  </summary>
        ///  <param name="value"></param>
        ///  <returns></returns>
        public DBOperation opLE(object value)
        {
            return new DBOperation(DBOps.LE, value);
        }

        /// <summary>
        ///  LESS THAN operation
        ///  Example: Dim rows = db.array("users", New Hashtable From {{"access_level", db.opLT(50)}})
        ///  <![CDATA[ select * from users where access_level<50 ]]>
        ///  </summary>
        ///  <param name="value"></param>
        ///  <returns></returns>
        public DBOperation opLT(object value)
        {
            return new DBOperation(DBOps.LT, value);
        }

        /// <summary>
        ///  GREATER or EQUAL than operation
        ///  Example: Dim rows = db.array("users", New Hashtable From {{"access_level", db.opGE(50)}})
        ///  <![CDATA[ select * from users where access_level>=50 ]]>
        ///  </summary>
        ///  <param name="value"></param>
        ///  <returns></returns>
        public DBOperation opGE(object value)
        {
            return new DBOperation(DBOps.GE, value);
        }

        /// <summary>
        ///  GREATER THAN operation
        ///  Example: Dim rows = db.array("users", New Hashtable From {{"access_level", db.opGT(50)}})
        ///  <![CDATA[ select * from users where access_level>50 ]]>
        ///  </summary>
        ///  <param name="value"></param>
        ///  <returns></returns>
        public DBOperation opGT(object value)
        {
            return new DBOperation(DBOps.GT, value);
        }

        /// <summary>
        ///  Example: Dim rows = db.array("users", New Hashtable From {{"field", db.opISNULL()}})
        ///  select * from users where field IS NULL
        ///  </summary>
        ///  <returns></returns>
        public DBOperation opISNULL()
        {
            return new DBOperation(DBOps.ISNULL);
        }
        /// <summary>
        ///  Example: Dim rows = db.array("users", New Hashtable From {{"field", db.opISNOTNULL()}})
        ///  select * from users where field IS NOT NULL
        ///  </summary>
        ///  <returns></returns>
        public DBOperation opISNOTNULL()
        {
            return new DBOperation(DBOps.ISNOTNULL);
        }
        /// <summary>
        ///  Example: Dim rows = DB.array("users", New Hashtable From {{"address1", db.opLIKE("%Orlean%")}})
        ///  select * from users where address1 LIKE '%Orlean%'
        ///  </summary>
        ///  <param name="value"></param>
        ///  <returns></returns>
        public DBOperation opLIKE(object value)
        {
            return new DBOperation(DBOps.LIKE, value);
        }
        /// <summary>
        ///  Example: Dim rows = DB.array("users", New Hashtable From {{"address1", db.opNOTLIKE("%Orlean%")}})
        ///  select * from users where address1 NOT LIKE '%Orlean%'
        ///  </summary>
        ///  <param name="value"></param>
        ///  <returns></returns>
        public DBOperation opNOTLIKE(object value)
        {
            return new DBOperation(DBOps.NOTLIKE, value);
        }

        /// <summary>
        ///  2 ways to call:
        ///  opIN(1,2,4) - as multiple arguments
        ///  opIN(array) - as one array of values
        ///  
        ///  Example: Dim rows = db.array("users", New Hashtable From {{"id", db.opIN(1, 2)}})
        ///  select * from users where id IN (1,2)
        ///  </summary>
        ///  <param name="args"></param>
        ///  <returns></returns>
        public DBOperation opIN(params object[] args)
        {
            object values;
            if (args.Length == 1 && (args[0].GetType().IsArray) || (args[0] is IList))
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
        ///  2 ways to call:
        ///  opIN(1,2,4) - as multiple arguments
        ///  opIN(array) - as one array of values
        ///  
        ///  Example: Dim rows = db.array("users", New Hashtable From {{"id", db.opNOTIN(1, 2)}})
        ///  select * from users where id NOT IN (1,2)
        ///  </summary>
        ///  <param name="args"></param>
        ///  <returns></returns>
        public DBOperation opNOTIN(params object[] args)
        {
            object values;
            if (args.Length == 1 && (args[0].GetType().IsArray) || (args[0] is IList))
            {
                values = args[0];
            }
            else
            {
                values = args;
            }
            return new DBOperation(DBOps.NOTIN, values);
        }

        /// <summary>
        ///  Example: Dim rows = db.array("users", New Hashtable From {{"field", db.opBETWEEN(10,20)}})
        ///  select * from users where field BETWEEN 10 AND 20
        ///  </summary>
        ///  <returns></returns>
        public DBOperation opBETWEEN(object from_value, object to_value)
        {
            return new DBOperation(DBOps.BETWEEN, new object[] { from_value, to_value });
        }

        // return last inserted id
        public int insert(string table, Hashtable fields)
        {
            if (fields.Count < 1) return 0;

            exec(hash2sql_i(table, fields));

            object insert_id;

            if (dbtype == "SQL")
            {
                insert_id = value("SELECT SCOPE_IDENTITY() AS [SCOPE_IDENTITY] ");
            }
            else if (dbtype == "OLE")
            {
                insert_id = value("SELECT @@identity");
            }
            else
            {
                throw new ApplicationException("Get last insert ID for DB type [" + dbtype + "] not implemented");
            }

            // if table doesn't have identity insert_id would be DBNull
            if (insert_id == DBNull.Value)
            {
                insert_id = 0;
            }

            return Utils.f2int(insert_id);
        }

        public int update(string sql)
        {
            return exec(sql);
        }

        public int update(string table, Hashtable fields, Hashtable where)
        {
            return exec(hash2sql_u(table, fields, where));
        }

        // retrun number of affected rows
        public int update_or_insert(string table, Hashtable fields, Hashtable where)
        {
            // merge fields and where
            Hashtable allfields = new();
            foreach (string k in fields.Keys)
            {
                allfields[k] = fields[k];
            }

            foreach (string k in where.Keys)
            {
                allfields[k] = where[k];
            }

            string update_sql = hash2sql_u(table, fields, where);
            string insert_sql = hash2sql_i(table, allfields);
            string full_sql = update_sql + "  IF @@ROWCOUNT = 0 " + insert_sql;

            return exec(full_sql);
        }

        // retrun number of affected rows
        public int del(string table, Hashtable where)
        {
            return exec(hash2sql_d(table, where));
        }

        // join key/values with quoting values according to table
        // h - already quoted! values
        // kv_delim = pass "" to autodetect " = " or " IS " (for NULL values)
        public string _join_hash(Hashtable h, string kv_delim, string pairs_delim)
        {
            string res = "";
            if (h.Count < 1) return res;

            string[] ar = new string[h.Count - 1 + 1];

            int i = 0;
            foreach (string k in h.Keys)
            {
                var vv = h[k];
                string v = "";
                var delim = kv_delim;
                if (string.IsNullOrEmpty(delim))
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
                        v = (string)vv;
                        if ((string)vv == "NULL")
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
                    v = (string)vv;
                }
                ar[i] = k + delim + v;
                i += 1;
            }
            res = string.Join(pairs_delim, ar);
            return res;
        }

        /// <summary>
        ///  build SELECT sql string
        ///  </summary>
        ///  <param name="table">table name</param>
        ///  <param name="where">where conditions</param>
        ///  <param name="order_by">optional order by string</param>
        ///  <param name="select_fields">MUST already be quoted!</param>
        ///  <returns></returns>
        private string hash2sql_select(string table, Hashtable where, string order_by = "", string select_fields = "*")
        {
            where = quote(table, where);
            // FW.logger(where)
            string where_string = _join_hash(where, "", " AND ");
            if (where_string.Length > 0)
            {
                where_string = " WHERE " + where_string;
            }

            string sql = "SELECT " + select_fields + " FROM " + q_ident(table) + " " + where_string;
            if (order_by.Length > 0)
            {
                sql = sql + " ORDER BY " + order_by;
            }
            return sql;
        }

        public string hash2sql_u(string table, Hashtable fields, Hashtable where)
        {
            fields = quote(table, fields);
            where = quote(table, where);

            string update_string = _join_hash(fields, "=", ", ");
            string where_string = _join_hash(where, "", " AND ");

            if (where_string.Length > 0)
                where_string = " WHERE " + where_string;

            string sql = "UPDATE " + q_ident(table) + " " + " SET " + update_string + where_string;

            return sql;
        }

        private string hash2sql_i(string table, Hashtable fields)
        {
            fields = quote(table, fields);

            string[] ar = new string[fields.Count - 1 + 1];

            fields.Keys.CopyTo(ar, 0);
            string names_string = string.Join(", ", ar);

            fields.Values.CopyTo(ar, 0);
            string values_string = string.Join(", ", ar);
            string sql = "INSERT INTO " + q_ident(table) + " (" + names_string + ") VALUES (" + values_string + ")";
            return sql;
        }

        private string hash2sql_d(string table, Hashtable where)
        {
            where = quote(table, where);
            string where_string = _join_hash(where, "", " AND ");
            if (where_string.Length > 0)
                where_string = " WHERE " + where_string;

            string sql = "DELETE FROM " + q_ident(table) + " " + where_string;
            return sql;
        }

        // return array of table names in current db
        public ArrayList tables()
        {
            ArrayList result = new();

            DbConnection conn = this.connect();
            DataTable dataTable = conn.GetSchema("Tables");
            foreach (DataRow row in dataTable.Rows)
            {
                // fw.logger("************ TABLE" & row("TABLE_NAME"))
                // For Each cl As DataColumn In dataTable.Columns
                // fw.logger(cl.Tostring & " = " & row(cl))
                // Next

                // skip any system tables or views (VIEW, ACCESS TABLE, SYSTEM TABLE)
                if ((string)row["TABLE_TYPE"] != "TABLE" && (string)row["TABLE_TYPE"] != "BASE TABLE" && (string)row["TABLE_TYPE"] != "PASS-THROUGH")
                    continue;
                string tblname = row["TABLE_NAME"].ToString();
                result.Add(tblname);
            }

            return result;
        }

        // return array of view names in current db
        public ArrayList views()
        {
            ArrayList result = new();

            DbConnection conn = this.connect();
            DataTable dataTable = conn.GetSchema("Tables");
            foreach (DataRow row in dataTable.Rows)
            {
                // skip non-views
                if (row["TABLE_TYPE"].ToString() != "VIEW") continue;

                string tblname = row["TABLE_NAME"].ToString();
                result.Add(tblname);
            }

            return result;
        }

        public string schema_field_type(string table, string field_name)
        {
            connect();
            load_table_schema(table);
            field_name = field_name.ToLower();
            if (!((Hashtable)schema[table]).ContainsKey(field_name))
                return "";
            string field_type = (string)((Hashtable)schema[table])[field_name];

            string result;
            if (Regex.IsMatch(field_type, "int"))
                result = "int";
            else if (field_type == "datetime")
                result = field_type;
            else if (field_type == "float")
                result = field_type;
            else
                result = "varchar";

            return result;
        }

        public ArrayList load_table_schema_full(string table)
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

            if (((Hashtable)schemafull_cache[connstr]).ContainsKey(table))
            {
                return (ArrayList)((Hashtable)schemafull_cache[connstr])[table];
            }

            // cache miss
            ArrayList result = new();
            if (dbtype == "SQL")
            {
                // fw.logger("cache MISS " & current_db & "." & table)
                // get information about all columns in the table
                // default = ((0)) ('') (getdate())
                // maxlen = -1 for nvarchar(MAX)
                string sql = "SELECT c.column_name as 'name'," + 
                    " c.data_type as 'type'," + 
                    " CASE c.is_nullable WHEN 'YES' THEN 1 ELSE 0 END AS 'is_nullable'," + 
                    " c.column_default as 'default'," + 
                    " c.character_maximum_length as 'maxlen'," + 
                    " c.numeric_precision," + 
                    " c.numeric_scale," + 
                    " c.character_set_name as 'charset'," + 
                    " c.collation_name as 'collation'," + 
                    " c.ORDINAL_POSITION as 'pos'," + 
                    " COLUMNPROPERTY(object_id(c.table_name), c.column_name, 'IsIdentity') as is_identity" + 
                    " FROM INFORMATION_SCHEMA.TABLES t," + 
                    "   INFORMATION_SCHEMA.COLUMNS c" + 
                    " WHERE t.table_name = c.table_name" +
                    "   AND t.table_name = @table_name"+ 
                    " order by c.ORDINAL_POSITION";
                result = arrayp(sql, DB.h("@table_name", table));
                foreach (Hashtable row in result)
                {
                    row["fw_type"] = map_mssqltype2fwtype((string)row["type"]); // meta type
                    row["fw_subtype"] = ((string)row["type"]).ToLower();
                }
            }
            else if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // OLE DB (Access)
                DataTable schemaTable = ((OleDbConnection)conn).GetOleDbSchemaTable(OleDbSchemaGuid.Columns, new object[] { null, null, table, null });

                List<Hashtable> fieldslist = new();
                foreach (DataRow row in schemaTable.Rows)
                {
                    // unused:
                    // COLUMN_HASDEFAULT True False
                    // COLUMN_FLAGS   74 86 90(auto) 102 106 114 122(date) 130 226 230 234
                    // CHARACTER_OCTET_LENGTH
                    // DATETIME_PRECISION=0
                    // DESCRIPTION
                    var h = new Hashtable();
                    h["name"] = row["COLUMN_NAME"].ToString();
                    h["type"] = row["DATA_TYPE"];
                    h["fw_type"] = map_oletype2fwtype((int)row["DATA_TYPE"]); // meta type
                    h["fw_subtype"] = ((string)Enum.GetName(typeof(OleDbType), row["DATA_TYPE"])).ToLower(); // exact type as string
                    h["is_nullable"] = (bool)row["IS_NULLABLE"] ? 1 : 0;
                    h["default"] = row["COLUMN_DEFAULT"]; // "=Now()" "0" "No"
                    h["maxlen"] = row["CHARACTER_MAXIMUM_LENGTH"];
                    h["numeric_precision"] = row["NUMERIC_PRECISION"];
                    h["numeric_scale"] = row["NUMERIC_SCALE"];
                    h["charset"] = row["CHARACTER_SET_NAME"];
                    h["collation"] = row["COLLATION_NAME"];
                    h["pos"] = row["ORDINAL_POSITION"];
                    h["is_identity"] = 0;
                    h["desc"] = row["DESCRIPTION"];
                    h["column_flags"] = row["COLUMN_FLAGS"];
                    fieldslist.Add(h);
                }
                // order by ORDINAL_POSITION

                result.AddRange((from Hashtable h in fieldslist orderby ((Hashtable)h["pos"]) ascending select h).ToList());

                // now detect identity (because order is important)
                foreach (Hashtable h in result)
                {
                    // actually this also triggers for Long Integers, so for now - only first field that match conditions will be an identity
                    if ((int)h["type"] == (int)OleDbType.Integer && (int)h["column_flags"] == 90)
                    {
                        h["is_identity"] = 1;
                        break;
                    }
                }
            }

            // save to cache
            ((Hashtable)schemafull_cache[connstr])[table] = result;

            return result;
        }

        // return database foreign keys, optionally filtered by table (that contains foreign keys)
        public ArrayList get_foreign_keys(string table = "")
        {
            ArrayList result = new();
            if (dbtype == "SQL")
            {
                var where = "";
                var where_params = new Hashtable();
                if (table != "")
                {
                    where = " WHERE col1.TABLE_NAME=@table_name";
                    where_params["@table_name"] = table;
                }
                result = this.arrayp("SELECT " + 
                    " col1.CONSTRAINT_NAME as [name]" + 
                    ", col1.TABLE_NAME As [table]" + 
                    ", col1.COLUMN_NAME as [column]" + 
                    ", col2.TABLE_NAME as [pk_table]" + 
                    ", col2.COLUMN_NAME as [pk_column]" + 
                    ", rc.UPDATE_RULE as [on_update]" + 
                    ", rc.DELETE_RULE as [on_delete]" + 
                    " FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc " + 
                    " INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE col1 " + 
                    "   ON (col1.CONSTRAINT_CATALOG = rc.CONSTRAINT_CATALOG  " + 
                    "       AND col1.CONSTRAINT_SCHEMA = rc.CONSTRAINT_SCHEMA " + 
                    "       AND col1.CONSTRAINT_NAME = rc.CONSTRAINT_NAME)" + 
                    " INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE col2 " + 
                    "   ON (col2.CONSTRAINT_CATALOG = rc.UNIQUE_CONSTRAINT_CATALOG  " + 
                    "       AND col2.CONSTRAINT_SCHEMA = rc.UNIQUE_CONSTRAINT_SCHEMA " + 
                    "       AND col2.CONSTRAINT_NAME = rc.UNIQUE_CONSTRAINT_NAME " + 
                    "       AND col2.ORDINAL_POSITION = col1.ORDINAL_POSITION)" + 
                    where, where_params);
            }
            else if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var dt = ((OleDbConnection)conn).GetOleDbSchemaTable(System.Data.OleDb.OleDbSchemaGuid.Foreign_Keys, new object[] { null });
                foreach (DataRow row in dt.Rows)
                {
                    if (table != "" && (string)row["FK_TABLE_NAME"] != table)
                        continue;

                    result.Add(new Hashtable()
                    {
                        {"table", row["FK_TABLE_NAME"]},
                        {"column", row["FK_COLUMN_NAME"]},
                        {"name", row["FK_NAME"]},
                        {"pk_table", row["PK_TABLE_NAME"]},
                        {"pk_column", row["PK_COLUMN_NAME"]},
                        {"on_update", row["UPDATE_RULE"]},
                        {"on_delete", row["DELETE_RULE"]}
                    });
                }
            }

            return result;
        }

        // load table schema from db
        public Hashtable load_table_schema(string table)
        {
            // for non-MSSQL schemas - just use config schema for now - TODO
            if (dbtype != "SQL" && dbtype != "OLE")
            {
                if (schema.Count == 0)
                {
                    schema = (Hashtable)conf["schema"];
                }
                return null;
            }

            // check if schema already there
            if (schema.ContainsKey(table))
            {
                return (Hashtable)schema[table];
            }

            if (schema_cache == null)
            {
                schema_cache = new();
            }
            if (!schema_cache.ContainsKey(connstr))
            {
                schema_cache[connstr] = new Hashtable();
            }
            if (!((Hashtable)schema_cache[connstr]).ContainsKey(table))
            {
                Hashtable h = new();

                ArrayList fields = load_table_schema_full(table);
                foreach (Hashtable row in fields)
                    h[row["name"].ToString().ToLower()] = row["fw_type"];

                schema[table] = h;
                ((Hashtable)schema_cache[connstr])[table] = h;
            }
            else
            {
                // fw.logger("schema_cache HIT " & current_db & "." & table)
                schema[table] = ((Hashtable)schema_cache[connstr])[table];
            }

            return (Hashtable)schema[table];
        }

        public void clear_schema_cache()
        {
            if (schemafull_cache != null)
                schemafull_cache.Clear();
            if (schema_cache != null)
                schema_cache.Clear();
            if (schema != null)
                schema.Clear();
        }

        private string map_mssqltype2fwtype(string mstype)
        {
            string result;
            switch (mstype.ToLower())
            {
                case "tinyint":
                case "smallint":
                case "int":
                case "bigint":
                case "bit":
                    {
                        result = "int";
                        break;
                    }

                case "real":
                case "numeric":
                case "decimal":
                case "money":
                case "smallmoney":
                case "float":
                    {
                        result = "float";
                        break;
                    }

                case "datetime":
                case "datetime2":
                case "date":
                case "smalldatetime":
                    {
                        result = "datetime";
                        break;
                    }

                default:
                    {
                        result = "varchar";
                        break;
                    }
            }

            return result;
        }

        [SupportedOSPlatform("windows")]
        private string map_oletype2fwtype(int mstype)
        {
            string result = "";
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
                default: // "text", "ntext", "varchar", "longvarchar" "nvarchar", "char", "nchar", "wchar", "varwchar", "longvarwchar", "dbtime":
                    result = "varchar";
                    break;
            }

            return result;
        }

        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    this.disconnect();
            }
            disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }

}
