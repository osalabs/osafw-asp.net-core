using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;

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
            Hashtable h1 = [];
            h1["AAA"] = 1;
            h1["BBB"] = 2;
            h1["CCC"] = 3;
            h1["DDD"] = 4;

            var p = new ParsePage(null!);
            string r = p.parse_json(h1);

            Assert.AreEqual(0, r.IndexOf("{"));
            Assert.IsGreaterThanOrEqualTo(0, r.IndexOf("\"AAA\":1"));
            Assert.IsGreaterThanOrEqualTo(0, r.IndexOf("\"BBB\":2"));
            Assert.IsGreaterThanOrEqualTo(0, r.IndexOf("\"CCC\":3"));
            Assert.IsGreaterThanOrEqualTo(0, r.IndexOf("\"DDD\":4"));

            bool isException = false;
            try
            {
                _ = p.parse_json(null!);
            }
            catch (NullReferenceException)
            {
                isException = true;
            }
            Assert.IsTrue(isException);

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
            Hashtable h1 = [];
            h1["AAA"] = 1;
            h1["BBB"] = 2;
            h1["CCC"] = 3;
            h1["DDD"] = 4;

            string r = new ParsePage(null!).parse_string(tpl, h1);
            Assert.AreEqual("1<br/>2<br/>3<br/>4<br/>", r);
        }

        [TestMethod()]
        public void parse_string_repeatTest()
        {
            string tpl = "<~arr repeat inline><~AAA><br/><~BBB><br/><~CCC><br/><~DDD><br/></~arr>";
            Hashtable h1 = [];
            h1["AAA"] = 1;
            h1["BBB"] = 2;
            h1["CCC"] = 3;
            h1["DDD"] = 4;
            ArrayList arr = [h1, h1, h1];
            Hashtable ps = new() { { "arr", arr } };

            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("1<br/>2<br/>3<br/>4<br/>1<br/>2<br/>3<br/>4<br/>1<br/>2<br/>3<br/>4<br/>", r);
        }

        private class User
        {
            // properties
            public int id { get; set; }
            public string Name { get; set; } = string.Empty;
            // fields
            public string Email = string.Empty;
        }

        [TestMethod()]
        public void parse_string_objectTest()
        {
            string tpl = "User ID:<~user[id]>;Username:<~user[Name]>;Email:<~user[Email]>;<~all_users repeat inline><~id>-<~Name>-<~Email>;</~all_users>";
            var u1 = new User { id = 1, Name = "John", Email = "john@email.com" };
            var u2 = new User { id = 2, Name = "Amy", Email = "amy@email.com" };
            var ps = new Hashtable
            {
                { "user", u1 },
                { "all_users", new List<User> { u1, u2 } }
            };

            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("User ID:1;Username:John;Email:john@email.com;1-John-john@email.com;2-Amy-amy@email.com;", r);
        }

        [TestMethod()]
        public void parse_string_ifTest()
        {
            string tpl = "<~if_block if=\"AAA\" inline>Text</~if_block>";
            Hashtable ps = [];

            ps["AAA"] = 1;
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);

            ps["AAA"] = true;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);

            ps["AAA"] = new Hashtable() { { "AAA", 1 } };
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);

            ps["AAA"] = 0;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);

            ps["AAA"] = null;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);

            ps["AAA"] = false;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_unlessTest()
        {
            string tpl = "<~if_block unless=\"AAA\" inline>Text</~if_block>";
            Hashtable ps = [];

            ps["AAA"] = 0;
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);
            ps["AAA"] = false;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);
            ps["AAA"] = null;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);

            ps["AAA"] = 1;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);
            ps["AAA"] = true;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);
            ps["AAA"] = new Hashtable() { { "AAA", 1 } };
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_ifqeTest()
        {
            string tpl = "<~if_block ifeq=\"AAA\" vvalue=\"value\" inline>Text</~if_block>";
            Hashtable ps = [];

            ps["AAA"] = "test";
            ps["value"] = "test";
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);
            ps["AAA"] = 123;
            ps["value"] = 123;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);
            ps["AAA"] = true;
            ps["value"] = true;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);


            ps["AAA"] = "test1";
            ps["value"] = "test";
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);
            ps["AAA"] = 1234;
            ps["value"] = 123;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);
            ps["AAA"] = true;
            ps["value"] = false;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_ifneTest()
        {
            string tpl = "<~if_block ifne=\"AAA\" vvalue=\"value\" inline>Text</~if_block>";
            Hashtable ps = [];

            ps["AAA"] = "test1";
            ps["value"] = "test";
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);
            ps["AAA"] = 1234;
            ps["value"] = 123;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);
            ps["AAA"] = true;
            ps["value"] = false;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);


            ps["AAA"] = "test";
            ps["value"] = "test";
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);
            ps["AAA"] = 123;
            ps["value"] = 123;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);
            ps["AAA"] = false;
            ps["value"] = false;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_ifgtTest()
        {
            string tpl = "<~if_block ifgt=\"AAA\" vvalue=\"value\" inline>Text</~if_block>";
            Hashtable ps = [];

            ps["AAA"] = 100;
            ps["value"] = 10;
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);


            ps["AAA"] = 10;
            ps["value"] = 100;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);
            ps["AAA"] = 100;
            ps["value"] = 100;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_ifgeTest()
        {
            string tpl = "<~if_block ifge=\"AAA\" vvalue=\"value\" inline>Text</~if_block>";
            Hashtable ps = [];

            ps["AAA"] = 100;
            ps["value"] = 10;
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);

            ps["AAA"] = 100;
            ps["value"] = 100;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);


            ps["AAA"] = 10;
            ps["value"] = 100;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_ifltTest()
        {
            string tpl = "<~if_block iflt=\"AAA\" vvalue=\"value\" inline>Text</~if_block>";
            Hashtable ps = [];

            ps["AAA"] = 10;
            ps["value"] = 100;
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);


            ps["AAA"] = 100;
            ps["value"] = 10;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);
            ps["AAA"] = 100;
            ps["value"] = 100;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_ifleTest()
        {
            string tpl = "<~if_block ifle=\"AAA\" vvalue=\"value\" inline>Text</~if_block>";
            Hashtable ps = [];

            ps["AAA"] = 10;
            ps["value"] = 100;
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);

            ps["AAA"] = 100;
            ps["value"] = 100;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Text", r);


            ps["AAA"] = 100;
            ps["value"] = 10;
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);
        }

        [TestMethod()]
        public void parse_string_selectTest()
        {
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
            Hashtable ps = [];
            ps["fruits_select"] = new ArrayList() {
                new Hashtable() { { "id", "1" }, { "iname", "Apple" } },
                new Hashtable() { { "id", "2" }, { "iname", "Plum" } },
                new Hashtable() { { "id", "3" }, { "iname", "Banana" } }
            };
            ps["fruit"] = "3";
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual(tpl_result, r);
        }


        [TestMethod()]
        public void parse_string_radioTest()
        {
            /*string tpl = "<~fradio radio=\"fradio\" name=\"item[fradio]\" delim=\"&nbsp;\">";
            string tpl_result = "<select name = \"item[fruit]\">" +
                "<option value=\"\">- select a fruit -</option>" +
                "<option value=\"1\">Apple</option>\r\n" +
                "<option value=\"2\">Plum</option>\r\n" +
                "<option value=\"3\" selected>Banana</option>\r\n" +
                "</select>";
            Hashtable ps = new();
            ps["fradio"] = new ArrayList() { "Apple", "Plum", "Banana" };
            var r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("", r);*/
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void parse_string_htmlescapeTest()
        {
            string tpl = "<~AAA htmlescape>";
            Hashtable ps = [];
            ps["AAA"] = "<p>tag</p>";
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("&amp;lt;p&amp;gt;tag&amp;lt;/p&amp;gt;", r);

            tpl = "<~AAA>";
            ps["AAA"] = "<p>tag</p>";
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("&lt;p&gt;tag&lt;/p&gt;", r);
        }

        [TestMethod()]
        public void parse_string_noescapeTest()
        {
            string tpl = "<~AAA noescape>";
            Hashtable ps = [];
            ps["AAA"] = "<p>tag</p>";
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("<p>tag</p>", r);

            tpl = "<~AAA>";
            ps["AAA"] = "<p>tag</p>";
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("&lt;p&gt;tag&lt;/p&gt;", r);
        }

        [TestMethod()]
        public void parse_string_urlTest()
        {
            string tpl = "<~AAA url>";
            Hashtable ps = [];
            ps["AAA"] = "test.com";
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("http://test.com", r);

            tpl = "<~AAA>";
            ps["AAA"] = "test.com";
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("test.com", r);
        }

        [TestMethod()]
        public void parse_string_number_formatTest()
        {
            string tpl = "<~AAA>";
            Hashtable ps = [];
            ps["AAA"] = "123456.789";
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("123456.789", r);

            tpl = "<~AAA number_format>";
            ps["AAA"] = "123456.789";
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("123,456.79", r);

            tpl = "<~AAA number_format=\"1\" nfthousands=\"\">";
            ps["AAA"] = "123456.789";
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("123456.8", r);
        }

        [TestMethod()]
        public void parse_string_dateTest()
        {
            DateTime d = DateTime.Now;
            string tpl = "<~AAA>";
            Hashtable ps = [];
            ps["AAA"] = d;
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual(d.ToString("M/d/yyyy h:mm:ss tt"), r);

            tpl = "<~AAA date>";
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual(d.ToString("M/d/yyyy"), r);

            tpl = "<~AAA date=\"short\">";
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual(d.ToString("M/d/yyyy HH:mm"), r);

            tpl = "<~AAA date=\"long\">";
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual(d.ToString("M/d/yyyy HH:mm:ss"), r);

            tpl = "<~AAA date=\"sql\">";
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual(d.ToString("yyyy-MM-dd HH:mm:ss"), r);


            tpl = "<~AAA date=\"d M Y H:i\">";
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual(d.ToString("d M Y H:i"), r);
        }


        [TestMethod()]
        public void parse_string_truncateTest()
        {
            string tpl = "<~AAA truncate>";
            Hashtable ps = [];
            ps["AAA"] = "test test test test test test test test test test test test test test test test test test test test test test test test test test test test test test test test ";
            string r = new ParsePage(null!).parse_string(tpl, ps);
            var text = ps["AAA"]?.ToString() ?? string.Empty;
            Assert.AreEqual(text[..80].Trim() + " test...", r);

            tpl = "<~AAA>";
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual(ps["AAA"], r);
        }

        [TestMethod()]
        public void parse_string_strip_tagsTest()
        {
            string tpl = "<~AAA noescape strip_tags>";
            Hashtable ps = [];
            ps["AAA"] = "<p>tag</p>";
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("tag", r);

            tpl = "<~AAA noescape>";
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual(ps["AAA"], r);
        }

        [TestMethod()]
        public void parse_string_trimTest()
        {
            string tpl = "<~AAA trim>";
            Hashtable ps = [];
            ps["AAA"] = " tag ";
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("tag", r);
        }

        [TestMethod()]
        public void parse_string_nl2brTest()
        {
            string tpl = "<~AAA nl2br>";
            Hashtable ps = [];
            ps["AAA"] = "tag\ntag2";
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("tag<br>tag2", r);
        }

        [TestMethod()]
        public void parse_string_countTest()
        {
            string tpl = "<~AAA count>";
            Hashtable ps = [];
            ps["AAA"] = new string[] { "AAA", "BBB", "CCC", "DDD" };
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("4", r);
        }

        [TestMethod()]
        public void parse_string_lowerTest()
        {
            string tpl = "<~AAA lower>";
            Hashtable ps = [];
            ps["AAA"] = "TAG";
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("tag", r);
            Assert.AreNotEqual(ps["AAA"], r);
        }

        [TestMethod()]
        public void parse_string_upperTest()
        {
            string tpl = "<~AAA upper>";
            Hashtable ps = [];
            ps["AAA"] = "tag";
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("TAG", r);
            Assert.AreNotEqual(ps["AAA"], r);
        }


        [TestMethod()]
        public void parse_string_capitalizeTest()
        {
            string tpl = "<~AAA capitalize>";
            Hashtable ps = [];
            ps["AAA"] = "test test1 test2";
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Test test1 test2", r);

            tpl = "<~AAA capitalize=\"all\">";
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("Test Test1 Test2", r);

            Assert.AreNotEqual(ps["AAA"], r);
        }

        [TestMethod()]
        public void parse_string_defaultTest()
        {
            string tpl = "<~AAA default=\"default value\">";
            Hashtable ps = [];
            ps["AAA"] = "tag";
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("tag", r);

            ps["AAA"] = "";
            r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("default value", r);
        }


        [TestMethod()]
        public void parse_string_urlencodeTest()
        {
            string tpl = "<~AAA urlencode>";
            Hashtable ps = [];
            ps["AAA"] = "item[tag]=1&item[tag2]=2";
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual("item%5btag%5d%3d1%26amp%3bitem%5btag2%5d%3d2", r);
        }


        [TestMethod()]
        public void parse_string_jsonTest()
        {
            string tpl = "<~AAA json>";
            Hashtable ps = [];
            Hashtable h1 = [];
            h1["AAA"] = 1;
            h1["BBB"] = 2;
            h1["CCC"] = 3;
            h1["DDD"] = 4;
            ps["AAA"] = h1;
            string r = new ParsePage(null!).parse_string(tpl, ps);
            Assert.AreEqual(0, r.IndexOf("{"));
            Assert.IsGreaterThanOrEqualTo(0, r.IndexOf("&quot;AAA&quot;:1"));
            Assert.IsGreaterThanOrEqualTo(0, r.IndexOf("&quot;BBB&quot;:2"));
            Assert.IsGreaterThanOrEqualTo(0, r.IndexOf("&quot;CCC&quot;:3"));
            Assert.IsGreaterThanOrEqualTo(0, r.IndexOf("&quot;DDD&quot;:4"));
        }

    }
}
