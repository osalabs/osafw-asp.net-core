using System;
using System.Collections;

namespace osafw
{
    public class FwCache
    {
        public static Hashtable cache = new();  // app level cache
        private static readonly Object locker = new();

        public Hashtable request_cache = new(); // request level cache

        public static object getValue(string key)
        {
            var result = cache[key];
            if (result != null)
            {
                var t = result.GetType();
                if (t.IsSerializable)
                {

                    result = Utils.deserialize((string)result);
                }
            }
            return result;
        }

        public static void setValue(string key, object value)
        {
            lock (locker)
            {
                if (value == null)
                    cache[key] = value;
                else
                {
                    var t = value.GetType();
                    if (t.IsSerializable)
                    {
                        // serialize in cache because when read - need object clone, not original object
                        cache[key] = Utils.serialize(value);
                    }
                    else
                    {
                        cache[key] = value;
                    }
                }
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

        // ******** request-level cache ***********

        public object getRequestValue(string key)
        {
            var result = request_cache[key];
            if (result != null)
            {
                var t = result.GetType();
                if (t.IsSerializable)
                {
                    result = Utils.deserialize((string)result);
                }
            }
            return result;
        }
        public void setRequestValue(string key, object value)
        {
            if (value == null)
                request_cache[key] = value;
            else
            {
                var t = value.GetType();
                if (t.IsSerializable)
                {
                    // serialize in cache because when read - need object clone, not original object
                    request_cache[key] = Utils.serialize(value);
                }
                else
                {
                    request_cache[key] = value;
                }
            }
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
                if (key.Substring(0, plen) == prefix)
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
}
