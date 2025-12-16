using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;

namespace osafw.Tests
{
    [TestClass]
    public class FwCollectionsTests
    {
        [TestMethod]
        public void FwDict_IndexerReturnsNullWhenMissing()
        {
            FwDict dict = [];

            Assert.IsNull(dict["missing"]);
        }

        [TestMethod]
        public void FwDict_ImplicitHashtableRoundTripPreservesStringKeys()
        {
            FwDict dict = [];
            dict["AAA"] = 1;
            dict["BBB"] = "two";

            Hashtable table = dict;
            table[123] = "ignored";

            var back = (FwDict)table;

            Assert.HasCount(2, back);
            Assert.AreEqual(1, back["AAA"]);
            Assert.AreEqual("two", back["BBB"]);
            Assert.IsNull(back["123"]);
        }

        [TestMethod]
        public void FwList_ConstructorsFilterDictionaries()
        {
            IList items = new ArrayList
            {
                new FwDict { { "key", "value" } },
                "skip-me",
            };

            var list = new FwList(items);

            Assert.HasCount(1, list);
            Assert.AreEqual("value", list[0]["key"]);

            IEnumerable enumerable = new ArrayList
            {
                new FwDict { { "foo", "bar" } },
                42,
            };

            var listFromEnumerable = new FwList(enumerable);

            Assert.HasCount(1, listFromEnumerable);
            Assert.AreEqual("bar", listFromEnumerable[0]["foo"]);
        }
    }
}
