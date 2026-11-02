using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace osafw.Tests;

[TestClass]
public class DBPureTests
{
    private readonly DB db = new RejectingDb();
    private const string table_name = "for_unit_testing";

        [TestMethod()]
        public void hTest()
        {
            FwDict h = DB.h("AAA", 1, "BBB", 2, "CCC", 3, "DDD", 4);
            Assert.AreEqual(1, h["AAA"]);
            Assert.AreEqual(2, h["BBB"]);
            Assert.AreEqual(3, h["CCC"]);
            Assert.AreEqual(4, h["DDD"]);
        }


        [TestMethod()]
        public void field2typedIntEmptyStringTest()
        {
            Assert.AreSame(DBNull.Value, db.field2typed("int", ""));
            Assert.AreEqual(0L, db.field2typed("int", "0"));
            Assert.AreNotSame(DBNull.Value, db.field2typed("int", "   "));
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

            // Test with StrList
            StrList list = ["a", "b", "c"];
            string result4 = db.insql(list);
            Assert.AreEqual(" IN ('a', 'b', 'c')", result4);

            // Test with empty StrList
            StrList emptyList = [];
            string result5 = db.insql(emptyList);
            Assert.AreEqual(" IN (NULL)", result5);

            // Test with IntList
            IntList intList = [1, 2, 3];
            string result6 = db.insql(intList);
            Assert.AreEqual(" IN (1, 2, 3)", result6);
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

            // Test with StrList
            StrList list = ["1", "2", "3"];
            string result5 = db.insqli(list);
            Assert.AreEqual(" IN (1, 2, 3)", result5);

            // Test with IntList
            IntList intList = [1, 2, 3];
            string result6 = db.insqli(intList);
            Assert.AreEqual(" IN (1, 2, 3)", result6);
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
            var values = r.value as object[] ?? throw new AssertFailedException("Expected value array");
            Assert.AreEqual("test_value", values[0].toStr());
            Assert.AreEqual("test_value2", values[1].toStr());
        }

        [TestMethod()]
        public void opNOTINTest()
        {
            DBOperation r = db.opNOTIN("test_value", "test_value2");
            Assert.IsTrue(r.is_value);
            Assert.AreEqual(DBOps.NOTIN, r.op);
            Assert.AreEqual("NOT IN", r.opstr);
            var values = r.value as object[] ?? throw new AssertFailedException("Expected value array");
            Assert.AreEqual("test_value", values[0].toStr());
            Assert.AreEqual("test_value2", values[1].toStr());
        }

        [TestMethod()]
        public void opBETWEENTest()
        {
            DBOperation r = db.opBETWEEN("test_value", "test_value2");
            Assert.IsTrue(r.is_value);
            Assert.AreEqual(DBOps.BETWEEN, r.op);
            Assert.AreEqual(r.opstr, DBOps.BETWEEN.ToString());
            var values = r.value as object[] ?? throw new AssertFailedException("Expected value array");
            Assert.AreEqual("test_value", values[0].toStr());
            Assert.AreEqual("test_value2", values[1].toStr());
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
        public void splitMultiSQLForSqlServerKeepsBeginEndBlockTogetherTest()
        {
            string sql = @"--- user timezone auto preference

IF COL_LENGTH('dbo.users', 'timezone') IS NOT NULL
BEGIN
    UPDATE dbo.users
    SET timezone = ''
    WHERE timezone = 'UTC';

    DECLARE @timezoneDefaultConstraint sysname;

    SELECT @timezoneDefaultConstraint = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.users')
      AND c.name = N'timezone';

    IF @timezoneDefaultConstraint IS NOT NULL
    BEGIN
        DECLARE @sql NVARCHAR(MAX);
        SET @sql = N'ALTER TABLE dbo.users DROP CONSTRAINT ' + QUOTENAME(@timezoneDefaultConstraint);
        EXEC sp_executesql @sql;
    END

    ALTER TABLE dbo.users ADD CONSTRAINT DF_users_timezone DEFAULT '' FOR timezone;
END";

            string[] queries = DB.splitMultiSQLForSqlServer(sql);

            Assert.AreEqual(1, queries.Length);
            Assert.Contains("UPDATE dbo.users", queries[0]);
            Assert.Contains("EXEC sp_executesql @sql", queries[0]);
            Assert.Contains("ALTER TABLE dbo.users ADD CONSTRAINT", queries[0]);
        }

        [TestMethod()]
        public void splitMultiSQLForSqlServerSplitsSimpleSemicolonBatchesTest()
        {
            string sql = @"DROP VIEW IF EXISTS dbo.AdminControllerList;
CREATE VIEW dbo.AdminControllerList AS SELECT 1 AS id;
";

            string[] queries = DB.splitMultiSQLForSqlServer(sql);

            Assert.AreEqual(2, queries.Length);
            Assert.AreEqual("DROP VIEW IF EXISTS dbo.AdminControllerList", queries[0]);
            Assert.AreEqual("CREATE VIEW dbo.AdminControllerList AS SELECT 1 AS id;", queries[1]);
        }

        [TestMethod()]
        public void splitMultiSQLForSqlServerKeepsModuleDdlBatchTogetherTest()
        {
            string sql = @"CREATE PROCEDURE dbo.TestProcedure AS
SELECT 1 AS first_value;
SELECT 2 AS second_value;
GO
SELECT 3;";

            string[] queries = DB.splitMultiSQLForSqlServer(sql);

            Assert.AreEqual(2, queries.Length);
            Assert.Contains("CREATE PROCEDURE dbo.TestProcedure", queries[0]);
            Assert.Contains("SELECT 1 AS first_value;", queries[0]);
            Assert.Contains("SELECT 2 AS second_value;", queries[0]);
            Assert.AreEqual("SELECT 3;", queries[1]);
        }

        [TestMethod()]
        public void splitMultiSQLBatchesIgnoresGoInsideCommentsAndStringsTest()
        {
            string sql = @"SELECT 'GO';
-- GO
/*
GO
*/
GO -- batch separator
SELECT 2;";

            string[] queries = DB.splitMultiSQLBatches(sql);

            Assert.AreEqual(2, queries.Length);
            Assert.Contains("SELECT 'GO';", queries[0]);
            Assert.Contains("-- GO", queries[0]);
            Assert.Contains("/*", queries[0]);
            Assert.AreEqual("SELECT 2;", queries[1]);
        }

        [TestMethod()]
        public void splitMultiSQLForSqlServerKeepsIfElseBranchesTogetherTest()
        {
            string sql = @"IF 1 = 1
    UPDATE dbo.users SET timezone = '';
ELSE
    UPDATE dbo.users SET timezone = 'UTC';
SELECT 2;";

            string[] queries = DB.splitMultiSQLForSqlServer(sql);

            Assert.AreEqual(1, queries.Length);
            Assert.Contains("ELSE", queries[0]);
            Assert.Contains("timezone = 'UTC'", queries[0]);
            Assert.Contains("SELECT 2;", queries[0]);
        }

        [TestMethod()]
        public void splitMultiSQLForSqlServerKeepsSqlServerVariablesInOneBatchTest()
        {
            string sql = @"DECLARE @ConstraintName nvarchar(256);
SELECT @ConstraintName = 'DF_users_timezone';
IF @ConstraintName IS NOT NULL
    EXEC('ALTER TABLE users DROP CONSTRAINT ' + @ConstraintName);
GO
SELECT 2;";

            string[] queries = DB.splitMultiSQLForSqlServer(sql);

            Assert.AreEqual(2, queries.Length);
            Assert.Contains("DECLARE @ConstraintName", queries[0]);
            Assert.Contains("SELECT @ConstraintName", queries[0]);
            Assert.Contains("EXEC('ALTER TABLE users DROP CONSTRAINT ' + @ConstraintName)", queries[0]);
            Assert.AreEqual("SELECT 2;", queries[1]);
        }

}
