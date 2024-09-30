﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            Assert.IsFalse(Utils.toBool(null));
            Assert.IsFalse(Utils.toBool(""));
            Assert.IsFalse(Utils.toBool(0));
            Assert.IsFalse(Utils.toBool(0.0));
            Assert.IsFalse(Utils.toBool(0.0f));
            Assert.IsFalse(Utils.toBool(0.0m));
            Assert.IsFalse(Utils.toBool(false));
            Assert.IsFalse(Utils.toBool("0"));
            Assert.IsFalse(Utils.toBool("false"));
            Assert.IsFalse(Utils.toBool("no"));
            Assert.IsFalse(Utils.toBool("off"));
            Assert.IsFalse(Utils.toBool("n"));
            Assert.IsFalse(Utils.toBool("N"));
            Assert.IsFalse(Utils.toBool("f"));
            Assert.IsFalse(Utils.toBool("F"));
            Assert.IsFalse(Utils.toBool("ABC"));
            Assert.IsFalse(Utils.toBool("yes"));
            Assert.IsFalse(Utils.toBool("on"));
            Assert.IsFalse(Utils.toBool(new ArrayList())); //empty arraylist false

            Assert.IsTrue(Utils.toBool("true"));
            Assert.IsTrue(Utils.toBool("True"));
            Assert.IsTrue(Utils.toBool("TRUE"));
            Assert.IsTrue(Utils.toBool("1")); //non-zero number            
            Assert.IsTrue(Utils.toBool(new ArrayList() { 1 })); //non-empty arraylist true
        }


        [TestMethod()]
        public void toDateTest()
        {
            var r = Utils.toDate(DateTime.Now);
            Assert.IsNotNull(r);
            Assert.IsTrue(r != DateTime.MinValue);
            Assert.IsInstanceOfType(r, typeof(DateTime));

            r = Utils.toDate("2021-10-9");
            Assert.AreEqual(r.Day, 9);
            Assert.AreEqual(r.Month, 10);
            Assert.AreEqual(r.Year, 2021);

            r = Utils.toDate("10/9/2021");
            Assert.AreEqual(r.Day, 9);
            Assert.AreEqual(r.Month, 10);
            Assert.AreEqual(r.Year, 2021);

            r = Utils.toDate("11/10/2020 01:02:03");
            Assert.AreEqual(r.Day, 10);
            Assert.AreEqual(r.Month, 11);
            Assert.AreEqual(r.Year, 2020);
            Assert.AreEqual(r.Hour, 1);
            Assert.AreEqual(r.Minute, 2);
            Assert.AreEqual(r.Second, 3);

            r = Utils.toDate("ABC");
            Assert.IsTrue(r == DateTime.MinValue);

            r = Utils.toDate("");
            Assert.IsTrue(r == DateTime.MinValue);

            r = Utils.toDate(null);
            Assert.IsTrue(r == DateTime.MinValue);

            r = Utils.toDate(DBNull.Value);
            Assert.IsTrue(r == DateTime.MinValue);
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
            Assert.AreEqual(Utils.toStr(null), "");
            Assert.AreEqual(Utils.toStr(123), "123");
        }

        [TestMethod()]
        public void f2intTest()
        {
            Assert.IsInstanceOfType(Utils.toInt("123"), typeof(int));
            Assert.AreEqual(Utils.toInt("123"), 123);
            Assert.AreEqual(Utils.toInt("123.123"), 0);
            Assert.AreEqual(Utils.toInt("123b"), 0);
            Assert.AreEqual(Utils.toInt("b123"), 0);
            Assert.AreEqual(Utils.toInt("ABC"), 0);
            Assert.AreEqual(Utils.toInt(null), 0);
        }

        [TestMethod()]
        public void f2floatTest()
        {
            Assert.IsInstanceOfType(Utils.toFloat("123.123"), typeof(double));
            Assert.AreEqual(Utils.toFloat("123.123"), 123.123);
            Assert.AreEqual(Utils.toFloat(123.123), 123.123);
            Assert.AreEqual(Utils.toFloat("123"), 123.0);
            Assert.AreEqual(Utils.toFloat("123.123b"), 0);
            Assert.AreEqual(Utils.toFloat("b123.123"), 0);
            Assert.AreEqual(Utils.toFloat("ABC"), 0);
            Assert.AreEqual(Utils.toFloat(""), 0);
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
            Assert.IsInstanceOfType(Utils.toLong(n), typeof(long));
            Assert.IsInstanceOfType(Utils.toLong("123"), typeof(long));
            Assert.AreEqual(Utils.toLong("100M"), 0);
            Assert.AreEqual(Utils.toLong(100M), 100M);
            Assert.AreEqual(Utils.toLong("123"), 123);
            Assert.AreEqual(Utils.toLong(123), 123);
            Assert.AreEqual(Utils.toLong("123"), 123.0);
            Assert.AreEqual(Utils.toLong("123.123b"), 0);
            Assert.AreEqual(Utils.toLong("b123.123"), 0);
            Assert.AreEqual(Utils.toLong("ABC"), 0);
            Assert.AreEqual(Utils.toLong(""), 0);
            Assert.AreEqual(Utils.toLong(null), 0);
        }

        [TestMethod]
        public void f2decimalTest()
        {
            // Test cases for different representations of decimals
            Assert.AreEqual(decimal.Zero, Utils.toDecimal("100M"));
            Assert.AreEqual(100m, Utils.toDecimal(100m));
            Assert.AreEqual(123m, Utils.toDecimal("123"));
            Assert.AreEqual(123m, Utils.toDecimal(123));
            Assert.AreEqual(123m, Utils.toDecimal("123.0"));
            Assert.AreEqual(decimal.Zero, Utils.toDecimal("123.123b"));
            Assert.AreEqual(decimal.Zero, Utils.toDecimal("b123.123"));
            Assert.AreEqual(decimal.Zero, Utils.toDecimal("ABC"));
            Assert.AreEqual(decimal.Zero, Utils.toDecimal(""));

            // Test case: Input is null
            object inputNull = null;
            Assert.AreEqual(decimal.Zero, Utils.toDecimal(inputNull));

            // Test case: Input is a valid decimal string
            object inputValidDecimal = "123.45";
            Assert.AreEqual(123.45m, Utils.toDecimal(inputValidDecimal));

            // Test case: Input is not a valid decimal string
            object inputInvalidDecimal = "not a decimal";
            Assert.AreEqual(decimal.Zero, Utils.toDecimal(inputInvalidDecimal));

            // Test case: Input is decimal overflow (max value)
            object inputDecimalOverflowMax = "79228162514264337593543950336"; // Max value of decimal + 1
            Assert.AreEqual(decimal.Zero, Utils.toDecimal(inputDecimalOverflowMax));

            // Test case: Input is negative decimal overflow (min value)
            object inputDecimalOverflowMin = "-79228162514264337593543950336"; // Min value of decimal - 1
            Assert.AreEqual(decimal.Zero, Utils.toDecimal(inputDecimalOverflowMin));

            // Test case: Input is decimal max value
            object inputDecimalMaxValue = decimal.MaxValue.ToString();
            Assert.AreEqual(decimal.MaxValue, Utils.toDecimal(inputDecimalMaxValue));

            // Test case: Input is decimal min value
            object inputDecimalMinValue = decimal.MinValue.ToString();
            Assert.AreEqual(decimal.MinValue, Utils.toDecimal(inputDecimalMinValue));
        }

        [TestMethod()]
        public void f2singleTest()
        {
            // Case 1: Convert valid float string to Single
            object field1 = "123.45";
            Single result1 = Utils.toSingle(field1);
            Assert.AreEqual(123.45f, result1, "Result should be converted Single from valid float string");

            // Case 2: Convert valid integer string to Single
            object field2 = "123";
            Single result2 = Utils.toSingle(field2);
            Assert.AreEqual(123f, result2, "Result should be converted Single from valid integer string");

            // Case 3: Convert null to Single
            object field3 = null;
            Single result3 = Utils.toSingle(field3);
            Assert.AreEqual(0f, result3, "Result should be 0 for null input");

            // Case 4: Convert empty string to Single
            object field4 = "";
            Single result4 = Utils.toSingle(field4);
            Assert.AreEqual(0f, result4, "Result should be 0 for empty string input");

            // Case 5: Convert invalid string to Single
            object field5 = "abc";
            Single result5 = Utils.toSingle(field5);
            Assert.AreEqual(0f, result5, "Result should be 0 for invalid string input");

            // Case 6: Convert Single to Single
            object field6 = 123.45f;
            Single result6 = Utils.toSingle(field6);
            Assert.AreEqual(123.45f, result6, "Result should be same Single for Single input");

            // Case 7: Convert integer to Single
            object field7 = 123;
            Single result7 = Utils.toSingle(field7);
            Assert.AreEqual(123f, result7, "Result should be converted Single from integer");

            // Case 8: Convert negative float string to Single
            object field8 = "-123.45";
            Single result8 = Utils.toSingle(field8);
            Assert.AreEqual(-123.45f, result8, "Result should be converted Single from negative float string");

            // Case 9: Convert negative integer string to Single
            object field9 = "-123";
            Single result9 = Utils.toSingle(field9);
            Assert.AreEqual(-123f, result9, "Result should be converted Single from negative integer string");
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
            object input3 = null;
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
            object input3 = null;
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
            object input3 = null;
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
            HashSet<string> idSet = new HashSet<string>();
            for (int i = 0; i < 100000; i++)
            {
                string id = Utils.nanoid();
                Assert.IsFalse(idSet.Contains(id), "Generated ID should be unique");
                idSet.Add(id);
            }
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
        public void percentChangeTest()
        {
            //TODO
            //how does it works
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
            Hashtable hattrs = new();
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
            //TODO

            // Case 1: Test ascending orderby with sortdir "asc"
            string result1 = Utils.orderbyApplySortdir("id", "asc");
            Assert.AreEqual("id", result1, "Result should remain unchanged for ascending orderby with sortdir 'asc'");

            // Case 2: Test ascending orderby with sortdir "desc"
            string result2 = Utils.orderbyApplySortdir("id", "desc");
            Assert.AreEqual("id desc", result2, "Result should be 'id desc' for ascending orderby with sortdir 'desc'");

            // Case 3: Test descending orderby with sortdir "asc"
            string result3 = Utils.orderbyApplySortdir("id desc", "asc");
            Assert.AreEqual("id asc", result3, "Result should be 'id asc' for descending orderby with sortdir 'asc'");

            // Case 4: Test descending orderby with sortdir "desc"
            string result4 = Utils.orderbyApplySortdir("id desc", "desc");
            Assert.AreEqual("id", result4, "Result should remain unchanged for descending orderby with sortdir 'desc'");

            // Case 5: Test multiple fields orderby with sortdir "asc"
            string result5 = Utils.orderbyApplySortdir("prio desc, id", "asc");
            Assert.AreEqual("prio asc, id asc", result5, "Result should be 'prio asc, id asc' for multiple fields orderby with sortdir 'asc'");

            // Case 6: Test multiple fields orderby with sortdir "desc"
            string result6 = Utils.orderbyApplySortdir("prio desc, id", "desc");
            Assert.AreEqual("prio asc, id desc", result6, "Result should be 'prio asc, id desc' for multiple fields orderby with sortdir 'desc'");
        }

        [TestMethod()]
        public void html2textTest()
        {
            //TODO bug?

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
            Assert.AreEqual("This is bold italic text", result4, "HTML tags should be removed");

            // Case 5: Test input with multiple HTML tags
            string input5 = "<div><h1>Title</h1><p>Paragraph</p></div>";
            string result5 = Utils.html2text(input5);
            Assert.AreEqual("Title Paragraph", result5, "Multiple HTML tags should be removed and spaces preserved");
        }

        [TestMethod()]
        public void commastr2hashTest()
        {
            //TODO 
            //how does it works

            throw new NotImplementedException();
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
            ArrayList rows1 = new ArrayList();
            Hashtable fields1 = new Hashtable();
            Utils.arrayInject(rows1, fields1);
            Assert.AreEqual(0, rows1.Count, "Empty rows and fields should result in no changes");

            // Case 2: Rows with values and empty fields
            ArrayList rows2 = new ArrayList();
            rows2.Add(new Hashtable { { "key1", "value1" } });
            rows2.Add(new Hashtable { { "key2", "value2" } });
            Hashtable fields2 = new Hashtable();
            Utils.arrayInject(rows2, fields2);
            Assert.AreEqual(2, rows2.Count, "Rows with values and empty fields should result in no changes");
            Assert.AreEqual("value1", ((Hashtable)rows2[0])["key1"]);
            Assert.AreEqual("value2", ((Hashtable)rows2[1])["key2"]);

            // Case 3: Rows with values and fields with some new and some existing keys
            ArrayList rows3 = new ArrayList();
            rows3.Add(new Hashtable { { "key1", "value1" } });
            rows3.Add(new Hashtable { { "key2", "value2" } });
            Hashtable fields3 = new Hashtable { { "key1", "newValue1" }, { "key3", "newValue3" } };
            Utils.arrayInject(rows3, fields3);
            Assert.AreEqual(2, rows3.Count, "Rows with values and fields with some new and some existing keys should merge properly");
            Assert.AreEqual("newValue1", ((Hashtable)rows3[0])["key1"]);
            Assert.AreEqual("value2", ((Hashtable)rows3[1])["key2"]);
            Assert.AreEqual("newValue3", ((Hashtable)rows3[0])["key3"]);
            Assert.AreEqual("newValue3", ((Hashtable)rows3[1])["key3"]);

            // Case 4: Null rows and null fields
            Assert.ThrowsException<NullReferenceException>(() => Utils.arrayInject(null, new Hashtable()), "Null rows should throw ArgumentNullException");
            Assert.ThrowsException<ArgumentNullException>(() => Utils.arrayInject(new ArrayList(), null), "Null fields should throw ArgumentNullException");
        }

        [TestMethod()]
        public void urlescapeTest()
        {
            //TODO

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
            string nullString = null;
            var x = Utils.urlescape(nullString);
            Assert.ThrowsException<ArgumentNullException>(() => Utils.urlescape(nullString), "Null string should throw ArgumentNullException");

            // Case 6: String with non-ASCII characters
            string stringWithNonAscii = "résumé";
            string result6 = Utils.urlescape(stringWithNonAscii);
            Assert.AreEqual("r%C3%A9sum%C3%A9", result6, "Non-ASCII characters should be properly encoded");

            // Case 7: String with all special characters
            string stringWithAllSpecialChars = "!@#$%^&*()_+-=[]{};:'\"\\|,.<>?/~`";
            string result7 = Utils.urlescape(stringWithAllSpecialChars);
            Assert.AreEqual("%21%40%23%24%25%5E%26*()_%2B-%3D%5B%5D%7B%7D%3B%3A'%22%5C%7C%2C.%3C%3E%3F~%2F%60", result7, "All special characters should be properly encoded");

            // Case 8: String with extended ASCII characters
            string stringWithExtendedAscii = "ü";
            string result8 = Utils.urlescape(stringWithExtendedAscii);
            Assert.AreEqual("%C3%BC", result8, "Extended ASCII characters should be properly encoded");

            // Case 9: String with multiple spaces
            string stringWithMultipleSpaces = "hello  world";
            string result9 = Utils.urlescape(stringWithMultipleSpaces);
            Assert.AreEqual("hello++world", result9, "Multiple spaces should be encoded as multiple '+'");
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
            Assert.AreEqual("User", Utils.name2human("user_ids"));
            Assert.AreEqual("Country", Utils.name2human("country_ids"));

            // Case 2: Singularize plural forms containing "Id"
            Assert.AreEqual("User", Utils.name2human("user_Ids"));
            Assert.AreEqual("Country", Utils.name2human("country_Ids"));

            // Case 3: Singularize plural forms containing "ID"
            Assert.AreEqual("User", Utils.name2human("user_IDs"));
            Assert.AreEqual("Country", Utils.name2human("country_IDs"));

            // Case 4: Singularize plural forms containing "id" in different positions
            Assert.AreEqual("User Role", Utils.name2human("user_roles_id"));
            Assert.AreEqual("Country Code", Utils.name2human("country_codes_id"));

            // Case 5: Singularize plural forms containing "id" with other characters
            Assert.AreEqual("User Profile", Utils.name2human("user_profiles_id"));
            Assert.AreEqual("Country Information", Utils.name2human("country_informations_id"));

            // Case 6: Singularize plural forms with different endings
            Assert.AreEqual("Penny", Utils.name2human("pennies"));
            Assert.AreEqual("Category", Utils.name2human("categories"));

            // Case 7: Singularize plural forms with mixed case
            Assert.AreEqual("User", Utils.name2human("user_Ids"));
            Assert.AreEqual("Category", Utils.name2human("CATEGORIES"));

            // Case 8: Singularize plural forms with irregular plural forms
            Assert.AreEqual("Person", Utils.name2human("people"));
            Assert.AreEqual("Child", Utils.name2human("children"));

            // Case 7: Null input
            Assert.ThrowsException<System.ArgumentNullException>(() => Utils.name2human(null));
        }

        [TestMethod()]
        public void nameCamelCaseTest()
        {
            //TODO
            //bug ?

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
            //arraylist
            Assert.IsTrue(Utils.isEmpty(new ArrayList()));
            Assert.IsFalse(Utils.isEmpty(new ArrayList() { 1 }));
            //hashtable
            Assert.IsTrue(Utils.isEmpty(new Hashtable()));
            Assert.IsFalse(Utils.isEmpty(new Hashtable() { { "1", 1 } }));
        }
    }
}