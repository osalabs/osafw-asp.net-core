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

            Assert.AreEqual(2, parts.Length);
            Assert.AreEqual("SELECT 1", parts[0].Trim());
            Assert.AreEqual("SELECT 2", parts[1].Trim().TrimEnd(';'));
        }
    }
}
