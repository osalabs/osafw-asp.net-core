using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;

namespace osafw.Tests
{
    [TestClass()]
    public class FwCacheTests
    {
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
        public void setValueTest()
        {
            FwCache.setValue("testCacheKey", "testing set");
            var strValue = FwCache.getValue("testCacheKey");
            Assert.IsNotNull(strValue);
            Assert.AreEqual("testing set", strValue.ToString());

            // test serialization
            FwDict h = [];
            h["CCC"] = "3";
            h["DDD"] = "4";
            FwCache.setValue("testCacheKey2", h);
            var r = (FwDict?)FwCache.getValue("testCacheKey2");
            Assert.IsNotNull(r);
            Assert.AreEqual("3", r!["CCC"]);
            Assert.AreEqual("4", r["DDD"]);
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
            Assert.AreEqual((System.Decimal)123.456, cache.getRequestValue("testCacheKey3"));

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
            Assert.AreEqual("1", r!["AAA"]);
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
            Assert.AreEqual("1", r3![0].ToString());
            Assert.AreEqual("2", r3[1].ToString());

            // test object that cannot be serialized to json, so it's stored as is
            cache.setRequestValue("testCacheKey5", new System.IO.MemoryStream());
            var result = cache.getRequestValue("testCacheKey5");
            Assert.IsInstanceOfType(result, typeof(System.IO.MemoryStream));
        }

        [TestMethod()]
        public void setRequestValueTest()
        {
            FwCache cache = new();
            cache.setRequestValue("testCacheKey", "testing set");
            var strValue = cache.getRequestValue("testCacheKey");
            Assert.IsNotNull(strValue);
            Assert.AreEqual("testing set", strValue.ToString());

            // test serialization
            FwDict h = [];
            h["CCC"] = "3";
            h["DDD"] = "4";
            cache.setRequestValue("testCacheKey2", h);
            var r = (FwDict?)cache.getRequestValue("testCacheKey2");
            Assert.IsNotNull(r);
            Assert.AreEqual("3", r!["CCC"]);
            Assert.AreEqual("4", r["DDD"]);
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