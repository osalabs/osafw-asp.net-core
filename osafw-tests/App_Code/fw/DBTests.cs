using Microsoft.VisualStudio.TestTools.UnitTesting;
using osafw;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace osafw.Tests
{
    [TestClass()]
    public class DBTests
    {
        private string connstr = "Server=localhost;Database=demo;Trusted_Connection=True";
        private DB db = null;
        private string table_name = "for_unit_testing";

        [TestInitialize()]
        public void Startup()
        {
            db = new DB(connstr, "SQL", "main");
            db.connect();
            // create tables for testing
            db.exec("DROP TABLE IF EXISTS " + table_name + ";");
            db.exec("CREATE TABLE " + table_name + " (  id INT,iname NVARCHAR(64) NOT NULL default '')");

            db.exec("INSERT INTO " + table_name + "(id, iname) VALUES(1,'test1'),(2,'test2'),(3,'test3');");

        }
        [TestCleanup()]
        public void Cleanup()
        {
            db.exec("DROP TABLE " + table_name + ";");
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
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void connectTest()
        {
            var _db = new DB(connstr, "SQL", "main");
            _db.connect();
            Assert.AreEqual(ConnectionState.Open, _db.getConnection());
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
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void check_create_mdbTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void queryTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void execTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void rowTest()
        {
            var row = db.row("SELECT * FROM " + table_name + " WHERE id=1;");

            Assert.IsTrue(row.Count > 0);
            Assert.IsTrue(row.ContainsKey("id"));
            Assert.IsTrue(row.ContainsKey("iname"));
            Assert.AreEqual("test1", row["iname"]);
            // TODO test all methods types
        }

        [TestMethod()]
        public void arrayTest()
        {
            DBList rows = db.array("SELECT * FROM " + table_name + ";");

            foreach (var row in rows) {
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
            List<string> col = db.col("SELECT iname FROM " + table_name);

            Assert.AreEqual("test1", col[0]);
            Assert.AreEqual("test2", col[1]);
            Assert.AreEqual("test3", col[2]);
            // TODO test all methods types
        }

        [TestMethod()]
        public void valueTest()
        {
            string value = (string)db.value("SELECT iname FROM " + table_name + " WHERE id=1;");
            Assert.AreEqual("test1", value);
            // TODO test all methods types
        }

        [TestMethod()]
        public void insqlTest()
        {
            string r = db.insql("test1,test2,test3");
            Assert.AreEqual(" IN ('test1', 'test2', 'test3')", r);
        }

        
        [TestMethod()]
        public void insqliTest()
        {
            string r = db.insqli("1,2,3");
            Assert.AreEqual(" IN (1, 2, 3)", r);

            r = db.insqli("test1,test2,test3");
            Assert.AreEqual(" IN (0, 0, 0)", r);
        }

        [TestMethod()]
        public void q_identTest()
        {
            string r = db.q_ident(table_name);
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
            Assert.AreEqual(123.123, r);
            r = db.qf("123.123");
            Assert.AreEqual(123.123, r);
            r = db.qf("AAA123.123");
            Assert.AreEqual(0, r);
        }

        [TestMethod()]
        public void qdTest()
        {
            string s = "2021-08-08 01:02:03";
            string r = db.qd(s);
            Assert.AreEqual("convert(DATETIME2, '" + s + "', 120)", r);

            r = db.qd("");
            Assert.AreEqual("NULL", r);

            // TODO test with non SQL connnection
        }

        [TestMethod()]
        public void quoteTest()
        {
            Hashtable r = db.quote(table_name, DB.h("id", "5", "iname", "test5"));
            Assert.AreEqual("5", r["[id]"]);
            Assert.AreEqual("'test5'", r["[iname]"]);
        }

        [TestMethod()]
        public void qoneTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void qone_by_typeTest()
        {
            throw new NotImplementedException();
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
            Assert.IsNull(r.opstr);
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
        public void update_or_insertTest()
        {
            db.update_or_insert(table_name, DB.h("iname", "test5"), DB.h("id", 5));
            var r = db.row(table_name, DB.h("id", 5));
            Assert.AreEqual("test5", r["iname"]);

            db.update_or_insert(table_name, DB.h("iname", "test5"), DB.h("id", 3));
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
        public void _join_hashTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void hash2sql_uTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void tablesTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void viewsTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void load_table_schema_fullTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void get_foreign_keysTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void load_table_schemaTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void clear_schema_cacheTest()
        {
            throw new NotImplementedException();
        }
    }
}