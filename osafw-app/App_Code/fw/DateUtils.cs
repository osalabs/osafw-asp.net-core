// Date framework utils
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Text.RegularExpressions;

namespace osafw;

public class DateUtils
{
    public const string DATABASE_TZ = "UTC"; // timezone of the database server


    public const string TZ_UTC = "UTC";

    public static string toFormat(object d, string format)
    {
        var dt = d.toDateOrNull();
        if (dt == null)
            return "";
        return ((DateTime)dt).ToString(format);
    }

    public static string Date2SQL(DateTime d)
    {
        return d.Year + "-" + d.Month + "-" + d.Day;
    }

    // IN: VB Date
    // OUT: MM/DD/YYYY
    public static string Date2Str(DateTime d)
    {
        return d.Month + "/" + d.Day + "/" + d.Year;
    }

    /// <summary>
    /// return true if string is date in format MM/DD/YYYY
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static bool isDateStr(string str)
    {
        return Regex.IsMatch(str, @"^\d{1,2}/\d{1,2}/\d{4}$");
    }

    public static DateTime? SQL2Date(string str)
    {
        DateTime? result = null;

        if (string.IsNullOrEmpty(str) || str == "0000-00-00" || str == "0000-00-00 00:00:00")
            return result;
        // yyyy-mm-dd
        Match m = Regex.Match(str, @"^(\d+)-(\d+)-(\d+)");
        // hh:mm:ss
        Match m2 = Regex.Match(str, @"(\d+):(\d+):(\d+)$");

        if (m2.Success)
            result = new DateTime(System.Convert.ToInt32(m.Groups[1].Value), System.Convert.ToInt32(m.Groups[2].Value), System.Convert.ToInt32(m.Groups[3].Value), System.Convert.ToInt32(m2.Groups[1].Value), System.Convert.ToInt32(m2.Groups[2].Value), System.Convert.ToInt32(m2.Groups[3].Value));
        else
            result = new DateTime(System.Convert.ToInt32(m.Groups[1].Value), System.Convert.ToInt32(m.Groups[2].Value), System.Convert.ToInt32(m.Groups[3].Value));

        return result;
    }

    // IN: MM/DD/YYYY[ HH:MM:SS]
    // OUT: YYYY-MM-DD[ HH:MM:SS]
    public static string Str2SQL(string str, bool is_time = false)
    {
        string result = "";
        if (DateTime.TryParse(str, out DateTime tmpdate))
        {
            string format = "yyyy-MM-dd HH:mm:ss";
            if (!is_time)
                format = "yyyy-MM-dd";
            result = tmpdate.ToString(format, System.Globalization.DateTimeFormatInfo.InvariantInfo);
        }

        return result;
    }

    // IN: datetime string
    // OUT: date string
    // Example: 1/17/2023 12:00:00 AM => 1/17/2023
    public static string Str2DateOnly(string str)
    {
        string result = str;
        var dt = str.toDate();
        if (Utils.isDate(dt))
        {
            result = dt.ToShortDateString();
        }
        return result;
    }

    // IN: datetime string
    // OUT: HH:MM
    public static string Date2TimeStr(string str)
    {
        string result = "";
        if (DateTime.TryParse(str, out DateTime tmpdate))
            result = tmpdate.Hour.ToString("00") + ":" + tmpdate.Minute.ToString("00");

        return result;
    }

    // return next day of week
    public static DateTime nextDOW(DayOfWeek whDayOfWeek, DateTime theDate = default)
    {
        if (theDate == default)
            theDate = DateTime.Today;
        DateTime d = theDate.AddDays(whDayOfWeek - theDate.DayOfWeek);
        if (d <= theDate)
        {
            return d.AddDays(7);
        }
        else
        {
            return d;
        }
    }

    // return utc unix timestamp
    public static long UnixTimestamp()
    {
        DateTime currentTime = DateTime.UtcNow;
        return ((DateTimeOffset)currentTime).ToUnixTimeSeconds();
    }

    public static DateTime Unix2Date(double unixTimeStamp)
    {
        DateTime result = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        result = result.AddSeconds(unixTimeStamp).ToLocalTime();
        return result;
    }

    // convert .net date to javascript timestamp
    public static long Date2JsTimestamp(DateTime dt)
    {
        TimeSpan span = new(DateTime.Parse("1/1/1970").Ticks);
        DateTime time = dt.Subtract(span);
        return System.Convert.ToInt64(time.Ticks / (double)10000);
    }
}