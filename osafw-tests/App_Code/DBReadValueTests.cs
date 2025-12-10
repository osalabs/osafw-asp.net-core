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
        var conf = new FwRow
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
        var conf = new FwRow
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

    private sealed class FakeSingleValueReader : DbDataReader
    {
        private readonly object _value;
        private readonly string _dataTypeName;
        private readonly Type _fieldType;
        private bool _read;

        public FakeSingleValueReader(object value, string dataTypeName, Type fieldType)
        {
            _value = value;
            _dataTypeName = dataTypeName;
            _fieldType = fieldType;
        }

        public override int FieldCount => 1;
        public override bool HasRows => true;
        public override bool IsClosed => _read;
        public override int RecordsAffected => 0;
        public override int Depth => 0;

        public override object this[int i] => _value;
        public override object this[string name] => _value;

        public override string GetName(int i) => "col";
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
