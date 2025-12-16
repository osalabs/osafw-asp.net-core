using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace osafw.Tests
{
    [TestClass]
    public class DBOperationTests
    {
        [TestMethod]
        public void DBOperation_SetsOperatorStringsAndValueFlags()
        {
            var isNull = new DBOperation(DBOps.ISNULL);
            var notIn = new DBOperation(DBOps.NOTIN, new[] { 1, 2 });
            var between = new DBOperation(DBOps.BETWEEN, new[] { 1, 5 });

            Assert.AreEqual("IS NULL", isNull.opstr);
            Assert.IsFalse(isNull.is_value);

            Assert.AreEqual("NOT IN", notIn.opstr);
            Assert.IsTrue(notIn.is_value);
            CollectionAssert.AreEqual(new[] { 1, 2 }, (System.Collections.ICollection)notIn.value!);

            Assert.AreEqual("BETWEEN", between.opstr);
            Assert.IsTrue(between.is_value);
        }

        [TestMethod]
        public void DBOpHelpers_CreateExpectedOperations()
        {
            var db = new DB("Server=.;Database=demo;", DB.DBTYPE_SQLSRV);

            var gt = db.opGT(10);
            var like = db.opLIKE("%abc%");
            var isNotNull = db.opISNOTNULL();

            Assert.AreEqual(DBOps.GT, gt.op);
            Assert.AreEqual(">", gt.opstr);
            Assert.AreEqual(10, gt.value);

            Assert.AreEqual(DBOps.LIKE, like.op);
            Assert.AreEqual("LIKE", like.opstr);
            Assert.AreEqual("%abc%", like.value);

            Assert.AreEqual(DBOps.ISNOTNULL, isNotNull.op);
            Assert.AreEqual("IS NOT NULL", isNotNull.opstr);
            Assert.IsFalse(isNotNull.is_value);
        }

        [TestMethod]
        public void SplitMultiSQL_RemovesCommentsAndSeparatesStatements()
        {
            string sql = "-- leading comment\nSELECT 1;\nGO\nSELECT 2;";

            var parts = DB.splitMultiSQL(sql);

            Assert.HasCount(2, parts);
            Assert.AreEqual("SELECT 1", parts[0].Trim());
            Assert.AreEqual("SELECT 2", parts[1].Trim().TrimEnd(';'));
        }

        [TestMethod]
        public void Left_TrimsAndLimitsLength()
        {
            var db = new DB("", DB.DBTYPE_SQLSRV);

            Assert.AreEqual("", db.left("", 5));
            Assert.AreEqual("abc", db.left("   abcdef", 3));
            Assert.AreEqual("short", db.left("short", 10));
        }

        [TestMethod]
        public void Qid_QuotesPerDbTypeAndSchema()
        {
            var sqlServerDb = new DB("", DB.DBTYPE_SQLSRV);
            var mysqlDb = new DB("", DB.DBTYPE_MYSQL);

            Assert.AreEqual("[dbo].[users]", sqlServerDb.qid("dbo.users"));
            Assert.AreEqual("`dbo`.`users`", mysqlDb.qid("dbo.users"));
            Assert.AreEqual("plain", sqlServerDb.qid("plain", is_force: false));
        }

        [TestMethod]
        public void Limit_UsesProviderSpecificSyntax()
        {
            var sqlServerDb = new DB("", DB.DBTYPE_SQLSRV);
            var mysqlDb = new DB("", DB.DBTYPE_MYSQL);

            Assert.AreEqual("SELECT TOP 5 * FROM table", sqlServerDb.limit("SELECT * FROM table", 5));
            Assert.AreEqual("SELECT * FROM table LIMIT 5", mysqlDb.limit("SELECT * FROM table", 5));
        }

        [TestMethod]
        public void Q_QuotesAndTruncates()
        {
            var db = new DB("", DB.DBTYPE_SQLSRV);

            Assert.AreEqual("'O''Malley'", db.q("O'Malley"));
            Assert.AreEqual("'abc'", db.q("   abcdef", 3));
        }

        [TestMethod]
        public void QQ_EscapesOnly()
        {
            var db = new DB("", DB.DBTYPE_SQLSRV);

            Assert.AreEqual("O''M", db.qq("O'M"));
        }

        [TestMethod]
        public void QiQfQdec_ConvertValues()
        {
            var db = new DB("", DB.DBTYPE_SQLSRV);

            Assert.AreEqual(12, db.qi("12"));
            Assert.AreEqual(1.5d, db.qf("1.5"));
            Assert.AreEqual(2.5m, db.qdec("2.5"));
        }

        [TestMethod]
        public void Qd_ParsesDatesOrReturnsNull()
        {
            var db = new DB("", DB.DBTYPE_SQLSRV);

            var dt = db.qd("2024-01-02");
            Assert.IsNotNull(dt);
            Assert.AreEqual(2024, dt!.Value.Year);
            Assert.IsNull(db.qd("not a date"));
        }

        [TestMethod]
        public void Insqli_HandlesEmptyAndValues()
        {
            var db = new DB("", DB.DBTYPE_SQLSRV);

            Assert.AreEqual(" IN (NULL)", db.insqli(new int[] { }));
            Assert.AreEqual(" IN (1, 2, 3)", db.insqli(new[] { "1", "2", "3" }));
        }
    }
}
