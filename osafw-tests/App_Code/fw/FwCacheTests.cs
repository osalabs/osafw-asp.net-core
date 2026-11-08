using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;

namespace osafw.Tests
{
    [TestClass()]
    public class FwCacheTests
    {
        public sealed class CachedRow
        {
            public int id { get; set; }
            public System.DateTime? run_utc { get; set; }
            public decimal amount { get; set; }
            public string? note { get; set; }
            [System.Text.Json.Serialization.JsonIgnore]
            [DBName("stored_name")]
            public string ignored { get; private set; } = "";
            public object scalar { get; set; } = 12;
            public byte[] binary { get; set; } = [1, 2];
        }

        public sealed class IndexedRow
        {
            public int id { get; set; }
            public string this[int index] { get => "value"; set { } }
        }

        [TestMethod]
        public void TypedRequestRow_UnsupportedDtoFallsBackToDatabaseReads()
        {
            var cache = new FwCache();
            cache.setRequestRow("row", new IndexedRow { id = 7 });
            Assert.IsNull(cache.getRequestRow<IndexedRow>("row"));
        }

        [TestMethod]
        public void TypedRequestRow_PreservesMappingAndReturnsIndependentCopies()
        {
            var cache = new FwCache();
            var instant = new System.DateTime(2026, 11, 8, 6, 30, 0, System.DateTimeKind.Utc).AddTicks(1234);
            var source = new CachedRow { id = 7, run_utc = instant, amount = 1.23456789m };
            DB.h("stored_name", "mapped").applyTo(source);
            cache.setRequestRow("row", source);
            source.binary[1] = 8;
            var row = cache.getRequestRow<CachedRow>("row")!;
            Assert.AreEqual(instant, row.run_utc);
            Assert.AreEqual(System.DateTimeKind.Utc, row.run_utc!.Value.Kind);
            Assert.AreEqual(1.23456789m, row.amount);
            Assert.IsNull(row.note);
            Assert.AreEqual("mapped", row.ignored);
            Assert.AreEqual((byte)2, row.binary[1]);
            Assert.IsInstanceOfType<int>(row.scalar);
            row.binary[0] = 9;
            Assert.AreEqual((byte)1, cache.getRequestRow<CachedRow>("row")!.binary[0]);
            row.id = 99;
            Assert.AreEqual(7, cache.getRequestRow<CachedRow>("row")!.id);
            Assert.IsNull(cache.getRequestRow<CachedRow>("missing"));
            cache.requestRemove("row");
            Assert.IsNull(cache.getRequestRow<CachedRow>("row"));
        }

        [TestMethod()]
        public void getValueTest()
        {
            FwCache.setValue("testCacheKey", "testing");
            var strValue = FwCache.getValue("testCacheKey");
            Assert.IsNotNull(strValue);
            Assert.AreEqual("testing", strValue.ToString());

            // test serialization
            FwDict h = [];
            h["AAA"] = "1";
            h["BBB"] = "2";
            FwCache.setValue("testCacheKey2", h);
            var r = (FwDict?)FwCache.getValue("testCacheKey2");
            Assert.IsNotNull(r);
            Assert.AreEqual("1", r!["AAA"]);
            Assert.AreEqual("2", r["BBB"]);
        }

        [TestMethod()]
        public void removeTest()
        {
            FwCache.setValue("testCacheKey", "testing remove");
            var strValue = FwCache.getValue("testCacheKey");
            Assert.IsNotNull(strValue);
            Assert.AreEqual("testing remove", strValue.ToString());
            FwCache.remove("testCacheKey");
            Assert.IsNull(FwCache.getValue("testCacheKey"));
        }

        [TestMethod()]
        public void clearTest()
        {
            FwCache.setValue("testCacheKey", "testing remove");
            var firstValue = FwCache.getValue("testCacheKey");
            Assert.IsNotNull(firstValue);
            Assert.AreEqual("testing remove", firstValue.ToString());
            FwCache.setValue("testCacheKey2", "testing remove2");
            var secondValue = FwCache.getValue("testCacheKey2");
            Assert.IsNotNull(secondValue);
            Assert.AreEqual("testing remove2", secondValue.ToString());

            FwCache.clear();
            Assert.IsNull(FwCache.getValue("testCacheKey"));
            Assert.IsNull(FwCache.getValue("testCacheKey2"));
        }

        [TestMethod()]
        public void getRequestValueTest()
        {
            FwCache cache = new();

            // test serialization of string
            cache.setRequestValue("testCacheKey", "testing");
            var strValue = cache.getRequestValue("testCacheKey");
            Assert.IsNotNull(strValue);
            Assert.AreEqual("testing", strValue.ToString());

            // test serialization of int
            cache.setRequestValue("testCacheKey2", 123);
            Assert.AreEqual((System.Int64)123, cache.getRequestValue("testCacheKey2"));

            // test serialization of decimal
            cache.setRequestValue("testCacheKey3", 123.456);
            Assert.AreEqual(123.456m, cache.getRequestValue("testCacheKey3"));

            // test serialization of bool
            cache.setRequestValue("testCacheKey4", true);
            Assert.IsTrue((bool?)cache.getRequestValue("testCacheKey4"));

            // test serialization of FwRow
            FwDict h = [];
            h["AAA"] = "1";
            h["BBB"] = "2";
            cache.setRequestValue("testCacheKey2", h);
            var r = (FwDict?)cache.getRequestValue("testCacheKey2");
            Assert.IsNotNull(r);
            Assert.AreEqual("1", r["AAA"]);
            Assert.AreEqual("2", r["BBB"]);

            // test serialization of DBRow
            DBRow row = [];
            row["AAA"] = "1";
            row["BBB"] = "2";
            cache.setRequestValue("testCacheKey3", row);
            var r2Hash = cache.getRequestValue("testCacheKey3") as FwDict;
            Assert.IsNotNull(r2Hash);
            var r2 = (DBRow)r2Hash!;
            Assert.AreEqual("1", r2["AAA"]);
            Assert.AreEqual("2", r2["BBB"]);

            // test serialization of IList (arrays)
            StrList a = ["1", "2"];
            cache.setRequestValue("testCacheKey4", a);
            var r3 = cache.getRequestValue("testCacheKey4") as IList;
            Assert.IsNotNull(r3);
            var r3List = r3 ?? throw new AssertFailedException("Expected list result");
            Assert.AreEqual("1", r3List[0]?.ToString());
            Assert.AreEqual("2", r3List[1]?.ToString());

            // test object that cannot be serialized to json, so it's stored as is
            cache.setRequestValue("testCacheKey5", new System.IO.MemoryStream());
            var result = cache.getRequestValue("testCacheKey5");
            Assert.IsInstanceOfType(result, typeof(System.IO.MemoryStream));
        }

        [TestMethod()]
        public void requestRemoveTest()
        {
            FwCache cache = new();
            cache.setRequestValue("testCacheKey", "testing remove");
            var strValue = cache.getRequestValue("testCacheKey");
            Assert.IsNotNull(strValue);
            Assert.AreEqual("testing remove", strValue.ToString());
            cache.requestRemove("testCacheKey");
            Assert.IsNull(cache.getRequestValue("testCacheKey"));
        }

        [TestMethod()]
        public void requestRemoveWithPrefixTest()
        {
            FwCache cache = new();
            cache.setRequestValue("test_CacheKey", "testing remove");
            var firstValue = cache.getRequestValue("test_CacheKey");
            Assert.IsNotNull(firstValue);
            Assert.AreEqual("testing remove", firstValue.ToString());
            cache.setRequestValue("test_CacheKey2", "testing remove2");
            var secondValue = cache.getRequestValue("test_CacheKey2");
            Assert.IsNotNull(secondValue);
            Assert.AreEqual("testing remove2", secondValue.ToString());

            cache.requestRemoveWithPrefix("test_");

            Assert.IsNull(cache.getRequestValue("test_CacheKey"));
            Assert.IsNull(cache.getRequestValue("test_CacheKey2"));
        }

        [TestMethod()]
        public void requestClearTest()
        {
            FwCache cache = new();
            cache.setRequestValue("testCacheKey", "testing remove");
            var firstValue = cache.getRequestValue("testCacheKey");
            Assert.IsNotNull(firstValue);
            Assert.AreEqual("testing remove", firstValue.ToString());
            cache.setRequestValue("testCacheKey2", "testing remove2");
            var secondValue = cache.getRequestValue("testCacheKey2");
            Assert.IsNotNull(secondValue);
            Assert.AreEqual("testing remove2", secondValue.ToString());

            cache.requestClear();

            Assert.IsNull(cache.getRequestValue("testCacheKey"));
            Assert.IsNull(cache.getRequestValue("testCacheKey2"));
        }
    }
}