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

namespace osafw;

public class DBRow : Dictionary<string, string>
{

    public DBRow() { }
    public DBRow(Hashtable h)
    {
        if (h != null)
        {
            foreach (string k in h.Keys)
            {
                this[k] = Utils.f2str(h[k]);
            }
        }
    }

    public new string this[string key]
    {
        get
        {
            return base.ContainsKey(key) ? base[key] : "";
        }
        set
        {
            base[key] = value;
        }
    }
    public static implicit operator Hashtable(DBRow row)
    {
        Hashtable result = new();
        foreach (string k in row.Keys)
        {
            result[k] = row[k];
        }
        return result;
    }
    public static explicit operator DBRow(Hashtable row)
    {
        return new DBRow(row);
    }
    public Hashtable toHashtable()
    {
        return this;
    }
}
public class DBList : List<DBRow>
{
    public static implicit operator ArrayList(DBList rows)
    {
        ArrayList result = new();
        foreach (var r in rows)
        {
            result.Add((Hashtable)r);
        }
        return result;
    }

    public ArrayList toArrayList()
    {
        return this;
    }
}

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
    public string sql; // value to be used in sql query string

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
            case DBOps.BETWEEN:
                opstr = "BETWEEN";
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

public struct DBQueryAndParams
{
    public ArrayList fields; // list of parametrized fields in order
    public string sql; // sql with parameter names, ex: field=@field
    public Hashtable @params; // paremeter name => value, ex: field => 123
}

public class DB : IDisposable
{
    private static Hashtable schemafull_cache; // cache for the full schema, lifetime = app lifetime
    private static Hashtable schema_cache; // cache for the schema, lifetime = app lifetime

    public static int SQL_QUERY_CTR = 0; // counter for SQL queries during request

    private readonly FW fw; // for now only used for: fw.logger and fw.cache (for request-level cacheing of multi-db connections)

    public string db_name = "";
    public string dbtype = "SQL";
    public int sql_command_timeout = 30; // default command timeout, override in model for long queries (in reports or export, for example)
    private readonly Hashtable conf = new();  // config contains: connection_string, type
    private readonly string connstr = "";

    private Hashtable schema = new(); // schema for currently connected db
    private DbConnection conn; // actual db connection - SqlConnection or OleDbConnection

    private bool is_check_ole_types = false; // if true - checks for unsupported OLE types during readRow
    private readonly Hashtable UNSUPPORTED_OLE_TYPES = new();

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
            throw new ArgumentException("h() accepts even number of arguments");

        for (var i = 0; i <= args.Length - 1; i += 2)
            result[args[i]] = args[i + 1];

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
            this.conf = conf;
        else
            this.conf = (Hashtable)((Hashtable)fw.config("db"))["main"];

        this.dbtype = (string)this.conf["type"];
        this.connstr = (string)this.conf["connection_string"];

        this.db_name = db_name;

        this.UNSUPPORTED_OLE_TYPES = Utils.qh("DBTYPE_IDISPATCH DBTYPE_IUNKNOWN"); // also? DBTYPE_ARRAY DBTYPE_VECTOR DBTYPE_BYTES
    }

    public DB(string connstr, string type, string db_name)
    {
        this.conf["type"] = type;
        this.conf["connection_string"] = connstr;

        this.dbtype = (string)this.conf["type"];
        this.connstr = (string)this.conf["connection_string"];

        this.db_name = db_name;

        this.UNSUPPORTED_OLE_TYPES = Utils.qh("DBTYPE_IDISPATCH DBTYPE_IUNKNOWN");
    }

    public void logger(LogLevel level, params object[] args)
    {
        if (args.Length == 0 || fw == null)
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
        if (conn == null && fw != null && fw.cache.getRequestValue(cache_key) != null)
            conn = (DbConnection)fw.cache.getRequestValue(cache_key);

        // if still no connection - re-make it
        if (conn == null)
        {
            schema = new Hashtable(); // reset schema cache
            conn = createConnection(connstr, (string)conf["type"]);
            if (fw != null)
                fw.cache.setRequestValue(cache_key, conn);
        }

        // if it's disconnected - re-connect
        if (conn.State != ConnectionState.Open)
            conn.Open();

        if (this.dbtype == "OLE")
            is_check_ole_types = true;
        else
            is_check_ole_types = false;

        return conn;
    }

    public void disconnect()
    {
        if (this.conn != null)
            this.conn.Close();
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
        if (@params != null && @params.Count > 0)
            logger(LogLevel.INFO, "DB:", db_name, " ", sql, @params);
        else
            logger(LogLevel.INFO, "DB:", db_name, " ", sql);

        SQL_QUERY_CTR += 1;

        DbDataReader dbread;
        if (dbtype == "SQL")
        {
            var dbcomm = new SqlCommand(sql, (SqlConnection)conn);
            dbcomm.CommandTimeout = sql_command_timeout;
            if (@params != null)
                foreach (string p in @params.Keys)
                    dbcomm.Parameters.AddWithValue(p, @params[p]);
            dbread = dbcomm.ExecuteReader();
        }
        else if (dbtype == "OLE" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var dbcomm = new OleDbCommand(sql, (OleDbConnection)conn);
            if (@params != null)
                foreach (string p in @params.Keys)
                    dbcomm.Parameters.AddWithValue(p, @params[p]);
            dbread = dbcomm.ExecuteReader();
        }
        else
            throw new ApplicationException("Unsupported DB Type");

        return dbread;
    }

    // like query(), but exectute without results (so db reader will be closed), return number of rows affected.
    // if is_get_identity=true - return last inserted id
    public int exec(string sql, Hashtable @params = null, bool is_get_identity = false)
    {
        connect();
        if (@params != null && @params.Count > 0)
            logger(LogLevel.INFO, "DB:", db_name, " ", sql, @params);
        else
            logger(LogLevel.INFO, "DB:", db_name, " ", sql);

        SQL_QUERY_CTR += 1;

        int result;
        if (dbtype == "SQL")
        {
            if (is_get_identity)
            {
                //TODO test with OLE
                sql += ";SELECT SCOPE_IDENTITY()";
            }
            var dbcomm = new SqlCommand(sql, (SqlConnection)conn);
            dbcomm.CommandTimeout = sql_command_timeout;
            if (@params != null)
                foreach (string p in @params.Keys)
                    dbcomm.Parameters.AddWithValue(p, @params[p]);

            if (is_get_identity)
                result = Utils.f2int(dbcomm.ExecuteScalar());
            else
                result = dbcomm.ExecuteNonQuery();
        }
        else if (dbtype == "OLE" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var dbcomm = new OleDbCommand(sql, (OleDbConnection)conn);
            if (@params != null)
                foreach (string p in @params.Keys)
                    dbcomm.Parameters.AddWithValue(p, @params[p]);
            result = dbcomm.ExecuteNonQuery();
        }
        else
            throw new ApplicationException("Unsupported DB Type");

        return result;
    }

    //read row values as a strings
    private DBRow readRow(DbDataReader dbread)
    {
        DBRow result = new();

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
                catch (Exception)
                {
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// read signle irst row using table/where/orderby
    /// </summary>
    /// <param name="table"></param>
    /// <param name="where"></param>
    /// <param name="order_by"></param>
    /// <returns></returns>        
    public DBRow row(string table, Hashtable where, string order_by = "")
    {
        var qp = buildSelect(table, where, order_by, "TOP 1 *");
        return rowp(qp.sql, qp.@params);
    }

    /// <summary>
    /// read single first row using parametrized sql query
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="params"></param>
    /// <returns></returns>
    public DBRow rowp(string sql, Hashtable @params = null)
    {
        DbDataReader dbread = query(sql, @params);
        dbread.Read();
        var result = readRow(dbread);
        dbread.Close();
        return result;
    }

    public DBList readArray(DbDataReader dbread)
    {
        DBList result = new();

        while (dbread.Read())
            result.Add(readRow(dbread));

        dbread.Close();
        return result;
    }

    /// <summary>
    /// read all rows using parametrized query
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="params"></param>
    /// <returns></returns>
    public DBList arrayp(string sql, Hashtable @params = null)
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
    /// <param name="aselect_fields">optional select fields array or hashtable("field"=>"alias") or arraylist of hashtable("field"=>1,"alias"=>1) for cases if there could be several same fields with diff aliases), if not set * returned</param>
    /// <returns></returns>
    public DBList array(string table, Hashtable where, string order_by = "", ICollection aselect_fields = null)
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
                    quoted.Add(this.qid((string)asf["field"]) + " as " + this.qid((string)asf["alias"]));
                }
            }
            else if (aselect_fields is IDictionary)
            {
                foreach (string field in (aselect_fields as IDictionary).Keys)
                {
                    quoted.Add(this.qid(field) + " as " + this.qid((string)(aselect_fields as IDictionary)[field]));// field as alias
                }
            }
            else
            {
                foreach (string field in aselect_fields)
                {
                    quoted.Add(this.qid(field));
                }
            }
            select_fields = quoted.Count > 0 ? string.Join(", ", quoted.ToArray()) : "*";
        }

        var qp = buildSelect(table, where, order_by, select_fields);
        return arrayp(qp.sql, qp.@params);
    }

    /// <summary>
    /// read column helper
    /// </summary>
    /// <param name="dbread"></param>
    /// <returns></returns>
    public List<string> readCol(DbDataReader dbread)
    {
        List<string> result = new();
        while (dbread.Read())
            result.Add(dbread[0].ToString());

        dbread.Close();
        return result;
    }

    /// <summary>
    /// read first column using parametrized query
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="params"></param>
    /// <returns></returns>
    public List<string> colp(string sql, Hashtable @params = null)
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
    public List<string> col(string table, Hashtable where, string field_name, string order_by = "")
    {
        if (field_name == null) field_name = "";

        if (string.IsNullOrEmpty(field_name))
            field_name = "*";
        else
            field_name = qid(field_name);
        var qp = buildSelect(table, where, order_by, field_name);
        return colp(qp.sql, qp.@params);
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
    public object valuep(string sql, Hashtable @params = null)
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
    public object value(string table, Hashtable where, string field_name = "", string order_by = "")
    {
        if (field_name == null) field_name = "";

        if (string.IsNullOrEmpty(field_name))
            field_name = "*";
        else if (field_name == "count(*)" || field_name == "1")
        {
        }
        else
            field_name = qid(field_name);
        var qp = buildSelect(table, where, order_by, field_name);
        return valuep(qp.sql, qp.@params);
    }

    // string will be Left(RTrim(str),length)
    // TODO move to Utils since its not belong DB
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
    public string qid(string str)
    {
        if (str == null) str = "";

        str = str.Replace("[", "");
        str = str.Replace("]", "");
        return "[" + str + "]";
    }

    [Obsolete("use qid() instead")]
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

    // value to Date (or null if value is not a date)
    public DateTime? qd(object value)
    {
        DateTime? result = null;
        if (value != null)
            if (value is DateTime dt)
                result = dt;
            else
                if (DateTime.TryParse(value.ToString(), out DateTime tmpdate))
                result = tmpdate;

        return result;
    }

    // simple quote as Date Value (string
    [Obsolete("This method is deprecated, use qd instead.")]
    public string qdstr(object str)
    {
        string result;
        if (dbtype == "SQL")
        {
            if (DateTime.TryParse(str.ToString(), out DateTime tmpdate))
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

    /// <summary>
    /// prepare query and parameters - parameters will be converted to types appropriate for the related fields
    /// </summary>
    /// <param name="table"></param>
    /// <param name="fields"></param>
    /// <param name="join_type">"where"(default), "update"(for SET), "insert"(for VALUES)</param>
    /// <param name="suffix">optional suffix to append to each param name</param>
    /// <returns></returns>
    public DBQueryAndParams prepareParams(string table, Hashtable fields, string join_type = "where", string suffix = "")
    {
        connect();
        loadTableSchema(table);
        if (!schema.ContainsKey(table))
        {
            throw new ApplicationException("table [" + table + "] does not defined in FW.config(\"schema\")");
        }

        if (fields.Count == 0)
            return new DBQueryAndParams()
            {
                sql = "",
                @params = new Hashtable()
            };

        var is_for_insert = (join_type == "insert");
        var is_for_where = (join_type == "where"); // if for where "IS NULL" will be used instead "=NULL"

        var join_delimiter = ",";
        if (is_for_where)
            join_delimiter = " AND ";

        ArrayList fields_list = new();
        ArrayList params_sqls = new();

        Hashtable @params = new();
        foreach (string fname in fields.Keys)
        {
            var dbop = field2Op(table, fname, fields[fname], is_for_where);

            var delim = " " + dbop.opstr + " ";
            var param_name = Regex.Replace(fname, @"\W", "_") + suffix; // replace any non-alphanum in param names and add suffix
            string sql;
            if (is_for_insert)
                // for insert VALUES it will be form @p1,@p2,... i.e. without field names
                sql = "";
            else
                // for update/where we need it in form like "field="
                sql = fname + delim;

            if (dbop.is_value)
            {
                // if we have value - add it to params
                if (dbop.op == DBOps.BETWEEN)
                {
                    // special case for between
                    @params[param_name + "_1"] = ((IList)dbop.value)[0];
                    @params[param_name + "_2"] = ((IList)dbop.value)[1];
                    // BETWEEN @p1 AND @p2
                    sql += "@" + param_name + "_1 AND @" + param_name + "_2";
                }
                else if (dbop.op == DBOps.IN || dbop.op == DBOps.NOTIN)
                {
                    ArrayList sql_params = new();
                    var i = 1;
                    foreach (var pvalue in (IList)dbop.value)
                    {
                        @params[param_name + "_" + i] = pvalue;
                        sql_params.Add("@" + param_name + "_" + i);
                        i += 1;
                    }
                    // [NOT] IN (@p1,@p2,@p3...)
                    sql += "(" + (sql_params.Count > 0 ? string.Join(",", sql_params.ToArray()) : "NULL") + ")";
                }
                else
                {
                    @params[param_name] = dbop.value;
                    sql += "@" + param_name;
                }
                fields_list.Add(fname); // only if field has a parameter - include in the list
            }
            params_sqls.Add(sql);
        }
        // logger(LogLevel.DEBUG, "fields:", fields_with_param_names)
        // logger(LogLevel.DEBUG, "params:", params)

        return new DBQueryAndParams()
        {
            fields = fields_list,
            sql = string.Join(join_delimiter, params_sqls.ToArray()),
            @params = @params
        };
    }

    public DBOperation field2Op(string table, string field_name, object field_value_or_op, bool is_for_where = false)
    {
        DBOperation dbop;
        if (field_value_or_op is DBOperation dbop1)
            dbop = dbop1;
        else
        {
            // if it's equal - convert to EQ db operation
            if (is_for_where)
                // for WHERE xxx=NULL should be xxx IS NULL
                dbop = opEQ(field_value_or_op);
            else
                // for update SET xxx=NULL should be as is
                dbop = new DBOperation(DBOps.EQ, field_value_or_op);
        }

        return field2Op(table, field_name, dbop, is_for_where);
    }

    // returnDBOperation class with value converted to type appropriate for the db field 
    public DBOperation field2Op(string table, string field_name, DBOperation dbop, bool is_for_where = false)
    {
        connect();
        loadTableSchema(table);
        field_name = field_name.ToLower();
        Hashtable schema_table = (Hashtable)schema[table];
        if (!schema_table.ContainsKey(field_name))
        {
            throw new ApplicationException("field " + table + "." + field_name + " does not defined in FW.config(\"schema\") ");
        }

        string field_type = (string)schema_table[field_name];
        //logger(LogLevel.DEBUG, "field2Op IN: ", table, ".", field_name, " ", field_type, " ", dbop.op, " ", dbop.value);

        // db operation
        if (dbop.op == DBOps.IN || dbop.op == DBOps.NOTIN)
        {
            ArrayList result = new();
            foreach (var pvalue in (IList)dbop.value)
                result.Add(field2typed(field_type, pvalue));
            dbop.value = result;
        }
        else if (dbop.op == DBOps.BETWEEN)
        {
            ((IList)dbop.value)[0] = field2typed(field_type, ((IList)dbop.value)[0]);
            ((IList)dbop.value)[1] = field2typed(field_type, ((IList)dbop.value)[1]);
        }
        else
        {
            // convert to field's type
            dbop.value = field2typed(field_type, dbop.value);
            if (is_for_where && dbop.value==DBNull.Value) {
                // for where if we got null value here for EQ/NOT operation - make it ISNULL/ISNOT NULL
                // (this could happen when comparing int field to empty string)
                if (dbop.op == DBOps.EQ)
                    dbop = opISNULL();
                else if (dbop.op == DBOps.NOT)
                    dbop = opISNOTNULL();
            }
        }

        return dbop;
    }

    public object field2typed(string field_type, object field_value)
    {
        object result = DBNull.Value;

        //logger(LogLevel.DEBUG, "field2typed IN: ", field_type, " ", field_value);

        // if value set to null or DBNull - assume it's NULL in db
        if (field_value == null || field_value == DBNull.Value)
        {
            //result is DBNull
        }
        else
        {
            if (Regex.IsMatch(field_type, "int"))
            {
                // if field is numerical and string true/false - convert to 1/0
                if (field_value is string str)
                {
                    if (Regex.IsMatch(str, "true", RegexOptions.IgnoreCase))
                        result = 1;
                    else if (Regex.IsMatch(str, "false", RegexOptions.IgnoreCase))
                        result = 0;
                    else if (string.IsNullOrEmpty(str))
                        // if empty string for numerical field - assume NULL
                        result = DBNull.Value;
                    else
                        result = Utils.f2int(field_value);
                }
                else
                    result = Utils.f2int(field_value);
            }
            else if (field_type == "datetime")
            {
                result = this.qd(field_value);
                if (result == null)
                {
                    result = DBNull.Value;
                }
            }
            else if (field_type == "float")
                result = Utils.f2float(field_value);
            else
                // string or other unknown value
                result = field_value;
        }
        //logger(LogLevel.DEBUG, "field2typed OUT: ", field_type, " ", field_value);

        return result;
    }

    // operations support for non-raw sql methods

    /// <summary>
    /// EQUAL operation, basically the same as assigning value directly
    /// But for null/DBNull values - return ISNULL operation - equivalent to opISNULL()
    /// Example: Dim rows = db.array("users", New Hashtable From {{"status", db.opEQ(0)}})
    /// <![CDATA[ select * from users where status=0 ]]>
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public DBOperation opEQ(object value)
    {
        if (value == null || value == DBNull.Value)
            return opISNULL();
        else
            return new DBOperation(DBOps.EQ, value);
    }

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
        if (fields.Count < 1)
            return 0;
        var qp = buildInsert(table, fields);

        object insert_id;

        if (dbtype == "SQL")
            // SELECT SCOPE_IDENTITY() not always return what we need
            insert_id = exec(qp.sql, qp.@params, true);
        else if (dbtype == "OLE")
        {
            exec(qp.sql, qp.@params);
            insert_id = valuep("SELECT @@identity");
        }
        else
            throw new ApplicationException("Get last insert ID for DB type [" + dbtype + "] not implemented");

        // if table doesn't have identity insert_id would be DBNull
        if (insert_id == DBNull.Value || insert_id == null)
            insert_id = 0;

        return (int)insert_id;
    }

    public int updatep(string sql, Hashtable @params = null)
    {
        return exec(sql, @params);
    }

    public int update(string table, Hashtable fields, Hashtable where)
    {
        var qp = buildUpdate(table, fields, where);
        return exec(qp.sql, qp.@params);
    }

    // retrun number of affected rows
    public int updateOrInsert(string table, Hashtable fields, Hashtable where)
    {
        // merge fields and where
        Hashtable allfields = new();
        foreach (string k in fields.Keys)
            allfields[k] = fields[k];

        foreach (string k in where.Keys)
            allfields[k] = where[k];

        var result = update(table, fields, where);
        if (result == 0)
            // if no rows inserted
            insert(table, fields);
        return result;
        //single query alternative, but too much params to pass: update_sql + "  IF @@ROWCOUNT = 0 " + insert_sql
    }

    /// <summary>
    /// delete records from table
    /// </summary>
    /// <param name="table">table name</param>
    /// <param name="where">optional where, WARNING, if empty - DELETE ALL RECORDS in table</param>
    /// <returns>number of affected rows</returns>
    public int del(string table, Hashtable where = null)
    {
        if (where == null) where = new Hashtable(); 
        var qp = buildDelete(table, where);
        return exec(qp.sql, qp.@params);
    }

    /// <summary>
    /// build SELECT sql string
    /// </summary>
    /// <param name="table">table name</param>
    /// <param name="where">where conditions</param>
    /// <param name="order_by">optional order by string, MUST already be quoted!</param>
    /// <param name="select_fields">optional (default "*") fields to select, MUST already be quoted!</param>
    /// <returns></returns>
    private DBQueryAndParams buildSelect(string table, Hashtable where, string order_by = "", string select_fields = "*")
    {
        DBQueryAndParams result = new();
        result.sql = "SELECT " + select_fields + " FROM " + qid(table);
        if (where.Count > 0)
        {
            var where_params = prepareParams(table, where);
            result.sql += " WHERE " + where_params.sql;
            result.@params = where_params.@params;
        }
        if (order_by.Length > 0)
            result.sql += " ORDER BY " + order_by;

        return result;
    }

    private DBQueryAndParams buildUpdate(string table, Hashtable fields, Hashtable where)
    {
        DBQueryAndParams result = new();
        result.sql = "UPDATE " + qid(table) + " " + " SET ";

        //logger(LogLevel.DEBUG, "buildUpdate:", table, fields);

        var set_params = prepareParams(table, fields, "update", "_SET");
        result.sql += set_params.sql;
        result.@params = set_params.@params;

        if (where.Count > 0)
        {
            var where_params = prepareParams(table, where);
            result.sql += " WHERE " + where_params.sql;
            Utils.mergeHash(result.@params, where_params.@params);
        }

        return result;
    }

    private DBQueryAndParams buildInsert(string table, Hashtable fields)
    {
        DBQueryAndParams result = new();

        var insert_params = prepareParams(table, fields, "insert");
        var sql_fields = string.Join(",", insert_params.fields.ToArray());

        result.sql = "INSERT INTO " + qid(table) + " (" + sql_fields + ") VALUES (" + insert_params.sql + ")";
        result.@params = insert_params.@params;

        return result;
    }

    private DBQueryAndParams buildDelete(string table, Hashtable where)
    {
        DBQueryAndParams result = new();
        result.sql = "DELETE FROM " + qid(table) + " ";

        if (where.Count > 0)
        {
            var where_params = prepareParams(table, where);
            result.sql += " WHERE " + where_params.sql;
            result.@params = where_params.@params;
        }

        return result;
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

    public string schemaFieldType(string table, string field_name)
    {
        connect();
        loadTableSchema(table);
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

    public ArrayList loadTableSchemaFull(string table)
    {
        // check if full schema already there
        if (schemafull_cache == null)
            schemafull_cache = new Hashtable();
        if (!schemafull_cache.ContainsKey(connstr))
            schemafull_cache[connstr] = new Hashtable();

        var cache = (Hashtable)schemafull_cache[connstr];
        if (cache.ContainsKey(table))
            return (ArrayList)cache[table];

        // cache miss
        ArrayList result = new();
        if (dbtype == "SQL")
        {
            // fw.logger("cache MISS " & current_db & "." & table)
            // get information about all columns in the table
            // default = ((0)) ('') (getdate())
            // maxlen = -1 for nvarchar(MAX)
            string sql = @"SELECT c.column_name as 'name',
                      c.data_type as 'type',
                      CASE c.is_nullable WHEN 'YES' THEN 1 ELSE 0 END AS 'is_nullable',
                      c.column_default as 'default',
                      c.character_maximum_length as 'maxlen',
                      c.numeric_precision,
                      c.numeric_scale,
                      c.character_set_name as 'charset',
                      c.collation_name as 'collation',
                      c.ORDINAL_POSITION as 'pos',
                      COLUMNPROPERTY(object_id(c.table_name), c.column_name, 'IsIdentity') as is_identity
                      FROM INFORMATION_SCHEMA.TABLES t,
                        INFORMATION_SCHEMA.COLUMNS c
                      WHERE t.table_name = c.table_name
                        AND t.table_name = @table_name
                      order by c.ORDINAL_POSITION";
            result = arrayp(sql, DB.h("@table_name", table));
            foreach (Hashtable row in result)
            {
                row["fw_type"] = mapTypeSQL2Fw((string)row["type"]); // meta type
                row["fw_subtype"] = ((string)row["type"]).ToLower();
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
                h["fw_type"] = mapTypeOLE2Fw((int)row["DATA_TYPE"]); // meta type
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

            result.AddRange((from Hashtable h in fieldslist orderby ((long)h["pos"]) ascending select h).ToList());

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
        cache[table] = result;

        return result;
    }

    // return database foreign keys, optionally filtered by table (that contains foreign keys)
    public ArrayList listForeignKeys(string table = "")
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

            result = this.arrayp(@"SELECT 
                      col1.CONSTRAINT_NAME as [name]
                     , col1.TABLE_NAME As [table]
                     , col1.COLUMN_NAME as [column]
                     , col2.TABLE_NAME as [pk_table]
                     , col2.COLUMN_NAME as [pk_column]
                     , rc.UPDATE_RULE as [on_update]
                     , rc.DELETE_RULE as [on_delete]
                      FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc 
                      INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE col1 
                        ON (col1.CONSTRAINT_CATALOG = rc.CONSTRAINT_CATALOG  
                            AND col1.CONSTRAINT_SCHEMA = rc.CONSTRAINT_SCHEMA 
                            AND col1.CONSTRAINT_NAME = rc.CONSTRAINT_NAME)
                      INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE col2 
                        ON (col2.CONSTRAINT_CATALOG = rc.UNIQUE_CONSTRAINT_CATALOG  
                            AND col2.CONSTRAINT_SCHEMA = rc.UNIQUE_CONSTRAINT_SCHEMA 
                            AND col2.CONSTRAINT_NAME = rc.UNIQUE_CONSTRAINT_NAME 
                            AND col2.ORDINAL_POSITION = col1.ORDINAL_POSITION)" +
                where, where_params);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
    public Hashtable loadTableSchema(string table)
    {
        // for unsupported schemas - use config schema
        if (dbtype != "SQL" && dbtype != "OLE")
        {
            if (schema.Count == 0)
                schema = (Hashtable)conf["schema"];
        }

        // check if schema already there
        if (schema.ContainsKey(table))
            return (Hashtable)schema[table];

        if (schema_cache == null)
            schema_cache = new();
        if (!schema_cache.ContainsKey(connstr))
            schema_cache[connstr] = new Hashtable();

        if (!((Hashtable)schema_cache[connstr]).ContainsKey(table))
        {
            Hashtable h = new();

            ArrayList fields = loadTableSchemaFull(table);
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

    public void clearSchemaCache()
    {
        if (schemafull_cache != null)
            schemafull_cache.Clear();
        if (schema_cache != null)
            schema_cache.Clear();
        if (schema != null)
            schema.Clear();
    }

    // map SQL Server type to FW's
    private static string mapTypeSQL2Fw(string mstype)
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
    // map OLE type to FW's
    private static string mapTypeOLE2Fw(int mstype)
    {
        string result = mstype switch
        {
            // TODO - unsupported: image, varbinary, longvarbinary, dbtime, timestamp
            // NOTE: Boolean here is: True=-1 (vbTrue), False=0 (vbFalse)
            (int)OleDbType.Boolean
            or (int)OleDbType.TinyInt
            or (int)OleDbType.UnsignedTinyInt
            or (int)OleDbType.SmallInt
            or (int)OleDbType.UnsignedSmallInt
            or (int)OleDbType.Integer
            or (int)OleDbType.UnsignedInt
            or (int)OleDbType.BigInt
            or (int)OleDbType.UnsignedBigInt => "int",

            (int)OleDbType.Double
            or (int)OleDbType.Numeric
            or (int)OleDbType.VarNumeric
            or (int)OleDbType.Single
            or (int)OleDbType.Decimal
            or (int)OleDbType.Currency => "float",

            (int)OleDbType.Date
            or (int)OleDbType.DBDate
            or (int)OleDbType.DBTimeStamp => "datetime",

            // "text", "ntext", "varchar", "longvarchar" "nvarchar", "char", "nchar", "wchar", "varwchar", "longvarwchar", "dbtime":
            _ => "varchar",
        };
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
        GC.SuppressFinalize(this);
    }
}

