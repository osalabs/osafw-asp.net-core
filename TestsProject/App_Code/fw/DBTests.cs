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
            DBRow row = db.row("SELECT * FROM " + table_name + " WHERE id=1;");

            Assert.IsTrue(row.Count > 0);
            Assert.IsTrue(row.ContainsKey("id"));
            Assert.IsTrue(row.ContainsKey("iname"));
            Assert.AreEqual("test1", row["iname"]);
        }

        [TestMethod()]
        public void arrayTest()
        {
            DBList rows = db.array("SELECT * FROM " + table_name + ";");

            foreach (DBRow row in rows) {
                Assert.IsTrue(row.Count > 0);
                Assert.IsTrue(row.ContainsKey("id"));
                Assert.IsTrue(row.ContainsKey("iname"));
            }

            Assert.AreEqual("test1", rows[0]["iname"]);
            Assert.AreEqual("test2", rows[1]["iname"]);
            Assert.AreEqual("test3", rows[2]["iname"]);
        }

        [TestMethod()]
        public void colTest()
        {
            List<string> col = db.col("SELECT iname FROM " + table_name);

            Assert.AreEqual("test1", col[0]);
            Assert.AreEqual("test2", col[1]);
            Assert.AreEqual("test3", col[2]);
        }

        [TestMethod()]
        public void valueTest()
        {
            string value = (string)db.value("SELECT iname FROM " + table_name + " WHERE id=1;");
            Assert.AreEqual("test1", value);
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
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void qTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void qTest1()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void qqTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void qiTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void qfTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void qdTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void quoteTest()
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void opLETest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void opLTTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void opGETest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void opGTTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void opISNULLTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void opISNOTNULLTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void opLIKETest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void opNOTLIKETest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void opINTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void opNOTINTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void opBETWEENTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void insertTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void updateTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void updateTest1()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void update_or_insertTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void delTest()
        {
            throw new NotImplementedException();
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

        [TestMethod()]
        public void DisposeTest()
        {
            throw new NotImplementedException();
        }
    }
}