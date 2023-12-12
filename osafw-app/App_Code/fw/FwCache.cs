using System;
using System.Collections;

namespace osafw;

public class FwCache
{
    public static Hashtable cache = new();  // app level cache
    private static readonly Object locker = new();

    public Hashtable request_cache = new(); // request level cache

    public static object getValue(string key)
    {
        var result = cache[key];
        return deserialize(result);
    }

    public static void setValue(string key, object value)
    {
        lock (locker)
        {
            cache[key] = serialize(value);
        }
    }

    // remove one key from cache
    public static void remove(string key)
    {
        lock (locker)
        {
            cache.Remove(key);
        }
    }

    // clear whole cache
    public static void clear()
    {
        lock (locker)
        {
            cache.Clear();
        }
    }

    protected static object serialize(object data)
    {
        if (data == null)
            return null;

        //TODO DEBUG exceptions
        //try
        //{
        // serialize in cache because when read - need object clone, not original object
        return Utils.serialize(data);
        //}
        //catch (Exception)
        //{
        //    // If serialization fails, store the original value.
        //    return value;
        //}
    }

    protected static object deserialize(object data)
    {
        if (data == null)
        {
            return null;
        }
        // Check if the result is a string - it might be a serialized object.
        if (data is string serialized_string)
        {
            try
            {
                // Attempt to deserialize the string back into an object.
                return Utils.deserialize(serialized_string);
            }
            catch (Exception)
            {
                // If deserialization fails, return the serialized string as is.
                return serialized_string;
            }
        }
        return data;
    }

    // ******** request-level cache ***********

    public object getRequestValue(string key)
    {
        var result = request_cache[key];
        return deserialize(result);
    }
    public void setRequestValue(string key, object value)
    {
        request_cache[key] = serialize(value);
    }
    // remove one key from request cache
    public void requestRemove(string key)
    {
        request_cache.Remove(key);
    }


    /// <summary>
    /// remove all keys with prefix from the request cache
    /// </summary>
    /// <param name="prefix">prefix key</param>
    public void requestRemoveWithPrefix(string prefix)
    {
        var plen = prefix.Length;
        foreach (string key in new ArrayList(request_cache.Keys))
        {
            if (key.Length > plen && key.Substring(0, plen) == prefix)
            {
                request_cache.Remove(key);
            }
        }
    }

    // clear whole request cache
    public void requestClear()
    {
        request_cache.Clear();
    }
}
