using Microsoft.VisualStudio.TestTools.UnitTesting;
using osafw;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace osafw.Tests
{
    [TestClass()]
    public class DateUtilsTests
    {
        [TestMethod()]
        public void Date2SQLTest()
        {
            DateTime d = DateTime.UtcNow;
            String r = DateUtils.Date2SQL(d);
            Assert.AreEqual(r, d.Year + "-" + d.Month + "-" + d.Day);
        }

        [TestMethod()]
        public void Date2StrTest()
        {
            DateTime d = DateTime.UtcNow;
            String r = DateUtils.Date2Str(d);
            Assert.AreEqual(r, d.Month + "/" + d.Day + "/" + d.Year);
        }

        [TestMethod()]
        public void SQL2DateTest()
        {
            DateTime d = DateTime.UtcNow;
            string str = d.ToString("yyyy-MM-dd HH:mm:ss");
            DateTime r = (DateTime)DateUtils.SQL2Date(str);
            Assert.AreEqual(d.Year, r.Year);
            Assert.AreEqual(d.Month, r.Month);
            Assert.AreEqual(d.Day, r.Day);
            Assert.AreEqual(d.Hour, r.Hour);
            Assert.AreEqual(d.Minute, r.Minute);
            Assert.AreEqual(d.Second, r.Second);
        }

        [TestMethod()]
        public void Str2SQLTest()
        {
            DateTime d = DateTime.UtcNow;
            string r = DateUtils.Str2SQL(d.ToString("MM/dd/yyyy"));
            
            Assert.AreEqual(r, d.ToString("yyyy-MM-dd"));
        }

        [TestMethod()]
        public void Date2TimeStrTest()
        {
            DateTime d = DateTime.UtcNow;
            string r = DateUtils.Date2TimeStr(d.ToString("MM/dd/yyyy HH:mm:ss"));
            Assert.AreEqual(r, d.ToString("HH:mm"));

            r = DateUtils.Date2TimeStr(d.ToString("yyyy-MM-dd HH:mm:ss"));
            Assert.AreEqual(r, d.ToString("HH:mm"));
        }

        [TestMethod()]
        public void nextDOWTest()
        {
            Assert.Fail("Not sure how to check this yet");
        }

        [TestMethod()]
        public void Unix2DateTest()
        {
            DateTime d = DateTime.Now;
            long ts = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();

            DateTime r = DateUtils.Unix2Date((double)ts);

            Assert.AreEqual(d.Year, r.Year);
            Assert.AreEqual(d.Month, r.Month);
            Assert.AreEqual(d.Day, r.Day);
            Assert.AreEqual(d.Hour, r.Hour);
            Assert.AreEqual(d.Minute, r.Minute);
            Assert.AreEqual(d.Second, r.Second);
        }

        [TestMethod()]
        public void Date2JsTimestampTest()
        {
            DateTime d = DateTime.Now;
            TimeSpan span = new TimeSpan(DateTime.Parse("1/1/1970").Ticks);
            DateTime time = d.Subtract(span);

            long r = DateUtils.Date2JsTimestamp(d);

            Assert.AreEqual(r, System.Convert.ToInt64(time.Ticks / (double)10000));
           
        }
    }
}