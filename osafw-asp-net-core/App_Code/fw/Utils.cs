using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace osafw_asp_net_core.fw
{
    public class Utils
    {
        /*
        * convert string like "AAA|1 BBB|2 CCC|3 DDD" to hash
        * AAA => 1
        * BBB => 2
        * CCC => 3
        * DDD => 1 (default value 1)
        * or "AAA BBB CCC DDD" => AAA=1, BBB=1, CCC=1, DDD=1
        * WARN! replaces all "&nbsp;" to spaces (after convert)
        */
        public static Hashtable qh(string str, object default_value = null) {
            Hashtable result = new Hashtable();
            if (str != null && str != "") {
                string[] arr = Regex.Split(str, @"\s+");
                foreach (string value in arr) {
                    string v = value.Replace("&nbsp;", " ");
                    string[] asub = v.Split("|", 2);
                    string val = (string)default_value;
                    if (asub.Length > 1) {
                        val = asub[1];
                    }
                    result.Add(asub[0], val);
                }
            }
            return result;
        }

        /* <summary>
        * Merge hashes - copy all key-values from hash2 to hash1 with overwriting existing keys
        * </summary>
        * <param name="hash1"></param>
        * <param name="hash2"></param>
        * <remarks></remarks>
        */
        public static void mergeHash(ref Hashtable hash1, ref Hashtable hash2) {
            if (hash2 != null) {
                ArrayList keys = new ArrayList(hash2.Keys); // make static copy of hash2.keys, so even if hash2.keys changing (ex: hash1 is same as hash2) it will not affect the loop
                foreach (string key in keys) {
                    hash1[key] = hash2[key];
                }
            }
        }

        // leave just allowed chars in string - for routers: controller, action or for route ID
        public static string routeFixChars(string str) {
            return Regex.Replace(str, @"[^A-Za-z0-9_-]+", "");
        }
    }
}
