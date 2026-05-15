using System;
using System.Collections;
using System.Data.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using osafw;

namespace osafw_tests.App_Code;

[TestClass]
public class DBReadValueTests
{
    [TestMethod]
    public void ReadValue_ConvertsDateTimeColumnToUtc()
    {
        var conf = new FwDict
        {
            { "type", DB.DBTYPE_SQLSRV },
            { "connection_string", "fake" },
            { "timezone", "Pacific Standard Time" },
        };
        var db = new DB(conf, "test");
        var dbValue = new DateTime(2024, 6, 1, 12, 0, 0);
        var reader = new FakeSingleValueReader(dbValue, "datetime", typeof(DateTime));

        var result = db.readValue(reader);

        Assert.IsInstanceOfType(result, typeof(DateTime));
        var utc = (DateTime)result;
        var expected = TimeZoneInfo.ConvertTimeToUtc(dbValue, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        Assert.AreEqual(DateTimeKind.Utc, utc.Kind);
        Assert.AreEqual(expected, utc);
    }

    [TestMethod]
    public void ReadValue_DoesNotConvertDateOnlyColumn()
    {
        var conf = new FwDict
        {
            { "type", DB.DBTYPE_SQLSRV },
            { "connection_string", "fake" },
            { "timezone", "Pacific Standard Time" },
        };
        var db = new DB(conf, "test");
        var dateOnly = new DateTime(2024, 6, 1, 0, 0, 0);
        var reader = new FakeSingleValueReader(dateOnly, "date", typeof(DateTime));

        var result = db.readValue(reader);

        Assert.IsInstanceOfType(result, typeof(DateTime));
        var preserved = (DateTime)result;
        Assert.AreEqual(DateTimeKind.Unspecified, preserved.Kind);
        Assert.AreEqual(dateOnly, preserved);
    }

    [TestMethod]
    public void ReadValue_DoesNotShiftUtcSuffixColumn()
    {
        var conf = new FwDict
        {
            { "type", DB.DBTYPE_SQLSRV },
            { "connection_string", "fake" },
            { "timezone", "Pacific Standard Time" },
        };
        var db = new DB(conf, "test");
        var dbValue = new DateTime(2024, 6, 1, 12, 0, 0);
        var reader = new FakeSingleValueReader(dbValue, "datetime", typeof(DateTime), "sent_at_utc");

        var result = db.readValue(reader);

        Assert.IsInstanceOfType(result, typeof(DateTime));
        var utc = (DateTime)result;
        Assert.AreEqual(DateTimeKind.Utc, utc.Kind);
        Assert.AreEqual(new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc), utc);
    }

    [TestMethod]
    public void ReadValue_ConvertsDateTimeOffsetToUtcDateTime()
    {
        var conf = new FwDict
        {
            { "type", DB.DBTYPE_SQLSRV },
            { "connection_string", "fake" },
            { "timezone", "Pacific Standard Time" },
        };
        var db = new DB(conf, "test");
        var dto = new DateTimeOffset(2024, 6, 1, 15, 0, 0, TimeSpan.FromHours(3));
        var reader = new FakeSingleValueReader(dto, "datetimeoffset", typeof(DateTimeOffset), "sent_at_utc");

        var result = db.readValue(reader);

        Assert.IsInstanceOfType(result, typeof(DateTime));
        var utc = (DateTime)result;
        Assert.AreEqual(DateTimeKind.Utc, utc.Kind);
        Assert.AreEqual(dto.UtcDateTime, utc);
    }

    [TestMethod]
    public void ReadRow_DoesNotShiftUtcSuffixColumn()
    {
        var db = new ExposedDB(testConf(), "test");
        var dbValue = new DateTime(2024, 6, 1, 12, 0, 0);
        var reader = new FakeSingleValueReader(dbValue, "datetime", typeof(DateTime), "event_time_utc");
        Assert.IsTrue(reader.Read());

        var row = db.ReadRowForTest(reader);

        Assert.AreEqual("2024-06-01 12:00:00", row["event_time_utc"]);
    }

    [TestMethod]
    public void ReadTypedRow_PreservesDateTimeOffset()
    {
        var db = new ExposedDB(testConf(), "test");
        var dto = new DateTimeOffset(2024, 6, 1, 15, 0, 0, TimeSpan.FromHours(3));
        var reader = new FakeSingleValueReader(dto, "datetimeoffset", typeof(DateTimeOffset), "event_time_utc");
        Assert.IsTrue(reader.Read());

        var row = db.ReadTypedRowForTest<OffsetRow>(reader);

        Assert.AreEqual(dto, row.event_time_utc);
    }

    private static FwDict testConf()
    {
        return new FwDict
        {
            { "type", DB.DBTYPE_SQLSRV },
            { "connection_string", "fake" },
            { "timezone", "Pacific Standard Time" },
        };
    }

    private sealed class ExposedDB(FwDict conf, string dbName) : DB(conf, dbName)
    {
        public DBRow ReadRowForTest(DbDataReader reader)
        {
            return readRow(reader);
        }

        public T ReadTypedRowForTest<T>(DbDataReader reader) where T : new()
        {
            return readRow<T>(reader);
        }
    }

    private sealed class OffsetRow
    {
        public DateTimeOffset event_time_utc { get; set; }
    }

    private sealed class FakeSingleValueReader : DbDataReader
    {
        private readonly object _value;
        private readonly string _dataTypeName;
        private readonly Type _fieldType;
        private bool _read;

        private readonly string _name;

        public FakeSingleValueReader(object value, string dataTypeName, Type fieldType, string name = "col")
        {
            _value = value;
            _dataTypeName = dataTypeName;
            _fieldType = fieldType;
            _name = name;
        }

        public override int FieldCount => 1;
        public override bool HasRows => true;
        public override bool IsClosed => _read;
        public override int RecordsAffected => 0;
        public override int Depth => 0;

        public override object this[int i] => _value;
        public override object this[string name] => _value;

        public override string GetName(int i) => _name;
        public override string GetDataTypeName(int i) => _dataTypeName;
        public override Type GetFieldType(int i) => _fieldType;
        public override int GetOrdinal(string name) => 0;

        public override bool Read()
        {
            if (_read)
                return false;

            _read = true;
            return true;
        }

        public override bool NextResult() => false;
        public override int GetValues(object[] values)
        {
            if (values == null || values.Length == 0)
                return 0;

            values[0] = _value;
            return 1;
        }

        public override object GetValue(int i) => _value;
        public override T GetFieldValue<T>(int ordinal) => (T)_value;
        public override bool IsDBNull(int i) => _value is null || _value == DBNull.Value;

        public override bool GetBoolean(int i) => (bool)_value!;
        public override byte GetByte(int i) => (byte)_value!;
        public override long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public override char GetChar(int i) => (char)_value!;
        public override long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public override Guid GetGuid(int i) => (Guid)_value!;
        public override short GetInt16(int i) => Convert.ToInt16(_value);
        public override int GetInt32(int i) => Convert.ToInt32(_value);
        public override long GetInt64(int i) => Convert.ToInt64(_value);
        public override DateTime GetDateTime(int i) => (DateTime)_value!;
        public override string GetString(int i) => _value?.ToString() ?? string.Empty;
        public override decimal GetDecimal(int i) => Convert.ToDecimal(_value);
        public override double GetDouble(int i) => Convert.ToDouble(_value);
        public override float GetFloat(int i) => Convert.ToSingle(_value);
        public override IEnumerator GetEnumerator() => new[] { _value }.GetEnumerator();
    }
}
