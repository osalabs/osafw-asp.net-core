using Microsoft.VisualStudio.TestTools.UnitTesting;
using osafw;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            cache.setRequestValue("testCacheKey", "testing");
            Assert.AreEqual(cache.getRequestValue("testCacheKey").ToString(), "testing");

            // test serialization
            Hashtable h = new();
            h["AAA"] = "1";
            h["BBB"] = "2";
            cache.setRequestValue("testCacheKey2", h);
            Hashtable r = (Hashtable)cache.getRequestValue("testCacheKey2");
            Assert.AreEqual(r["AAA"], "1");
            Assert.AreEqual(r["BBB"], "2");
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