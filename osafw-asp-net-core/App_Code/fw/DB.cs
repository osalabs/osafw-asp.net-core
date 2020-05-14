using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Data.SqlClient;
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
        private readonly Hashtable conf = null; // config contains: connection_string, type
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
        public DB(FW fw, Hashtable conf = null, string db_name = "main") {
            this.fw = fw;
            if (conf != null) {
                this.conf = conf;
            } else {
                //conf = fw.config("db")("main")
            }
            dbtype = "SQL";
            connstr = "Data Source=(local);Initial Catalog=demo;Integrated Security=True";

            this.db_name = db_name;

            //UNSUPPORTED_OLE_TYPES = Utils.qh("DBTYPE_IDISPATCH DBTYPE_IUNKNOWN") 'also? DBTYPE_ARRAY DBTYPE_VECTOR DBTYPE_BYTES

        }

        /*public void logger(level As LogLevel, ByVal ParamArray args() As Object) {
            If args.Length = 0 Then Return
            fw.logger(level, args)
        }*/

        // <summary>
        // connect to DB server using connection string defined in web.config appSettings, key db|main|connection_string (by default)
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
                conn = createConnection(connstr/*, conf("type")*/);
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
            if (conn != null){
                conn.Close();
            }
        }

        public DbConnection createConnection(string connstr, string dbtype = "SQL") {
            if (dbtype == "SQL") {
                DbConnection result = new SqlConnection(connstr);
                result.Open();
                return result;
            } else if (dbtype == "OLE") {
            //    DbConnection result = new OLEDBConnection(connstr);
            //    result.Open();
            //    return result;
            } else {
                //   Dim msg As String = "Unknown type [" & dbtype & "]"
                //logger(LogLevel.FATAL, msg)
                //Throw New ApplicationException(msg)
            }
            return null;
        }
    }
}
