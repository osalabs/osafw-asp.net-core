// Contains extension methods.
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net-core
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using osafw;

/// <summary>
/// Provides extension methods for safe type conversions.
/// </summary>
public static class FwExtensions
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> WritablePropertiesCache = new();
    private static readonly ConcurrentDictionary<Type, Dictionary<string, Func<object, object>>> ReadableMembersCache = new();

    private static Dictionary<string, PropertyInfo> getWritablePropertiesCore(Type type)
    {
        return WritablePropertiesCache.GetOrAdd(type, static t =>
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var dict = new Dictionary<string, PropertyInfo>(comparer);

            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite)
                    continue;

                var attr = prop.GetCustomAttribute<DBNameAttribute>(inherit: false);
                var key = attr?.Description ?? prop.Name;
                if (!dict.ContainsKey(key))
                    dict[key] = prop;
            }

            return dict;
        });
    }

    internal static Dictionary<string, PropertyInfo> getWritableProperties<T>() => getWritablePropertiesCore(typeof(T));

    internal static Dictionary<string, PropertyInfo> getWritableProperties(this Type type) => getWritablePropertiesCore(type);

    internal static void clearWritablePropertiesCache() => WritablePropertiesCache.Clear();

    private static void setPropertyValue<T>(this T obj, PropertyInfo property, object value)
    {
        if (value is null || value is DBNull)
        {
            property.SetValue(obj, null);
            return;
        }

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (targetType.IsAssignableFrom(value.GetType()))
        {
            property.SetValue(obj, value);
            return;
        }

        object convertedValue;
        if (targetType.IsEnum)
        {
            convertedValue = value is string str
                ? Enum.Parse(targetType, str, ignoreCase: true)
                : Enum.ToObject(targetType, Convert.ChangeType(value, Enum.GetUnderlyingType(targetType)));
        }
        else if (targetType == typeof(Guid))
        {
            convertedValue = value is Guid guid
                ? guid
                : Guid.Parse(value.ToString());
        }
        else
        {
            convertedValue = Convert.ChangeType(value, targetType);
        }

        property.SetValue(obj, convertedValue);
    }

    internal static void setPropertyValue<T>(this T obj, Dictionary<string, PropertyInfo> props, string field, object value)
    {
        if (props.TryGetValue(field, out var property))
        {
            obj.setPropertyValue(property, value);
        }
    }

    private static T populateObject<T>(IDictionary kv, Dictionary<string, PropertyInfo> props, T obj)
    {
        foreach (DictionaryEntry entry in kv)
        {
            var key = entry.Key?.ToString();
            if (string.IsNullOrEmpty(key))
                continue;

            obj.setPropertyValue(props, key, entry.Value);
        }

        return obj;
    }

    public static T @as<T>(this IDictionary kv) where T : new()
    {
        ArgumentNullException.ThrowIfNull(kv);

        var props = getWritableProperties<T>();
        return populateObject(kv, props, new T());
    }

    public static List<T> asList<T>(this IList rows) where T : new()
    {
        ArgumentNullException.ThrowIfNull(rows);

        var result = new List<T>(rows.Count);
        if (rows.Count == 0)
            return result;

        var props = getWritableProperties<T>();
        foreach (var item in rows)
        {
            if (item is null)
            {
                continue;
            }

            if (item is T typed)
            {
                result.Add(typed);
            }
            else if (item is IDictionary dict)
            {
                result.Add(populateObject(dict, props, new T()));
            }
            else
            {
                var keyValues = (IDictionary)item.toKeyValue();
                result.Add(populateObject(keyValues, props, new T()));
            }
        }

        return result;
    }

    public static Dictionary<string, object> toKeyValue(this object dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto is Dictionary<string, object> dictionary)
            return new Dictionary<string, object>(dictionary, StringComparer.OrdinalIgnoreCase);

        if (dto is IDictionary dict)
        {
            Dictionary<string, object> result = new(dict.Count, StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dict)
            {
                var key = entry.Key?.ToString();
                if (!string.IsNullOrEmpty(key))
                    result[key] = entry.Value;
            }
            return result;
        }

        var props = getWritableProperties(dto.GetType());
        Dictionary<string, object> kv = new(props.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in props)
        {
            kv[pair.Key] = pair.Value.GetValue(dto);
        }

        return kv;
    }

    public static Hashtable toHashtable(this object dto)
    {
        if (dto is null)
            return [];

        if (dto is Hashtable ht)
            return (Hashtable)ht.Clone();

        if (dto is IDictionary dict)
        {
            Hashtable result = new(dict.Count);
            foreach (DictionaryEntry entry in dict)
                result[entry.Key] = entry.Value;
            return result;
        }

        var props = dto.GetType().getWritableProperties();
        Hashtable htResult = new(props.Count);
        foreach (var kv in props)
        {
            htResult[kv.Key] = kv.Value.GetValue(dto);
        }

        return htResult;
    }

    public static void applyTo<T>(this IDictionary kv, T dto)
    {
        ArgumentNullException.ThrowIfNull(kv);
        ArgumentNullException.ThrowIfNull(dto);

        var props = dto.GetType().getWritableProperties();
        populateObject(kv, props, dto);
    }

    public static Dictionary<string, Func<object, object>> getReadableMembers(this object obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        var type = obj.GetType();
        return ReadableMembersCache.GetOrAdd(type, static t =>
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var dict = new Dictionary<string, Func<object, object>>(comparer);

            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                    continue;

                var key = prop.Name;
                if (!dict.ContainsKey(key))
                {
                    dict[key] = obj =>
                    {
                        try { return prop.GetValue(obj); }
                        catch { return null; }
                    };
                }
            }

            foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var key = field.Name;
                if (!dict.ContainsKey(key))
                {
                    dict[key] = obj =>
                    {
                        try { return field.GetValue(obj); }
                        catch { return null; }
                    };
                }
            }

            return dict;
        });
    }

    public static object valueByMemberName(this object obj, string memberName)
    {
        ArgumentNullException.ThrowIfNull(obj);
        if (string.IsNullOrEmpty(memberName))
            return null;

        var members = obj.getReadableMembers();
        return members.TryGetValue(memberName, out var getter) ? getter(obj) : null;
    }

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
