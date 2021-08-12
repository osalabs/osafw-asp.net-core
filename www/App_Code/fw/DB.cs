using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;

namespace osafw
{
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
        public String opstr; // String value for op
        public bool is_value = true; // if false - operation is unary (no value)
        public object value; // can be array for IN, NOT IN, OR
        public String quoted_value;
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

        public String db_name = "";
        public String dbtype = "SQL";
        private Hashtable conf = new Hashtable();  // config contains: connection_String, type
        private String connstr = "";

        private Hashtable schema = new Hashtable(); // schema for currently connected db
        private DbConnection conn; // actual db connection - SqlConnection or OleDbConnection

        private bool is_check_ole_types = false; // if true - checks for unsupported OLE types during readRow
        private Hashtable UNSUPPORTED_OLE_TYPES = new Hashtable();

        /// <summary>
        ///  "synax sugar" helper to build Hashtable from list of arguments instead more complex New Hashtable from {...}
        ///  Example: db.row("table", h("id", 123)) => "select * from table where id=123"
        ///  </summary>
        ///  <param name="args">even number of args required</param>
        ///  <returns></returns>
        public static Hashtable h(params object[] args)
        {
            if (args.Length == 0 || args.Length % 2 != 0)
            {
                throw new ArgumentException("h() accepts even number of arguments");
            }
            Hashtable result = new Hashtable();
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
        ///  <param name="conf">config hashtable with "connection_String" and "type" keys. If none - fw.config("db")("main") used</param>
        ///  <param name="db_name">database human name, only used for logger</param>
        public DB(FW fw, Hashtable conf = null/* TODO Change to default(_) if this is not a reference type */, String db_name = "main")
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
            this.dbtype = (String)this.conf["type"];
            this.connstr = (String)this.conf["connection_String"];

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
        /// connect to DB server using connection String defined in web.config appSettings, key db|main|connection_String (by default)
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
                conn = createConnection(connstr, (String)conf["type"]);
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

        public DbConnection createConnection(String connstr, String dbtype = "SQL")
        {
            DbConnection result;

            if (dbtype == "SQL")
            {
                result = new SqlConnection(connstr);
            }
            else if (dbtype == "OLE")
            {
                result = new OleDbConnection(connstr);
            }
            else if (dbtype == "ODBC")
            {
                result = new OdbcConnection(connstr);
            }
            else
            {
                String msg = "Unknown type [" + dbtype + "]";
                logger(LogLevel.FATAL, msg);
                throw new ApplicationException(msg);
            }

            result.Open();
            return result;
        }

        public void check_create_mdb(String filepath)
        {
            if (File.Exists(filepath))return;

            String connstr = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" + filepath;

            // TODO migrate
            /*object cat = Interaction.CreateObject("ADOX.Catalog");
            cat.Create(connstr);
            cat.ActiveConnection.Close();*/
        }

        public DbDataReader query(String sql)
        {
            connect();
            logger(LogLevel.INFO, "DB:", db_name, " ", sql);

            SQL_QUERY_CTR += 1;

            DbCommand dbcomm = null/* TODO Change to default(_) if this is not a reference type */;
            if (dbtype == "SQL")
            {
                dbcomm = new SqlCommand(sql, (SqlConnection)conn);
            }
            else if (dbtype == "OLE")
            {
                dbcomm = new OleDbCommand(sql, (OleDbConnection)conn);
            }

            DbDataReader dbread = dbcomm.ExecuteReader();
            return dbread;
        }

        // exectute without results (so db reader will be closed), return number of rows affected.
        public int exec(String sql)
        {
            connect();
            logger(LogLevel.INFO, "DB:", db_name, ", SQL QUERY: ", sql);

            SQL_QUERY_CTR += 1;

            DbCommand dbcomm = null/* TODO Change to default(_) if this is not a reference type */;
            if (dbtype == "SQL")
            {
                dbcomm = new SqlCommand(sql, (SqlConnection)conn);
            }
            else if (dbtype == "OLE")
            {
                dbcomm = new OleDbCommand(sql, (OleDbConnection)conn);
            }

            return dbcomm.ExecuteNonQuery();
        }

        private Hashtable readRow(DbDataReader dbread)
        {
            Hashtable result = new Hashtable();

            for (int i = 0; i <= dbread.FieldCount - 1; i++)
            {
                try
                {
                    if (is_check_ole_types && UNSUPPORTED_OLE_TYPES.ContainsKey(dbread.GetDataTypeName(i)))continue;

                    String value = dbread[i].ToString();
                    String name = dbread.GetName(i).ToString();
                    result.Add(name, value);
                }
                catch (Exception Ex)
                {
                    break;
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
                h = readRow(dbread);

            dbread.Close();
            return h;
        }

        public Hashtable row(String table, Hashtable where, String order_by = "")
        {
            return row(hash2sql_select(table, where, ref order_by));
        }

        public ArrayList array(String sql)
        {
            DbDataReader dbread = query(sql);
            ArrayList a = new ArrayList();

            while (dbread.Read())
                a.Add(readRow(dbread));

            dbread.Close();
            return a;
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
        public ArrayList array(String table, Hashtable where, ref String order_by, ICollection aselect_fields = null/* TODO Change to default(_) if this is not a reference type */)
        {
            if (order_by == null) order_by = "";

            String select_fields = "*";
            if (aselect_fields != null)
            {
                ArrayList quoted = new ArrayList();
                if (aselect_fields is ArrayList)
                {
                    // arraylist of hashtables with "field","alias" keys - usable for the case when we need same field to be selected more than once with different aliases
                    foreach (Hashtable asf in aselect_fields)
                    {
                        quoted.Add(this.q_ident((String)asf["field"]) + " as " + this.q_ident((String)asf["alias"]));
                    }
                }
                else if (aselect_fields is IDictionary)
                {
                    foreach (String field in (aselect_fields as IDictionary).Keys)
                    {
                        quoted.Add(this.q_ident(field) + " as " + this.q_ident((String)(aselect_fields as IDictionary)[field]));// field as alias
                    }
                }
                else
                {
                    foreach (String field in aselect_fields)
                    {
                        quoted.Add(this.q_ident(field));
                    }
                    select_fields = quoted.Count > 0 ? String.Join(", ", quoted.ToArray()) : "*";
                }
            }

            return array(hash2sql_select(table, where, ref order_by, select_fields));
        }

        // return just first column values as arraylist
        public ArrayList col(String sql)
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

        /// <summary>
        /// return just one column values as arraylist
        /// </summary>
        /// <param name="table">table name</param>
        /// <param name="where">where conditions</param>
        /// <param name="field_name">optional field name, if empty - first field returned</param>
        /// <param name="order_by">optional order by (MUST be quoted)</param>
        /// <returns></returns>
        public ArrayList col(String table, Hashtable where, String field_name, ref String order_by)
        {
            if (field_name == null) field_name = "";
            if (order_by == null) order_by = "";

            if (String.IsNullOrEmpty(field_name))
            {
                field_name = "*";
            }
            else
            {
                field_name = q_ident(field_name);
            }
            return col(hash2sql_select(table, where, ref order_by, field_name));
        }

        // return just first value from column
        public object value(String sql)
        {
            DbDataReader dbread = query(sql);
            object result = null;

            while (dbread.Read())
            {
                result = dbread[0];
                break; // just return first row
            }

            dbread.Close();
            return result;
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
        public object value(String table, Hashtable where, String field_name, ref String order_by)
        {
            if (field_name == null) field_name = "";
            if (order_by == null) order_by = "";

            if (String.IsNullOrEmpty(field_name))
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
            return value(hash2sql_select(table, where, ref order_by, field_name));
        }

        // String will be Left(RTrim(str),length)
        public String left(String str, int length)
        {
            if (String.IsNullOrEmpty(str)) return "";
            return str.TrimStart().Substring(0, length);
        }

        // create "IN (1,2,3)" sql or IN (NULL) if empty params passed
        // examples:
        // where = " field "& db.insql("a,b,c,d")
        // where = " field "& db.insql(String())
        // where = " field "& db.insql(ArrayList)
        public String insql(String parameters)
        {
            return insql(parameters.Split(","));
        }
        public String insql(IList parameters)
        {
            ArrayList result = new ArrayList();
            foreach (String param in parameters)
            {
                result.Add(this.q(param));
            }
            return " IN (" + (result.Count > 0 ? String.Join(", ", result.ToArray()) : "NULL") + ")";
        }

        // same as insql, but for quoting numbers - uses qi() 
        public String insqli(String parameters)
        {
            return insqli(parameters.Split(","));
        }

        public String insqli(IList parameters)
        {
            ArrayList result = new ArrayList();
            foreach (String param in parameters)
            {
                result.Add(this.qi(param));
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

            if (length > 0)
            {
                str = this.left(str, length);
            }
            return "'" + str.Replace("'", "''") + "'";
        }

        // simple just replace quotes, don't add start/end single quote - for example, for use with LIKE
        public String qq(String str)
        {
            if (str == null) str = "";

            return str.Replace("'", "''");
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
            String result;
            if (dbtype == "SQL")
            {
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
        public object qone(String table, String field_name, object field_value_or_op)
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
            DBOperation dbop = null; /* TODO Change to default(_) if this is not a reference type */;
            if (field_value_or_op is DBOperation)
            {
                dbop = (DBOperation)field_value_or_op;
                field_value = dbop.value;
            }
            else
            {
                field_value = field_value_or_op;
            }

            String field_type = (String)schema_table[field_name];
            String quoted;
            if (dbop != null)
            {
                if (dbop.op == DBOps.IN || dbop.op == DBOps.NOTIN)
                {
                    if (dbop.value != null && (dbop.value) is IList)
                    {
                        ArrayList result = new ArrayList();
                        foreach (object param in (ArrayList)dbop.value)
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
                else if (dbop.op == DBOps.BETWEEN)
                {
                    ArrayList values = (ArrayList)dbop.value;
                    quoted = qone_by_type(field_type, (String)values[0]) + " AND " + qone_by_type(field_type, (String)values[1]);
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

        public String qone_by_type(String field_type, object field_value)
        {
            String quoted;

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
                    if (field_value != null && Regex.IsMatch((String)field_value, "true", RegexOptions.IgnoreCase))
                    {
                        quoted = "1";
                    }
                    else if (field_value != null && Regex.IsMatch((String)field_value, "false", RegexOptions.IgnoreCase))
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
                    quoted = this.qd((String)field_value);
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
                        quoted = Regex.Replace((String)field_value, @"\\(\r\n?)", @"\\$1$1");
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
        public int insert(String table, Hashtable fields)
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

            return (int)insert_id;
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
        public String _join_hash(Hashtable h, String kv_delim, String pairs_delim)
        {
            String res = "";
            if (h.Count < 1) return res;

            String[] ar = new String[h.Count - 1 + 1];

            int i = 0;
            foreach (String k in h.Keys)
            {
                var vv = h[k];
                String v = "";
                var delim = kv_delim;
                if (String.IsNullOrEmpty(delim))
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
                        if ((String)vv == "NULL")
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

        /// <summary>
        ///  build SELECT sql String
        ///  </summary>
        ///  <param name="table">table name</param>
        ///  <param name="where">where conditions</param>
        ///  <param name="order_by">optional order by String</param>
        ///  <param name="select_fields">MUST already be quoted!</param>
        ///  <returns></returns>
        private String hash2sql_select(String table, Hashtable where, ref String order_by, String select_fields = "*")
        {
            if (order_by == null) order_by = "";

            where = quote(table, where);
            // FW.logger(where)
            String where_String = _join_hash(where, "", " AND ");
            if (where_String.Length > 0)
                where_String = " WHERE " + where_String;

            String sql = "SELECT " + select_fields + " FROM " + q_ident(table) + " " + where_String;
            if (order_by.Length > 0)
                sql = sql + " ORDER BY " + order_by;
            return sql;
        }

        public String hash2sql_u(String table, Hashtable fields, Hashtable where)
        {
            fields = quote(table, fields);
            where = quote(table, where);

            String update_String = _join_hash(fields, "=", ", ");
            String where_String = _join_hash(where, "", " AND ");

            if (where_String.Length > 0)
                where_String = " WHERE " + where_String;

            String sql = "UPDATE " + q_ident(table) + " " + " SET " + update_String + where_String;

            return sql;
        }

        private String hash2sql_i(String table, Hashtable fields)
        {
            fields = quote(table, fields);

            String[] ar = new String[fields.Count - 1 + 1];

            fields.Keys.CopyTo(ar, 0);
            String names_String = String.Join(", ", ar);

            fields.Values.CopyTo(ar, 0);
            String values_String = String.Join(", ", ar);
            String sql = "INSERT INTO " + q_ident(table) + " (" + names_String + ") VALUES (" + values_String + ")";
            return sql;
        }

        private String hash2sql_d(String table, Hashtable where)
        {
            where = quote(table, where);
            String where_String = _join_hash(where, "", " AND ");
            if (where_String.Length > 0)
                where_String = " WHERE " + where_String;

            String sql = "DELETE FROM " + q_ident(table) + " " + where_String;
            return sql;
        }

        // return array of table names in current db
        public ArrayList tables()
        {
            ArrayList result = new ArrayList();

            DbConnection conn = this.connect();
            DataTable dataTable = conn.GetSchema("Tables");
            foreach (DataRow row in dataTable.Rows)
            {
                // fw.logger("************ TABLE" & row("TABLE_NAME"))
                // For Each cl As DataColumn In dataTable.Columns
                // fw.logger(cl.ToString & " = " & row(cl))
                // Next

                // skip any system tables or views (VIEW, ACCESS TABLE, SYSTEM TABLE)
                if ((String)row["TABLE_TYPE"] != "TABLE" && (String)row["TABLE_TYPE"] != "BASE TABLE" && (String)row["TABLE_TYPE"] != "PASS-THROUGH")
                    continue;
                String tblname = row["TABLE_NAME"].ToString();
                result.Add(tblname);
            }

            return result;
        }

        // return array of view names in current db
        public ArrayList views()
        {
            ArrayList result = new ArrayList();

            DbConnection conn = this.connect();
            DataTable dataTable = conn.GetSchema("Tables");
            foreach (DataRow row in dataTable.Rows)
            {
                // skip non-views
                if (row["TABLE_TYPE"] != "VIEW") continue;

                String tblname = row["TABLE_NAME"].ToString();
                result.Add(tblname);
            }

            return result;
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

            if (((Hashtable)schemafull_cache[connstr]).ContainsKey(table))
            {
                return (ArrayList)((Hashtable)schemafull_cache[connstr])[table];
            }

            // cache miss
            ArrayList result = new ArrayList();
            if (dbtype == "SQL")
            {
                // fw.logger("cache MISS " & current_db & "." & table)
                // get information about all columns in the table
                // default = ((0)) ('') (getdate())
                // maxlen = -1 for nvarchar(MAX)
                String sql = "SELECT c.column_name as 'name'," + " c.data_type as 'type'," + " CASE c.is_nullable WHEN 'YES' THEN 1 ELSE 0 END AS 'is_nullable'," + " c.column_default as 'default'," + " c.character_maximum_length as 'maxlen'," + " c.numeric_precision," + " c.numeric_scale," + " c.character_set_name as 'charset'," + " c.collation_name as 'collation'," + " c.ORDINAL_POSITION as 'pos'," + " COLUMNPROPERTY(object_id(c.table_name), c.column_name, 'IsIdentity') as is_identity" + " FROM INFORMATION_SCHEMA.TABLES t," + "   INFORMATION_SCHEMA.COLUMNS c" + " WHERE t.table_name = c.table_name" + "   AND t.table_name = " + q(table) + " order by c.ORDINAL_POSITION";
                result = array(sql);
                foreach (Hashtable row in result)
                {
                    row["fw_type"] = map_mssqltype2fwtype((String)row["type"]); // meta type
                    row["fw_subtype"] = ((String)row["type"]).ToLower();
                }
            }
            else
            {
                // OLE DB (Access)
                DataTable schemaTable = ((OleDbConnection)conn).GetOleDbSchemaTable(OleDbSchemaGuid.Columns, new object[] { null, null, table, null });

                List<Hashtable> fieldslist = new List<Hashtable>();
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
                    h["fw_subtype"] = ((String)Enum.GetName(typeof(OleDbType), row["DATA_TYPE"])).ToLower(); // exact type as String
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

                // TODO migrate sorting
                // fieldslist = fieldslist.Sort(h => (int)h["pos"]);

                result.AddRange(fieldslist); 

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
        public ArrayList get_foreign_keys(String table = "")
        {
            ArrayList result = new ArrayList();
            if (dbtype == "SQL")
            {
                var where = "";
                if (table != "")
                {
                    where = " WHERE col1.TABLE_NAME=" + this.q(table);
                }
                result = this.array("SELECT " + " col1.CONSTRAINT_NAME as [name]" + ", col1.TABLE_NAME As [table]" + ", col1.COLUMN_NAME as [column]" + ", col2.TABLE_NAME as [pk_table]" + ", col2.COLUMN_NAME as [pk_column]" + ", rc.UPDATE_RULE as [on_update]" + ", rc.DELETE_RULE as [on_delete]" + " FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc " + " INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE col1 " + "   ON (col1.CONSTRAINT_CATALOG = rc.CONSTRAINT_CATALOG  " + "       AND col1.CONSTRAINT_SCHEMA = rc.CONSTRAINT_SCHEMA " + "       AND col1.CONSTRAINT_NAME = rc.CONSTRAINT_NAME)" + " INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE col2 " + "   ON (col2.CONSTRAINT_CATALOG = rc.UNIQUE_CONSTRAINT_CATALOG  " + "       AND col2.CONSTRAINT_SCHEMA = rc.UNIQUE_CONSTRAINT_SCHEMA " + "       AND col2.CONSTRAINT_NAME = rc.UNIQUE_CONSTRAINT_NAME " + "       AND col2.ORDINAL_POSITION = col1.ORDINAL_POSITION)" + where);
            }
            else
            {
                var dt = ((OleDbConnection)conn).GetOleDbSchemaTable(System.Data.OleDb.OleDbSchemaGuid.Foreign_Keys, new object[] { null });
                foreach (DataRow row in dt.Rows)
                {
                    if (table != "" && (String)row["FK_TABLE_NAME"] != table)
                    {
                        continue;
                    }

                    result.Add(new Hashtable()
                    {
                        {
                            "table", row["FK_TABLE_NAME"]
                        },
                        {
                            "column", row["FK_COLUMN_NAME"]
                        },
                        {
                            "name", row["FK_NAME"]
                        },
                        {
                            "pk_table", row["PK_TABLE_NAME"]
                        },
                        {
                            "pk_column", row["PK_COLUMN_NAME"]
                        },
                        {
                            "on_update", row["UPDATE_RULE"]
                        },
                        {
                            "on_delete", row["DELETE_RULE"]
                        }
                    });
                }
            }

            return result;
        }

        // load table schema from db
        public Hashtable load_table_schema(String table)
        {
            // for non-MSSQL schemas - just use config schema for now - TODO
            if (dbtype != "SQL" && dbtype != "OLE")
            {
                if (schema.Count == 0)
                {
                    schema = (Hashtable)conf["schema"];
                }
                return null; /* TODO Change to default(_) if this is not a reference type */;
            }

            // check if schema already there
            if (schema.ContainsKey(table))
            {
                return (Hashtable)schema[table];
            }

            if (schema_cache == null)
            {
                schema_cache = new Hashtable();
            }
            if (!schema_cache.ContainsKey(connstr))
            {
                schema_cache[connstr] = new Hashtable();
            }
            if (!((Hashtable)schema_cache[connstr]).ContainsKey(table))
            {
                Hashtable h = new Hashtable();

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

        private String map_mssqltype2fwtype(String mstype)
        {
            String result;
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
