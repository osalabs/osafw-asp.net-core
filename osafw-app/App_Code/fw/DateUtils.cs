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

    // keep in sync with template/common/sel/date_format.sel
    public const int DATE_FORMAT_MDY = 0; // MM/DD/YYYY
    public const int DATE_FORMAT_DMY = 10; // DD/MM/YYYY

    // keep in sync with template/common/sel/time_format.sel
    public const int TIME_FORMAT_12 = 0;
    public const int TIME_FORMAT_24 = 10;

    public const string TZ_UTC = "UTC";

    public static string mapDateFormat(int date_format)
    {
        string result = "MM/dd/yyyy";
        if (date_format == DATE_FORMAT_DMY)
            result = "dd/MM/yyyy";
        return result;
    }

    public static string mapTimeFormat(int time_format)
    {
        string result = "hh:mm tt";
        if (time_format == TIME_FORMAT_24)
            result = "HH:mm";
        return result;
    }

    public static string mapTimeWithSecondsFormat(int time_format)
    {
        string result = "hh:mm:ss tt";
        if (time_format == TIME_FORMAT_24)
            result = "HH:mm:ss";
        return result;
    }

    /// <summary>
    /// Converts the specified object to a formatted date string.
    /// </summary>
    /// <remarks>This method attempts to convert the input object to a <see cref="DateTime"/> using the
    /// <c>toDateOrNull</c> extension method. If the conversion fails, an empty string is returned. Otherwise, the
    /// resulting <see cref="DateTime"/> is formatted using the specified format string.</remarks>
    /// <param name="d">The object to convert. The object must be convertible to a <see cref="DateTime"/> or <see langword="null"/>.</param>
    /// <param name="format">A standard or custom date and time format string. See <see cref="DateTime.ToString(string)"/> for valid formats.</param>
    /// <returns>A string representation of the date in the specified format, or an empty string if the object cannot be
    /// converted to a valid date.</returns>
    public static string toFormat(object d, string format)
    {
        var dt = d.toDateOrNull();
        if (dt == null)
            return "";
        return ((DateTime)dt).ToString(format);
    }

    /// <summary>
    /// Converts a <see cref="DateTime"/> value to its equivalent SQL-compatible string representation.
    /// </summary>
    /// <remarks>The method uses the invariant culture to ensure consistent formatting regardless of the
    /// current culture.</remarks>
    /// <param name="d">The <see cref="DateTime"/> value to convert.</param>
    /// <param name="is_include_time">A value indicating whether to include the time component in the resulting string. <see langword="true"/> to
    /// include the time component; otherwise, <see langword="false"/>.</param>
    /// <returns>A string representing the <paramref name="d"/> value in SQL-compatible format.  The format is "yyyy-MM-dd" if
    /// <paramref name="is_include_time"/> is <see langword="false"/>,  or "yyyy-MM-dd HH:mm:ss" if <paramref
    /// name="is_include_time"/> is <see langword="true"/>.</returns>
    public static string Date2SQL(DateTime d, bool is_include_time = false)
    {
        string format = "yyyy-MM-dd";
        if (is_include_time)
            format = "yyyy-MM-dd HH:mm:ss";
        return d.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Converts the specified <see cref="DateTime"/> to a string representation based on the given date format.
    /// </summary>
    /// <remarks>The method supports two formats: "dd/MM/yyyy" and "MM/dd/yyyy". The format is determined by
    /// the value of  <paramref name="date_format"/>. See DATE_FORMAT_* constants </remarks>
    /// <param name="d">The <see cref="DateTime"/> value to convert.</param>
    /// <param name="date_format">An integer representing the desired date format. See DATE_FORMAT_* constants.</param>
    /// <returns>A string representation of the date in the specified format.</returns>
    public static string Date2Str(DateTime d, int date_format)
    {
        if (date_format == DATE_FORMAT_DMY)
            return d.ToString("dd/MM/yyyy");
        else
            return d.ToString("MM/dd/yyyy");
    }

    /// <summary>
    /// return true if string is date in format MM/DD/YYYY or D/M/YYYY
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

        // Only accept SQL formats
        if (Regex.IsMatch(str, @"^\d{4}-\d{2}-\d{2}$") || Regex.IsMatch(str, @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$"))
        {
            if (DateTime.TryParse(str, out DateTime tmpdate))
                result = tmpdate;
        }
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
    public static string ParseDate2TimeStr(string str)
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