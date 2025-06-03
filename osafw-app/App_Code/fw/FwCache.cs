using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections;

namespace osafw;

public class FwCache
{
    public static IMemoryCache MemoryCache { get; set; }

    public Hashtable request_cache = []; // request level cache

    // ******** application-level cache with IMemoryCache ***********

    public static object getValue(string key)
    {
        return MemoryCache.Get(key);
    }

    /// <summary>
    /// set value to cache with default expire time 3600 seconds
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="expire_seconds"></param>
    public static void setValue(string key, object value, int expire_seconds = 3600)
    {
        MemoryCache.Set(key, value, TimeSpan.FromSeconds(expire_seconds));
    }

    // remove one key from cache
    public static void remove(string key)
    {
        MemoryCache.Remove(key);
    }

    // clear whole cache
    public static void clear()
    {
        if (MemoryCache is MemoryCache memoryCache)
        {
            memoryCache.Compact(1.0); //remove all entries
        }
    }

    protected static object serialize(object data)
    {
        if (data == null)
            return null;

        try
        {
            // serialize in cache because when read - need object clone, not original object
            return Utils.serialize(data);
        }
        catch (Exception)
        {
            // If serialization fails, store the original value.
            return data;
        }
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

    /// <summary>
    /// get value from request cache
    /// </summary>
    /// <param name="key"></param>
    /// <returns>Hashtable, ArrayList, other value or null - since objects in cache serialized using json when stored</returns>
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
            if (key.Length > plen && key[..plen] == prefix)
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
