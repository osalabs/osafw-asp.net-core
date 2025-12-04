// Date framework utils
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Text.RegularExpressions;

namespace osafw;

public class DateUtils
{
    // keep in sync with template/common/sel/date_format.sel
    public const int DATE_FORMAT_MDY = 0; // M/D/YYYY
    public const int DATE_FORMAT_DMY = 10; // D/M/YYYY

    // keep in sync with template/common/sel/time_format.sel
    public const int TIME_FORMAT_12 = 0;
    public const int TIME_FORMAT_24 = 10;

    public const string TZ_UTC = "UTC";

    public static string mapDateFormat(int date_format)
    {
        string result = "M/d/yyyy";
        if (date_format == DATE_FORMAT_DMY)
            result = "d/M/yyyy";
        return result;
    }

    public static string mapTimeFormat(int time_format)
    {
        string result = "h:mm tt";
        if (time_format == TIME_FORMAT_24)
            result = "H:mm";
        return result;
    }

    public static string mapTimeWithSecondsFormat(int time_format)
    {
        string result = "h:mm:ss tt";
        if (time_format == TIME_FORMAT_24)
            result = "H:mm:ss";
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
    /// <param name="date_format">See DATE_FORMAT_* constants.</param>
    /// <returns>A string representation of the date in the specified format.</returns>
    public static string Date2Str(DateTime d, int date_format)
    {
        return d.ToString(mapDateFormat(date_format));
    }

    /// <summary>
    /// Converts the specified <see cref="DateTime"/> to a string representation based on the given date and time format.
    /// </summary>
    /// <remarks>The method supports two formats: "dd/MM/yyyy" and "MM/dd/yyyy". The format is determined by
    /// the value of  <paramref name="date_format"/>. See DATE_FORMAT_* constants </remarks>
    /// <param name="d">The <see cref="DateTime"/> value to convert.</param>
    /// <param name="date_format">See DATE_FORMAT_* constants.</param>
    /// <param name="time_format">See TIME_FORMAT_* constants</param>
    /// <returns>A string representation of the date in the specified format.</returns>
    public static string DateTime2Str(DateTime d, int date_format, int time_format)
    {
        return d.ToString(mapDateFormat(date_format) + " " + mapTimeFormat(time_format));
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

    /// <summary>
    /// Determines whether the specified string is in a valid SQL date/datetime format.
    /// </summary>
    /// <param name="str">The string to validate as a SQL date or datetime.</param>
    /// <returns><see langword="true"/> if the string matches the format "yyyy-MM-dd" or "yyyy-MM-dd HH:mm:ss"; otherwise, <see
    /// langword="false"/>.</returns>
    public static bool isDateSQL(string str)
    {
        return Regex.IsMatch(str, @"^\d{4}-\d{2}-\d{2}$") || Regex.IsMatch(str, @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$");
    }

    public static DateTime? SQL2Date(string str)
    {
        DateTime? result = null;

        if (string.IsNullOrEmpty(str) || str == "0000-00-00" || str == "0000-00-00 00:00:00")
            return result;

        // Only accept SQL formats
        if (isDateSQL(str))
        {
            if (DateTime.TryParse(str, out DateTime tmpdate))
                result = tmpdate;
        }
        return result;
    }

    /// <summary>
    /// convert human date input to SQL date
    /// </summary>
    /// <param name="str">human date input per current user settings (see date_format)</param>
    /// <param name="date_format">See DATE_FORMAT_* constants.</param>
    /// <param name="time_format">See TIME_FORMAT_* constants</param>
    /// <param name="is_time">if true SQL date also has time</param>
    /// <returns>SQL YYYY-MM-DD[ HH:MM:SS]</returns>
    public static string Str2SQL(string str, int date_format, int time_format = TIME_FORMAT_24, bool is_time = false)
    {
        if (isDateSQL(str))
            return str; // already in SQL format

        string result = "";
        //convert str to DateTime using date_format
        string format = mapDateFormat(date_format);
        if (is_time)
            format += " " + mapTimeFormat(time_format); // use format without seconds for input
        if (DateTime.TryParseExact(str, format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime tmpdate))
            result = Date2SQL(tmpdate, is_time);

        return result;
    }

    /// <summary>
    /// convert human date input to date only string per user settings
    /// </summary>
    /// <remarks>Example: 1/17/2023 12:00:00 AM => 1/17/2023</remarks>
    /// <param name="str">date/time string</param>
    /// <param name="date_format">See DATE_FORMAT_* constants.</param>
    /// <returns>date string</returns>
    public static string Str2DateOnly(string str, int date_format)
    {
        string result = str;
        string format = mapDateFormat(date_format);

        var dt = str.toDate(format);
        if (Utils.isDate(dt))
        {
            result = dt.ToString(format);
        }
        return result;
    }

    /// <summary>
    /// convert human date input to time only string per user settings
    /// </summary>
    /// <remarks>Example: 1/17/2023 3:12 AM => 3:12 AM</remarks>
    /// <param name="str">date/time string</param>
    /// <param name="date_format">See DATE_FORMAT_* constants.</param>
    /// <param name="time_format">See TIME_FORMAT_* constants</param>
    /// <returns></returns>
    public static string Str2TimeOnly(string str, int date_format, int time_format)
    {
        string result = "";

        string format = mapDateFormat(date_format) + " " + mapTimeFormat(time_format);
        var dt = str.toDate(format);
        if (Utils.isDate(dt))
        {
            result = dt.ToString(mapTimeFormat(time_format));
        }

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

    public static DateTime convertTimezone(DateTime dt, string from_tz, string to_tz)
    {
        if (from_tz == to_tz)
            return dt;
        try
        {
            TimeZoneInfo tzi_from = TimeZoneInfo.FindSystemTimeZoneById(from_tz);
            TimeZoneInfo tzi_to = TimeZoneInfo.FindSystemTimeZoneById(to_tz);
            DateTime dt_utc = TimeZoneInfo.ConvertTimeToUtc(dt, tzi_from);
            DateTime dt_to = TimeZoneInfo.ConvertTimeFromUtc(dt_utc, tzi_to);
            return dt_to;
        }
        catch (Exception ex)
        {
            // invalid timezone
            Console.WriteLine("DateUtils - invalid timezone conversion from " + from_tz + " to " + to_tz + ": " + ex.Message);
            return dt;
        }
    }
}
