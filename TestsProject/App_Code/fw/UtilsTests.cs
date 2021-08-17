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
    public class UtilsTests
    {
        [TestMethod()]
        public void qwTest()
        {
            string[] r = Utils.qw("test1 test2 test3");
            Assert.AreEqual(r.Length, 3);
            Assert.AreEqual(r[0], "test1");
            Assert.AreEqual(r[1], "test2");
            Assert.AreEqual(r[2], "test3");
        }

        [TestMethod()]
        public void qwRevertTest()
        {
            IList<string> list = new List<string>() { };
            list.Add("test1");
            list.Add("test2");
            list.Add("test3");
            string r = Utils.qwRevert(list.ToList());

            Assert.AreEqual(r, "test1 test2 test3 ");
        }

        [TestMethod()]
        public void qhTest()
        {
            string s = "AAA|1 BBB|2 CCC|3 DDD";
            Hashtable h = Utils.qh(s);

            Assert.AreEqual(h["AAA"], "1");
            Assert.AreEqual(h["BBB"], "2");
            Assert.AreEqual(h["CCC"], "3");
            Assert.IsNull(h["DDD"]);
        }

        [TestMethod()]
        public void qhRevertTest()
        {
            Hashtable h = new Hashtable();
            h["AAA"] = "1";
            h["BBB"] = "2";
            h["CCC"] = 3;
            h["DDD"] = null;

            string r = Utils.qhRevert((IDictionary)h);

            Assert.IsTrue(r.IndexOf("AAA|1") >= 0);
            Assert.IsTrue(r.IndexOf("BBB|2") >= 0);
            Assert.IsTrue(r.IndexOf("CCC|3") >= 0);
            int p = r.IndexOf("DDD");
            // chekc is DDD not have value in string
            Assert.IsTrue(p >= 0);
            if (p < r.Length - 1)
            {
                Assert.IsTrue(r.IndexOf("DDD| ") >= 0);
            }
        }

        [TestMethod()]
        public void hashFilterTest()
        {
            Hashtable h = new Hashtable();
            h["AAA"] = "1";
            h["BBB"] = "2";
            h["CCC"] = 3;
            h["DDD"] = null;

            string [] keys = { "DDD", "CCC" };
            Utils.hashFilter(h, keys);

            Assert.AreEqual(h.Keys.Count, 2);
            Assert.IsTrue(h.Contains("AAA"));
            Assert.IsTrue(h.Contains("BBB"));
            Assert.AreEqual(h["AAA"], "1");
            Assert.AreEqual(h["BBB"], "2");
            Assert.IsFalse(h.Contains("CCC"));
            Assert.IsFalse(h.Contains("DDD"));

            throw new NotImplementedException();
        }

        [TestMethod()]
        public void routeFixCharsTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void split2Test()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void splitEmailsTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void htmlescapeTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void str2urlTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void ConvertStreamToBase64Test()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void f2boolTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void f2dateTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void isDateTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void f2strTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void f2intTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void f2floatTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void isFloatTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void sTrimTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void getRandStrTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void importCSVTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void importExcelTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void toCSVRowTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void getCSVExportTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void writeCSVExportTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void writeXLSExportTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void rotateImageTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void resizeImageTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void fileSizeTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void fileNameTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void mergeHashTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void mergeHashDeepTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void bytes2strTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void jsonEncodeTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void jsonDecodeTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void jsonDecodeTest1()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void cast2stdTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void serializeTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void deserializeTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void hashKeysTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void capitalizeTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void strRepeatTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void uuidTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void getTmpFilenameTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void cleanupTmpFilesTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void md5Test()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void toXXTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void num2ordinalTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void str2truncateTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void orderbyApplySortdirTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void html2textTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void commastr2hashTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void commastr2nlstrTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void arrayInjectTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void urlescapeTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void UploadFilesToRemoteUrlTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void name2fwTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void name2humanTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void nameCamelCaseTest()
        {
            throw new NotImplementedException();
        }
    }
}