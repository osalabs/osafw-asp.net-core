using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using osafw;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace osafw.Tests
{
    [TestClass()]
    public class ParsePageTests
    {
        [TestMethod()]
        public void ParsePageTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void parse_jsonTest()
        {
            Hashtable h1 = new Hashtable();
            h1["AAA"] = 1;
            h1["BBB"] = 2;
            h1["CCC"] = 3;
            h1["DDD"] = 4;

            var p = new ParsePage(null);
            string r = p.parse_json(h1);

            Assert.AreEqual(0, r.IndexOf("{"));
            Assert.IsTrue(r.IndexOf("\"AAA\":1") >= 0);
            Assert.IsTrue(r.IndexOf("\"BBB\":2") >= 0);
            Assert.IsTrue(r.IndexOf("\"CCC\":3") >= 0);
            Assert.IsTrue(r.IndexOf("\"DDD\":4") >= 0);

            bool isException = false;
            try
            {
                _ = p.parse_json(null);
            } catch(NullReferenceException e)
            {
                isException = true;
            }
            Assert.IsTrue(isException);
            
        }

        [TestMethod()]
        public void clear_cacheTest()
        {

            throw new NotImplementedException();
        }

        [TestMethod()]
        public void tag_tplpathTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void langMapTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void parse_pageTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void parse_stringTest()
        {
            string tpl = "<~AAA><br/><~BBB><br/><~CCC><br/><~DDD><br/>";
            Hashtable h1 = new Hashtable();
            h1["AAA"] = 1;
            h1["BBB"] = 2;
            h1["CCC"] = 3;
            h1["DDD"] = 4;

            string r = new ParsePage(null).parse_string(tpl, h1);
            Assert.AreEqual("1<br/>2<br/>3<br/>4<br/>", r);
        }

        [TestMethod()]
        public void parse_string_repeatTest()
        {
            string tpl = "<~arr repeat inline><~AAA><br/><~BBB><br/><~CCC><br/><~DDD><br/></~arr>";
            Hashtable h1 = new Hashtable();
            h1["AAA"] = 1;
            h1["BBB"] = 2;
            h1["CCC"] = 3;
            h1["DDD"] = 4;
            ArrayList arr = new ArrayList();
            arr.Add(h1);
            arr.Add(h1);
            arr.Add(h1);
            Hashtable ps = new Hashtable() { { "arr", arr } };

            string r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("1<br/>2<br/>3<br/>4<br/>1<br/>2<br/>3<br/>4<br/>1<br/>2<br/>3<br/>4<br/>", r);
        }

        [TestMethod()]
        public void parse_string_ifTest()
        {
            string r = "";
            string tpl = "<~if_block if=\"AAA\" inline>Text</~if_block>";
            Hashtable ps = new Hashtable();

            ps["AAA"] = 1;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);

            ps["AAA"] = true;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);

            ps["AAA"] = new Hashtable() { { "AAA", 1} };
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);

            ps["AAA"] = 0;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);

            ps["AAA"] = null;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);

            ps["AAA"] = false;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_unlessTest()
        {
            string r = "";
            string tpl = "<~if_block unless=\"AAA\" inline>Text</~if_block>";
            Hashtable ps = new Hashtable();
            
            ps["AAA"] = 0;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);
            ps["AAA"] = false;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);
            ps["AAA"] = null;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);

            ps["AAA"] = 1;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
            ps["AAA"] = true;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
            ps["AAA"] = new Hashtable() { { "AAA", 1 } };
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_ifqeTest()
        {
            string r = "";
            string tpl = "<~if_block ifeq=\"AAA\" vvalue=\"value\" inline>Text</~if_block>";
            Hashtable ps = new Hashtable();
            
            ps["AAA"] = "test";
            ps["value"] = "test";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);
            ps["AAA"] = 123;
            ps["value"] = 123;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);
            ps["AAA"] = true;
            ps["value"] = true;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);


            ps["AAA"] = "test1";
            ps["value"] = "test";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
            ps["AAA"] = 1234;
            ps["value"] = 123;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
            ps["AAA"] = true;
            ps["value"] = false;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_ifneTest()
        {
            string r = "";
            string tpl = "<~if_block ifne=\"AAA\" vvalue=\"value\" inline>Text</~if_block>";
            Hashtable ps = new Hashtable();

            ps["AAA"] = "test1";
            ps["value"] = "test";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);
            ps["AAA"] = 1234;
            ps["value"] = 123;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);
            ps["AAA"] = true;
            ps["value"] = false;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);


            ps["AAA"] = "test";
            ps["value"] = "test";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
            ps["AAA"] = 123;
            ps["value"] = 123;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
            ps["AAA"] = false;
            ps["value"] = false;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_ifgtTest()
        {
            string r = "";
            string tpl = "<~if_block ifgt=\"AAA\" vvalue=\"value\" inline>Text</~if_block>";
            Hashtable ps = new Hashtable();

            ps["AAA"] = 100;
            ps["value"] = 10;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);


            ps["AAA"] = 10;
            ps["value"] = 100;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
            ps["AAA"] = 100;
            ps["value"] = 100;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_ifgeTest()
        {
            string r = "";
            string tpl = "<~if_block ifge=\"AAA\" vvalue=\"value\" inline>Text</~if_block>";
            Hashtable ps = new Hashtable();

            ps["AAA"] = 100;
            ps["value"] = 10;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);

            ps["AAA"] = 100;
            ps["value"] = 100;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);


            ps["AAA"] = 10;
            ps["value"] = 100;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_ifltTest()
        {
            string r = "";
            string tpl = "<~if_block iflt=\"AAA\" vvalue=\"value\" inline>Text</~if_block>";
            Hashtable ps = new Hashtable();

            ps["AAA"] = 10;
            ps["value"] = 100;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);


            ps["AAA"] = 100;
            ps["value"] = 10;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
            ps["AAA"] = 100;
            ps["value"] = 100;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_ifleTest()
        {
            string r = "";
            string tpl = "<~if_block ifle=\"AAA\" vvalue=\"value\" inline>Text</~if_block>";
            Hashtable ps = new Hashtable();

            ps["AAA"] = 10;
            ps["value"] = 100;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);

            ps["AAA"] = 100;
            ps["value"] = 100;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);


            ps["AAA"] = 100;
            ps["value"] = 10;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_selectTest()
        {
            string r = "";
            string tpl = "<select name = \"item[fruit]\">" +
                "<option value=\"\">- select a fruit -</option>" +
                "<~fruits_select select=\"fruit\">" +
                "</select>";
            string tpl_result = "<select name = \"item[fruit]\">" +
                "<option value=\"\">- select a fruit -</option>" +
                "<option value=\"1\">Apple</option>\r\n" +
                "<option value=\"2\">Plum</option>\r\n" +
                "<option value=\"3\" selected>Banana</option>\r\n" +
                "</select>";
            Hashtable ps = new Hashtable();
            ps["fruits_select"] = new ArrayList() {
                new Hashtable() { { "id", "1" }, { "iname", "Apple" } },
                new Hashtable() { { "id", "2" }, { "iname", "Plum" } },
                new Hashtable() { { "id", "3" }, { "iname", "Banana" } }
            };
            ps["fruit"] = "3";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual(tpl_result, r);
        }


        [TestMethod()]
        public void parse_string_ratioTest()
        {
            string r = "";
            string tpl = "<~fradio radio=\"fradio\" name=\"item[fradio]\" delim=\"&nbsp;\">";
            string tpl_result = "<select name = \"item[fruit]\">" +
                "<option value=\"\">- select a fruit -</option>" +
                "<option value=\"1\">Apple</option>\r\n" +
                "<option value=\"2\">Plum</option>\r\n" +
                "<option value=\"3\" selected>Banana</option>\r\n" +
                "</select>";
            Hashtable ps = new Hashtable();
            ps["fradio"] = new ArrayList() { "Apple", "Plum", "Banana" };
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_htmlescapeTest()
        {
            string r = "";
            string tpl = "<~AAA htmlescape>";
            Hashtable ps = new Hashtable();
            ps["AAA"] = "<p>tag</p>";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("&amp;lt;p&amp;gt;tag&amp;lt;/p&amp;gt;", r);

            tpl = "<~AAA>";
            ps["AAA"] = "<p>tag</p>";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("&lt;p&gt;tag&lt;/p&gt;", r);
        }

        [TestMethod()]
        public void parse_string_noescapeTest()
        {
            string r = "";
            string tpl = "<~AAA noescape>";
            Hashtable ps = new Hashtable();
            ps["AAA"] = "<p>tag</p>";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("<p>tag</p>", r);

            tpl = "<~AAA>";
            ps["AAA"] = "<p>tag</p>";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("&lt;p&gt;tag&lt;/p&gt;", r);
        }

        [TestMethod()]
        public void parse_string_urlTest()
        {
            string r = "";
            string tpl = "<~AAA url>";
            Hashtable ps = new Hashtable();
            ps["AAA"] = "test.com";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("http://test.com", r);

            tpl = "<~AAA>";
            ps["AAA"] = "test.com";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("test.com", r);
        }

        [TestMethod()]
        public void parse_string_number_formatTest()
        {
            string r = "";
            string tpl = "<~AAA>";
            Hashtable ps = new Hashtable();
            ps["AAA"] = "123456.789";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("123456.789", r);

            tpl = "<~AAA number_format>";
            ps["AAA"] = "123456.789";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("123,456.79", r);

            tpl = "<~AAA number_format=\"1\" nfthousands=\"\">";
            ps["AAA"] = "123456.789";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("123456.8", r);
        }

        [TestMethod()]
        public void parse_string_dateTest()
        {
            DateTime d = DateTime.Now;
            string r = "";
            string tpl = "<~AAA>";
            Hashtable ps = new Hashtable();
            ps["AAA"] = d;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual(d.ToString("M/dd/yyyy h:m:ss tt"), r);

            tpl = "<~AAA date>";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual(d.ToString("M/dd/yyyy"), r);

            tpl = "<~AAA date=\"short\">";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual(d.ToString("M/dd/yyyy HH:mm"), r);

            tpl = "<~AAA date=\"long\">";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual(d.ToString("M/dd/yyyy HH:mm:ss"), r);

            tpl = "<~AAA date=\"sql\">";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual(d.ToString("yyyy-MM-dd HH:mm:ss"), r);


            tpl = "<~AAA date=\"d M Y H:i\">";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual(d.ToString("d M Y H:i"), r);
        }


        [TestMethod()]
        public void parse_string_truncateTest()
        {
            string r = "";
            string tpl = "<~AAA truncate>";
            Hashtable ps = new Hashtable();
            ps["AAA"] = "test test test test test test test test test test test test test test test test test test test test test test test test test test test test test test test test ";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual(ps["AAA"].ToString().Substring(0, 80).Trim() + " test...", r);

            tpl = "<~AAA>";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual(ps["AAA"], r);
        }

        [TestMethod()]
        public void parse_string_strip_tagsTest()
        {
            string r = "";
            string tpl = "<~AAA noescape strip_tags>";
            Hashtable ps = new Hashtable();
            ps["AAA"] = "<p>tag</p>";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("tag", r);

            tpl = "<~AAA noescape>";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual(ps["AAA"], r);
        }

        [TestMethod()]
        public void parse_string_trimTest()
        {
            string r = "";
            string tpl = "<~AAA trim>";
            Hashtable ps = new Hashtable();
            ps["AAA"] = " tag ";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("tag", r);
        }

        [TestMethod()]
        public void parse_string_nl2brTest()
        {
            string r = "";
            string tpl = "<~AAA nl2br>";
            Hashtable ps = new Hashtable();
            ps["AAA"] = "tag\ntag2";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("tag<br>tag2", r);
        }

        [TestMethod()]
        public void parse_string_countTest()
        {
            string r = "";
            string tpl = "<~AAA count>";
            Hashtable ps = new Hashtable();
            ps["AAA"] = new string[] { "AAA", "BBB", "CCC", "DDD"};
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("4", r);
        }

        [TestMethod()]
        public void parse_string_lowerTest()
        {
            string r = "";
            string tpl = "<~AAA lower>";
            Hashtable ps = new Hashtable();
            ps["AAA"] = "TAG";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("tag", r);
            Assert.AreNotEqual(ps["AAA"], r);
        }

        [TestMethod()]
        public void parse_string_upperTest()
        {
            string r = "";
            string tpl = "<~AAA upper>";
            Hashtable ps = new Hashtable();
            ps["AAA"] = "tag";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("TAG", r);
            Assert.AreNotEqual(ps["AAA"], r);
        }


        [TestMethod()]
        public void parse_string_capitalizeTest()
        {
            string r = "";
            string tpl = "<~AAA capitalize>";
            Hashtable ps = new Hashtable();
            ps["AAA"] = "test test1 test2";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Test test1 test2", r);

            tpl = "<~AAA capitalize=\"all\">";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("Test Test1 Test2", r);

            Assert.AreNotEqual(ps["AAA"], r);
        }

        [TestMethod()]
        public void parse_string_defaultTest()
        {
            string r = "";
            string tpl = "<~AAA default=\"default value\">";
            Hashtable ps = new Hashtable();
            ps["AAA"] = "tag";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("tag", r);

            ps["AAA"] = "";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("default value", r);
        }


        [TestMethod()]
        public void parse_string_urlencodeTest()
        {
            string r = "";
            string tpl = "<~AAA urlencode>";
            Hashtable ps = new Hashtable();
            ps["AAA"] = "item[tag]=1&item[tag2]=2";
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.AreEqual("item%5btag%5d%3d1%26amp%3bitem%5btag2%5d%3d2", r);
        }


        [TestMethod()]
        public void parse_string_jsonTest()
        {
            string r = "";
            string tpl = "<~AAA json>";
            Hashtable ps = new Hashtable();
            Hashtable h1 = new Hashtable();
            h1["AAA"] = 1;
            h1["BBB"] = 2;
            h1["CCC"] = 3;
            h1["DDD"] = 4;
            ps["AAA"] = h1;
            r = new ParsePage(null).parse_string(tpl, ps);
            Assert.IsTrue(r.IndexOf("{") == 0);
            Assert.IsTrue(r.IndexOf("&quot;AAA&quot;:1") >= 0);
            Assert.IsTrue(r.IndexOf("&quot;BBB&quot;:2") >= 0);
            Assert.IsTrue(r.IndexOf("&quot;CCC&quot;:3") >= 0);
            Assert.IsTrue(r.IndexOf("&quot;DDD&quot;:4") >= 0);
        }

    }
}