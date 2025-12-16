using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
            Assert.HasCount(3, r);
            Assert.AreEqual("test1", r[0]);
            Assert.AreEqual("test2", r[1]);
            Assert.AreEqual("test3", r[2]);
        }

        [TestMethod()]
        public void qwRevertTest()
        {
            StrList list = ["test1", "test2", "test3"];
            string r = Utils.qwRevert(list);

            Assert.AreEqual("test1 test2 test3 ", r);
        }

        [TestMethod()]
        public void qhTest()
        {
            string s = "AAA|1 BBB|2 CCC|3 DDD";
            FwDict h = Utils.qh(s);

            Assert.AreEqual("1", h["AAA"]);
            Assert.AreEqual("2", h["BBB"]);
            Assert.AreEqual("3", h["CCC"]);
            Assert.AreEqual("1", h["DDD"]);
        }

        [TestMethod()]
        public void qhRevertTest()
        {
            FwDict h = [];
            h["AAA"] = "1";
            h["BBB"] = "2";
            h["CCC"] = 3;
            h["DDD"] = null;
            h["EE"] = 5;

            string r = Utils.qhRevert(h);

            Assert.Contains("AAA|1", r);
            Assert.Contains("BBB|2", r);
            Assert.Contains("CCC|3", r);
            int p = r.IndexOf("DDD");
            int n = r.IndexOf("ZZZZ");
            // check is DDD not have value in string
            Assert.IsGreaterThanOrEqualTo(0, p);
            if (p < r.Length - 1)
            {
                Assert.Contains("DDD| ", r);
            }
            Assert.IsLessThan(0, n);
        }

        [TestMethod()]
        public void hashFilterTest()
        {
            FwDict h = [];
            h["AAA"] = "1";
            h["BBB"] = "2";
            h["CCC"] = 3;
            h["DDD"] = null;

            string[] keys = ["DDD", "CCC"];
            Utils.hashFilter(h, keys);

            Assert.HasCount(2, h.Keys);
            Assert.IsFalse(h.ContainsKey("AAA"));
            Assert.IsFalse(h.ContainsKey("BBB"));
            Assert.IsTrue(h.ContainsKey("CCC"));
            Assert.IsTrue(h.ContainsKey("DDD"));
            Assert.AreEqual(3, h["CCC"]);
            Assert.IsNull(h["DDD"]);

        }

        [TestMethod()]
        public void routeFixCharsTest()
        {
            string s = "ABC123?!_-'%/\\\"";
            string r = Utils.routeFixChars(s);
            Assert.AreEqual("ABC123_-", r);
        }

        [TestMethod()]
        public void split2Test()
        {
            string s = "test1===test2";
            string r1 = "", r2 = "";
            Utils.split2("===", s, ref r1, ref r2);

            Assert.AreEqual("test1", r1);
            Assert.AreEqual("test2", r2);
        }

        [TestMethod()]
        public void splitEmailsTest()
        {
            string s = "1@1.com 2@2.com\r\n3@3.com";
            StrList r = Utils.splitEmails(s);

            Assert.AreEqual("1@1.com", r[0]);
            Assert.AreEqual("2@2.com", r[1]);
            Assert.AreEqual("3@3.com", r[2]);
        }

        [TestMethod()]
        public void htmlescapeTest()
        {
            string s = "<html>";
            string r = Utils.htmlescape(s);

            Assert.AreEqual("&lt;html&gt;", r);
        }

        [TestMethod()]
        public void str2urlTest()
        {
            string s = "test.com";
            string r = Utils.str2url(s);

            Assert.AreEqual("http://test.com", r);

            s = "http://test.com";
            r = Utils.str2url(s);

            Assert.AreEqual("http://test.com", r);
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
        public void isDateTest()
        {
            Assert.IsTrue(Utils.isDate("11/10/2020 01:02:03"));
            Assert.IsFalse(Utils.isDate("ABC"));
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
            Assert.AreEqual("test text ...", r);

            r = Utils.sTrim(s, 100);
            Assert.AreEqual(r, s);
        }

        [TestMethod()]
        public void getRandStrTest()
        {
            string r = Utils.getRandStr(10);
            Assert.AreEqual(10, r.Length);
        }

        [TestMethod]
        public void IsFloatTests()
        {
            // Case 1: Test valid float string
            object input1 = "123.45";
            bool result1 = Utils.isFloat(input1);
            Assert.IsTrue(result1, "Result should be true for valid float string");

            // Case 2: Test valid integer string
            object input2 = "123";
            bool result2 = Utils.isFloat(input2);
            Assert.IsTrue(result2, "Result should be true for valid integer string");

            // Case 3: Test null input
            object? input3 = null;
            bool result3 = Utils.isFloat(input3);
            Assert.IsFalse(result3, "Result should be false for null input");

            // Case 4: Test empty string input
            object input4 = "";
            bool result4 = Utils.isFloat(input4);
            Assert.IsFalse(result4, "Result should be false for empty string input");

            // Case 5: Test invalid string input
            object input5 = "abc";
            bool result5 = Utils.isFloat(input5);
            Assert.IsFalse(result5, "Result should be false for invalid string input");

            // Case 6: Test float input
            object input6 = 123.45f;
            bool result6 = Utils.isFloat(input6);
            Assert.IsTrue(result6, "Result should be true for float input");

            // Case 7: Test integer input
            object input7 = 123;
            bool result7 = Utils.isFloat(input7);
            Assert.IsTrue(result7, "Result should be true for integer input");

            // Case 8: Test negative float string
            object input8 = "-123.45";
            bool result8 = Utils.isFloat(input8);
            Assert.IsTrue(result8, "Result should be true for negative float string");

            // Case 9: Test negative integer string
            object input9 = "-123";
            bool result9 = Utils.isFloat(input9);
            Assert.IsTrue(result9, "Result should be true for negative integer string");

        }

        [TestMethod]
        public void isIntTest()
        {
            // Case 1: Test valid integer string
            object input1 = "123";
            bool result1 = Utils.isInt(input1);
            Assert.IsTrue(result1, "Result should be true for valid integer string");

            // Case 2: Test valid negative integer string
            object input2 = "-123";
            bool result2 = Utils.isInt(input2);
            Assert.IsTrue(result2, "Result should be true for valid negative integer string");

            // Case 3: Test null input
            object? input3 = null;
            bool result3 = Utils.isInt(input3);
            Assert.IsFalse(result3, "Result should be false for null input");

            // Case 4: Test empty string input
            object input4 = "";
            bool result4 = Utils.isInt(input4);
            Assert.IsFalse(result4, "Result should be false for empty string input");

            // Case 5: Test invalid string input
            object input5 = "abc";
            bool result5 = Utils.isInt(input5);
            Assert.IsFalse(result5, "Result should be false for invalid string input");

            // Case 6: Test float input
            object input6 = 123.45f;
            bool result6 = Utils.isInt(input6);
            Assert.IsFalse(result6, "Result should be false for float input");

            // Case 7: Test float string input
            object input7 = "123.45";
            bool result7 = Utils.isInt(input7);
            Assert.IsFalse(result7, "Result should be false for float string input");
        }

        [TestMethod()]
        public void isLongTest()
        {
            // Case 1: Test valid long string
            object input1 = "1234567890123456789";
            bool result1 = Utils.isLong(input1);
            Assert.IsTrue(result1, "Result should be true for valid long string");

            // Case 2: Test valid negative long string
            object input2 = "-1234567890123456789";
            bool result2 = Utils.isLong(input2);
            Assert.IsTrue(result2, "Result should be true for valid negative long string");

            // Case 3: Test null input
            object? input3 = null;
            bool result3 = Utils.isLong(input3);
            Assert.IsFalse(result3, "Result should be false for null input");

            // Case 4: Test empty string input
            object input4 = "";
            bool result4 = Utils.isLong(input4);
            Assert.IsFalse(result4, "Result should be false for empty string input");

            // Case 5: Test invalid string input
            object input5 = "abc";
            bool result5 = Utils.isLong(input5);
            Assert.IsFalse(result5, "Result should be false for invalid string input");

            // Case 6: Test float input
            object input6 = 123.45f;
            bool result6 = Utils.isLong(input6);
            Assert.IsFalse(result6, "Result should be false for float input");

            // Case 7: Test float string input
            object input7 = "123.45";
            bool result7 = Utils.isLong(input7);
            Assert.IsFalse(result7, "Result should be false for float string input");
        }

        [TestMethod()]
        public void ImportSpreadsheetNotSupportedWithoutPackage()
        {
#if ExcelDataReader
            Assert.Inconclusive("ExcelDataReader should not be available in this test environment");
#else
            var thrown = false;
            try
            {
                Utils.ImportSpreadsheet("missing.csv", (_, _) => true);
            }
            catch (NotSupportedException)
            {
                thrown = true;
            }

            Assert.IsTrue(thrown);
#endif
        }

        [TestMethod()]
        public void toCSVRowTest()
        {
            var row = new FwDict
            {
                { "plain", "abc" },
                { "comma", "a,b" },
                { "quote", "a\"b" },
                { "newline", "a\nb" }
            };

            string result = Utils.toCSVRow(row, new[] { "plain", "comma", "quote", "newline" });

            Assert.AreEqual("abc,\"a,b\",\"a\"\"b\",\"a\nb\"", result);
        }

        [TestMethod()]
        public void getCSVExportTest()
        {
            var rows = new FwList
            {
                new FwDict { { "id", 1 }, { "name", "Alpha" } },
                new FwDict { { "id", 2 }, { "name", "Beta, Inc" } }
            };

            var csv = Utils.getCSVExport("Identifier,Title", "id name", rows).ToString();

            StringAssert.StartsWith(csv, "Identifier,Title\r\n");
            StringAssert.Contains(csv, "1,Alpha\r\n");
            StringAssert.Contains(csv, "2,\"Beta, Inc\"\r\n");
        }

        [TestMethod()]
        public void writeCSVExportTest()
        {
            var rows = new FwList
            {
                new FwDict { { "id", 1 }, { "name", "Alpha" } }
            };

            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            Utils.writeCSVExport(context.Response, "test", "id,name", "*", rows);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(context.Response.Body);
            var body = reader.ReadToEnd();

            StringAssert.Contains(context.Response.Headers.ContentType!, "text/csv");
            StringAssert.StartsWith(body, "id,name\r\n1,Alpha\r\n");
        }

        [TestMethod()]
        public void fileSizeTest()
        {
            string tmpFile = Path.GetTempFileName();
            File.WriteAllText(tmpFile, "12345");

            Assert.AreEqual(5, Utils.fileSize(tmpFile));
            Assert.AreEqual(0, Utils.fileSize(Path.Combine(tmpFile, "missing")));

            File.Delete(tmpFile);
        }

        [TestMethod()]
        public void fileNameTest()
        {
            Assert.AreEqual("file.txt", Utils.fileName("/tmp/path/file.txt"));
            Assert.AreEqual("file.txt", Utils.fileName("file.txt"));
        }

        [TestMethod()]
        public void mergeHashTest()
        {
            FwDict h1 = [];
            h1["AAA"] = 1;
            h1["BBB"] = 2;
            FwDict h2 = [];
            h2["CCC"] = 3;
            h2["DDD"] = 4;

            Utils.mergeHash(h1, h2);

            Assert.IsTrue(h1.ContainsKey("AAA"));
            Assert.IsTrue(h1.ContainsKey("BBB"));
            Assert.IsTrue(h1.ContainsKey("CCC"));
            Assert.IsTrue(h1.ContainsKey("DDD"));

            Assert.AreEqual(1, h1["AAA"]);
            Assert.AreEqual(2, h1["BBB"]);
            Assert.AreEqual(3, h1["CCC"]);
            Assert.AreEqual(4, h1["DDD"]);
        }

        [TestMethod()]
        public void mergeHashDeepTest()
        {
            FwDict h1 = [];
            h1["AAA"] = 1;
            h1["BBB"] = 2;
            FwDict h2 = [];
            h2["CCC"] = 3;
            h2["DDD"] = new FwDict() { { "EEE", 5 } };

            Utils.mergeHashDeep(h1, h2);

            Assert.IsTrue(h1.ContainsKey("AAA"));
            Assert.IsTrue(h1.ContainsKey("BBB"));
            Assert.IsTrue(h1.ContainsKey("CCC"));
            Assert.IsTrue(h1.ContainsKey("DDD"));

            Assert.AreEqual(1, h1["AAA"]);
            Assert.AreEqual(2, h1["BBB"]);
            Assert.AreEqual(3, h1["CCC"]);
            Assert.IsInstanceOfType(h1["DDD"], typeof(FwDict));
            var inner = h1["DDD"] as FwDict;
            Assert.IsNotNull(inner);
            Assert.IsTrue(inner.ContainsKey("EEE"));
            Assert.AreEqual(5, inner["EEE"]);

        }

        [TestMethod()]
        public void bytes2strTest()
        {
            int val = 123;
            string r = Utils.bytes2str(val);
            Assert.AreEqual("123 B", r);

            val = 1024;
            r = Utils.bytes2str(val);
            Assert.AreEqual("1 KiB", r);

            val = 1024 * 1024;
            r = Utils.bytes2str(val);
            Assert.AreEqual("1 MiB", r);

            val = 1024 * 1024 * 1024;
            r = Utils.bytes2str(val);
            Assert.AreEqual("1 GiB", r);
        }

        [TestMethod()]
        public void jsonEncodeTest()
        {
            FwDict h1 = [];
            h1["AAA"] = 1;
            h1["BBB"] = 2;
            h1["CCC"] = 3;
            h1["DDD"] = 4;

            string r = Utils.jsonEncode(h1);
            Assert.AreEqual(0, r.IndexOf("{"));
            Assert.Contains("\"AAA\":1", r);
            Assert.Contains("\"BBB\":2", r);
            Assert.Contains("\"CCC\":3", r);
            Assert.Contains("\"DDD\":4", r);
        }

        [TestMethod()]
        public void jsonDecodeTest()
        {
            string s = "{\"AAA\":1,\"BBB\":2,\"CCC\":3,\"DDD\":4,\"EEE\":{\"AAA\": \"sub\"}}";
            var decoded = Utils.jsonDecode(s) as FwDict;
            Assert.IsNotNull(decoded);
            var h1 = decoded!;

            Assert.IsTrue(h1.ContainsKey("AAA"));
            Assert.IsTrue(h1.ContainsKey("BBB"));
            Assert.IsTrue(h1.ContainsKey("CCC"));
            Assert.IsTrue(h1.ContainsKey("DDD"));

            var nAAA = h1["AAA"] as long?;
            var nBBB = h1["BBB"] as long?;
            var nCCC = h1["CCC"] as long?;
            var nDDD = h1["DDD"] as long?;

            Assert.IsNotNull(nAAA);
            Assert.IsNotNull(nBBB);
            Assert.IsNotNull(nCCC);
            Assert.IsNotNull(nDDD);

            Assert.AreEqual(1, nAAA.Value);
            Assert.AreEqual(2, nBBB.Value);
            Assert.AreEqual(3, nCCC.Value);
            Assert.AreEqual(4, nDDD.Value);

            Assert.IsInstanceOfType(h1["EEE"], typeof(FwDict));
            var inner = h1["EEE"] as FwDict;
            Assert.IsNotNull(inner);
            Assert.AreEqual("sub", inner["AAA"]);
        }

        [TestMethod()]
        public void hashKeysTest()
        {
            FwDict h1 = [];
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

            Assert.AreEqual("Test1 test2 test3", r);
            r = Utils.capitalize(s, "all");
            Assert.AreEqual("Test1 Test2 Test3", r);
        }

        [TestMethod()]
        public void strRepeatTest()
        {
            string s = "test1";
            string r = Utils.strRepeat(s, 3);
            Assert.AreEqual("test1test1test1", r);
        }

        [TestMethod()]
        public void uuidTest()
        {
            string r = Utils.uuid();
            Assert.AreEqual(36, r.Length);
        }

        [TestMethod]
        public void NanoidTest()
        {
            // Case 1: Test default size
            string result1 = Utils.nanoid();
            Assert.AreEqual(21, result1.Length, "Result should have default length of 21 characters");

            // Case 2: Test custom size
            string result2 = Utils.nanoid(10);
            Assert.AreEqual(10, result2.Length, "Result should have custom length of 10 characters");

            // Case 3: Test large custom size
            string result3 = Utils.nanoid(100);
            Assert.AreEqual(100, result3.Length, "Result should have custom length of 100 characters");

            // Case 4: Test uniqueness of generated IDs
            HashSet<string> idSet = [];
            for (int i = 0; i < 100000; i++)
            {
                string id = Utils.nanoid();
                Assert.DoesNotContain(id, idSet, "Generated ID should be unique");
                idSet.Add(id);
            }
        }

        [TestMethod()]
        public void getTmpFilenameTest()
        {
            string prefix = Utils.TMP_PREFIX;
            string tmp_path = Utils.getTmpDir(prefix);
            string r = Utils.getTmpFilename(prefix);

            Assert.StartsWith(tmp_path, r);
            Assert.Contains(prefix, r);
            Assert.AreEqual(r.Length, tmp_path.Length + prefix.Length + 1 + 31);
        }

        [TestMethod()]
        public void cleanupTmpFilesTest()
        {
            string prefix = "unittest";
            string tmpDir = Utils.getTmpDir(prefix);

            string oldFile = Path.Combine(tmpDir, "old.tmp");
            File.WriteAllText(oldFile, "old");
            File.SetCreationTime(oldFile, DateTime.Now.AddHours(-2));

            string newFile = Path.Combine(tmpDir, "new.tmp");
            File.WriteAllText(newFile, "new");

            Utils.cleanupTmpFiles(prefix);

            Assert.IsFalse(File.Exists(oldFile));
            Assert.IsTrue(File.Exists(newFile));

            File.Delete(newFile);
        }

        [TestMethod()]
        public void md5Test()
        {
            string s = "ABC";
            string r = Utils.md5(s);
            Assert.AreEqual("902fbdd2b1df0c4f70b4a5d23525e932", r);
        }

        [TestMethod()]
        public void toXXTest()
        {
            string r = Utils.toXX("1");
            Assert.AreEqual("01", r);
        }

        [TestMethod()]
        public void num2ordinalTest()
        {
            Assert.AreEqual("1st", Utils.num2ordinal(1));
            Assert.AreEqual("2nd", Utils.num2ordinal(2));
            Assert.AreEqual("3rd", Utils.num2ordinal(3));
            Assert.AreEqual("4th", Utils.num2ordinal(4));

            Assert.AreEqual("21st", Utils.num2ordinal(21));
            Assert.AreEqual("22nd", Utils.num2ordinal(22));
            Assert.AreEqual("23rd", Utils.num2ordinal(23));
            Assert.AreEqual("24th", Utils.num2ordinal(24));

            Assert.AreEqual("11th", Utils.num2ordinal(11));
            Assert.AreEqual("12th", Utils.num2ordinal(12));
            Assert.AreEqual("13th", Utils.num2ordinal(13));
        }

        [TestMethod()]
        public void percentChangeTest()
        {
            Assert.AreEqual("0%", Utils.percentChange(0, 0), "Zero against zero should stay neutral");
            Assert.AreEqual("+100%", Utils.percentChange(5, 5), "Any non-zero over zero previous should be +100%");
            Assert.AreEqual("+0%", Utils.percentChange(50, 100), "No change should be formatted with a leading plus");
            Assert.AreEqual("+200%", Utils.percentChange(75, 100), "Growth should be positive with rounding");
            Assert.AreEqual("-66.67%", Utils.percentChange(25, 100), "Decline should be negative with two decimals");
        }


        [TestMethod()]
        public void str2truncateTest()
        {
            // test for Utils.str2truncate - truncate string to specified length
            // truncate  - This truncates a variable to a character length, the default is 80.
            // trchar    - As an optional second parameter, you can specify a string of text to display at the end if the variable was truncated.
            // The characters in the string are included with the original truncation length.
            // trword    - 0/1. By default, truncate will attempt to cut off at a word boundary =1.
            // trend     - 0/1. If you want to cut off at the exact character length, pass the optional third parameter of 1.
            //<~tag truncate="80" trchar="..." trword="1" trend="1">
            string s = "1234567890";

            //test for truncate
            FwDict hattrs = [];
            hattrs["truncate"] = "5";
            hattrs["trword"] = "0";
            hattrs["trchar"] = "";
            string r = Utils.str2truncate(s, hattrs);
            Assert.AreEqual("12345", r);

            // test for trchar
            hattrs.Clear();
            hattrs["truncate"] = "5";
            hattrs["trword"] = "0";
            hattrs["trchar"] = "...";
            r = Utils.str2truncate(s, hattrs);
            Assert.AreEqual("12345...", r);

            // test for trword
            hattrs.Clear();
            hattrs["truncate"] = "5";
            hattrs["trword"] = "1";
            hattrs["trchar"] = "";
            r = Utils.str2truncate(s, hattrs);
            Assert.AreEqual("1234567890", r);

        }

        [TestMethod()]
        public void orderbyApplySortdirTest()
        {
            // Case 1: Test ascending orderby with sortdir "asc"
            string result1 = Utils.orderbyApplySortdir("id", "asc");
            Assert.AreEqual("id", result1, "Result should remain unchanged for ascending orderby with sortdir 'asc'");

            // Case 2: Test ascending orderby with sortdir "desc"
            string result2 = Utils.orderbyApplySortdir("id", "desc");
            Assert.AreEqual("id desc", result2, "Result should be 'id desc' for ascending orderby with sortdir 'desc'");

            // Case 3: Test descending orderby with sortdir "desc" - no change
            string result3 = Utils.orderbyApplySortdir("id desc", "asc");
            Assert.AreEqual("id desc", result3, "Result should be 'id desc' for descending orderby with sortdir 'asc'");

            // Case 4: Test descending orderby with sortdir "desc"
            string result4 = Utils.orderbyApplySortdir("id desc", "desc");
            Assert.AreEqual("id asc", result4, "Result should be changed for descending orderby with sortdir 'desc'");

            // Case 5: Test multiple fields orderby with sortdir "asc" - no change
            string result5 = Utils.orderbyApplySortdir("prio desc, id", "asc");
            Assert.AreEqual("prio desc, id", result5, "Result should be 'prio desc, id' for multiple fields orderby with sortdir 'asc'");

            // Case 6: Test multiple fields orderby with sortdir "desc"
            string result6 = Utils.orderbyApplySortdir("prio desc, id", "desc");
            Assert.AreEqual("prio asc, id desc", result6, "Result should be 'prio asc, id desc' for multiple fields orderby with sortdir 'desc'");
        }

        [TestMethod()]
        public void html2textTest()
        {
            // Case 1: Test empty input
            string input1 = "";
            string result1 = Utils.html2text(input1);
            Assert.AreEqual("", result1, "Result should be empty for empty input");

            // Case 2: Test input with line breaks converted to spaces
            string input2 = "This is a\nmultiline\nstring.";
            string result2 = Utils.html2text(input2);
            Assert.AreEqual("This is a multiline string.", result2, "Line breaks should be converted to spaces");

            // Case 3: Test input with HTML line breaks converted to line breaks
            string input3 = "This is a<br/>multiline<br>string.";
            string result3 = Utils.html2text(input3);
            Assert.AreEqual("This is a\nmultiline\nstring.", result3, "HTML line breaks should be converted to line breaks");

            // Case 4: Test input with HTML tags removed
            string input4 = "<p>This is <b>bold</b> <i>italic</i> text</p>";
            string result4 = Utils.html2text(input4);
            Assert.AreEqual(" This is  bold   italic  text ", result4, "HTML tags should be removed");

            // Case 5: Test input with multiple HTML tags
            string input5 = "<div><h1>Title</h1><p>Paragraph</p></div>";
            string result5 = Utils.html2text(input5);
            Assert.AreEqual(" Title Paragraph ", result5, "Multiple HTML tags should be removed and spaces preserved");
        }

        [TestMethod()]
        public void commastr2hashTest()
        {
            var resultDefault = Utils.commastr2hash("1,2, 3");
            Assert.AreEqual("1", resultDefault["1"]);
            Assert.AreEqual("2", resultDefault["2"]);
            Assert.AreEqual("3", resultDefault["3"]);

            var resultIndexed = Utils.commastr2hash("a,b", "123...");
            Assert.AreEqual(0, resultIndexed["a"]);
            Assert.AreEqual(1, resultIndexed["b"]);

            var resultValue = Utils.commastr2hash("x,y", "val");
            Assert.AreEqual("val", resultValue["x"]);
            Assert.AreEqual("val", resultValue["y"]);

            var resultEmpty = Utils.commastr2hash(",,");
            Assert.IsEmpty(resultEmpty);
        }

        [TestMethod()]
        public void commastr2nlstrTest()
        {
            // Case 1: Test with empty input
            string input1 = "";
            string result1 = Utils.commastr2nlstr(input1);
            Assert.AreEqual("", result1, "Result should be empty for empty input");

            // Case 2: Test with input containing single item
            string input2 = "item";
            string result2 = Utils.commastr2nlstr(input2);
            Assert.AreEqual("item", result2, "Result should be same as input for single item");

            // Case 3: Test with input containing multiple items
            string input3 = "item1,item2,item3";
            string result3 = Utils.commastr2nlstr(input3);
            Assert.AreEqual("item1\r\nitem2\r\nitem3", result3, "Result should be newline-delimited string for multiple items");

            // Case 4: Test with input containing no items
            string input4 = ",";
            string result4 = Utils.commastr2nlstr(input4);
            Assert.AreEqual("\r\n", result4, "Result should be two newline characters for no items");
        }

        [TestMethod()]
        public void arrayInjectTest()
        {
            // Case 1: Empty rows and empty fields
            FwList rows1 = [];
            FwDict fields1 = [];
            Utils.arrayInject(rows1, fields1);
            Assert.IsEmpty(rows1, "Empty rows and fields should result in no changes");

            // Case 2: Rows with values and empty fields
            FwList rows2 = [new FwDict { { "key1", "value1" } }, new FwDict { { "key2", "value2" } }];
            FwDict fields2 = [];
            Utils.arrayInject(rows2, fields2);
            Assert.HasCount(2, rows2, "Rows with values and empty fields should result in no changes");
            var row2_0 = rows2[0] as FwDict;
            var row2_1 = rows2[1] as FwDict;
            Assert.IsNotNull(row2_0);
            Assert.IsNotNull(row2_1);
            Assert.AreEqual("value1", row2_0["key1"]);
            Assert.AreEqual("value2", row2_1["key2"]);

            // Case 3: Rows with values and fields with some new and some existing keys
            FwList rows3 = [new FwDict { { "key1", "value1" } }, new FwDict { { "key2", "value2" } }];
            FwDict fields3 = new() { { "key1", "newValue1" }, { "key3", "newValue3" } };
            Utils.arrayInject(rows3, fields3);
            Assert.HasCount(2, rows3, "Rows with values and fields with some new and some existing keys should merge properly");
            var row3_0 = rows3[0] as FwDict;
            var row3_1 = rows3[1] as FwDict;
            Assert.IsNotNull(row3_0);
            Assert.IsNotNull(row3_1);
            Assert.AreEqual("newValue1", row3_0["key1"]);
            Assert.AreEqual("value2", row3_1["key2"]);
            Assert.AreEqual("newValue3", row3_0["key3"]);
            Assert.AreEqual("newValue3", row3_1["key3"]);

            // Case 4: Null rows and null fields
            Assert.ThrowsExactly<NullReferenceException>(() => Utils.arrayInject(null!, []), "Null rows should throw ArgumentNullException");
            Assert.ThrowsExactly<NullReferenceException>(() => Utils.arrayInject([new FwDict { { "key1", "value1" } }], null!), "Null fields should throw ArgumentNullException");
        }

        [TestMethod()]
        public void urlescapeTest()
        {
            // Case 1: Empty string
            string emptyString = "";
            string result1 = Utils.urlescape(emptyString);
            Assert.AreEqual("", result1, "Empty string should return empty string");

            // Case 2: String with no special characters
            string stringWithoutSpecialChars = "hello";
            string result2 = Utils.urlescape(stringWithoutSpecialChars);
            Assert.AreEqual("hello", result2, "String with no special characters should return same string");

            // Case 3: String with special characters
            string stringWithSpecialChars = "hello world!";
            string result3 = Utils.urlescape(stringWithSpecialChars);
            Assert.AreEqual("hello+world!", result3, "String with special characters should be properly encoded");

            // Case 4: String with space
            string stringWithSpace = "hello world";
            string result4 = Utils.urlescape(stringWithSpace);
            Assert.AreEqual("hello+world", result4, "Space should be encoded as '+'");

            // Case 5: String with null value
            string? nullString = null;
            var x = Utils.urlescape(nullString!);
            Assert.IsNotNull(x);
            //Assert.ThrowsException<ArgumentNullException>(() => Utils.urlescape(nullString), "Null string should throw ArgumentNullException");

            // Case 6: String with non-ASCII characters
            string stringWithNonAscii = "r\u00e9sum\u00e9";
            string result6 = Utils.urlescape(stringWithNonAscii);
            Assert.AreEqual("r%c3%a9sum%c3%a9", result6, "Non-ASCII characters should be properly encoded");

            // Case 7: String with all special characters
            string stringWithAllSpecialChars = "!@#$%^&*()_+-=[]{};:'\"\\|,.<>?/~`";
            string result7 = Utils.urlescape(stringWithAllSpecialChars);
            Assert.AreEqual("!%40%23%24%25%5e%26*()_%2b-%3d%5b%5d%7b%7d%3b%3a%27%22%5c%7c%2c.%3c%3e%3f%2f%7e%60", result7, "All special characters should be properly encoded");

            // Case 8: String with extended ASCII characters
            string stringWithExtendedAscii = "\u00fc";
            string result8 = Utils.urlescape(stringWithExtendedAscii);
            Assert.AreEqual("%c3%bc", result8, "Extended ASCII characters should be properly encoded");

            // Case 9: String with multiple spaces
            string stringWithMultipleSpaces = "hello  world";
            string result9 = Utils.urlescape(stringWithMultipleSpaces);
            Assert.AreEqual("hello++world", result9, "Multiple spaces should be encoded as multiple '+'");
        }

        [TestMethod()]
        public void generateUniqueTmpDirPerPrefix()
        {
            string prefixA = "prefixA";
            string prefixB = "prefixB";

            string dirA = Utils.getTmpDir(prefixA);
            string dirB = Utils.getTmpDir(prefixB);

            Assert.AreNotEqual(dirA, dirB);
            Assert.IsTrue(Directory.Exists(dirA));
            Assert.IsTrue(Directory.Exists(dirB));

            Directory.Delete(dirA, true);
            Directory.Delete(dirB, true);
        }

        [TestMethod()]
        public void name2fwTest()
        {
            Assert.AreEqual("users", Utils.name2fw("dbo.users"));
            Assert.AreEqual("orders_products_amount", Utils.name2fw("OrdersProducts/Amount"));
            Assert.AreEqual("roles_users", Utils.name2fw("roles&users"));
            Assert.AreEqual("roles_users", Utils.name2fw("roles_+__users"));
            Assert.AreEqual("jobs_dates", Utils.name2fw("_JobsDates_"));
            Assert.AreEqual("jobs", Utils.name2fw("JOBS_"));
        }

        [TestMethod()]
        public void name2humanTest()
        {
            // Case 1: Convert system names to human-friendly names
            Assert.AreEqual("First Name", Utils.name2human("fname"));
            Assert.AreEqual("Last Name", Utils.name2human("lname"));
            Assert.AreEqual("Middle Name", Utils.name2human("midname"));

            // Case 2: Handling different cases and underscores
            Assert.AreEqual("Code", Utils.name2human("iCode"));
            Assert.AreEqual("Description", Utils.name2human("idesc"));
            Assert.AreEqual("First Name", Utils.name2human("first_name"));
            Assert.AreEqual("System Name", Utils.name2human("SYSTEM_NAME"));

            // Case 3: Removing prefixes
            Assert.AreEqual("Name", Utils.name2human("tbl_name"));
            Assert.AreEqual("Type", Utils.name2human("dbo_type"));

            // Case 4: Singularizing plural forms and removing "id"
            Assert.AreEqual("Person", Utils.name2human("person_id"));

            // Case 5: Proper capitalization and spacing
            Assert.AreEqual("Customer Order", Utils.name2human("customer_order"));
            Assert.AreEqual("Order Items Desc", Utils.name2human("order_items_DESC"));

            // Case 6: Empty input
            Assert.AreEqual("", Utils.name2human(""));

            // Case 1: Singularize plural forms containing "id"
            Assert.AreEqual("User Ids", Utils.name2human("user_ids"));
            Assert.AreEqual("Country Ids", Utils.name2human("country_ids"));

            // Case 2: Singularize plural forms containing "Id"
            Assert.AreEqual("User Ids", Utils.name2human("user_Ids"));
            Assert.AreEqual("Country Ids", Utils.name2human("country_Ids"));

            // Case 3: Singularize plural forms containing "ID"
            Assert.AreEqual("User Ids", Utils.name2human("user_IDs"));
            Assert.AreEqual("Country Ids", Utils.name2human("country_IDs"));

            // Case 4: Singularize plural forms containing "id" in different positions
            Assert.AreEqual("User Role", Utils.name2human("user_roles_id"));
            Assert.AreEqual("Country Code", Utils.name2human("country_codes_id"));

            // Case 5: Singularize plural forms containing "id" with other characters
            Assert.AreEqual("User Profile", Utils.name2human("user_profiles_id"));
            Assert.AreEqual("Country Information", Utils.name2human("country_informations_id"));

            // Case 6: no changes
            Assert.AreEqual("Pennies", Utils.name2human("pennies"));
            Assert.AreEqual("Categories", Utils.name2human("categories"));

            // Case 7: case
            Assert.AreEqual("Categories", Utils.name2human("CATEGORIES"));

            // Case 8: 
            Assert.AreEqual("People", Utils.name2human("people"));
            Assert.AreEqual("Children", Utils.name2human("children"));

            // Case 7: Null input
            Assert.ThrowsExactly<System.NullReferenceException>(() => Utils.name2human(null!));
        }

        [TestMethod()]
        public void nameCamelCaseTest()
        {
            // Case 1: Test with empty input
            string input1 = "";
            string result1 = Utils.nameCamelCase(input1);
            Assert.AreEqual("", result1, "Result should be empty for empty input");

            // Case 2: Test with single word input
            string input2 = "system";
            string result2 = Utils.nameCamelCase(input2);
            Assert.AreEqual("System", result2, "Result should be 'System' for single word input");

            // Case 3: Test with snake case input
            string input3 = "system_name";
            string result3 = Utils.nameCamelCase(input3);
            Assert.AreEqual("SystemName", result3, "Result should be 'SystemName' for snake case input");

            // Case 4: Test with mixed case input
            string input4 = "System_name";
            string result4 = Utils.nameCamelCase(input4);
            Assert.AreEqual("SystemName", result4, "Result should be 'SystemName' for mixed case input");

            // Case 5: Test with input containing non-alphanumeric characters
            string input5 = "system_name_123";
            string result5 = Utils.nameCamelCase(input5);
            Assert.AreEqual("SystemName123", result5, "Result should be 'SystemName123' for input containing non-alphanumeric characters");
        }

        [TestMethod()]
        public void isEmptyTestOld()
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
            //list
            Assert.IsTrue(Utils.isEmpty(new FwList()));
            Assert.IsFalse(Utils.isEmpty(new StrList() { "1" }));
            Assert.IsFalse(Utils.isEmpty(new IntList() { 1 }));
            //dictionary
            Assert.IsTrue(Utils.isEmpty(new FwDict()));
            Assert.IsFalse(Utils.isEmpty(new FwDict() { { "1", 1 } }));
        }

        [TestMethod]
        public void Base64EncodeDecode_RoundTrips()
        {
            var original = "plain text";
            var encoded = Utils.base64encode(original);
            var decoded = Utils.base64decode(encoded);

            Assert.AreEqual(original, decoded);
        }

        [TestMethod]
        public void Ext2Mime_MapsKnownTypes()
        {
            Assert.AreEqual("application/pdf", Utils.ext2mime(".pdf"));
            Assert.AreEqual("application/octet-stream", Utils.ext2mime(".unknown"));
        }

        [TestMethod]
        public void CloneHelpersDeepCopyCollections()
        {
            var source = new FwDict
            {
                { "num", 1 },
                { "nested", new FwDict { { "child", "yes" } } },
                { "list", new FwList { new FwDict { { "value", "x" } } } }
            };

            var clone = Utils.cloneHashDeep(source)!;
            var clonedList = Utils.cloneFwList(new FwList { source });
            var dbClone = Utils.cloneDBRow(new DBRow { { "k", "v" } });

            var nested = clone["nested"] as FwDict;
            var listClone = clone["list"] as IList;

            Assert.IsNotNull(nested);
            Assert.IsNotNull(listClone);
            var nestedDict = nested ?? throw new AssertFailedException("Expected nested dict");
            var listCopy = listClone ?? throw new AssertFailedException("Expected list copy");
            var firstCopyItem = listCopy[0] as FwDict ?? throw new AssertFailedException("Expected first list item");
            Assert.AreEqual("yes", nestedDict["child"]);
            Assert.AreEqual("x", firstCopyItem["value"]);
            Assert.AreEqual("v", dbClone["k"]);

            var sourceNested = source["nested"] as FwDict ?? throw new AssertFailedException("Expected source nested dict");
            sourceNested["child"] = "changed";
            Assert.AreEqual("yes", nestedDict["child"]);
            Assert.AreNotSame(source, clonedList[0]);
        }

        [TestMethod]
        public void JsonStringifyValues_NormalizesTypes()
        {
            var json = new FwDict
            {
                { "flag", true },
                { "number", 5 },
                { "nested", new FwDict { { "inner", 10 } } },
                { "list", new FwList { new FwDict { { "value", 2 } }, new FwDict { { "value", "two" } } } }
            };

            var result = Utils.jsonStringifyValues(json) as FwDict;

            Assert.IsNotNull(result);
            var jsonResult = result ?? throw new AssertFailedException("Expected json result");
            var nestedResult = jsonResult["nested"] as FwDict ?? throw new AssertFailedException("Expected nested result");
            var listResult = jsonResult["list"] as FwList ?? throw new AssertFailedException("Expected list result");
            var secondListItem = listResult[1] as FwDict ?? throw new AssertFailedException("Expected second list item");
            Assert.AreEqual("true", jsonResult["flag"].toStr());
            Assert.AreEqual("5", jsonResult["number"].toStr());
            Assert.AreEqual("10", nestedResult["inner"].toStr());
            Assert.AreEqual("two", secondListItem["value"].toStr());
        }

        [TestMethod]
        public void SerializeDeserialize_RoundTripDictionary()
        {
            var data = new FwDict { { "a", 1 }, { "b", "two" } };
            var serialized = Utils.serialize(data);
            var deserialized = Utils.deserialize(serialized) as FwDict;

            Assert.IsNotNull(deserialized);
            var dict = deserialized!;
            Assert.AreEqual("1", dict["a"].toStr());
            Assert.AreEqual("two", dict["b"].toStr());
        }

        [TestMethod]
        public void Nlstr2commastr_ConvertsLineBreaks()
        {
            Assert.AreEqual("one,two", Utils.nlstr2commastr("one\ntwo"));
        }

        [TestMethod]
        public void UrlUnescape_RestoresEncodedStrings()
        {
            Assert.AreEqual("hello world", Utils.urlunescape("hello+world"));
        }

        [TestMethod]
        public void Sha256_ProducesExpectedHash()
        {
            Assert.AreEqual("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", Utils.sha256("hello"));
        }

        [TestMethod]
        public void Array2Hashtable_BuildsDictionary()
        {
            FwList arr = [new FwDict { { "id", 1 }, { "name", "Alpha" } }, new FwDict { { "id", 2 }, { "name", "Beta" } }];
            var result = Utils.array2hashtable(arr, "id");

            Assert.HasCount(2, result);
            Assert.AreEqual("Alpha", (result["1"] as FwDict)?["name"]);
        }

        [TestMethod]
        public void Right_ReturnsTrailingCharacters()
        {
            Assert.AreEqual("world", Utils.Right("hello world", 5));
            Assert.AreEqual("", Utils.Right("", 3));
        }

        [TestMethod]
        public void CopyDirectory_CopiesAllFilesRecursively()
        {
            var source = Path.Combine(Path.GetTempPath(), "copy-src-" + Guid.NewGuid());
            var dest = Path.Combine(Path.GetTempPath(), "copy-dest-" + Guid.NewGuid());
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(Path.Combine(source, "child"));
            File.WriteAllText(Path.Combine(source, "file.txt"), "source");
            File.WriteAllText(Path.Combine(source, "child", "nested.txt"), "child");
            Directory.CreateDirectory(dest);
            File.WriteAllText(Path.Combine(dest, "file.txt"), "existing");

            Utils.CopyDirectory(source, dest, true);

            Assert.AreEqual("existing", File.ReadAllText(Path.Combine(dest, "file.txt")));
            Assert.AreEqual("child", File.ReadAllText(Path.Combine(dest, "child", "nested.txt")));

            Directory.Delete(source, true);
            Directory.Delete(dest, true);
        }

        [TestMethod]
        public void CookieHelpers_CreateGetAndDelete()
        {
            var fw = TestHelpers.CreateFw();
            Utils.createCookie(fw, "test", "value", 60);
            Assert.IsTrue(fw.response.Headers.TryGetValue("Set-Cookie", out var setCookie));
            StringAssert.Contains(setCookie.ToString(), "test=value");

            fw.request.Headers.Append("Cookie", "test=value");
            Assert.AreEqual("value", Utils.getCookie(fw, "test"));

            Utils.deleteCookie(fw, "test");
            Assert.IsTrue(fw.response.Headers.TryGetValue("Set-Cookie", out var deleted));
            StringAssert.Contains(deleted.ToString(), "test=;"); // deleted cookie
        }

        [TestMethod]
        public void PrepareRowsHeaders_AddsMissingHeadersAndCols()
        {
            var rows = new FwList { new FwDict { { "id", 1 }, { "name", "Alpha" } } };
            var headers = new FwList();

            Utils.prepareRowsHeaders(rows, headers);

            var headerNames = headers.Cast<FwDict>().Select(h => h["field_name"].toStr()).ToList();
            CollectionAssert.AreEquivalent(new List<string> { "id", "name" }, headerNames);
            var firstRow = rows[0] as FwDict ?? throw new AssertFailedException("Expected row dictionary");
            Assert.IsTrue(firstRow.ContainsKey("cols"));
            var cols = firstRow["cols"] as FwList ?? throw new AssertFailedException("Expected cols list");
            var nameCol = cols.Cast<FwDict>().First(c => c["field_name"].toStr() == "name");
            Assert.AreEqual("Alpha", nameCol["data"]);
        }

        [TestMethod]
        public void FileContentHelpers_ReadAndWriteFiles()
        {
            var path = Path.Combine(Path.GetTempPath(), "utils-file-" + Guid.NewGuid() + ".txt");
            var content = "sample";
            Utils.setFileContent(path, ref content);

            Exception? error;
            Assert.AreEqual(content, Utils.getFileContent(path, out error));
            Assert.IsNull(error);

            var lines = Utils.getFileLines(path);
            Assert.HasCount(1, lines);

            File.Delete(path);
            Assert.AreEqual("", Utils.getFileContent(path));
            CollectionAssert.AreEqual(Array.Empty<string>(), Utils.getFileLines(path, out error));
            Assert.IsNotNull(error);
        }

        [TestMethod]
        public void GetIP_ReturnsRemoteAddress()
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

            Assert.AreEqual("127.0.0.1", Utils.getIP(context));
        }

        [TestMethod]
        public async Task LoadUrl_PerformsGetAndPost()
        {
            var (getUrl, getHandler) = StartHttpListener(_ => "GET");
            var getResponse = Utils.loadUrl(getUrl);
            await getHandler;
            Assert.AreEqual("GET", getResponse);

            var (postUrl, postHandler) = StartHttpListener(ctx =>
            {
                using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                return reader.ReadToEnd();
            });

            var postResponse = Utils.loadUrl(postUrl, new FwDict { { "a", 1 } });
            await postHandler;
            StringAssert.Contains(postResponse, "a=1");
        }

        [TestMethod]
        public async Task SendFileToUrl_PostsMultipartContent()
        {
            var (url, handler) = StartHttpListener(ctx =>
            {
                using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                var body = reader.ReadToEnd();
                var hasFileName = body.Contains("filename=\"");
                return $"{ctx.Request.ContentType}|{hasFileName}";
            });

            var tmpFile = Path.GetTempFileName();
            File.WriteAllText(tmpFile, "data");
            var response = Utils.sendFileToUrl(url, new FwDict { { "file1", tmpFile } });
            await handler;

            StringAssert.StartsWith(response, "multipart/form-data");
            StringAssert.Contains(response, "True");
            File.Delete(tmpFile);
        }

        [TestMethod]
        public void GetPostedJson_ReadsRequestBody()
        {
            var fw = TestHelpers.CreateFw();
            var payload = "{\"name\":\"alpha\"}";
            fw.request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            var parsed = Utils.getPostedJson(fw);

            Assert.AreEqual("alpha", parsed["name"]);
        }

        private static (string url, Task handlerTask) StartHttpListener(Func<HttpListenerContext, string> responder)
        {
            var port = GetFreeTcpPort();
            var listener = new HttpListener();
            var prefix = $"http://127.0.0.1:{port}/";
            listener.Prefixes.Add(prefix);
            listener.Start();

            var handlerTask = Task.Run(async () =>
            {
                var ctx = await listener.GetContextAsync();
                var responseText = responder(ctx);
                var buffer = Encoding.UTF8.GetBytes(responseText);
                ctx.Response.ContentLength64 = buffer.Length;
                await ctx.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                ctx.Response.OutputStream.Close();
                listener.Stop();
            });

            return (prefix, handlerTask);
        }

        private static int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
