using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace osafw.Tests;

[TestClass]
public class FwKeysTests
{
    private class ThrowingDb : DB
    {
        public ThrowingDb() : base("", DB.DBTYPE_SQLSRV) { }
        public override List<string> col(string table, FwDict where, string field_name, string order_by = "", int limit = -1) => throw new Exception("fail");
    }

    private class MemoryKeysDb : DB
    {
        public List<FwDict> Rows { get; } = new();
        public bool ExecCalled { get; private set; }

        public MemoryKeysDb() : base("", DB.DBTYPE_SQLSRV) { }

        public override List<string> col(string table, FwDict where, string field_name, string order_by = "", int limit = -1)
        {
            return Rows.Where(r => r["itype"].toInt() == where["itype"].toInt()).Select(r => r[field_name].toStr()).ToList();
        }

        public override object? value(string table, FwDict where, string field_name = "", string order_by = "")
            => Rows.Any(r => r["itype"].toInt() == where["itype"].toInt() && r["iname"].toStr() == where["iname"].toStr()) ? 1 : 0;

        public override int insert(string table, IDictionary fields)
        {
            var iname = fields["iname"].toStr();
            Rows.RemoveAll(r => r["iname"].toStr() == iname);
            var row = new FwDict();
            foreach (DictionaryEntry kv in fields)
                row[kv.Key.toStr()] = kv.Value;
            Rows.Add(row);
            return Rows.Count;
        }

        public override int insert(string table, FwDict fields)
        {
            return insert(table, (IDictionary)fields);
        }

        public override int update(string table, IDictionary fields, IDictionary where)
        {
            var row = Rows.Single(r => r["itype"].toInt() == where["itype"].toInt() && r["iname"].toStr() == where["iname"].toStr());
            foreach (DictionaryEntry kv in fields)
                row[kv.Key.toStr()] = kv.Value;
            return 1;
        }

        public override int update(string table, FwDict fields, FwDict where)
            => update(table, (IDictionary)fields, (IDictionary)where);

        public override int exec(string sql, FwDict? @params = null, bool is_get_identity = false)
        {
            ExecCalled = true;
            return 0;
        }
    }

    [TestMethod]
    public void GetAllElements_ThrowsWhenDurableStoreCannotBeRead()
    {
        var repo = new FwKeysXmlRepository(new ThrowingDb());

        Assert.ThrowsExactly<CryptographicException>(() => repo.GetAllElements());
    }

    [TestMethod]
    public void StoreElement_InsertsAndUpdatesKeys()
    {
        var db = new MemoryKeysDb();
        var repo = new FwKeysXmlRepository(db);
        var element = new XElement("key", new XAttribute("id", "abc"));

        repo.StoreElement(element, "friendly");

        Assert.HasCount(1, db.Rows);
        Assert.AreEqual("abc", db.Rows[0]["iname"]);

        var updated = new XElement("key", new XAttribute("id", "abc"), new XElement("child", "v"));
        repo.StoreElement(updated, "friendly");

        Assert.HasCount(1, db.Rows);
        StringAssert.Contains(db.Rows[0]["XmlValue"].toStr(), "child");
        Assert.IsFalse(db.ExecCalled, "Old protection keys must not be deleted by age.");
    }
}
