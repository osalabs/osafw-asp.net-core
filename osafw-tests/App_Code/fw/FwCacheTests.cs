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
            Assert.AreEqual(FwCache.getValue("testCacheKey").ToString(), "testing");

            // test serialization
            Hashtable h = new();
            h["AAA"] = "1";
            h["BBB"] = "2";
            FwCache.setValue("testCacheKey2", h);
            Hashtable r = (Hashtable)FwCache.getValue("testCacheKey2");
            Assert.AreEqual(r["AAA"], "1");
            Assert.AreEqual(r["BBB"], "2");
        }

        [TestMethod()]
        public void setValueTest()
        {
            FwCache.setValue("testCacheKey", "testing set");
            Assert.AreEqual(FwCache.getValue("testCacheKey").ToString(), "testing set");

            // test serialization
            Hashtable h = new();
            h["CCC"] = "3";
            h["DDD"] = "4";
            FwCache.setValue("testCacheKey2", h);
            Hashtable r = (Hashtable)FwCache.getValue("testCacheKey2");
            Assert.AreEqual(r["CCC"], "3");
            Assert.AreEqual(r["DDD"], "4");
        }

        [TestMethod()]
        public void removeTest()
        {
            FwCache.setValue("testCacheKey", "testing remove");
            Assert.AreEqual(FwCache.getValue("testCacheKey").ToString(), "testing remove");
            FwCache.remove("testCacheKey");
            Assert.IsNull(FwCache.getValue("testCacheKey"));
        }

        [TestMethod()]
        public void clearTest()
        {
            FwCache.setValue("testCacheKey", "testing remove");
            Assert.AreEqual(FwCache.getValue("testCacheKey").ToString(), "testing remove");
            FwCache.setValue("testCacheKey2", "testing remove2");
            Assert.AreEqual(FwCache.getValue("testCacheKey2").ToString(), "testing remove2");

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
            Assert.AreEqual(cache.getRequestValue("testCacheKey").ToString(), "testing");

            // test serialization of int
            cache.setRequestValue("testCacheKey2", 123);
            Assert.AreEqual(cache.getRequestValue("testCacheKey2"), (System.Int64)123);

            // test serialization of decimal
            cache.setRequestValue("testCacheKey3", 123.456);
            Assert.AreEqual(cache.getRequestValue("testCacheKey3"), (System.Decimal)123.456);

            // test serialization of bool
            cache.setRequestValue("testCacheKey4", true);
            Assert.AreEqual(cache.getRequestValue("testCacheKey4"), true);

            // test serialization of Hashtable
            Hashtable h = new();
            h["AAA"] = "1";
            h["BBB"] = "2";
            cache.setRequestValue("testCacheKey2", h);
            Hashtable r = (Hashtable)cache.getRequestValue("testCacheKey2");
            Assert.AreEqual(r["AAA"], "1");
            Assert.AreEqual(r["BBB"], "2");

            // test serialization of DBRow
            DBRow row = new();
            row["AAA"] = "1";
            row["BBB"] = "2";
            cache.setRequestValue("testCacheKey3", row);
            DBRow r2 = (DBRow)(Hashtable)cache.getRequestValue("testCacheKey3");
            Assert.AreEqual(r2["AAA"], "1");
            Assert.AreEqual(r2["BBB"], "2");

            // test serialization of IList (arrays)
            ArrayList a = new();
            a.Add("1");
            a.Add("2");
            cache.setRequestValue("testCacheKey4", a);
            ArrayList r3 = (ArrayList)cache.getRequestValue("testCacheKey4");
            Assert.AreEqual(r3[0], "1");
            Assert.AreEqual(r3[1], "2");

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
            Assert.AreEqual(cache.getRequestValue("testCacheKey").ToString(), "testing set");

            // test serialization
            Hashtable h = new();
            h["CCC"] = "3";
            h["DDD"] = "4";
            cache.setRequestValue("testCacheKey2", h);
            Hashtable r = (Hashtable)cache.getRequestValue("testCacheKey2");
            Assert.AreEqual(r["CCC"], "3");
            Assert.AreEqual(r["DDD"], "4");
        }

        [TestMethod()]
        public void requestRemoveTest()
        {
            FwCache cache = new();
            cache.setRequestValue("testCacheKey", "testing remove");
            Assert.AreEqual(cache.getRequestValue("testCacheKey").ToString(), "testing remove");
            cache.requestRemove("testCacheKey");
            Assert.IsNull(cache.getRequestValue("testCacheKey"));
        }

        [TestMethod()]
        public void requestRemoveWithPrefixTest()
        {
            FwCache cache = new();
            cache.setRequestValue("test_CacheKey", "testing remove");
            Assert.AreEqual(cache.getRequestValue("test_CacheKey").ToString(), "testing remove");
            cache.setRequestValue("test_CacheKey2", "testing remove2");
            Assert.AreEqual(cache.getRequestValue("test_CacheKey2").ToString(), "testing remove2");

            cache.requestRemoveWithPrefix("test_");

            Assert.IsNull(cache.getRequestValue("test_CacheKey"));
            Assert.IsNull(cache.getRequestValue("test_CacheKey2"));
        }

        [TestMethod()]
        public void requestClearTest()
        {
            FwCache cache = new();
            cache.setRequestValue("testCacheKey", "testing remove");
            Assert.AreEqual(cache.getRequestValue("testCacheKey").ToString(), "testing remove");
            cache.setRequestValue("testCacheKey2", "testing remove2");
            Assert.AreEqual(cache.getRequestValue("testCacheKey2").ToString(), "testing remove2");

            cache.requestClear();

            Assert.IsNull(cache.getRequestValue("testCacheKey"));
            Assert.IsNull(cache.getRequestValue("testCacheKey2"));
        }
    }
}