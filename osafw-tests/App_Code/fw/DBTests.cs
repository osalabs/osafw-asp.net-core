using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace osafw.Tests
{
    [TestClass, TestCategory("SqlServer")]
    public class DBTests
    {
        private static SqlServerTestDatabase? database;
        private static string connstr => database!.ConnectionString;

        [ClassInitialize]
        public static void InitializeDatabase(TestContext context)
        {
            if (Environment.GetEnvironmentVariable("FW_TEST_SQLSERVER") != "1")
                Assert.Inconclusive("Use scripts/test.ps1 -Profile SqlServer to provision an isolated database.");
            database = new SqlServerTestDatabase();
        }

        [ClassCleanup]
        public static void DisposeDatabase() => database?.Dispose();
        private DB db = null!;
        private readonly string table_name = "for_unit_testing";
        private bool isDbAvailable = false;

        [TestInitialize()]
        public void Startup()
        {
            try
            {
                db = new DB(connstr, "SQL", "main");
                db.connect();
                // create tables for testing
                db.exec($"DROP TABLE IF EXISTS {table_name}");
                db.exec($@"CREATE TABLE {table_name} (
                            id              INT,
                            iname           NVARCHAR(64) NOT NULL default '',
                            idatetime       DATETIME2,
                            idatetime_utc   DATETIME2,
                            ioffset         DATETIMEOFFSET,
                            ioffset_utc     DATETIMEOFFSET,
                            fdate           DATE
                        )");
                db.exec($"INSERT INTO {table_name} (id, iname) VALUES (1,'test1'),(2,'test2'),(3,'test3')");
                isDbAvailable = true;
            }
            catch (Exception ex)
            {
                isDbAvailable = false;
                db?.Dispose();
                throw new AssertFailedException("Disposable SQL Server fixture setup failed.", ex);
            }
        }

        [TestCleanup()]
        public void Cleanup()
        {
            if (!isDbAvailable)
                return;

            db.exec($"DROP TABLE {table_name}");
            db.disconnect();
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
            Assert.IsInstanceOfType<DbConnection>(db.getConnection());
        }

        [TestMethod()]
        public void createConnectionTest()
        {
            using var result = db.createConnection(connstr);
            Assert.AreEqual(System.Data.ConnectionState.Open, result.State);
        }

        [TestMethod()]
        public void queryTest()
        {
            DbDataReader dbread = db.query("SELECT * FROM " + table_name + " WHERE id=1;");

            dbread.Read();
            Assert.IsTrue(dbread.HasRows);
            Assert.IsGreaterThan(0, dbread.FieldCount);

            dbread.Close();
        }

        [TestMethod()]
        public void execTest()
        {
            var tables = db.tables();
            if (tables.Contains("exec_unit_testing"))
            {
                db.exec("DROP TABLE exec_unit_testing");
            }
            db.exec("CREATE TABLE exec_unit_testing(id INT)");
            tables = db.tables();
            Assert.Contains("exec_unit_testing", tables);
            db.exec("DROP TABLE exec_unit_testing");
        }

        [TestMethod()]
        public void rowTest()
        {
            var row = db.rowp("SELECT * FROM " + table_name + " WHERE id=1;");

            Assert.IsNotEmpty(row);
            Assert.IsTrue(row.ContainsKey("id"));
            Assert.IsTrue(row.ContainsKey("iname"));
            Assert.AreEqual("test1", row["iname"]);
            // TODO test all methods types
        }

        [TestMethod()]
        public void rowTypedTest()
        {
            var row = db.row<Demos.Row>(table_name, DB.h("id", 1)) ?? throw new AssertFailedException("Expected typed row.");
            Assert.AreEqual(1, row.id);
            Assert.AreEqual("test1", row.iname);
        }

        [TestMethod()]
        public void rowTypedMissingTest()
        {
            var row = db.row<Demos.Row>(table_name, DB.h("id", 999));
            Assert.IsNull(row);

            var rowp = db.rowp<Demos.Row>("SELECT * FROM " + table_name + " WHERE id=999;");
            Assert.IsNull(rowp);

            var dictRow = db.row(table_name, DB.h("id", 999));
            Assert.IsNotNull(dictRow);
            Assert.AreEqual(0, dictRow.Count);
        }

        [TestMethod()]
        public void arrayTest()
        {
            DBList rows = db.arrayp("SELECT * FROM " + table_name + ";");

            foreach (var row in rows)
            {
                Assert.IsNotEmpty(row);
                Assert.IsTrue(row.ContainsKey("id"));
                Assert.IsTrue(row.ContainsKey("iname"));
            }

            Assert.AreEqual("test1", rows[0]["iname"]);
            Assert.AreEqual("test2", rows[1]["iname"]);
            Assert.AreEqual("test3", rows[2]["iname"]);
            // TODO test all methods types
        }

        [TestMethod()]
        public void arrayTypedTest()
        {
            List<Demos.Row> rows = db.array<Demos.Row>(table_name, DB.h());
            foreach (var row in rows)
            {
                Assert.IsGreaterThan(0, row.id);
                Assert.IsGreaterThan(0, row.iname.Length);
            }
            Assert.AreEqual("test1", rows[0].iname);
            Assert.AreEqual("test2", rows[1].iname);
            Assert.AreEqual("test3", rows[2].iname);
        }

        [TestMethod()]
        public void arrayLimitOffsetTest()
        {
            DBList firstPage = db.array(table_name, DB.h(), "id", offset: 0, limit: 2);
            Assert.HasCount(2, firstPage);
            Assert.AreEqual("test1", firstPage[0]["iname"]);
            Assert.AreEqual("test2", firstPage[1]["iname"]);

            DBList secondRow = db.array(table_name, DB.h(), "id", offset: 1, limit: 1);
            Assert.HasCount(1, secondRow);
            Assert.AreEqual("test2", secondRow[0]["iname"]);

            DBList empty = db.array(table_name, DB.h(), "id", offset: 0, limit: 0);
            Assert.HasCount(0, empty);
        }

        [TestMethod()]
        public void arrayTypedLimitOffsetTest()
        {
            List<Demos.Row> rows = db.array<Demos.Row>(table_name, DB.h(), "id", offset: 1, limit: 1);

            Assert.HasCount(1, rows);
            Assert.AreEqual("test2", rows[0].iname);
        }

        [TestMethod()]
        public void modelListByWhereUsesOffsetLimitTest()
        {
            var fw = TestHelpers.CreateFw();
            fw.db = db;
            var model = new PagingModel(fw, table_name);

            DBList rows = model.listByWhere(offset: 1, limit: 1, orderby: "id");
            Assert.HasCount(1, rows);
            Assert.AreEqual("test2", rows[0]["iname"]);

            List<PagingRow> typedRows = model.listTByWhere(offset: 2, limit: 1, orderby: "id");
            Assert.HasCount(1, typedRows);
            Assert.AreEqual("test3", typedRows[0].iname);
        }

        [TestMethod()]
        public void colTest()
        {
            StrList col = db.colp("SELECT iname FROM " + table_name);

            Assert.AreEqual("test1", col[0]);
            Assert.AreEqual("test2", col[1]);
            Assert.AreEqual("test3", col[2]);
            // TODO test all methods types
        }

        [TestMethod()]
        public void valuepTest()
        {
            string value = db.valuep("SELECT iname FROM " + table_name + " WHERE id=1;").toStr();
            Assert.AreEqual("test1", value);
        }

        [TestMethod()]
        public void valueTest()
        {
            // 1. value("table", where)
            string value = db.value(table_name, DB.h("id", 1)).toStr();
            Assert.AreEqual("1", value);

            // 2. value("table", where, "field1")
            value = db.value(table_name, DB.h("id", 1), "iname").toStr();
            Assert.AreEqual("test1", value);

            // 3. value("table", where, "1") 'just return 1, useful for exists queries
            value = db.value(table_name, DB.h("id", 1), "1").toStr();
            Assert.AreEqual("1", value);

            // 4. value("table", where, "count(*)", "id asc")
            value = db.value(table_name, [], "count(*)").toStr();
            Assert.AreEqual("3", value);

            // 5. value("table", where, "MAX(id)")
            value = db.value(table_name, [], "MAX(id)").toStr();
            Assert.AreEqual("3", value);

            //fail test with using bad aggregate function
            try
            {
                value = db.value(table_name, [], "BAD(id)").toStr();
                Assert.Fail("Expected exception not thrown");
            }
            catch (Exception ex)
            {
                Assert.AreEqual("'BAD' is not a recognized built-in function name.", ex.Message);
            }
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
            var now_time = db.Now();
            db.insert(table_name, DB.h("id", 6, "iname", "test6", "idatetime", DB.NOW));
            var r = db.row(table_name, DB.h("id", 6));
            var db_time = DateTime.Parse(r["idatetime"].toStr());
            Assert.AreEqual(now_time.ToString("yyyy-MM-dd HH:mm:ss"), db_time.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        [TestMethod()]
        public void DateFieldReadTest()
        {
            var dt = new DateTime(2023, 1, 17);
            db.insert(table_name, DB.h("id", 7, "iname", "testdate", "fdate", dt));
            var r = db.row(table_name, DB.h("id", 7));
            Assert.AreEqual(dt.ToString("yyyy-MM-dd"), r["fdate"]);
        }

        [TestMethod()]
        public void UtcSuffixAndDateTimeOffsetRoundTripWithConfiguredDbTimezone()
        {
            using var tzDb = dbWithTimezone("Pacific Standard Time");
            var utc = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            tzDb.insert(table_name, DB.h(
                "id", 8,
                "iname", "test_utc_suffix",
                "idatetime", utc,
                "idatetime_utc", utc,
                "ioffset_utc", utc));

            var row = tzDb.row(table_name, DB.h("id", 8));
            Assert.AreEqual("2024-06-01 12:00:00", row["idatetime"]);
            Assert.AreEqual("2024-06-01 12:00:00", row["idatetime_utc"]);
            Assert.AreEqual("2024-06-01 12:00:00", row["ioffset_utc"]);

            var stored = tzDb.rowp($@"
                SELECT
                    CONVERT(varchar(19), idatetime, 120) AS idatetime,
                    CONVERT(varchar(19), idatetime_utc, 120) AS idatetime_utc,
                    CONVERT(varchar(19), CAST(ioffset_utc AS datetime2), 120) AS ioffset_utc,
                    DATEPART(TZOFFSET, ioffset_utc) AS ioffset_offset
                FROM {tzDb.qid(table_name)}
                WHERE id = @id", DB.h("@id", 8));

            Assert.AreEqual("2024-06-01 05:00:00", stored["idatetime"]);
            Assert.AreEqual("2024-06-01 12:00:00", stored["idatetime_utc"]);
            Assert.AreEqual("2024-06-01 12:00:00", stored["ioffset_utc"]);
            Assert.AreEqual("0", stored["ioffset_offset"]);
        }

        [TestMethod()]
        public void TypedReadPreservesDateTimeOffset()
        {
            using var tzDb = dbWithTimezone("Pacific Standard Time");
            var utc = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var offset = new DateTimeOffset(2024, 6, 1, 15, 0, 0, TimeSpan.FromHours(3));

            tzDb.insert(table_name, DB.h(
                "id", 9,
                "iname", "test_datetimeoffset",
                "idatetime_utc", utc,
                "ioffset_utc", offset));

            var row = tzDb.row<TimezoneRow>(table_name, DB.h("id", 9));

            Assert.IsNotNull(row);
            Assert.AreEqual(utc, row!.idatetime_utc);
            Assert.AreEqual(offset.ToUniversalTime(), row.ioffset_utc);
        }

        [TestMethod()]
        public void UtcSuffixDbNowUsesUtcCurrentTime()
        {
            using var tzDb = dbWithTimezone("Pacific Standard Time");
            var before = DateTime.UtcNow.AddSeconds(-10);

            tzDb.insert(table_name, DB.h(
                "id", 10,
                "iname", "test_now_utc",
                "idatetime_utc", DB.NOW,
                "ioffset_utc", DB.NOW));

            var after = DateTime.UtcNow.AddSeconds(10);
            var stored = tzDb.rowp($@"
                SELECT
                    CONVERT(varchar(19), idatetime_utc, 120) AS idatetime_utc,
                    CONVERT(varchar(19), CAST(SWITCHOFFSET(ioffset_utc, '+00:00') AS datetime2), 120) AS ioffset_utc,
                    DATEPART(TZOFFSET, ioffset_utc) AS ioffset_offset
                FROM {tzDb.qid(table_name)}
                WHERE id = @id", DB.h("@id", 10));

            var storedDateTime = DateTime.Parse(stored["idatetime_utc"].toStr(), CultureInfo.InvariantCulture);
            var storedOffsetUtc = DateTime.Parse(stored["ioffset_utc"].toStr(), CultureInfo.InvariantCulture);

            Assert.IsTrue(storedDateTime >= before && storedDateTime <= after);
            Assert.IsTrue(storedOffsetUtc >= before && storedOffsetUtc <= after);
            Assert.AreEqual("0", stored["ioffset_offset"].toStr());
        }

        [TestMethod()]
        public void RawDateTimeOffsetParameterPreservesOffset()
        {
            using var tzDb = dbWithTimezone("Pacific Standard Time");
            var offset = new DateTimeOffset(2024, 6, 1, 15, 0, 0, TimeSpan.FromHours(3));

            tzDb.exec($@"
                INSERT INTO {tzDb.qid(table_name)} (id, iname, ioffset)
                VALUES (@id, @iname, @value)", DB.h(
                "@id", 11,
                "@iname", "test_raw_offset",
                "@value", offset));

            var stored = tzDb.rowp($@"
                SELECT
                    CONVERT(varchar(19), CAST(ioffset AS datetime2), 120) AS wall_time,
                    CONVERT(varchar(19), CAST(SWITCHOFFSET(ioffset, '+00:00') AS datetime2), 120) AS utc_time,
                    DATEPART(TZOFFSET, ioffset) AS offset_minutes
                FROM {tzDb.qid(table_name)}
                WHERE id = @id", DB.h("@id", 11));

            Assert.AreEqual("2024-06-01 15:00:00", stored["wall_time"]);
            Assert.AreEqual("2024-06-01 12:00:00", stored["utc_time"]);
            Assert.AreEqual("180", stored["offset_minutes"].toStr());
        }

        [TestMethod()]
        public void RawExpandedUtcParameterKeepsUtcSuffix()
        {
            using var tzDb = dbWithTimezone("Pacific Standard Time");
            var utc = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            var row = tzDb.rowp(
                "SELECT CONVERT(varchar(19), @sent_at_utc, 120) AS sent_at",
                DB.h("sent_at_utc", new[] { utc }));

            Assert.AreEqual("2024-06-01 12:00:00", row["sent_at"]);
        }

        [TestMethod()]
        public void InvalidConfiguredTimezoneFallsBackButIsNotDetected()
        {
            var conf = new FwDict
            {
                ["type"] = DB.DBTYPE_SQLSRV,
                ["connection_string"] = connstr,
                ["timezone"] = "Invalid/Timezone",
            };
            using var tzDb = new DB(conf, "main");

            tzDb.connect();

            Assert.AreEqual(DateUtils.TZ_UTC, tzDb.getTimezoneId());
            Assert.IsFalse(tzDb.isTimezoneDetectionOk());
        }

        [TestMethod()]
        public void DemoTimezoneFieldsRoundTripAgainstDemoTable()
        {
            if (db.valuep("SELECT OBJECT_ID('dbo.demos')").toStr() == "")
                Assert.Inconclusive("dbo.demos is not available in the local demo database.");

            using var tzDb = dbWithTimezone("Pacific Standard Time");
            var email = "tz-demo-" + Guid.NewGuid().ToString("N") + "@example.test";
            var utc = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var offset = new DateTimeOffset(2024, 6, 1, 15, 0, 0, TimeSpan.FromHours(3));

            try
            {
                tzDb.insert("demos", DB.h(
                    "iname", "Timezone demo test",
                    "email", email,
                    "fdatetime", utc,
                    "fdatetime_utc", utc,
                    "fdatetime_offset", offset,
                    "fdatetime_local", utc));

                var row = tzDb.row("demos", DB.h("email", email));
                Assert.AreEqual("2024-06-01 12:00:00", row["fdatetime"]);
                Assert.AreEqual("2024-06-01 12:00:00", row["fdatetime_utc"]);
                Assert.AreEqual("2024-06-01 12:00:00", row["fdatetime_offset"]);
                Assert.AreEqual("2024-06-01 12:00:00", row["fdatetime_local"]);

                var stored = tzDb.rowp(@"
                    SELECT
                        CONVERT(varchar(19), fdatetime, 120) AS fdatetime,
                        CONVERT(varchar(19), fdatetime_utc, 120) AS fdatetime_utc,
                        CONVERT(varchar(19), CAST(fdatetime_offset AS datetime2), 120) AS fdatetime_offset,
                        DATEPART(TZOFFSET, fdatetime_offset) AS offset_minutes,
                        CONVERT(varchar(19), fdatetime_local, 120) AS fdatetime_local
                    FROM demos
                    WHERE email=@email", DB.h("@email", email));

                Assert.AreEqual("2024-06-01 05:00:00", stored["fdatetime"]);
                Assert.AreEqual("2024-06-01 12:00:00", stored["fdatetime_utc"]);
                Assert.AreEqual("2024-06-01 15:00:00", stored["fdatetime_offset"]);
                Assert.AreEqual("180", stored["offset_minutes"].toStr());
                Assert.AreEqual("2024-06-01 05:00:00", stored["fdatetime_local"]);
            }
            finally
            {
                tzDb.del("demos", DB.h("email", email));
            }
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

        private DB dbWithTimezone(string timezone)
        {
            var conf = new FwDict
            {
                ["type"] = DB.DBTYPE_SQLSRV,
                ["connection_string"] = connstr,
                ["timezone"] = timezone,
            };

            return new DB(conf, "main");
        }

        private sealed class TimezoneRow
        {
            public DateTime idatetime_utc { get; set; }
            public DateTimeOffset ioffset_utc { get; set; }
        }

        private sealed class PagingRow
        {
            public int id { get; set; }
            public string iname { get; set; } = string.Empty;
        }

        private sealed class PagingModel : FwModel<PagingRow>
        {
            public PagingModel(FW fw, string tableName)
                : base(fw)
            {
                table_name = tableName;
                field_status = "";
            }
        }

        [TestMethod()]
        public void delTest()
        {
            db.del(table_name, DB.h("id", 3));
            var r = db.row(table_name, DB.h("id", 3));
            Assert.IsEmpty(r);
        }

        [TestMethod()]
        public void tablesTest()
        {
            string[] tablesToCheck = Utils.qw("users att settings menu_items att_categories fwsessions");
            var tables = db.tables();
            foreach (var tableName in tablesToCheck)
            {
                Assert.IsGreaterThanOrEqualTo(0, tables.IndexOf(tableName), tableName + " not found");
            }
        }

        [TestMethod()]
        public void viewsTest()
        {
            db.exec("CREATE VIEW view_for_unit_tests AS SELECT * FROM users");
            var views = db.views();
            db.exec("DROP VIEW view_for_unit_tests");
            Assert.IsGreaterThanOrEqualTo(0, views.IndexOf("view_for_unit_tests"));
        }

        [TestMethod()]
        public void tableSchemaFullTest()
        {
            FwDict schema = db.tableSchemaFull("users");
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
            _ = db.loadTableSchema("users");
            Assert.IsFalse(db.isSchemaCacheEmpty());
            db.clearSchemaCache();
            Assert.IsTrue(db.isSchemaCacheEmpty());
        }

        [TestMethod()]
        public void prepareParams()
        {
            // 1. Test Insert
            var fields = new FwDict {
                { "iname", "John" },
                { "email", "john@example.com" }
            };
            var result = db.prepareParams("demos", fields, "insert");
            Assert.Contains("@iname", result.sql);
            Assert.Contains("@email", result.sql);

            // 2. Test Update
            fields["iname"] = "Jane";
            result = db.prepareParams("demos", fields, "update");
            Assert.Contains("iname = @iname", result.sql);
            Assert.Contains("email = @email", result.sql);

            // 3. Test WHERE
            result = db.prepareParams("demos", fields);
            // separate as order of fields is not guaranteed
            Assert.Contains(" AND ", result.sql, "failed result: " + result.sql);
            Assert.Contains("iname = @iname", result.sql, "failed result: " + result.sql);
            Assert.Contains("email = @email", result.sql, "failed result: " + result.sql);

            // 4. Test Empty Fields
            fields.Clear();
            result = db.prepareParams("demos", fields);
            Assert.AreEqual("", result.sql);

            // 5. Test Special Operations (BETWEEN, IN)
            fields["fint"] = db.opIN(new[] { 1, 2, 3 }); // Assuming this will be interpreted as an IN operation
            result = db.prepareParams("demos", fields);
            Assert.Contains("fint IN (@fint_1,@fint_2,@fint_3)", result.sql, "failed result: " + result.sql);

            fields.Clear();
            fields["fdate_pop"] = db.opBETWEEN(DateTime.Today, DateTime.Today.AddDays(1));
            result = db.prepareParams("demos", fields);
            Assert.Contains("fdate_pop BETWEEN @fdate_pop_1 AND @fdate_pop_2", result.sql, "failed result: " + result.sql);
        }
    }
}
