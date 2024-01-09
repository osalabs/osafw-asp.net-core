using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
            Hashtable h = new();
            h["AAA"] = "1";
            h["BBB"] = "2";
            h["CCC"] = 3;
            h["DDD"] = null;

            string r = Utils.qhRevert((IDictionary)h);

            Assert.IsTrue(r.Contains("AAA|1"));
            Assert.IsTrue(r.Contains("BBB|2"));
            Assert.IsTrue(r.Contains("CCC|3"));
            int p = r.IndexOf("DDD");
            int n = r.IndexOf("ZZZZ");
            // chekc is DDD not have value in string
            Assert.IsTrue(p >= 0);
            if (p < r.Length - 1)
            {
                Assert.IsTrue(r.Contains("DDD| "));
            }
            Assert.IsTrue(n < 0);
        }

        [TestMethod()]
        public void hashFilterTest()
        {
            Hashtable h = new();
            h["AAA"] = "1";
            h["BBB"] = "2";
            h["CCC"] = 3;
            h["DDD"] = null;

            string[] keys = { "DDD", "CCC" };
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
            string s = "ABC123?!_-'%/\\\"";
            string r = Utils.routeFixChars(s);
            Assert.AreEqual(r, "ABC123_-");
        }

        [TestMethod()]
        public void split2Test()
        {
            string s = "test1===test2";
            string r1 = "", r2 = "";
            Utils.split2("===", s, ref r1, ref r2);

            Assert.AreEqual(r1, "test1");
            Assert.AreEqual(r2, "test2");
        }

        [TestMethod()]
        public void splitEmailsTest()
        {
            string s = "1@1.com 2@2.com\r\n3@3.com";
            ArrayList r = Utils.splitEmails(s);

            Assert.AreEqual(r[0], "1@1.com");
            Assert.AreEqual(r[1], "2@2.com");
            Assert.AreEqual(r[2], "3@3.com");
        }

        [TestMethod()]
        public void htmlescapeTest()
        {
            string s = "<html>";
            string r = Utils.htmlescape(s);

            Assert.AreEqual(r, "&lt;html&gt;");
        }

        [TestMethod()]
        public void str2urlTest()
        {
            string s = "test.com";
            string r = Utils.str2url(s);

            Assert.AreEqual(r, "http://test.com");

            s = "http://test.com";
            r = Utils.str2url(s);

            Assert.AreEqual(r, "http://test.com");
        }

        [TestMethod()]
        public void ConvertStreamToBase64Test()
        {
            string s = "ABC123";
            string sb64 = "QUJDMTIz";

            Stream sw = new MemoryStream(Encoding.UTF8.GetBytes(s));
            string r = Utils.streamToBase64(sw);

            Assert.AreEqual(r, sb64);
        }

        [TestMethod()]
        public void f2boolTest()
        {
            // tests for Utils.f2bool() - convert object of any type to bool, in case of error return false
            Assert.IsFalse(Utils.f2bool(null));
            Assert.IsFalse(Utils.f2bool(""));
            Assert.IsFalse(Utils.f2bool(0));
            Assert.IsFalse(Utils.f2bool(0.0));
            Assert.IsFalse(Utils.f2bool(0.0f));
            Assert.IsFalse(Utils.f2bool(0.0m));
            Assert.IsFalse(Utils.f2bool(false));
            Assert.IsFalse(Utils.f2bool("0"));
            Assert.IsFalse(Utils.f2bool("false"));
            Assert.IsFalse(Utils.f2bool("no"));
            Assert.IsFalse(Utils.f2bool("off"));
            Assert.IsFalse(Utils.f2bool("n"));
            Assert.IsFalse(Utils.f2bool("N"));
            Assert.IsFalse(Utils.f2bool("f"));
            Assert.IsFalse(Utils.f2bool("F"));
            Assert.IsFalse(Utils.f2bool("ABC"));
            Assert.IsFalse(Utils.f2bool("yes"));
            Assert.IsFalse(Utils.f2bool("on"));
            Assert.IsFalse(Utils.f2bool(new ArrayList())); //empty arraylist false

            Assert.IsTrue(Utils.f2bool("true"));
            Assert.IsTrue(Utils.f2bool("True"));
            Assert.IsTrue(Utils.f2bool("TRUE"));
            Assert.IsTrue(Utils.f2bool("1")); //non-zero number            
            Assert.IsTrue(Utils.f2bool(new ArrayList() { 1 })); //non-empty arraylist true
        }


        [TestMethod()]
        public void f2dateTest()
        {
            object r = (DateTime)Utils.f2date(DateTime.Now);
            Assert.IsNotNull(r);
            Assert.IsInstanceOfType(r, typeof(DateTime));

            r = (DateTime)Utils.f2date("2021-10-9");

            Assert.IsInstanceOfType(r, typeof(DateTime));
            DateTime d = (DateTime)r;
            Assert.AreEqual(d.Day, 9);
            Assert.AreEqual(d.Month, 10);
            Assert.AreEqual(d.Year, 2021);

            r = (DateTime)Utils.f2date("10/9/2021");

            Assert.IsInstanceOfType(r, typeof(DateTime));
            d = (DateTime)r;
            Assert.AreEqual(d.Day, 9);
            Assert.AreEqual(d.Month, 10);
            Assert.AreEqual(d.Year, 2021);

            r = (DateTime)Utils.f2date("11/10/2020 01:02:03");

            Assert.IsInstanceOfType(r, typeof(DateTime));
            d = (DateTime)r;
            Assert.AreEqual(d.Day, 10);
            Assert.AreEqual(d.Month, 11);
            Assert.AreEqual(d.Year, 2020);
            Assert.AreEqual(d.Hour, 1);
            Assert.AreEqual(d.Minute, 2);
            Assert.AreEqual(d.Second, 3);

            r = Utils.f2date("ABC");
            Assert.IsNull(r);

            r = Utils.f2date("");
            Assert.IsNull(r);

            r = Utils.f2date(null);
            Assert.IsNull(r);

            r = Utils.f2date(DBNull.Value);
            Assert.IsNull(r);
        }

        [TestMethod()]
        public void isDateTest()
        {
            Assert.IsTrue(Utils.isDate("11/10/2020 01:02:03"));
            Assert.IsFalse(Utils.isDate("ABC"));
        }

        [TestMethod()]
        public void f2strTest()
        {
            Assert.AreEqual(Utils.f2str(null), "");
            Assert.AreEqual(Utils.f2str(123), "123");
        }

        [TestMethod()]
        public void f2intTest()
        {
            Assert.IsInstanceOfType(Utils.f2int("123"), typeof(int));
            Assert.AreEqual(Utils.f2int("123"), 123);
            Assert.AreEqual(Utils.f2int("123.123"), 0);
            Assert.AreEqual(Utils.f2int("123b"), 0);
            Assert.AreEqual(Utils.f2int("b123"), 0);
            Assert.AreEqual(Utils.f2int("ABC"), 0);
            Assert.AreEqual(Utils.f2int(null), 0);
        }

        [TestMethod()]
        public void f2floatTest()
        {
            Assert.IsInstanceOfType(Utils.f2float("123.123"), typeof(double));
            Assert.AreEqual(Utils.f2float("123.123"), 123.123);
            Assert.AreEqual(Utils.f2float(123.123), 123.123);
            Assert.AreEqual(Utils.f2float("123"), 123.0);
            Assert.AreEqual(Utils.f2float("123.123b"), 0);
            Assert.AreEqual(Utils.f2float("b123.123"), 0);
            Assert.AreEqual(Utils.f2float("ABC"), 0);
            Assert.AreEqual(Utils.f2float(""), 0);
        }

        [TestMethod()]
        public void isFloatTest()
        {
            Assert.IsFalse(Utils.isFloat(""));
            Assert.IsFalse(Utils.isFloat("ABC"));
            Assert.IsTrue(Utils.isFloat("123.123"));
            Assert.IsTrue(Utils.isFloat(123.123));
        }

        [TestMethod()]
        public void sTrimTest()
        {
            string s = "test text to trim";
            string r = Utils.sTrim(s, 10);
            Assert.AreEqual(r, "test text ...");

            r = Utils.sTrim(s, 100);
            Assert.AreEqual(r, s);
        }

        [TestMethod()]
        public void getRandStrTest()
        {
            string r = Utils.getRandStr(10);
            Assert.AreEqual(r.Length, 10);
        }

        [TestMethod()]
        public void f2longTest()
        {
            long? n = 42;
            Assert.IsInstanceOfType(Utils.f2long(n), typeof(long));
            Assert.IsInstanceOfType(Utils.f2long("123"), typeof(long));
            Assert.AreEqual(Utils.f2long("100M"), 0);
            Assert.AreEqual(Utils.f2long(100M), 100M);
            Assert.AreEqual(Utils.f2long("123"), 123);
            Assert.AreEqual(Utils.f2long(123), 123);
            Assert.AreEqual(Utils.f2long("123"), 123.0);
            Assert.AreEqual(Utils.f2long("123.123b"), 0);
            Assert.AreEqual(Utils.f2long("b123.123"), 0);
            Assert.AreEqual(Utils.f2long("ABC"), 0);
            Assert.AreEqual(Utils.f2long(""), 0);
            Assert.AreEqual(Utils.f2long(null), 0);
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
            Hashtable h1 = new();
            h1["AAA"] = 1;
            h1["BBB"] = 2;
            Hashtable h2 = new();
            h2["CCC"] = 3;
            h2["DDD"] = 4;

            Utils.mergeHash(h1, h2);

            Assert.IsTrue(h1.ContainsKey("AAA"));
            Assert.IsTrue(h1.ContainsKey("BBB"));
            Assert.IsTrue(h1.ContainsKey("CCC"));
            Assert.IsTrue(h1.ContainsKey("DDD"));

            Assert.AreEqual(h1["AAA"], 1);
            Assert.AreEqual(h1["BBB"], 2);
            Assert.AreEqual(h1["CCC"], 3);
            Assert.AreEqual(h1["DDD"], 4);
        }

        [TestMethod()]
        public void mergeHashDeepTest()
        {
            Hashtable h1 = new();
            h1["AAA"] = 1;
            h1["BBB"] = 2;
            Hashtable h2 = new();
            h2["CCC"] = 3;
            h2["DDD"] = new Hashtable() { { "EEE", 5 } };

            Utils.mergeHashDeep(ref h1, ref h2);

            Assert.IsTrue(h1.ContainsKey("AAA"));
            Assert.IsTrue(h1.ContainsKey("BBB"));
            Assert.IsTrue(h1.ContainsKey("CCC"));
            Assert.IsTrue(h1.ContainsKey("DDD"));

            Assert.AreEqual(h1["AAA"], 1);
            Assert.AreEqual(h1["BBB"], 2);
            Assert.AreEqual(h1["CCC"], 3);
            Assert.IsInstanceOfType(h1["DDD"], typeof(Hashtable));
            Assert.IsTrue(((Hashtable)h1["DDD"]).ContainsKey("EEE"));
            Assert.AreEqual(((Hashtable)h1["DDD"])["EEE"], 5);

        }

        [TestMethod()]
        public void bytes2strTest()
        {
            int val = 123;
            string r = Utils.bytes2str(val);
            Assert.AreEqual(r, "123 B");

            val = 1024;
            r = Utils.bytes2str(val);
            Assert.AreEqual(r, "1 KiB");

            val = 1024 * 1024;
            r = Utils.bytes2str(val);
            Assert.AreEqual(r, "1 MiB");

            val = 1024 * 1024 * 1024;
            r = Utils.bytes2str(val);
            Assert.AreEqual(r, "1 GiB");
        }

        [TestMethod()]
        public void jsonEncodeTest()
        {
            Hashtable h1 = new();
            h1["AAA"] = 1;
            h1["BBB"] = 2;
            h1["CCC"] = 3;
            h1["DDD"] = 4;

            string r = Utils.jsonEncode(h1);
            Assert.AreEqual(r.IndexOf("{"), 0);
            Assert.IsTrue(r.Contains("\"AAA\":1"));
            Assert.IsTrue(r.Contains("\"BBB\":2"));
            Assert.IsTrue(r.Contains("\"CCC\":3"));
            Assert.IsTrue(r.Contains("\"DDD\":4"));
        }

        [TestMethod()]
        public void jsonDecodeTest()
        {
            string s = "{\"AAA\":1,\"BBB\":2,\"CCC\":3,\"DDD\":4,\"EEE\":{\"AAA\": \"sub\"}}";
            Hashtable h1 = (Hashtable)Utils.jsonDecode(s);

            Assert.IsTrue(h1.ContainsKey("AAA"));
            Assert.IsTrue(h1.ContainsKey("BBB"));
            Assert.IsTrue(h1.ContainsKey("CCC"));
            Assert.IsTrue(h1.ContainsKey("DDD"));

            Assert.IsTrue((System.Int64)h1["AAA"] == 1);
            Assert.IsTrue((System.Int64)h1["BBB"] == 2);
            Assert.IsTrue((System.Int64)h1["CCC"] == 3);
            Assert.IsTrue((System.Int64)h1["DDD"] == 4);

            Assert.IsInstanceOfType(h1["EEE"], typeof(Hashtable));
            Assert.AreEqual(((Hashtable)h1["EEE"])["AAA"], "sub");
        }

        [TestMethod()]
        public void hashKeysTest()
        {
            Hashtable h1 = new();
            h1["AAA"] = 1;
            h1["BBB"] = 2;
            h1["CCC"] = 3;
            h1["DDD"] = 4;

            string[] r = Utils.hashKeys(h1);

            Assert.IsTrue(r[0] == "AAA" || r[0] == "BBB" || r[0] == "CCC" || r[0] == "DDD");
            Assert.IsTrue(r[1] == "AAA" || r[1] == "BBB" || r[1] == "CCC" || r[1] == "DDD");
            Assert.IsTrue(r[2] == "AAA" || r[2] == "BBB" || r[2] == "CCC" || r[2] == "DDD");
            Assert.IsTrue(r[3] == "AAA" || r[3] == "BBB" || r[3] == "CCC" || r[3] == "DDD");
        }

        [TestMethod()]
        public void capitalizeTest()
        {
            string s = "test1 test2 test3";
            string r = Utils.capitalize(s);

            Assert.AreEqual(r, "Test1 test2 test3");
            r = Utils.capitalize(s, "all");
            Assert.AreEqual(r, "Test1 Test2 Test3");
        }

        [TestMethod()]
        public void strRepeatTest()
        {
            string s = "test1";
            string r = Utils.strRepeat(s, 3);
            Assert.AreEqual(r, "test1test1test1");
        }

        [TestMethod()]
        public void uuidTest()
        {
            string r = Utils.uuid();
            Assert.AreEqual(r.Length, 36);
        }

        [TestMethod()]
        public void getTmpFilenameTest()
        {
            string prefix = Utils.TMP_PREFIX;
            string tmp_path = Utils.getTmpDir(prefix);
            string r = Utils.getTmpFilename(prefix);

            Assert.IsTrue(r.IndexOf(tmp_path) == 0);
            Assert.IsTrue(r.Contains(prefix));
            Assert.AreEqual(r.Length, tmp_path.Length + prefix.Length + 1 + 31);
        }

        [TestMethod()]
        public void cleanupTmpFilesTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void md5Test()
        {
            string s = "ABC";
            string r = Utils.md5(s);
            Assert.AreEqual(r, "902fbdd2b1df0c4f70b4a5d23525e932");
        }

        [TestMethod()]
        public void toXXTest()
        {
            string r = Utils.toXX("1");
            Assert.AreEqual(r, "01");
        }

        [TestMethod()]
        public void num2ordinalTest()
        {
            Assert.AreEqual(Utils.num2ordinal(1), "1st");
            Assert.AreEqual(Utils.num2ordinal(2), "2nd");
            Assert.AreEqual(Utils.num2ordinal(3), "3rd");
            Assert.AreEqual(Utils.num2ordinal(4), "4th");

            Assert.AreEqual(Utils.num2ordinal(21), "21st");
            Assert.AreEqual(Utils.num2ordinal(22), "22nd");
            Assert.AreEqual(Utils.num2ordinal(23), "23rd");
            Assert.AreEqual(Utils.num2ordinal(24), "24th");

            Assert.AreEqual(Utils.num2ordinal(11), "11th");
            Assert.AreEqual(Utils.num2ordinal(12), "12th");
            Assert.AreEqual(Utils.num2ordinal(13), "13th");
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
            Assert.AreEqual(Utils.name2fw("dbo.users"), "users");
            Assert.AreEqual(Utils.name2fw("OrdersProducts/Amount"), "orders_products_amount");
            Assert.AreEqual(Utils.name2fw("roles&users"), "roles_users");
            Assert.AreEqual(Utils.name2fw("roles_+__users"), "roles_users");
            Assert.AreEqual(Utils.name2fw("_JobsDates_"), "jobs_dates");
            Assert.AreEqual(Utils.name2fw("JOBS_"), "jobs");
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

        [TestMethod()]
        public void isEmptyTest()
        {
            // test for Utils.isEmpty - null, empty string, space-only string, integers, long, double, bool, arraylist, hashtable
            Assert.IsTrue(Utils.isEmpty(null));
            Assert.IsTrue(Utils.isEmpty(""));
            Assert.IsTrue(Utils.isEmpty(" "));
            Assert.IsTrue(Utils.isEmpty("  "));
            Assert.IsFalse(Utils.isEmpty("a"));
            Assert.IsFalse(Utils.isEmpty("0"));
            //integers
            Assert.IsTrue(Utils.isEmpty(0));
            Assert.IsFalse(Utils.isEmpty(1));
            Assert.IsFalse(Utils.isEmpty(-1));
            //long
            Assert.IsTrue(Utils.isEmpty(0L));
            Assert.IsFalse(Utils.isEmpty(1L));
            Assert.IsFalse(Utils.isEmpty(-1L));
            //double
            Assert.IsTrue(Utils.isEmpty(0.0));
            Assert.IsFalse(Utils.isEmpty(1.0));
            Assert.IsFalse(Utils.isEmpty(-1.0));
            //bool
            Assert.IsTrue(Utils.isEmpty(false));
            Assert.IsFalse(Utils.isEmpty(true));
            //arraylist
            Assert.IsTrue(Utils.isEmpty(new ArrayList()));
            Assert.IsFalse(Utils.isEmpty(new ArrayList() { 1 }));
            //hashtable
            Assert.IsTrue(Utils.isEmpty(new Hashtable()));
            Assert.IsFalse(Utils.isEmpty(new Hashtable() { { "1", 1 } }));
        }
    }
}