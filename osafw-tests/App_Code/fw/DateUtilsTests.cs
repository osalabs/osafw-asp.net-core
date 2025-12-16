using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

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
            Assert.AreEqual(r, d.ToString("yyyy-MM-dd"));

            // with time
            r = DateUtils.Date2SQL(d, true);
            Assert.AreEqual(r, d.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        [TestMethod()]
        public void Date2StrTest()
        {
            DateTime d = DateTime.UtcNow;
            String r = DateUtils.Date2Str(d, DateUtils.DATE_FORMAT_MDY);
            Assert.AreEqual(d.ToString("M/d/yyyy"), r);
        }

        [TestMethod()]
        public void SQL2DateTest()
        {
            DateTime d = DateTime.UtcNow;
            string str = d.ToString("yyyy-MM-dd HH:mm:ss");
            var r = DateUtils.SQL2Date(str);
            Assert.IsNotNull(r);
            Assert.AreEqual(d.Year, r.Value.Year);
            Assert.AreEqual(d.Month, r.Value.Month);
            Assert.AreEqual(d.Day, r.Value.Day);
            Assert.AreEqual(d.Hour, r.Value.Hour);
            Assert.AreEqual(d.Minute, r.Value.Minute);
            Assert.AreEqual(d.Second, r.Value.Second);

            str = d.ToString("yyyy-MM-dd");
            r = DateUtils.SQL2Date(str);
            Assert.IsNotNull(r);
            Assert.AreEqual(d.Year, r.Value.Year);
            Assert.AreEqual(d.Month, r.Value.Month);
            Assert.AreEqual(d.Day, r.Value.Day);
            Assert.AreEqual(0, r.Value.Hour);
            Assert.AreEqual(0, r.Value.Minute);
            Assert.AreEqual(0, r.Value.Second);

            // not an SQL date - should return null
            str = "1/1/2000";
            DateTime? r2 = DateUtils.SQL2Date(str);
            Assert.IsNull(r2);

            // null input should return null
            r2 = DateUtils.SQL2Date(null!);
            Assert.IsNull(r2);

            // empty input should return null
            r2 = DateUtils.SQL2Date("");
            Assert.IsNull(r2);

            // invalid input should return null
            r2 = DateUtils.SQL2Date("invalid_date");
            Assert.IsNull(r2);

        }

        [TestMethod()]
        public void Str2SQLTest()
        {
            DateTime d = DateTime.UtcNow;
            string r = DateUtils.Str2SQL(d.ToString("MM/dd/yyyy"), DateUtils.DATE_FORMAT_MDY);
            Assert.AreEqual(d.ToString("yyyy-MM-dd"), r);
            r = DateUtils.Str2SQL(d.ToString("dd/MM/yyyy"), DateUtils.DATE_FORMAT_DMY);
            Assert.AreEqual(d.ToString("yyyy-MM-dd"), r);

            r = DateUtils.Str2SQL(d.ToString("MM/dd/yyyy HH:mm"), DateUtils.DATE_FORMAT_MDY, DateUtils.TIME_FORMAT_24, true);
            Assert.AreEqual(d.ToString("yyyy-MM-dd HH:mm:00"), r);
            r = DateUtils.Str2SQL(d.ToString("MM/dd/yyyy h:mm tt"), DateUtils.DATE_FORMAT_MDY, DateUtils.TIME_FORMAT_12, true);
            Assert.AreEqual(d.ToString("yyyy-MM-dd HH:mm:00"), r);

            r = DateUtils.Str2SQL(d.ToString("dd/MM/yyyy HH:mm"), DateUtils.DATE_FORMAT_DMY, DateUtils.TIME_FORMAT_24, true);
            Assert.AreEqual(d.ToString("yyyy-MM-dd HH:mm:00"), r);
            r = DateUtils.Str2SQL(d.ToString("dd/MM/yyyy h:mm tt"), DateUtils.DATE_FORMAT_DMY, DateUtils.TIME_FORMAT_12, true);
            Assert.AreEqual(d.ToString("yyyy-MM-dd HH:mm:00"), r);

            // invalid date should return empty string
            r = DateUtils.Str2SQL("invalid_date", DateUtils.DATE_FORMAT_MDY);
            Assert.AreEqual("", r);

            // empty date should return empty string
            r = DateUtils.Str2SQL("", DateUtils.DATE_FORMAT_MDY);
            Assert.AreEqual("", r);

        }

        [TestMethod]
        public void Str2DateOnlyTest()
        {
            // Case 1: Test with empty input
            string input1 = "";
            string result1 = DateUtils.Str2DateOnly(input1, DateUtils.DATE_FORMAT_MDY);
            Assert.AreEqual("", result1, "Result should be empty for empty input");

            // Case 2: Test with valid datetime string
            string input2 = "1/17/2023 12:00:00 AM";
            string result2 = DateUtils.Str2DateOnly(input2, DateUtils.DATE_FORMAT_MDY);
            Assert.AreEqual("1/17/2023", result2, "Result should be date only");

            // Case 3: Test with invalid datetime string
            string input3 = "invalid_datetime";
            string result3 = DateUtils.Str2DateOnly(input3, DateUtils.DATE_FORMAT_MDY);
            Assert.AreEqual("invalid_datetime", result3, "Result should remain unchanged for invalid datetime string");
        }

        [TestMethod()]
        public void Date2TimeStrTest()
        {
            // Case 1: Test with empty input
            string input1 = "";
            string result1 = DateUtils.Str2DateOnly(input1, DateUtils.DATE_FORMAT_MDY);
            Assert.AreEqual("", result1, "Result should be empty for empty input");

            // Case 2: Test with valid datetime string in "M/d/yyyy h:mm:ss tt" format
            string input2 = "1/17/2023 12:00:00 AM";
            string result2 = DateUtils.Str2DateOnly(input2, DateUtils.DATE_FORMAT_MDY);
            Assert.AreEqual("1/17/2023", result2, "Result should be date only for 'M/d/yyyy h:mm:ss tt' format");

            // Case 3: Test with valid datetime string in "MM/dd/yyyy HH:mm:ss" format
            string input3 = "01/17/2023 00:00:00";
            string result3 = DateUtils.Str2DateOnly(input3, DateUtils.DATE_FORMAT_MDY);
            Assert.AreEqual("1/17/2023", result3, "Result should be date only for 'MM/dd/yyyy HH:mm:ss' format");

            // Case 4: Test with valid datetime string in "yyyy-MM-dd HH:mm:ss" format
            string input4 = "2023-01-17 00:00:00";
            string result4 = DateUtils.Str2DateOnly(input4, DateUtils.DATE_FORMAT_MDY);
            Assert.AreEqual("1/17/2023", result4, "Result should be date only for 'yyyy-MM-dd HH:mm:ss' format");

            // Case 5: Test with valid datetime string in "yyyy/MM/dd HH:mm:ss" format
            string input5 = "2023/01/17 00:00:00";
            string result5 = DateUtils.Str2DateOnly(input5, DateUtils.DATE_FORMAT_MDY);
            Assert.AreEqual("1/17/2023", result5, "Result should be date only for 'yyyy/MM/dd HH:mm:ss' format");

            // Case 6: Test with invalid datetime string
            string input6 = "invalid_datetime";
            string result6 = DateUtils.Str2DateOnly(input6, DateUtils.DATE_FORMAT_MDY);
            Assert.AreEqual("invalid_datetime", result6, "Result should remain unchanged for invalid datetime string");
        }

        [TestMethod()]
        public void nextDOWTest()
        {
            // Case 1: Test with default parameters (no specific date provided)
            DateTime expected1 = DateTime.Today.AddDays(((int)DayOfWeek.Monday - (int)DateTime.Today.DayOfWeek + 7) % 7 == 0 ? 7 : ((int)DayOfWeek.Monday - (int)DateTime.Today.DayOfWeek + 7) % 7);
            DateTime result1 = DateUtils.nextDOW(DayOfWeek.Monday);
            Assert.AreEqual(expected1, result1, "Result should be next Monday from today's date");

            // Case 2: Test with specific date provided
            DateTime specificDate = new(2023, 2, 10); // February 10, 2023 (Friday)
            DateTime result2 = DateUtils.nextDOW(DayOfWeek.Monday, specificDate);
            Assert.AreEqual(new DateTime(2023, 2, 13), result2, "Result should be next Monday after the specific date");

            // Case 3: Test with specific date provided which is the same day as the requested day
            DateTime specificDate2 = new(2023, 2, 13); // February 13, 2023 (Monday)
            DateTime result3 = DateUtils.nextDOW(DayOfWeek.Monday, specificDate2);
            Assert.AreEqual(new DateTime(2023, 2, 20), result3, "Result should be next Monday after the specific date, considering it's the same day");

            // Case 4: Test with specific date provided which is after the requested day
            DateTime specificDate3 = new(2023, 2, 15); // February 15, 2023 (Wednesday)
            DateTime result4 = DateUtils.nextDOW(DayOfWeek.Monday, specificDate3);
            Assert.AreEqual(new DateTime(2023, 2, 20), result4, "Result should be next Monday after the specific date, even if it's after the requested day");

            // Case 5: Test with specific date provided which is before the requested day
            DateTime specificDate4 = new(2023, 2, 6); // February 6, 2023 (Monday)
            DateTime result5 = DateUtils.nextDOW(DayOfWeek.Monday, specificDate4);
            Assert.AreEqual(new DateTime(2023, 2, 13), result5, "Result should be next Monday after the specific date, even if it's before the requested day");
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
            TimeSpan span = new(DateTime.Parse("1/1/1970").Ticks);
            DateTime time = d.Subtract(span);

            long r = DateUtils.Date2JsTimestamp(d);

            Assert.AreEqual(r, System.Convert.ToInt64(time.Ticks / (double)10000));

        }

        [TestMethod]
        public void MapTimeWithSecondsFormat_ReturnsExpectedPatterns()
        {
            Assert.AreEqual("h:mm:ss tt", DateUtils.mapTimeWithSecondsFormat(DateUtils.TIME_FORMAT_12));
            Assert.AreEqual("H:mm:ss", DateUtils.mapTimeWithSecondsFormat(DateUtils.TIME_FORMAT_24));
        }

        [TestMethod]
        public void MapDateAndTimeFormat_ReturnsExpectedPatterns()
        {
            Assert.AreEqual("M/d/yyyy", DateUtils.mapDateFormat(DateUtils.DATE_FORMAT_MDY));
            Assert.AreEqual("d/M/yyyy", DateUtils.mapDateFormat(DateUtils.DATE_FORMAT_DMY));
            Assert.AreEqual("h:mm tt", DateUtils.mapTimeFormat(DateUtils.TIME_FORMAT_12));
            Assert.AreEqual("H:mm", DateUtils.mapTimeFormat(DateUtils.TIME_FORMAT_24));
        }

        [TestMethod]
        public void ToFormat_FormatsDateTimeWhenValid()
        {
            var dt = new DateTime(2024, 1, 2, 3, 4, 5);

            var formatted = DateUtils.toFormat(dt, "yyyy-MM-dd");

            Assert.AreEqual("2024-01-02", formatted);
        }

        [TestMethod]
        public void ToFormat_ReturnsEmptyStringOnNullInput()
        {
            Assert.AreEqual("", DateUtils.toFormat(null!, "yyyy"));
        }

        [TestMethod]
        public void ConvertTimezone_ReturnsInputOnInvalidZone()
        {
            var now = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Unspecified);

            var same = DateUtils.convertTimezone(now, "Invalid/From", "Invalid/To");

            Assert.AreEqual(now, same);
        }
    }
}
