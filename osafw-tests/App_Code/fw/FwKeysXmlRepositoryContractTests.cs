using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace osafw.Tests;

[TestClass]
public class FwKeysXmlRepositoryContractTests
{
    [TestMethod]
    public void StoreElementUpdatesFieldsUsingKeyIdentityAsWhere()
    {
        var db = new RecordingKeysDb() { Exists = true };
        var repository = new FwKeysXmlRepository(db);
        var element = new XElement(
            "key",
            new XAttribute("id", "key-42"),
            new XElement("descriptor", "protected material"));

        repository.StoreElement(element, "friendly-key");

        Assert.AreEqual("fwkeys", db.UpdatedTable);
        Assert.IsNotNull(db.UpdatedFields);
        Assert.IsNotNull(db.UpdatedWhere);
        Assert.AreEqual("key-42", db.UpdatedWhere!["iname"]);
        Assert.AreEqual(10, db.UpdatedWhere["itype"]);
        Assert.IsTrue(db.UpdatedFields!.ContainsKey("XmlValue"));
        Assert.IsTrue(db.UpdatedFields.ContainsKey("upd_time"));
        Assert.IsFalse(db.UpdatedFields.ContainsKey("iname"));
        Assert.IsFalse(db.UpdatedFields.ContainsKey("itype"));
        StringAssert.Contains(db.UpdatedFields["XmlValue"].toStr(), "descriptor");
    }

    [TestMethod]
    public void StoreElementUsesFriendlyNameForRevocationElements()
    {
        var db = new RecordingKeysDb();
        var repository = new FwKeysXmlRepository(db);
        var revocation = new XElement(
            "revocation",
            new XAttribute("version", "1"),
            new XElement("revocationDate", "2026-09-27T00:00:00Z"));

        repository.StoreElement(revocation, "revocation-20260927");

        Assert.AreEqual("fwkeys", db.InsertedTable);
        Assert.IsNotNull(db.InsertedFields);
        Assert.AreEqual("revocation-20260927", db.InsertedFields!["iname"]);
        Assert.AreEqual(10, db.InsertedFields["itype"]);
        StringAssert.Contains(db.InsertedFields["XmlValue"].toStr(), "revocationDate");
    }

    [TestMethod]
    public void RepositoryDoesNotDeleteKeysByAge()
    {
        var db = new RecordingKeysDb();
        db.StoredXml.Add(new XElement("key", new XAttribute("id", "old-key")).ToString());
        var repository = new FwKeysXmlRepository(db);

        var elements = repository.GetAllElements();
        repository.StoreElement(new XElement("key", new XAttribute("id", "new-key")), "friendly-key");

        Assert.HasCount(1, elements);
        Assert.AreEqual(0, db.ExecCount, "The repository must retain old keys for decryption and revocation.");
    }

    [TestMethod]
    public void GetAllElementsTurnsReadFailureIntoCryptographicFailure()
    {
        var db = new RecordingKeysDb() { ReadError = new InvalidOperationException("database detail") };
        var repository = new FwKeysXmlRepository(db);

        var error = Assert.ThrowsExactly<CryptographicException>(() => repository.GetAllElements());

        StringAssert.Contains(error.Message, "Unable to read");
        Assert.IsFalse(error.ToString().Contains("database detail", StringComparison.Ordinal));
    }

    [TestMethod]
    public void StoreElementTurnsWriteFailureIntoCryptographicFailure()
    {
        var db = new RecordingKeysDb() { WriteError = new InvalidOperationException("database detail") };
        var repository = new FwKeysXmlRepository(db);

        var error = Assert.ThrowsExactly<CryptographicException>(
            () => repository.StoreElement(new XElement("key", new XAttribute("id", "key-1")), "friendly-key"));

        StringAssert.Contains(error.Message, "Unable to persist");
        Assert.IsFalse(error.ToString().Contains("database detail", StringComparison.Ordinal));
    }

    private sealed class RecordingKeysDb : DB
    {
        public bool Exists;
        public int ExecCount;
        public string? InsertedTable;
        public FwDict? InsertedFields;
        public Exception? ReadError;
        public List<string> StoredXml { get; } = [];
        public string? UpdatedTable;
        public FwDict? UpdatedFields;
        public FwDict? UpdatedWhere;
        public Exception? WriteError;

        public RecordingKeysDb() : base("", DBTYPE_SQLSRV)
        {
        }

        public override List<string> col(
            string table,
            FwDict where,
            string field_name,
            string order_by = "",
            int limit = -1)
        {
            if (ReadError != null)
                throw ReadError;

            return [.. StoredXml];
        }

        public override object? value(string table, FwDict where, string field_name = "", string order_by = "")
        {
            if (WriteError != null)
                throw WriteError;

            return Exists ? 1 : 0;
        }

        public override int insert(string table, IDictionary fields)
        {
            if (WriteError != null)
                throw WriteError;

            InsertedTable = table;
            InsertedFields = copy(fields);
            return 1;
        }

        public override int insert(string table, FwDict fields)
        {
            return insert(table, (IDictionary)fields);
        }

        public override int update(string table, IDictionary fields, IDictionary where)
        {
            if (WriteError != null)
                throw WriteError;

            UpdatedTable = table;
            UpdatedFields = copy(fields);
            UpdatedWhere = copy(where);
            return 1;
        }

        public override int update(string table, FwDict fields, FwDict where)
        {
            return update(table, (IDictionary)fields, (IDictionary)where);
        }

        public override int exec(string sql, FwDict? @params = null, bool is_get_identity = false)
        {
            ExecCount++;
            return 0;
        }

        private static FwDict copy(IDictionary values)
        {
            var result = new FwDict();

            foreach (DictionaryEntry entry in values)
                result[entry.Key.toStr()] = entry.Value;

            return result;
        }
    }
}
