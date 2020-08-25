using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace osafw_asp.net_core.fw
{
    public class Utils
    {
        // convert "space" delimited string to an array
        // WARN! replaces all "&nbsp;" to spaces (after convert)
        public static String[] qw(String str)
        {
            String[] arr = str.Trim().Split(" ");

            foreach (int i in Enumerable.Range(arr.GetLowerBound(0), arr.GetLowerBound(0)))
            {
                if (arr[i] == null) arr[i] = "";
                arr[i] = arr[i].Replace("&nbsp;", " ");
            }

            return arr;
        }
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
            return result; ;
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


        public static bool f2bool(Object AField)
        {
            bool result = false;
            if (AField == null) return false;
            Boolean.TryParse(AField.ToString(), out result);
            return result;
        }

        // TODO parse without Try/Catch
        public static Object f2date(String AField)
        {
            Object result = null;
            try
            {
                if (AField == null || AField == "Null" || AField == "")
                {
                    result = null;
                }
                else
                {
                    result = Convert.ToDateTime(AField.ToString().Trim());
                }
            }
            catch (Exception ex)
            {
                result = null;
            }
            return result;
        }


        public static bool isDate(Object AField)
        {
            Object result = f2date(AField.ToString());
            return result != null;
        }

        // guarantee to return string (if cannot convert to string - just return empty string)
        public static String f2str(Object AField)
        {
            if (AField == null) return "";
            String result = Convert.ToString(AField);
            return result;
        }

        public static int f2int(Object AField)
        {
            if (AField == null) return 0;
            int result = 0;
            Int32.TryParse(AField.ToString(), out result);
            return result;
        }

        // convert to double, optionally throw error
        public static double f2float(Object AField, bool is_error = false)
        {
            double result = 0.0;
            if (AField == null || !Double.TryParse(AField.ToString(), out result) && is_error)
            {
                throw new FormatException();
            }
            return result;
        }

        // just return false if input cannot be converted to float
        public static bool isFloat(Object AField)
        {
            double result = 0.0;
            return Double.TryParse(AField.ToString(), out result);
        }

        // convert/normalize external table/field name to fw standard name
        // "SomeCrazy/Name" => "some_crazy_name"
        public static String name2fw(String str)
        {
            String result = str;
            result = Regex.Replace(result, @"^tbl|dbo", "", RegexOptions.IgnoreCase); // remove tbl,dbo prefixes if any
            result = Regex.Replace(result, @"([A-Z]+)", "_$1"); // split CamelCase to underscore, but keep abbrs together ZIP/Code -> zip_code

            result = Regex.Replace(result, @"\W+", "_"); // replace all non-alphanum to underscore
            result = Regex.Replace(result, @"_+", "_"); // deduplicate underscore
            result = Regex.Replace(result, @"^_+|_+$", ""); // remove first and last _ if any
            result = result.ToLower(); // and finally to lowercase
            result = result.Trim();
            return result; ;
        }

        /// <summary>
        /// standard function for exporting to csv
        /// </summary>
        /// <param name="csv_export_headers">CSV headers row, comma-separated format</param>
        /// <param name="csv_export_fields">empty, * or Utils.qw format</param>
        /// <param name="rows">DB array</param>
        /// <returns></returns>
        public static StringBuilder getCSVExport(String csv_export_headers, String csv_export_fields, ArrayList rows)
        {
            String headers_str = csv_export_headers;
            StringBuilder csv = new StringBuilder();
            /*string[] fields = null;
            if (String.IsNullOrEmpty(csv_export_fields) || String.IsNullOrEmpty(csv_export_fields))
            {
                //just read field names from first row
                if (rows.Count > 0)
                {
                    fields = new ArrayList((rows[0] as Hashtable).Keys).ToArray();
                    headers_str = String.Join(",", fields);
                }
            }
            else
            {
                fields = Utils.qw(csv_export_fields);
            }

            csv.Append(headers_str & vbLf);
            foreach (Hashtable row in rows)
            {
                csv.Append(Utils.toCSVRow(row, fields) & vbLf);
            }*/
            return csv;
        }

        // return unique file name in form UUID (without extension)
        public static String uuid()
        {
            return System.Guid.NewGuid().ToString();
        }
    }
}
