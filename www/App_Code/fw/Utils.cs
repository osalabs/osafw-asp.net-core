using Microsoft.AspNetCore.Http;
using System;
using System.Collections;
using System.Data.Common;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using System.Net;
using System.Security.Cryptography;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace osafw
{
    public class Utils
    {
        public const String OLEDB_PROVIDER = "Microsoft.ACE.OLEDB.12.0"; // used for import from CSV/Excel, change it to your provider if necessary

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

        // convert from array (IList) back to qw-string
        // spaces converted to "&nbsp;"
        public static String qwRevert(IList slist)
        {
            StringBuilder result = new StringBuilder();
            foreach (String el in slist)
            {
                result.Append(el.Replace(" ", "&nbsp;") + " ");
            }

            return result.ToString();
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
        public static Hashtable qh(string str, object default_value = null)
        {
            Hashtable result = new Hashtable();
            if (str != null && str != "")
            {
                string[] arr = Regex.Split(str, @"\s+");
                foreach (string value in arr)
                {
                    string v = value.Replace("&nbsp;", " ");
                    string[] avoid = v.Split("|", 2);
                    string val = (string)default_value;
                    if (avoid.Length > 1)
                    {
                        val = avoid[1];
                    }
                    result.Add(avoid[0], val);
                }
            }
            return result; ;
        }

        public static String qhRevert(IDictionary sh)
        {
            ArrayList result = new ArrayList();
            foreach (String key in sh.Keys)
            {
                result.Add(key.Replace(" ", "&nbsp;") + "|" + sh[key]);
            }
            return String.Join(" ", result.ToArray());
        }


        // remove elements from hash, leave only those which keys passed
        public static void hashFilter(Hashtable hash, String[] keys)
        {
            ArrayList all_keys = new ArrayList(keys);
            ArrayList to_remove = new ArrayList();
            foreach (String key in hash.Keys)
            {
                if (all_keys.IndexOf(key) < 0)
                {
                    to_remove.Add(key);
                }
            }
            // remove keys
            foreach (String key in to_remove)
            {
                hash.Remove(key);
            }
        }

        // leave just allowed chars in string - for routers: controller, action or for route ID
        public static String routeFixChars(String str)
        {
            return Regex.Replace(str, "[^A-Za-z0-9_-]+", "");
        }

        /* <summary>
        * Split string exactly into 2 voidstrings using regular expression
        * </summary>
        * <param name="re">string suitable for RegEx</param>
        * <param name="source">string to be splitted</param>
        * <param name="dest1">ByRef destination string 1</param>
        * <param name="dest2">ByRef destination string 2</param>
        * <remarks></remarks>
        */
        public static void split2(String re, String source, ref String dest1, ref String dest2)
        {
            dest1 = "";
            dest2 = "";
            String[] arr = Regex.Split(source, re);
            if (arr.Length > 0)
            {
                dest1 = arr[0];
            }
            if (arr.Length > 1)
            {
                dest2 = arr[1];
            }
        }

        // IN: email addresses delimited with ; space or newline
        // OUT: arraylist of email addresses
        public static ArrayList splitEmails(String emails)
        {
            ArrayList result = new ArrayList();
            String[] arr = Regex.Split(emails, "[; \n\r]+");
            foreach (String email in arr)
            {
                String _email = email.Trim();
                if (_email == "") continue;
                result.Add(_email);
            }
            return result;
        }

        public static String htmlescape(String str)
        {
            str = HttpUtility.HtmlEncode(str);
            // str = Regex.Replace(str, "\&", "&amp;");
            // str = Regex.Replace(str, "\$", "&#36;");
            return str;
        }

        public static String str2url(String str)
        {
            if (!Regex.IsMatch(str, @"^\w+://"))
            {
                str = "http://" + str;
            }
            return str;
        }

        public static String ConvertStreamToBase64(Stream fs)
        {
            BinaryReader BinRead = new BinaryReader(fs);
            Byte[] BinBytes = BinRead.ReadBytes((int)fs.Length);
            return Convert.ToBase64String(BinBytes);
            // Convert.ToBase64CharArray();
        }

        public static bool f2bool(Object AField)
        {
            bool result = false;
            if (AField == null) return false;
            Boolean.TryParse(AField.ToString(), out result);
            return result;
        }

        // TODO parse without Try/Catch
        public static Object f2date(Object AField)
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

        public static String sTrim(String str, int size)
        {
            if (str.Length > size) str = str.Substring(0, size) + "...";
            return str;
        }

        public static String getRandStr(int size)
        {
            StringBuilder result = new StringBuilder();
            String[] chars = qw("A B C D E F a b c d e f 0 1 2 3 4 5 6 7 8 9");

            Random _random = new Random();
            for (int i = 1; i < size; i++)
            {
                result.Append(chars[_random.Next(0, chars.Length - 1)]);
            }

            return result.ToString();
        }

        /* <summary>
        * helper for importing csv files. Example:
        *    Utils.importCSV(fw, AddressOf importer, "c:\import.csv")
        *    void importer(row as Hashtable)
        *       ...your custom import code
        *    End void
        * </summary>
        * <param name="fw">fw instance</param>
        * <param name="callback">callback to custom code, accept one row of fields(as Hashtable)</param>
        * <param name="filepath">.csv file name to import</param>
        */
        public static void importCSV(FW fw, Action<Hashtable> callback, String filepath, bool is_header = true)
        {
            String dir = Path.GetDirectoryName(filepath);
            String filename = Path.GetFileName(filepath);

            String ConnectionString = "Provider=" + OLEDB_PROVIDER + ";" +
                                      "Data Source=" + dir + ";" +
                                      "Extended Properties=\"Text;HDR=" + (is_header ? "Yes" : "No") + ";IMEX=1;FORMAT=Delimited\";";

            using (OleDbConnection cn = new OleDbConnection(ConnectionString))
            {
                cn.Open();

                String WorkSheetName = filename;
                // quote as table name
                WorkSheetName = WorkSheetName.Replace("[", "");
                WorkSheetName = WorkSheetName.Replace("]", "");

                String sql = "select * from [" + WorkSheetName + "]";
                OleDbCommand dbcomm = new OleDbCommand(sql, cn);
                DbDataReader dbread = dbcomm.ExecuteReader();

                while (dbread.Read())
                {
                    Hashtable row = new Hashtable();
                    for (int i = 0; i < dbread.FieldCount; i++)
                    {
                        String value = dbread[i].ToString();
                        String name = dbread.GetName(i).ToString();
                        row.Add(name, value);
                    }

                    // logger(h)
                    callback(row);
                }
            }
        }

        /* <summary>
        * helper for importing Excel files. Example:
        *    Utils.importExcel(fw, AddressOf importer, "c:\import.xlsx")
        *    void importer(sheet_name as String, rows as ArrayList)
        *       ...your custom import code
        *    End void
        * </summary>
        * <param name="fw">fw instance</param>
        * <param name="callback">callback to custom code, accept worksheet name and all rows(as ArrayList of Hashtables)</param>
        * <param name="filepath">.xlsx file name to import</param>
        * <param name="is_header"></param>
        * <returns></returns>
        */
        public static Hashtable importExcel(FW fw, Action<String, ArrayList> callback, String filepath, bool is_header = true)
        {
            Hashtable result = new Hashtable();
            Hashtable conf = new Hashtable();
            conf["type"] = "OLE";
            conf["connection_string"] = "Provider=" + OLEDB_PROVIDER + ";Data Source=" + filepath + ";Extended Properties=\"Excel 12.0 Xml;HDR=" + (is_header ? "Yes" : "No") + ";ReadOnly=True;IMEX=1\"";
            DB accdb = new DB(fw, conf);
            OleDbConnection conn = (OleDbConnection)accdb.connect();
            var schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);

            if (schema == null || schema.Rows.Count < 1)
            {
                throw new ApplicationException("No worksheets found in the Excel file");
            }

            Hashtable where = new Hashtable();
            for (int i = 0; i < schema.Rows.Count; i++)
            {
                String sheet_name_full = schema.Rows[i]["TABLE_NAME"].ToString();
                String sheet_name = sheet_name_full.Replace("\"", "");
                sheet_name = sheet_name.Replace("'", "");
                sheet_name = sheet_name.Substring(0, sheet_name.Length - 1);
                try
                {
                    ArrayList rows = accdb.array(sheet_name_full, where);
                    callback(sheet_name, rows);
                }
                catch (Exception ex)
                {
                    throw new ApplicationException("Error while reading data from [" + sheet_name + "] sheet: " + ex.Message);
                }
            }
            // close connection to release the file
            accdb.disconnect();
            return result;
        }

        public static String toCSVRow(Hashtable row, Array fields)
        {
            StringBuilder result = new StringBuilder();
            bool is_first = true;
            foreach (String fld in fields)
            {
                if (!is_first) result.Append(",");

                String str = Regex.Replace(row[fld] + "", "[\n\r]+", " ");
                str = str.Replace("\"", "\\\"");
                // check if string need to be quoted (if it contains " or ,)
                if (str.IndexOf("\"") > 0 || str.IndexOf(",") > 0)
                {
                    str = "\"" + str + "\"";
                }
                result.Append(str);
                is_first = false;
            }
            return result.ToString();
        }

        /* <summary>
        * standard function for exporting to csv
        * </summary>
        * <param name="csv_export_headers">CSV headers row, comma-separated format</param>
        * <param name="csv_export_fields">empty, * or Utils.qw format</param>
        * <param name="rows">DB array</param>
        * <returns></returns>
        */
        public static StringBuilder getCSVExport(String csv_export_headers, String csv_export_fields, ArrayList rows)
        {
            String headers_str = csv_export_headers;
            StringBuilder csv = new StringBuilder();
            String[] fields = null;
            if (csv_export_fields == "" || csv_export_fields == "*")
            {
                // just read field names from first row
                if (rows.Count > 0)
                {
                    fields = (rows[0] as Hashtable).Keys.Cast<String>().ToArray();
                    headers_str = String.Join(",", fields);
                }
            }
            else
            {
                fields = Utils.qw(csv_export_fields);
            }

            csv.Append(headers_str + "\n");
            foreach (Hashtable row in rows)
            {
                csv.Append(Utils.toCSVRow(row, fields) + "\n");
            }
            return csv;
        }

        public static async void writeCSVExport(HttpResponse response, String filename, String csv_export_headers, String csv_export_fields, ArrayList rows)
        {
            filename = filename.Replace("\"", "'"); // quote doublequotes

            response.Headers.Add("Content-type", "text/csv");
            response.Headers.Add("Content-Disposition", "attachment; filename=\"" + filename + "\"");

            await HttpResponseWritingExtensions.WriteAsync(response, Utils.getCSVExport(csv_export_headers, csv_export_fields, rows).ToString());
        }

        public static async void writeXLSExport(FW fw, String filename, String csv_export_headers, String csv_export_fields, ArrayList rows)
        {
            Hashtable ps = new Hashtable();
            ps["rows"] = rows;

            ArrayList headers = new ArrayList();
            foreach (String str in csv_export_headers.Split(","))
            {
                Hashtable h = new Hashtable();
                h["iname"] = str;
                headers.Add(h);
            }
            ps["headers"] = headers;

            String[] fields = Utils.qw(csv_export_fields);
            foreach (Hashtable row in rows)
            {
                ArrayList cell = new ArrayList();
                foreach (String f in fields)
                {
                    Hashtable h = new Hashtable();
                    h["value"] = row[f];
                    cell.Add(h);
                }
                row["cell"] = cell;
            }

            // parse and out document
            // TODO ConvUtils.parse_page_xls(fw, LCase(fw.cur_controller_path & "/index/export"), "xls.html", hf, "filename")

            ParsePage parser = new ParsePage(fw);
            // Dim tpl_dir = LCase(fw.cur_controller_path & "/index/export")
            String tpl_dir = "/common/list/export";
            String page = parser.parse_page(tpl_dir, "xls.html", ps);

            filename = filename.Replace("\"", "_");

            fw.resp.Headers.Add("Content-type", "application/vnd.ms-excel");
            fw.resp.Headers.Add("Content-Disposition", "attachment; filename=\"" + filename + "\"");
            await HttpResponseWritingExtensions.WriteAsync(fw.resp, page);
        }

        // Detect orientation and auto-rotate correctly
        public static bool rotateImage(System.Drawing.Image image)
        {
            bool result = false;
            var rot = RotateFlipType.RotateNoneFlipNone;
            PropertyItem[] props = image.PropertyItems;

            foreach (PropertyItem p in props)
            {
                if (p.Id == 274)
                {
                    switch (BitConverter.ToInt16(p.Value, 0))
                    {
                        case 1:
                            rot = RotateFlipType.RotateNoneFlipNone;
                            break;
                        case 3:
                            rot = RotateFlipType.Rotate180FlipNone;
                            break;
                        case 6:
                            rot = RotateFlipType.Rotate90FlipNone;
                            break;
                        case 8:
                            rot = RotateFlipType.Rotate270FlipNone;
                            break;
                    }
                }
            }

            if (rot != RotateFlipType.RotateNoneFlipNone)
            {
                image.RotateFlip(rot);
                result = true;
            }
            return result;
        }

        // resize image in from_file to w/h and save to to_file
        // (optional)w and h - mean max weight and max height (i.e. image will not be upsized if it's smaller than max w/h)
        // if no w/h passed - then no resizing occurs, just conversion (based on destination extension)
        // return false if no resize performed (if image already smaller than necessary). Note if to_file is not same as from_file - to_file will have a copy of the from_file
        public static bool resizeImage(string from_file, string to_file, int w = -1, int h = -1)
        {
            FileStream stream = new FileStream(from_file, FileMode.Open, FileAccess.Read);

            // Create new image.
            System.Drawing.Image image = System.Drawing.Image.FromStream(stream);

            // Detect orientation and auto-rotate correctly
            var is_rotated = rotateImage(image);

            // Calculate proportional max width and height.
            int oldWidth = image.Width;
            int oldHeight = image.Height;

            if (w == -1)
                w = oldWidth;
            if (h == -1)
                h = oldHeight;

            if (oldWidth / (double)w >= 1 | oldHeight / (double)h >= 1)
            {
            }
            else
            {
                // image already smaller no resize required - keep sizes same
                image.Dispose();
                stream.Close();
                if (to_file != from_file)
                    // but if destination file is different - make a copy
                    File.Copy(from_file, to_file);
                return false;
            }

            if (((double)oldWidth / (double)oldHeight) > ((double)w / (double)h))
            {
                double ratio = (double)w / (double)oldWidth;
                h = (int)(oldHeight * ratio);
            }
            else
            {
                double ratio = (double)h / (double)oldHeight;
                w = (int)(oldWidth * ratio);
            }

            // Create a new bitmap with the same resolution as the original image.
            Bitmap bitmap = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            bitmap.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            // Create a new graphic.
            Graphics gr = Graphics.FromImage(bitmap);
            gr.Clear(Color.White);
            gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
            gr.SmoothingMode = SmoothingMode.HighQuality;
            gr.PixelOffsetMode = PixelOffsetMode.HighQuality;
            gr.CompositingQuality = CompositingQuality.HighQuality;

            // Create a scaled image based on the original.
            gr.DrawImage(image, new Rectangle(0, 0, w, h), new Rectangle(0, 0, oldWidth, oldHeight), GraphicsUnit.Pixel);
            gr.Dispose();

            // Save the scaled image.
            string ext = UploadUtils.getUploadFileExt(to_file);
            ImageFormat out_format = image.RawFormat;
            EncoderParameters EncoderParameters = null/* TODO Change to default(_) if this is not a reference type */;
            ImageCodecInfo ImageCodecInfo = null/* TODO Change to default(_) if this is not a reference type */;

            if (ext == ".gif")
            {
                out_format = ImageFormat.Gif;
            }
            else if (ext == ".jpg")
            {
                out_format = ImageFormat.Jpeg;
                // set jpeg quality to 80
                ImageCodecInfo = GetEncoderInfo(out_format);
                System.Drawing.Imaging.Encoder encoder = System.Drawing.Imaging.Encoder.Quality;
                EncoderParameters = new EncoderParameters(1);
                EncoderParameters.Param[0] = new EncoderParameter(encoder, System.Convert.ToInt32(80L));
            }
            else if (ext == ".png")
            {
                out_format = ImageFormat.Png;
            }

            // close read stream before writing as to_file might be same as from_file
            image.Dispose();
            stream.Close();

            if (EncoderParameters == null)
            {
                bitmap.Save(to_file, out_format); // image.RawFormat
            }
            else
            {
                bitmap.Save(to_file, ImageCodecInfo, EncoderParameters);
            }
            bitmap.Dispose();

            // if( contentType == "image/gif" )
            // {
            // Using (thumbnail)
            // {
            // OctreeQuantizer quantizer = new OctreeQuantizer ( 255 , 8 ) ;
            // using ( Bitmap quantized = quantizer.Quantize ( bitmap ) )
            // {
            // Response.ContentType = "image/gif";
            // quantized.Save ( Response.OutputStream , ImageFormat.Gif ) ;
            // }
            // }
            // }

            return true;
        }

        private static ImageCodecInfo GetEncoderInfo(ImageFormat format)
        {
            int j = 0;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();

            j = 0;
            while (j < encoders.Length)
            {
                if (encoders[j].FormatID == format.Guid) return encoders[j];
                j += 1;
            }
            return null;

        } // GetEncoderInfo

        public static long fileSize(String filepath)
        {
            FileInfo fi = new FileInfo(filepath);
            return fi.Length;
        }

        // extract just file name (with ext) from file path
        public static String fileName(String filepath)
        {
            return System.IO.Path.GetFileName(filepath);
        }


        /* <summary>
        * Merge hashes - copy all key-values from hash2 to hash1 with overwriting existing keys
        * </summary>
        * <param name="hash1"></param>
        * <param name="hash2"></param>
        * <remarks></remarks>
        */
        public static void mergeHash(ref Hashtable hash1, ref Hashtable hash2)
        {
            if (hash2 != null)
            {
                ArrayList keys = new ArrayList(hash2.Keys); // make static copy of hash2.keys, so even if hash2.keys changing (ex: hash1 is same as hash2) it will not affect the loop
                foreach (string key in keys)
                {
                    hash1[key] = hash2[key];
                }
            }
        }

        // deep hash merge, i.e. if hash2 contains values that is hash value - go in it and copy such values to hash2 at same place accordingly
        // recursive
        public static void mergeHashDeep(ref Hashtable hash1, ref Hashtable hash2)
        {
            if (hash2 != null)
            {
                ArrayList keys = new ArrayList(hash2.Keys);
                foreach (string key in keys)
                {
                    if (hash2[key] is Hashtable)
                    {
                        if (!(hash1[key] is Hashtable))
                            hash1[key] = new Hashtable();
                        Hashtable _hash1 = (Hashtable)hash1[key];
                        Hashtable _hash2 = (Hashtable)hash2[key];
                        mergeHashDeep(ref _hash1, ref _hash2);
                    }
                    else
                        hash1[key] = hash2[key];
                }
            }
        }

        public static String bytes2str(long b)
        {
            String result = b.ToString();

            if (b < 1024)
            {
                result += " B";
            }
            else if (b < 1048576)
            {
                result = (Math.Floor((double)b / 1024 * 100) / 100) + " KiB";
            }
            else if (b < 1073741824)
            {
                result = (Math.Floor((double)b / 1048576 * 100) / 100) + " MiB";
            }
            else
            {
                result = (Math.Floor((double)b / 1073741824 * 100) / 100) + " GiB";
            }
            return result;
        }

        /* <summary>
        * convert data structure to JSON string
        * </summary>
        * <param name="data">any data like single value, arraylist, hashtable, etc..</param>
        * <returns></returns>
        */
        public static string jsonEncode(object data, bool is_pretty = false)
        {
            JsonSerializerOptions options = new();
            if (is_pretty) options.WriteIndented = true;
            return JsonSerializer.Serialize(data, data.GetType(), options); //TODO MIGRATE test if GetType() is enough or we need explicitly have Hashtable/ArrayList as overloads
        }

        //overload alias for jsonDecode(string)
        public static Object jsonDecode(object str)
        {
            return jsonDecode((string)str);
        }

        /* <summary>
        * convert JSON string into data structure
        * </summary>
        * <param name="str">JSON string</param>
        * <returns>single value, arraylist, hashtable, etc.. or Nothing if cannot be converted</returns>
        * <remarks>Note, JavaScriptSerializer.MaxJsonLength is about 4MB unicode</remarks>
        */
        public static Object jsonDecode(String str)
        {
            object result;
            try
            {
                //detect if json contains array or hashtable or something else
                if (Regex.IsMatch(str, @"^\s*{"))
                {
                    result = JsonSerializer.Deserialize(str, typeof(Hashtable));
                }
                else if (Regex.IsMatch(str, @"^\s*\["))
                {
                    result = JsonSerializer.Deserialize(str, typeof(ArrayList));
                }
                else
                {
                    result = JsonSerializer.Deserialize(str, typeof(Object));
                }
                result = cast2std(result);
            }
            catch (Exception ex)
            {
                // if error during conversion - return null
                FW.Current.logger(ex.Message);
                result = null;
            }

            return result;
        }

        /* <summary>
        * depp convert data structure to standard framework's Hashtable/Arraylist
        * </summary>
        * <param name="data"></param>
        * <remarks>RECURSIVE!</remarks>
        */
        public static object cast2std(object data)
        {
            object result = data;

            if (result is IDictionary dictionary)
            {
                // convert dictionary to Hashtable
                Hashtable result2 = new(); // because we can't iterate hashtable and change it
                foreach (string key in dictionary.Keys)
                {
                    Hashtable _data = (Hashtable)dictionary;
                    result2[key] = cast2std(_data[key]);
                }
                result = result2;
            }
            else if (result is IList list)
            {
                // convert arrays to ArrayList
                result = new ArrayList(list);
                for (int i = 0; i < list.Count; i++)
                {
                    ((ArrayList)result)[i] = cast2std(((ArrayList)result)[i]);
                }
            }
            else if (result is System.Text.Json.JsonElement el)
            {
                if (el.ValueKind == JsonValueKind.Number)
                    result = el.GetInt32();
                else if (el.ValueKind == JsonValueKind.String)
                    result = el.GetString();
                else if (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False)
                    result = el.GetBoolean();
                else if (el.ValueKind == JsonValueKind.Null)
                    result = null;
            }

            return result;
        }

        // serialize using BinaryFormatter.Serialize
        // return as base64 string
        public static String serialize(object data)
        {
            return jsonEncode(data);

            //binary fomatter is not secure TODO MIGRATE cleanup
            //var xstream = new System.IO.MemoryStream(); ;
            //var xformatter = new BinaryFormatter();

            //xformatter.Serialize(xstream, data);

            //return Convert.ToBase64String(xstream.ToArray());
        }

        // deserialize base64 string serialized with Utils.serialize
        // return object or Nothing (if error)
        public static object deserialize(String str)
        {
            return jsonDecode(str);
            //binary fomatter is not secure TODO MIGRATE cleanup
            //object data;
            //try
            //{
            //    MemoryStream xstream = new MemoryStream(Convert.FromBase64String(str));
            //    var xformatter = new BinaryFormatter();
            //    data = xformatter.Deserialize(xstream);
            //}
            //catch (Exception ex)
            //{
            //    data = null;
            //}
            //return data;
        }

        // return Hashtable keys as an array
        public static String[] hashKeys(Hashtable h)
        {
            return h.Keys.Cast<String>().ToArray();
        }

        // capitalize first word in string
        // if mode='all' - capitalize all words
        // EXAMPLE: mode="" : sample string => Sample string
        // mode="all" : sample STRING => Sample String
        public static String capitalize(String str, String mode = "")
        {
            if (mode == "all")
            {
                str = str.ToLower();
                str = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str);
            }
            else
            {
                str = str.Substring(0, 1).ToUpper() + str.Substring(1);
            }

            return str;
        }

        // repeat string num times
        public static String strRepeat(String str, int num)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < num; i++)
            {
                result.Append(str);
            }
            return result.ToString();
        }

        // return unique file name in form UUID (without extension)
        public static String uuid()
        {
            return System.Guid.NewGuid().ToString();
        }

        // return path to tmp filename WITHOUT extension
        public static String getTmpFilename(String prefix = "osafw")
        {
            return Path.GetTempPath() + "\\" + prefix + Utils.uuid();
        }

        // scan tmp directory, find all tmp files created by website and delete older than 1 hour
        public static void cleanupTmpFiles(String prefix = "osafw")
        {
            String[] files = Directory.GetFiles(Path.GetTempPath(), prefix + "*");
            foreach (String file in files)
            {
                FileInfo fi = new FileInfo(file);
                TimeSpan ts = DateTime.Now - fi.CreationTime;
                if (ts.TotalMinutes > 60)
                {
                    fi.Delete();
                }
            }
        }

        // return md5 hash (hexadecimals) for a string
        public static String md5(String str)
        {
            // convert string to bytes
            UTF8Encoding ustr = new UTF8Encoding();
            Byte[] bstr = ustr.GetBytes(str);

            MD5 md5hasher = MD5CryptoServiceProvider.Create();
            Byte[] bhash = md5hasher.ComputeHash(bstr);

            // convert hash value to hex string
            StringBuilder sb = new StringBuilder();
            foreach (Byte one_byte in bhash)
            {
                sb.Append(one_byte.ToString("x2").ToUpper());
            }

            return sb.ToString().ToLower();
        }

        // 1 => 01
        // 10 => 10
        public static String toXX(String str)
        {
            if (str.Length < 2) str = "0" + str;
            return str;
        }

        public static String num2ordinal(int num)
        {
            if (num <= 0) return num.ToString();

            switch (num % 100)
            {
                case 11:
                case 12:
                case 13:
                    return num + "th";
            }

            switch (num % 10)
            {
                case 1:
                    return num + "st";
                case 2:
                    return num + "nd";
                case 3:
                    return num + "rd";
                default:
                    return num + "th";
            }
        }

        // truncate  - This truncates a variable to a character length, the default is 80.
        // trchar    - As an optional second parameter, you can specify a string of text to display at the end if the variable was truncated.
        // The characters in the string are included with the original truncation length.
        // trword    - 0/1. By default, truncate will attempt to cut off at a word boundary =1.
        // trend     - 0/1. If you want to cut off at the exact character length, pass the optional third parameter of 1.
        //<~tag truncate="80" trchar="..." trword="1" trend="1">
        public static string str2truncate(String str, Hashtable hattrs)
        {
            int trlen = 80;
            String trchar = "...";
            int trword = 1;
            int trend = 1;  // if trend=0 trword - ignored

            if (hattrs["truncate"].ToString().Length > 0)
            {
                int trlen1 = f2int(hattrs["truncate"]);
                if (trlen1 > 0) trlen = trlen1;
            }
            if (hattrs.ContainsKey("trchar")) trchar = (String)hattrs["trchar"];
            if (hattrs.ContainsKey("trend")) trend = (int)hattrs["trend"];
            if (hattrs.ContainsKey("trword")) trword = (int)hattrs["trword"];

            int orig_len = str.Length;
            if (orig_len < trlen) return str; // no need truncate

            if (trend == 1)
            {
                if (trword == 1)
                {
                    str = Regex.Replace(str, @"^(.{" + trlen + @",}?)[\n \t\.\,\!\?]+(.*)$", "$1", RegexOptions.Singleline);
                    if (str.Length < orig_len) str += trchar;
                }
                else
                {
                    str = str.Substring(0, trlen) + trchar;
                }
            }
            else
            {
                str = str.Substring(0, trlen / 2) + trchar + str.Substring(trlen / 2 + 1);
            }
            return str;
        }

        // IN: orderby string for default asc sorting, ex: "id", "id desc", "prio desc, id"
        // OUT: orderby or inversed orderby (if sortdir="desc"), ex: "id desc", "id asc", "prio asc, id desc"
        public static String orderbyApplySortdir(String orderby, String sortdir)
        {
            String result = orderby;

            if (sortdir == "desc")
            {
                // TODO - move this to fw utils
                ArrayList order_fields = new ArrayList();
                foreach (String fld in orderby.Split(","))
                {
                    String _fld = fld;
                    // if fld contains asc or desc - change to opposite
                    if (_fld.IndexOf(" asc") >= 0)
                    {
                        _fld = _fld.Replace(" asc", " desc");
                    }
                    else if (_fld.IndexOf("desc") >= 0)
                    {
                        _fld = _fld.Replace(" desc", " asc");
                    }
                    else
                    {
                        // if no asc/desc - just add desc at the end
                        _fld += " desc";
                    }
                    order_fields.Add(_fld);
                }
                // result = String.Join(", ", order_fields.ToArray(GetType(String))) // net 2
                result = String.Join(", ", order_fields.ToArray());  // net 4
            }

            return result;
        }

        public static String html2text(String str)
        {
            str = Regex.Replace(str, @"\n+", " ");
            str = Regex.Replace(str, @"<br\s*\/?>", "\n");
            str = Regex.Replace(str, @"(?:<[^>]*>)+", " ");
            return str;
        }

        // sel_ids - comma-separated ids
        // value:
        //      nothing - use id value from input
        //      "123..."  - use index (by order)
        //      "other value" - use this value
        // return hash: id => id
        public static Hashtable commastr2hash(String sel_ids, String value = null)
        {
            Hashtable result = new Hashtable();
            ArrayList ids = new ArrayList(sel_ids.Split(","));
            for (int i = 0; i < ids.Count; i++)
            {
                String v = (String)ids[i];
                if (value == null)
                {
                    result[v] = v;
                }
                else if (value == "123...")
                {
                    result[v] = i;
                }
                else
                {
                    result[v] = value;
                }
            }
            return result;
        }

        // comma-delimited str to newline-delimited str
        public static String commastr2nlstr(String str)
        {
            return str.Replace(",", "\r\n");
        }

        // newline-delimited str to comma-delimited str
        static string nlstr2commastr(String str)
        {
            return Regex.Replace(str, @"[\n\r]+", ",");
        }

        /* <summary>
        * for each row in rows add keys/values to this row (by ref)
        * </summary>
        * <param name="rows">db array</param>
        * <param name="fields">keys/values to add</param>
        */
        public static void arrayInject(ArrayList rows, Hashtable fields)
        {
            foreach (Hashtable row in rows)
            {
                // array merge
                foreach (var key in fields.Keys)
                {
                    row[key] = fields[key];
                }
            }
        }

        /* <summary>
        *  escapes/encodes string so it can be passed as part of the url
        *  </summary>
        *  <param name="str"></param>
        *  <returns></returns>
        */
        public static String urlescape(String str)
        {
            return HttpUtility.UrlEncode(str);
        }

        // sent multipart/form-data POST request to remote URL with files (key=fieldname, value=filepath) and formFields
        public static String UploadFilesToRemoteUrl(
            String url,
            Hashtable files,
            System.Collections.Specialized.NameValueCollection formFields = null,
            System.Security.Cryptography.X509Certificates.X509Certificate2 cert = null)
        {
            String boundary = "----------------------------" + DateTime.Now.Ticks.ToString("x");
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentType = "multipart/form-data; boundary=" + boundary;
            request.Method = "POST";
            request.KeepAlive = true;
            if (cert != null) request.ClientCertificates.Add(cert);

            var memStream = new MemoryStream();
            var boundarybytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");
            var endBoundaryBytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--");

            // String formdataTemplate = "\r\n--" & boundary + "\r\nContent-Disposition: form-data; name=\"{0}\";\r\n\r\n{1}";
            String formdataTemplate = "--" + boundary + "\r\n" + "Content-Disposition: form-data; name=\"{0}\";\r\n{1}\r\n";
            if (formFields != null)
            {
                foreach (String key in formFields.Keys)
                {
                    String formitem = String.Format(formdataTemplate, key, formFields[key]);
                    if (memStream.Length > 0)
                    {
                        formitem = "\r\n" + formitem; // add crlf before the string only for second and further lines
                    }

                    Byte[] formitembytes = Encoding.UTF8.GetBytes(formitem);
                    memStream.Write(formitembytes, 0, formitembytes.Length);
                }
            }

            String headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
            foreach (String fileField in files.Keys)
            {
                memStream.Write(boundarybytes, 0, boundarybytes.Length);

                // mime (TODO use System.Web.MimeMapping.GetMimeMapping() for .net 4.5+)
                String mimeType = "application/octet-stream";
                if (Path.GetExtension((String)files[fileField]) == ".xml") mimeType = "text/xml";

                String header = String.Format(headerTemplate, fileField, System.IO.Path.GetFileName((String)files[fileField]), mimeType);
                var headerbytes = Encoding.UTF8.GetBytes(header);
                memStream.Write(headerbytes, 0, headerbytes.Length);

                using (var fileStream = new FileStream((String)files[fileField], FileMode.Open, FileAccess.Read))
                {
                    Byte[] buffer = new Byte[1023];
                    int bytesRead = fileStream.Read(buffer, 0, buffer.Length);
                    while (bytesRead != 0)
                    {
                        memStream.Write(buffer, 0, bytesRead);
                        bytesRead = fileStream.Read(buffer, 0, buffer.Length);
                    }
                }
            }

            memStream.Write(endBoundaryBytes, 0, endBoundaryBytes.Length);
            // Diagnostics.Debug.WriteLine("***")
            // Diagnostics.Debug.WriteLine(Encoding.ASCII.GetString(memStream.ToArray()))
            // Diagnostics.Debug.WriteLine("***")

            request.ContentLength = memStream.Length;
            using (var requestStream = request.GetRequestStream())
            {
                memStream.Position = 0;
                Byte[] tempBuffer = new Byte[memStream.Length - 1];
                memStream.Read(tempBuffer, 0, tempBuffer.Length);
                memStream.Close();
                requestStream.Write(tempBuffer, 0, tempBuffer.Length);
            }

            using (var response = request.GetResponse())
            {
                var stream2 = response.GetResponseStream();
                var reader2 = new StreamReader(stream2);
                return reader2.ReadToEnd();
            }
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


        // convert some system name to human-friendly name'
        // "system_name_id" => "System Name ID"
        public static String name2human(String str)
        {
            String str_lc = str.ToLower();
            if (str_lc == "icode") return "Code";
            if (str_lc == "iname") return "Name";
            if (str_lc == "idesc") return "Description";
            if (str_lc == "id") return "ID";
            if (str_lc == "fname") return "First Name";
            if (str_lc == "lname") return "Last Name";
            if (str_lc == "midname") return "Middle Name";

            String result = str;
            result = Regex.Replace(result, @"^tbl|dbo", "", RegexOptions.IgnoreCase); // remove tbl prefix if any
            result = Regex.Replace(result, @"_+", " "); // underscores to spaces
            result = Regex.Replace(result, @"([a-z ])([A-Z]+)", "$1 $2"); // split CamelCase words
            result = Regex.Replace(result, @" +", " "); // deduplicate spaces
            result = Utils.capitalize(result, "all"); // Title Case

            if (Regex.IsMatch(result, "\bid\b", RegexOptions.IgnoreCase))
            {
                // if contains id/ID - remove it and make singular
                result = Regex.Replace(result, @"\bid\b", "", RegexOptions.IgnoreCase);
                result = Regex.Replace(result, @"(?:es|s)\s*$", "", RegexOptions.IgnoreCase); // remove -es or -s at the end
            }

            result = result.Trim();
            return result;
        }

        // convert c/snake style name to CamelCase
        // system_name => SystemName
        public static String nameCamelCase(String str)
        {
            String result = str;
            result = Regex.Replace(result, @"\W+", " "); // non-alphanum chars to spaces
            result = Utils.capitalize(result);
            result = Regex.Replace(result, " +", ""); // remove spaces
            return str;
        }
    }
}
