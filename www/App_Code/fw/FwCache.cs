using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace osafw
{
    public class FwCache
    {
        public static Hashtable cache = new Hashtable();  // app level cache
        private static readonly Object locker = new Object();

        public Hashtable request_cache = new Hashtable(); // request level cache

        public static Object getValue(String key)
        {
            return cache[key];
        }

        public static void setValue(String key, Object value) {
            lock (locker)
            {
                cache[key] = value;
            }
        }

        // remove one key from cache
        public static void remove(String key)
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

        /******** request-level cache ***********/

        public Object getRequestValue(String key)
        {
            return request_cache[key];
        }
        public void setRequestValue(String key, Object value)
        {
            request_cache[key] = value;
        }
        // remove one key from request cache
        public void requestRemove(String key)
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
                    request_cache.Remove(key);
            }
        }

        // clear whole request cache
        public void requestClear()
        {
            request_cache.Clear();
        }
    }
}
