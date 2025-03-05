using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace osafw.Tests
{
    [TestClass()]
    public class DBTests
    {
        private readonly string connstr = "Server=(local);Database=demo;Trusted_Connection=True;TrustServerCertificate=true;";
        private DB db = null;
        private string table_name = "for_unit_testing";

        [TestInitialize()]
        public void Startup()
        {
            db = new DB(connstr, "SQL", "main");
            db.connect();
            // create tables for testing
            db.exec($"DROP TABLE IF EXISTS {table_name}");
            db.exec($@"CREATE TABLE {table_name} (
                        id              INT,
                        iname           NVARCHAR(64) NOT NULL default '',
                        idatetime       DATETIME2
                    )");
            db.exec($"INSERT INTO {table_name} (id, iname) VALUES (1,'test1'),(2,'test2'),(3,'test3')");
        }

        [TestCleanup()]
        public void Cleanup()
        {
            db.exec($"DROP TABLE {table_name}");
            db.disconnect();
        }

        [TestMethod()]
        public void hTest()
        {
            Hashtable h = DB.h("AAA", 1, "BBB", 2, "CCC", 3, "DDD", 4);
            Assert.AreEqual(1, h["AAA"]);
            Assert.AreEqual(2, h["BBB"]);
            Assert.AreEqual(3, h["CCC"]);
            Assert.AreEqual(4, h["DDD"]);
        }


        [TestMethod()]
        public void loggerTest()
        {
            try
            {
                db.logger(LogLevel.OFF, "test db.logger(LogLevel.OFF)");
                db.logger(LogLevel.DEBUG, "test db.logger(LogLevel.DEBUG)");
                db.logger(LogLevel.ERROR, "test db.logger(LogLevel.ERROR)");
                db.logger(LogLevel.FATAL, "test db.logger(LogLevel.FATAL)");
                db.logger(LogLevel.INFO, "test db.logger(LogLevel.INFO)");
                db.logger(LogLevel.TRACE, "test db.logger(LogLevel.TRACE)");
                db.logger(LogLevel.WARN, "test db.logger(LogLevel.WARN)");
            }
            catch (Exception)
            {
                Assert.Fail();
            }
        }

        [TestMethod()]
        public void connectTest()
        {
            var _db = new DB(connstr, "SQL", "main");
            _db.connect();
            Assert.AreEqual(ConnectionState.Open, _db.getConnection().State);
            _db.disconnect();
        }

        [TestMethod()]
        public void disconnectTest()
        {
            var _db = new DB(connstr, "SQL", "main");
            _db.connect();
            _db.disconnect();
            Assert.AreEqual(ConnectionState.Closed, _db.getConnection().State);
        }

        [TestMethod()]
        public void getConnectionTest()
        {
            Assert.IsInstanceOfType(db.getConnection(), typeof(DbConnection));
        }

        [TestMethod()]
        public void createConnectionTest()
        {
            var result = db.createConnection(connstr);
            Assert.IsTrue(result.State == System.Data.ConnectionState.Open);
        }

        [TestMethod()]
        public void queryTest()
        {
            DbDataReader dbread = db.query("SELECT * FROM " + table_name + " WHERE id=1;");

            dbread.Read();
            Assert.IsTrue(dbread.HasRows);
            Assert.IsTrue(dbread.FieldCount > 0);

            dbread.Close();
        }

        [TestMethod()]
        public void execTest()
        {
            ArrayList tables = db.tables();
            if (tables.Contains("exec_unit_testing"))
            {
                db.exec("DROP TABLE exec_unit_testing");
            }
            db.exec("CREATE TABLE exec_unit_testing(id INT)");
            tables = db.tables();
            Assert.IsTrue(tables.Contains("exec_unit_testing"));
            db.exec("DROP TABLE exec_unit_testing");
        }

        [TestMethod()]
        public void rowTest()
        {
            var row = db.rowp("SELECT * FROM " + table_name + " WHERE id=1;");

            Assert.IsTrue(row.Count > 0);
            Assert.IsTrue(row.ContainsKey("id"));
            Assert.IsTrue(row.ContainsKey("iname"));
            Assert.AreEqual("test1", row["iname"]);
            // TODO test all methods types
        }

        [TestMethod()]
        public void arrayTest()
        {
            DBList rows = db.arrayp("SELECT * FROM " + table_name + ";");

            foreach (var row in rows)
            {
                Assert.IsTrue(row.Count > 0);
                Assert.IsTrue(row.ContainsKey("id"));
                Assert.IsTrue(row.ContainsKey("iname"));
            }

            Assert.AreEqual("test1", rows[0]["iname"]);
            Assert.AreEqual("test2", rows[1]["iname"]);
            Assert.AreEqual("test3", rows[2]["iname"]);
            // TODO test all methods types
        }

        [TestMethod()]
        public void colTest()
        {
            List<string> col = db.colp("SELECT iname FROM " + table_name);

            Assert.AreEqual("test1", col[0]);
            Assert.AreEqual("test2", col[1]);
            Assert.AreEqual("test3", col[2]);
            // TODO test all methods types
        }

        [TestMethod()]
        public void valuepTest()
        {
            string value = (string)db.valuep("SELECT iname FROM " + table_name + " WHERE id=1;");
            Assert.AreEqual("test1", value);
        }

        [TestMethod()]
        public void valueTest()
        {
            // 1. value("table", where)
            string value = db.value(table_name, DB.h("id", 1)).ToString();
            Assert.AreEqual("1", value);

            // 2. value("table", where, "field1")
            value = (string)db.value(table_name, DB.h("id", 1), "iname");
            Assert.AreEqual("test1", value);

            // 3. value("table", where, "1") 'just return 1, useful for exists queries
            value = (string)db.value(table_name, DB.h("id", 1), "1").ToString();
            Assert.AreEqual("1", value);

            // 4. value("table", where, "count(*)", "id asc")
            value = (string)db.value(table_name, [], "count(*)").ToString();
            Assert.AreEqual("3", value);

            // 5. value("table", where, "MAX(id)")
            value = (string)db.value(table_name, [], "MAX(id)").ToString();
            Assert.AreEqual("3", value);

            //fail test with using bad aggregate function
            try
            {
                value = (string)db.value(table_name, [], "BAD(id)").ToString();
                Assert.Fail("Expected exception not thrown");
            }
            catch (Exception ex)
            {
                Assert.AreEqual("'BAD' is not a recognized built-in function name.", ex.Message);
            }
        }

        [TestMethod()]
        public void insqlTest()
        {
            // Test with comma-separated string
            string result1 = db.insql("test1,test2,test3");
            Assert.AreEqual(" IN ('test1', 'test2', 'test3')", result1);

            // Test with empty string
            string result2 = db.insql("");
            Assert.AreEqual(" IN ('')", result2);

            // Test with string array
            string[] strArray = ["a", "b", "c"];
            string result3 = db.insql(strArray);
            Assert.AreEqual(" IN ('a', 'b', 'c')", result3);

            // Test with ArrayList
            ArrayList list = ["a", "b", "c"];
            string result4 = db.insql(list);
            Assert.AreEqual(" IN ('a', 'b', 'c')", result4);

            // Test with empty ArrayList
            ArrayList emptyList = [];
            string result5 = db.insql(emptyList);
            Assert.AreEqual(" IN (NULL)", result5);
        }

        [TestMethod()]
        public void insqliTest()
        {
            // Test with comma-separated string
            string result1 = db.insqli("test1,test2,test3");
            Assert.AreEqual(" IN (0, 0, 0)", result1);

            string result2 = db.insqli("1,2,3");
            Assert.AreEqual(" IN (1, 2, 3)", result2);

            // Test with empty string
            string result3 = db.insqli("");
            Assert.AreEqual(" IN (0)", result3);

            // Test with string array
            string[] strArray = ["1", "2", "3"];
            string result4 = db.insqli(strArray);
            Assert.AreEqual(" IN (1, 2, 3)", result4);

            // Test with ArrayList
            ArrayList list = ["1", "2", "3"];
            string result5 = db.insqli(list);
            Assert.AreEqual(" IN (1, 2, 3)", result5);

            // Test with empty ArrayList
            ArrayList emptyList = [];
            string result6 = db.insqli(emptyList);
            Assert.AreEqual(" IN (NULL)", result6);
        }

        [TestMethod()]
        public void q_identTest()
        {
            string r = db.qid(table_name);
            Assert.AreEqual("[" + table_name + "]", r);
        }

        [TestMethod()]
        public void qTest()
        {
            string r = db.q(table_name);
            Assert.AreEqual("'" + table_name + "'", r);

            r = db.q("test'test");
            Assert.AreEqual("'test''test'", r);
        }


        [TestMethod()]
        public void qqTest()
        {
            string r = db.qq("test'test");
            Assert.AreEqual("test''test", r);
        }

        [TestMethod()]
        public void qiTest()
        {
            int r = db.qi(123);
            Assert.AreEqual(123, r);
            r = db.qi("123");
            Assert.AreEqual(123, r);
            r = db.qi("AAA123");
            Assert.AreEqual(0, r);
        }

        [TestMethod()]
        public void qfTest()
        {
            double r = db.qf(123.123);
            Assert.AreEqual(123.12300109863281, r);
            r = db.qf("123.123");
            Assert.AreEqual(123.12300109863281, r);
            r = db.qf("AAA123.123");
            Assert.AreEqual(0, r);
        }

        [TestMethod()]
        public void opNOTTest()
        {
            DBOperation r = db.opNOT("test_value");
            Assert.IsTrue(r.is_value);
            Assert.AreEqual(DBOps.NOT, r.op);
            Assert.AreEqual("<>", r.opstr);
            Assert.AreEqual("test_value", r.value);
        }

        [TestMethod()]
        public void opLETest()
        {
            DBOperation r = db.opLE("test_value");
            Assert.IsTrue(r.is_value);
            Assert.AreEqual(DBOps.LE, r.op);
            Assert.AreEqual("<=", r.opstr);
            Assert.AreEqual("test_value", r.value);
        }

        [TestMethod()]
        public void opLTTest()
        {
            DBOperation r = db.opLT("test_value");
            Assert.IsTrue(r.is_value);
            Assert.AreEqual(DBOps.LT, r.op);
            Assert.AreEqual("<", r.opstr);
            Assert.AreEqual("test_value", r.value);
        }

        [TestMethod()]
        public void opGETest()
        {
            DBOperation r = db.opGE("test_value");
            Assert.IsTrue(r.is_value);
            Assert.AreEqual(DBOps.GE, r.op);
            Assert.AreEqual(">=", r.opstr);
            Assert.AreEqual("test_value", r.value);
        }

        [TestMethod()]
        public void opGTTest()
        {
            DBOperation r = db.opGT("test_value");
            Assert.IsTrue(r.is_value);
            Assert.AreEqual(DBOps.GT, r.op);
            Assert.AreEqual(">", r.opstr);
            Assert.AreEqual("test_value", r.value);
        }

        [TestMethod()]
        public void opISNULLTest()
        {
            DBOperation r = db.opISNULL();
            Assert.IsFalse(r.is_value);
            Assert.AreEqual(DBOps.ISNULL, r.op);
            Assert.AreEqual("IS NULL", r.opstr);
            Assert.IsNull(r.value);
        }

        [TestMethod()]
        public void opISNOTNULLTest()
        {
            DBOperation r = db.opISNOTNULL();
            Assert.IsFalse(r.is_value);
            Assert.AreEqual(DBOps.ISNOTNULL, r.op);
            Assert.AreEqual("IS NOT NULL", r.opstr);
            Assert.IsNull(r.value);
        }

        [TestMethod()]
        public void opLIKETest()
        {
            DBOperation r = db.opLIKE("test_value");
            Assert.IsTrue(r.is_value);
            Assert.AreEqual(DBOps.LIKE, r.op);
            Assert.AreEqual("LIKE", r.opstr);
            Assert.AreEqual("test_value", r.value);
        }

        [TestMethod()]
        public void opNOTLIKETest()
        {
            DBOperation r = db.opNOTLIKE("test_value");
            Assert.IsTrue(r.is_value);
            Assert.AreEqual(DBOps.NOTLIKE, r.op);
            Assert.AreEqual("NOT LIKE", r.opstr);
            Assert.AreEqual("test_value", r.value);
        }

        [TestMethod()]
        public void opINTest()
        {
            DBOperation r = db.opIN("test_value", "test_value2");
            Assert.IsTrue(r.is_value);
            Assert.AreEqual(DBOps.IN, r.op);
            Assert.AreEqual("IN", r.opstr);
            Assert.AreEqual("test_value", (string)((object[])r.value)[0]);
            Assert.AreEqual("test_value2", (string)((object[])r.value)[1]);
        }

        [TestMethod()]
        public void opNOTINTest()
        {
            DBOperation r = db.opNOTIN("test_value", "test_value2");
            Assert.IsTrue(r.is_value);
            Assert.AreEqual(DBOps.NOTIN, r.op);
            Assert.AreEqual("NOT IN", r.opstr);
            Assert.AreEqual("test_value", (string)((object[])r.value)[0]);
            Assert.AreEqual("test_value2", (string)((object[])r.value)[1]);
        }

        [TestMethod()]
        public void opBETWEENTest()
        {
            DBOperation r = db.opBETWEEN("test_value", "test_value2");
            Assert.IsTrue(r.is_value);
            Assert.AreEqual(DBOps.BETWEEN, r.op);
            Assert.AreEqual(r.opstr, DBOps.BETWEEN.ToString());
            Assert.AreEqual("test_value", (string)((object[])r.value)[0]);
            Assert.AreEqual("test_value2", (string)((object[])r.value)[1]);
        }

        [TestMethod()]
        public void insertTest()
        {
            db.insert(table_name, DB.h("id", 5, "iname", "test5"));
            var r = db.row(table_name, DB.h("id", 5));
            Assert.AreEqual("test5", r["iname"]);
            // TODO test all methods types
        }

        [TestMethod()]
        public void updateTest()
        {
            db.update(table_name, DB.h("iname", "test5"), DB.h("id", 3));
            var r = db.row(table_name, DB.h("id", 3));
            Assert.AreEqual("test5", r["iname"]);

            // TODO test all methods types
        }

        [TestMethod()]
        public void sqlNOWTest()
        {
            // test NOW/GETDATE via update table record (assuming select and update will happen in the same second)
            //var rnow = db.rowp($"SELECT {db.sqlNOW()} as [now]");
            var now_time = db.Now();
            db.insert(table_name, DB.h("id", 6, "iname", "test6", "idatetime", DB.NOW));
            var r = db.row(table_name, DB.h("id", 6));
            Assert.AreEqual(now_time.ToString(), r["idatetime"], "");
        }

        [TestMethod()]
        public void updateOrInsertTest()
        {
            db.updateOrInsert(table_name, DB.h("iname", "test5", "id", 5), DB.h("id", 5));
            var r = db.row(table_name, DB.h("id", 5));
            Assert.AreEqual("test5", r["iname"]);

            db.updateOrInsert(table_name, DB.h("iname", "test5", "id", 3), DB.h("id", 3));
            r = db.row(table_name, DB.h("id", 3));
            Assert.AreEqual("test5", r["iname"]);
        }

        [TestMethod()]
        public void delTest()
        {
            db.del(table_name, DB.h("id", 3));
            var r = db.row(table_name, DB.h("id", 3));
            Assert.IsTrue(r.Count == 0);
        }

        [TestMethod()]
        public void tablesTest()
        {
            string[] tablesToCheck = Utils.qw("users att settings lookup_manager_tables menu_items att_categories fwsessions");
            ArrayList tables = db.tables();
            foreach (var tableName in tablesToCheck)
            {
                Assert.IsTrue(tables.IndexOf(tableName) >= 0);
            }
        }

        [TestMethod()]
        public void viewsTest()
        {
            db.exec("CREATE VIEW view_for_unit_tests AS SELECT * FROM users");
            ArrayList views = db.views();
            db.exec("DROP VIEW view_for_unit_tests");
            Assert.IsTrue(views.IndexOf("view_for_unit_tests") >= 0);
        }

        [TestMethod()]
        public void tableSchemaFullTest()
        {
            Hashtable schema = db.tableSchemaFull("users");
            Assert.IsTrue(schema.ContainsKey("id"));
            Assert.IsTrue(schema.ContainsKey("status"));
            Assert.IsTrue(schema.ContainsKey("add_users_id"));
            Assert.IsTrue(schema.ContainsKey("add_time"));
            Assert.IsTrue(schema.ContainsKey("upd_users_id"));
            Assert.IsTrue(schema.ContainsKey("upd_time"));
        }

        [TestMethod()]
        public void clearchemaCacheTest()
        {
            Hashtable schema = db.loadTableSchema("users");
            Assert.IsTrue(db.isSchemaCacheEmpty() == false);
            db.clearSchemaCache();
            Assert.IsTrue(db.isSchemaCacheEmpty());
        }

        [TestMethod()]
        public void splitMultiSQLTest()
        {
            string sql1 = "CREATE TABLE test(id INT)";
            string sql2 = "INSERT INTO test(id) VALUES(0)";
            string[] queries = DB.splitMultiSQL(sql1 + ";\n\r" + sql2 + ";\n\r");
            Assert.AreEqual(queries[0], sql1);
            Assert.AreEqual(queries[1], sql2);
        }

        [TestMethod()]
        public void prepareParams()
        {
            // 1. Test Insert
            var fields = new Hashtable {
                { "iname", "John" },
                { "email", "john@example.com" }
            };
            var result = db.prepareParams("demos", fields, "insert");
            Assert.IsTrue(result.sql.Contains("@iname"));
            Assert.IsTrue(result.sql.Contains("@email"));

            // 2. Test Update
            fields["iname"] = "Jane";
            result = db.prepareParams("demos", fields, "update");
            Assert.IsTrue(result.sql.Contains("iname = @iname"));
            Assert.IsTrue(result.sql.Contains("email = @email"));

            // 3. Test WHERE
            result = db.prepareParams("demos", fields);
            // separate as order of fields is not guaranteed
            Assert.IsTrue(result.sql.Contains(" AND "), "failed result: " + result.sql);
            Assert.IsTrue(result.sql.Contains("iname = @iname"), "failed result: " + result.sql);
            Assert.IsTrue(result.sql.Contains("email = @email"), "failed result: " + result.sql);

            // 4. Test Empty Fields
            fields.Clear();
            result = db.prepareParams("demos", fields);
            Assert.AreEqual("", result.sql);

            // 5. Test Special Operations (BETWEEN, IN)
            fields["fint"] = db.opIN(new[] { 1, 2, 3 }); // Assuming this will be interpreted as an IN operation
            result = db.prepareParams("demos", fields);
            Assert.IsTrue(result.sql.Contains("fint IN (@fint_1,@fint_2,@fint_3)"), "failed result: " + result.sql);

            fields.Clear();
            fields["fdate_pop"] = db.opBETWEEN(DateTime.Today, DateTime.Today.AddDays(1));
            result = db.prepareParams("demos", fields);
            Assert.IsTrue(result.sql.Contains("fdate_pop BETWEEN @fdate_pop_1 AND @fdate_pop_2"), "failed result: " + result.sql);
        }
    }
}