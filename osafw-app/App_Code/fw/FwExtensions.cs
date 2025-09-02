// Contains extension methods.
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net-core
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Globalization;

/// <summary>
/// Provides extension methods for safe type conversions.
/// </summary>
public static class FwExtensions
{
    /// <summary>
    /// Converts an object to a boolean.
    /// <para>- <c>null</c> returns <c>false</c>.</para>
    /// <para>- <c>bool</c> returns its own value.</para>
    /// <para>- <c>ICollection</c> returns <c>true</c> if Count &gt; 0, otherwise <c>false</c>.</para>
    /// <para>- A non-zero number returns <c>true</c>, zero returns <c>false</c>.</para>
    /// <para>- "true"/"false" strings - <see cref="bool.TryParse"/></para>
    /// <para>- otherwise check string: non-empty returns <c>true</c>, empty or whitespace or "0" returns <c>false</c>.</para>
    /// </summary>
    /// <param name="o">The object to convert to a boolean.</param>
    /// <returns>The boolean value interpreted from <paramref name="o"/>.</returns>
    public static bool toBool(this object o)
    {
        if (o is null)
        {
            return false;
        }
        if (o is bool b)
        {
            return b;
        }
        if (o is ICollection ic)
        {
            return ic.Count > 0;
        }
        if (o.toDouble() != 0.0)
        {
            return true;
        }
        var s = o.toStr().Trim();
        if (bool.TryParse(s, out bool result))
        {
            return result;
        }

        // finally check string
        if (!string.IsNullOrWhiteSpace(s) && s != "0")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Converts an object to <see cref="DateTime"/> using the specified <paramref name="format"/> if provided.
    /// If conversion fails, returns <see cref="DateTime.MinValue"/>.
    /// </summary>
    /// <param name="o">The object to convert.</param>
    /// <param name="format">If provided, <see cref="DateTime.TryParseExact(string,string[],IFormatProvider,DateTimeStyles,out DateTime)"/> is used.</param>
    /// <returns>A <see cref="DateTime"/>, or <see cref="DateTime.MinValue"/> on failure.</returns>
    public static DateTime toDate(this object o, string format = "")
    {
        if (o is null)
        {
            return DateTime.MinValue;
        }
        if (o is DateTime dt)
        {
            return dt;
        }
        return o.toStr().toDate(format);
    }

    /// <summary>
    /// Converts a string to <see cref="DateTime"/> using the specified <paramref name="format"/> if provided.
    /// If conversion fails, returns <see cref="DateTime.MinValue"/>.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <param name="format">If provided, <see cref="DateTime.TryParseExact(string,string[],IFormatProvider,DateTimeStyles,out DateTime)"/> is used.</param>
    /// <returns>A <see cref="DateTime"/>, or <see cref="DateTime.MinValue"/> on failure.</returns>
    public static DateTime toDate(this string s, string format = "")
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return DateTime.MinValue;
        }
        if (!string.IsNullOrWhiteSpace(format))
        {
            if (DateTime.TryParseExact(s, format, null, DateTimeStyles.None, out DateTime exactParsed))
            {
                return exactParsed;
            }
        }
        if (DateTime.TryParse(s, out DateTime parsed))
        {
            return parsed;
        }
        return DateTime.MinValue;
    }

    /// <summary>
    /// Converts an object to a nullable <see cref="DateTime"/> using the specified <paramref name="format"/> if provided.
    /// If conversion fails, returns <c>null</c>.
    /// </summary>
    /// <param name="o">The object to convert.</param>
    /// <param name="format">If provided, <see cref="DateTime.TryParseExact(string,string[],IFormatProvider,DateTimeStyles,out DateTime)"/> is used.</param>
    /// <returns>A <see cref="DateTime"/> value, or <c>null</c> on failure.</returns>
    public static DateTime? toDateOrNull(this object o, string format = "")
    {
        if (o is null)
        {
            return null;
        }
        if (o is DateTime dt)
        {
            return dt;
        }
        return o.toStr().toDateOrNull(format);
    }

    /// <summary>
    /// Converts a string to a nullable <see cref="DateTime"/> using the specified <paramref name="format"/> if provided.
    /// If conversion fails, returns <c>null</c>.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <param name="format">If provided, <see cref="DateTime.TryParseExact(string,string[],IFormatProvider,DateTimeStyles,out DateTime)"/> is used.</param>
    /// <returns>A <see cref="DateTime"/> value, or <c>null</c> on failure.</returns>
    public static DateTime? toDateOrNull(this string s, string format = "")
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }
        if (!string.IsNullOrWhiteSpace(format))
        {
            if (DateTime.TryParseExact(s, format, null, DateTimeStyles.None, out DateTime exactParsed))
            {
                return exactParsed;
            }
        }
        if (DateTime.TryParse(s, out DateTime parsed))
        {
            return parsed;
        }

        return null;
    }

    /// <summary>
    /// Converts an object to a <see cref="decimal"/>.  
    /// If the object is already a <see cref="decimal"/>, it is returned directly.  
    /// If conversion fails, returns the specified <paramref name="defaultValue"/>.
    /// </summary>
    /// <param name="o">The object to convert.</param>
    /// <param name="defaultValue">The value returned if conversion fails.</param>
    /// <returns>A decimal representation of the object, or <paramref name="defaultValue"/>.</returns>
    public static decimal toDecimal(this object o, decimal defaultValue = decimal.Zero)
    {
        if (o is null)
        {
            return defaultValue;
        }
        if (o is decimal d)
        {
            return d;
        }
        return o.toStr().toDecimal(defaultValue);
    }

    /// <summary>
    /// Converts a string to a <see cref="decimal"/>.  
    /// If conversion fails, returns the specified <paramref name="defaultValue"/>.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <param name="defaultValue">The value returned if conversion fails.</param>
    /// <returns>A decimal, or <paramref name="defaultValue"/> if parsing fails.</returns>
    public static decimal toDecimal(this string s, decimal defaultValue = decimal.Zero)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return defaultValue;
        }
        if (decimal.TryParse(s, out decimal result))
        {
            return result;
        }
        return defaultValue;
    }

    /// <summary>
    /// Converts an object to a <see cref="double"/>.  
    /// If the object is already a <see cref="double"/>, it is returned directly.  
    /// If conversion fails, returns the specified <paramref name="defaultValue"/>.
    /// </summary>
    /// <param name="o">The object to convert.</param>
    /// <param name="defaultValue">The value returned if conversion fails.</param>
    /// <returns>A double representation of the object, or <paramref name="defaultValue"/>.</returns>
    public static double toDouble(this object o, double defaultValue = 0.0)
    {
        if (o is null)
        {
            return defaultValue;
        }
        if (o is double d)
        {
            return d;
        }
        return o.toStr().toDouble(defaultValue);
    }

    /// <summary>
    /// Converts a string to a <see cref="double"/>.  
    /// If conversion fails, returns the specified <paramref name="defaultValue"/>.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <param name="defaultValue">The value returned if conversion fails.</param>
    /// <returns>A double, or <paramref name="defaultValue"/> if parsing fails.</returns>
    public static double toDouble(this string s, double defaultValue = 0.0)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return defaultValue;
        }
        if (double.TryParse(s, out double result))
        {
            return result;
        }
        return defaultValue;
    }

    /// <summary>
    /// Converts an object to a <see cref="float"/>.  
    /// If the object is already a <see cref="float"/>, it is returned directly.  
    /// If conversion fails, returns the specified <paramref name="defaultValue"/>.
    /// </summary>
    /// <param name="o">The object to convert.</param>
    /// <param name="defaultValue">The value returned if conversion fails.</param>
    /// <returns>A float representation of the object, or <paramref name="defaultValue"/>.</returns>
    public static float toFloat(this object o, float defaultValue = 0.0f)
    {
        if (o is null)
        {
            return defaultValue;
        }
        if (o is float f)
        {
            return f;
        }
        return o.toStr().toFloat(defaultValue);
    }

    /// <summary>
    /// Converts a string to a <see cref="float"/>.  
    /// If conversion fails, returns the specified <paramref name="defaultValue"/>.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <param name="defaultValue">The value returned if conversion fails.</param>
    /// <returns>A float, or <paramref name="defaultValue"/> if parsing fails.</returns>
    public static float toFloat(this string s, float defaultValue = 0.0f)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return defaultValue;
        }
        if (float.TryParse(s, out float result))
        {
            return result;
        }
        return defaultValue;
    }

    /// <summary>
    /// Converts an object to an <see cref="int"/>.  
    /// If the object is already an <see cref="int"/>, it is returned directly.  
    /// If conversion fails, returns the specified <paramref name="defaultValue"/> (default is 0).
    /// </summary>
    /// <param name="o">The object to convert.</param>
    /// <param name="defaultValue">The value returned if conversion fails.</param>
    /// <returns>An integer representation of the object, or <paramref name="defaultValue"/>.</returns>
    public static int toInt(this object o, int defaultValue = 0)
    {
        if (o is null)
        {
            return defaultValue;
        }
        if (o is int i)
        {
            return i;
        }
        return o.toStr().toInt(defaultValue);
    }

    /// <summary>
    /// Converts a string to an <see cref="int"/>.  
    /// If conversion fails, returns the specified <paramref name="defaultValue"/> (default is 0).
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <param name="defaultValue">The value returned if conversion fails.</param>
    /// <returns>An integer, or <paramref name="defaultValue"/> if parsing fails.</returns>
    public static int toInt(this string s, int defaultValue = 0)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return defaultValue;
        }
        if (int.TryParse(s, out int result))
        {
            return result;
        }
        return defaultValue;
    }

    /// <summary>
    /// Converts an object to a <see cref="long"/>.  
    /// If the object is already a <see cref="long"/>, it is returned directly.  
    /// If conversion fails, returns the specified <paramref name="defaultValue"/>.
    /// </summary>
    /// <param name="o">The object to convert.</param>
    /// <param name="defaultValue">The value returned if conversion fails.</param>
    /// <returns>A long representation of the object, or <paramref name="defaultValue"/>.</returns>
    public static long toLong(this object o, long defaultValue = 0)
    {
        if (o is null)
        {
            return defaultValue;
        }
        if (o is long l)
        {
            return l;
        }
        return o.toStr().toLong(defaultValue);
    }

    /// <summary>
    /// Converts a string to a <see cref="long"/>.  
    /// If conversion fails, returns the specified <paramref name="defaultValue"/>.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <param name="defaultValue">The value returned if conversion fails.</param>
    /// <returns>A long, or <paramref name="defaultValue"/> if parsing fails.</returns>
    public static long toLong(this string s, long defaultValue = 0)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return defaultValue;
        }
        if (long.TryParse(s, out long result))
        {
            return result;
        }
        return defaultValue;
    }

    /// <summary>
    /// Converts any object to a string. Returns an empty string if <paramref name="o"/> is null.
    /// </summary>
    /// <param name="o">The object to convert.</param>
    /// <returns>The string representation of the object, or an empty string if null.</returns>
    public static string toStr(this object o, string defaultValule = "")
    {
        return o?.ToString() ?? defaultValule;
    }
}
