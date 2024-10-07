//disable warning that some imaging functions are only available on Windows
#pragma warning disable CA1416

using Microsoft.AspNetCore.Http;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.OleDb;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace osafw;

public class Utils
{
    public const string OLEDB_PROVIDER = "Microsoft.ACE.OLEDB.12.0"; // used for import from CSV/Excel, change it to your provider if necessary

    public const string TMP_PREFIX = "osafw"; // prefix for temp directory where framework stores temporary files

    public const string MIME_MAP = "doc|application/msword docx|application/msword xls|application/vnd.ms-excel xlsx|application/vnd.ms-excel ppt|application/vnd.ms-powerpoint pptx|application/vnd.ms-powerpoint csv|text/csv pdf|application/pdf html|text/html zip|application/x-zip-compressed jpg|image/jpeg jpeg|image/jpeg gif|image/gif png|image/png wmv|video/x-ms-wmv avi|video/x-msvideo mp4|video/mp4";

    // convert "space" delimited string to an array
    // WARN! replaces all "&nbsp;" to spaces (after convert)
    public static string[] qw(string str)
    {
        if (String.IsNullOrEmpty(str)) return [];

        string[] arr = str.Trim().Split(" ");

        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] == null) arr[i] = "";
            arr[i] = arr[i].Replace("&nbsp;", " ");
        }

        return arr;
    }

    // convert from array (IList) back to qw-string
    // spaces converted to "&nbsp;"
    public static string qwRevert(IList slist)
    {
        StringBuilder result = new();
        foreach (string el in slist)
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
        Hashtable result = new();
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

    public static string qhRevert(IDictionary sh)
    {
        ArrayList result = new();
        foreach (string key in sh.Keys)
        {
            result.Add(key.Replace(" ", "&nbsp;") + "|" + sh[key]);
        }
        return string.Join(" ", result.ToArray());
    }


    // remove elements from hash, leave only those which keys passed
    public static void hashFilter(Hashtable hash, string[] keys)
    {
        ArrayList all_keys = new(keys);
        ArrayList to_remove = new();
        foreach (string key in hash.Keys)
        {
            if (all_keys.IndexOf(key) < 0)
            {
                to_remove.Add(key);
            }
        }
        // remove keys
        foreach (string key in to_remove)
        {
            hash.Remove(key);
        }
    }

    // leave just allowed chars in string - for routers: prefix part, controller, action or for route ID
    public static string routeFixChars(string str)
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
    public static void split2(string re, string source, ref string dest1, ref string dest2)
    {
        dest1 = "";
        dest2 = "";
        string[] arr = Regex.Split(source, re);
        if (arr.Length > 0)
        {
            dest1 = arr[0];
        }
        if (arr.Length > 1)
        {
            dest2 = arr[1];
        }
    }

    // IN: email addresses delimited with ,; space or newline
    // OUT: arraylist of email addresses
    public static ArrayList splitEmails(string emails)
    {
        ArrayList result = new();
        string[] arr = Regex.Split(emails, @"[,; \n\r]+");
        foreach (string email in arr)
        {
            string _email = email.Trim();
            if (_email == "") continue;
            result.Add(_email);
        }
        return result;
    }

    public static string htmlescape(string str)
    {
        str = HttpUtility.HtmlEncode(str);
        // str = Regex.Replace(str, "\&", "&amp;");
        // str = Regex.Replace(str, "\$", "&#36;");
        return str;
    }

    public static string str2url(string str)
    {
        if (!Regex.IsMatch(str, @"^\w+://"))
        {
            str = "http://" + str;
        }
        return str;
    }

    public static string base64encode(string str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        return Convert.ToBase64String(bytes);
    }

    public static string base64decode(string str64)
    {
        byte[] bytes = Convert.FromBase64String(str64);
        return Encoding.UTF8.GetString(bytes);
    }


    public static string streamToBase64(Stream fs)
    {
        BinaryReader BinRead = new(fs);
        byte[] BinBytes = BinRead.ReadBytes((int)fs.Length);
        return Convert.ToBase64String(BinBytes);
        // Convert.ToBase64CharArray();
    }

    #region toBool, toInt, toStr... isDate, isEmpty functions
    /// <summary>
    /// convert anything to bool, in case of error return false:
    ///   null - false
    ///   collections - true if not empty
    ///   non-zero number - true
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static bool toBool(object o)
    {
        if (o == null) return false;
        if (o is bool b) return b;
        if (o is ICollection ic) return ic.Count > 0; //for collections return true if not empty
        if (toFloat(o) != 0) return true; //non-zero number is true
        if (bool.TryParse(o.ToString(), out bool result))
            return result;

        return false;

    }

    [Obsolete("This method is deprecated, use toBool instead.")]
    public static bool f2bool(object o)
    {
        return toBool(o);
    }

    /// <summary>
    /// convert anything to DateTime, in case of error return DateTime.MinValue:
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static DateTime toDate(object o)
    {
        if (o == null) return DateTime.MinValue;
        if (o is DateTime dt) return dt;
        if (DateTime.TryParse(o.ToString(), out DateTime result))
            return result;

        return DateTime.MinValue;
    }

    [Obsolete("This method is deprecated, use toDate instead.")]
    public static DateTime? f2date(object field)
    {
        if (field is DateTime dateTimeValue)
            return dateTimeValue;

        if (DateTime.TryParse(field?.ToString().Trim(), out DateTime parsedDate))
            return parsedDate;

        return null;
    }

    /// <summary>
    /// return true if field is date
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static bool isDate(object o)
    {
        return toDate(o) != DateTime.MinValue;
    }

    /// <summary>
    /// convert anything to string (if cannot convert to string - just return empty string)
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static string toStr(object o)
    {
        if (o == null) return "";
        return o.ToString();
    }

    [Obsolete("This method is deprecated, use toStr instead.")]
    public static string f2str(object field)
    {
        return toStr(field);
    }

    /// <summary>
    /// convert anything to int, in case of error return 0:
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static int toInt(object o)
    {
        if (o == null) return 0;
        if (o is int i) return i;
        if (int.TryParse(o.ToString(), out int result))
            return result;

        return 0;
    }

    [Obsolete("This method is deprecated, use toInt instead.")]
    public static int f2int(object AField)
    {
        return toInt(AField);
    }

    public static long toLong(object o)
    {
        if (o == null) return 0;
        if (o is long l) return l;
        if (long.TryParse(o.ToString(), out long result))
            return result;

        return 0;
    }

    [Obsolete("This method is deprecated, use toLong instead.")]
    public static long f2long(object AField)
    {
        return toLong(AField);
    }

    /// <summary>
    /// convert anything to decimal, in case of error return 0:
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static decimal toDecimal(object o)
    {
        if (o == null) return decimal.Zero;
        if (o is decimal d) return d;
        if (decimal.TryParse(o.ToString(), out decimal result))
            return result;

        return decimal.Zero;
    }

    [Obsolete("This method is deprecated, use toDecimal instead.")]
    public static decimal f2decimal(object AField)
    {
        return toDecimal(AField);
    }

    /// <summary>
    /// convert anything to single, in case of error return 0:
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static Single toSingle(object o)
    {
        if (o == null) return 0f;
        if (o is Single s) return s;
        if (Single.TryParse(o.ToString(), out Single result))
            return result;

        return 0f;
    }

    [Obsolete("This method is deprecated, use toSingle instead.")]
    public static Single f2single(object AField)
    {
        return toSingle(AField);
    }

    /// <summary>
    /// convert anything to double, in case of error return 0:
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static double toFloat(object o)
    {
        if (o == null) return 0.0;
        if (o is double d) return d;
        if (double.TryParse(o.ToString(), out double result))
            return result;

        return 0.0;
    }

    // convert to double, optionally throw error
    [Obsolete("This method is deprecated, use toFloat instead.")]
    public static double f2float(object AField, bool is_error = false)
    {
        if (AField == null && !is_error) return 0.0;
        if (AField is double d) return d;
        if ((AField == null || !double.TryParse(AField.ToString(), out double result) && is_error))
            throw new FormatException();
        else
            return result;
    }

    /// <summary>
    /// just return false if input cannot be converted to float
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static bool isFloat(object o)
    {
        return o != null && double.TryParse(o.ToString(), out double _);
    }

    /// <summary>
    /// just return false if input cannot be converted to int
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static bool isInt(object o)
    {
        return o != null && int.TryParse(o.ToString(), out int _);
    }

    /// <summary>
    /// just return false if input cannot be converted to long
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static bool isLong(object o)
    {
        return o != null && long.TryParse(o.ToString(), out long _);
    }

    /// <summary>
    /// check that object is empty:
    /// - null object
    /// - or for strings it's trimmed zero-length string
    /// - or for numbers it's zero
    /// - or for bool it's false
    /// - or for collections - no elements
    /// Example:
    /// instead of `string.IsNullOrEmpty((string)itemdb["iname"])`
    /// use `isEmpty(itemdb["iname"])`
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static bool isEmpty(object o)
    {
        if (o == null) return true;
        if (o is string s) return s.Trim() == "";
        if (o is int i) return i == 0;
        if (o is long l) return l == 0;
        if (o is double d) return d == 0;
        if (o is bool b) return !b;
        if (o is ICollection col) return col.Count == 0;
        return false;
    }

    #endregion

    public static string sTrim(string str, int size)
    {
        if (str.Length > size) str = str.Substring(0, size) + "...";
        return str;
    }

    public static string getRandStr(int size)
    {
        StringBuilder result = new();
        string[] chars = qw("A B C D E F a b c d e f 0 1 2 3 4 5 6 7 8 9");

        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] data = new byte[size];
            rng.GetBytes(data);
            for (int i = 0; i < size; i++)
            {
                result.Append(chars[data[i] % chars.Length]);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// return content-type mime string by file extension, default is "application/octet-stream"
    /// </summary>
    /// <param name="ext">extension - doc, .jpg, ... (dot is optional)</param>
    /// <returns></returns>
    public static string ext2mime(string ext)
    {
        Hashtable mime_map = qh(MIME_MAP);
        ext = ext.ToLower(); //to lower
        //remove first dot if any
        if (ext.StartsWith('.'))
            ext = ext[1..];

        return (string)mime_map[ext] ?? "application/octet-stream";
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
    [SupportedOSPlatform("windows")]
    public static void importCSV(FW fw, Action<Hashtable> callback, string filepath, bool is_header = true)
    {
        string dir = Path.GetDirectoryName(filepath);
        string filename = Path.GetFileName(filepath);

        string ConnectionString = "Provider=" + OLEDB_PROVIDER + ";" +
                                  "Data Source=" + dir + ";" +
                                  "Extended Properties=\"Text;HDR=" + (is_header ? "Yes" : "No") + ";IMEX=1;FORMAT=Delimited\";";

        using (OleDbConnection cn = new(ConnectionString))
        {
            cn.Open();

            string WorkSheetName = filename;
            // quote as table name
            WorkSheetName = WorkSheetName.Replace("[", "");
            WorkSheetName = WorkSheetName.Replace("]", "");

            string sql = "select * from [" + WorkSheetName + "]";
            OleDbCommand dbcomm = new(sql, cn);
            DbDataReader dbread = dbcomm.ExecuteReader();

            while (dbread.Read())
            {
                Hashtable row = new();
                for (int i = 0; i < dbread.FieldCount; i++)
                {
                    string value = dbread[i].ToString();
                    string name = dbread.GetName(i).ToString();
                    row.Add(name, value);
                }

                // logger(h)
                callback(row);
            }

            dbread.Close();
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
    * <param name="callback">callback to custom code, accept worksheet name and all rows(as ArrayList of Hashtables). Returns bool - to continue after first page or break.</param>
    * <param name="filepath">.xlsx file name to import</param>
    * <param name="is_header"></param>
    * <returns></returns>
    */
    [SupportedOSPlatform("windows")]
    public static Hashtable importExcel(FW fw, Func<string, ArrayList, bool> callback, string filepath, bool is_header = true)
    {
        Hashtable result = new();
        Hashtable conf = new();
        conf["type"] = "OLE";
        conf["connection_string"] = "Provider=" + OLEDB_PROVIDER + ";Data Source=" + filepath + ";Extended Properties=\"Excel 12.0 Xml;HDR=" + (is_header ? "Yes" : "No") + ";ReadOnly=True;IMEX=1\"";
        DB accdb = new(fw, conf);
        OleDbConnection conn = (OleDbConnection)accdb.connect();
        var schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);

        if (schema == null || schema.Rows.Count < 1)
        {
            throw new ApplicationException("No worksheets found in the Excel file");
        }

        Hashtable where = new();
        for (int i = 0; i < schema.Rows.Count; i++)
        {
            string sheet_name_full = schema.Rows[i]["TABLE_NAME"].ToString();
            string sheet_name = sheet_name_full.Replace("\"", "");
            sheet_name = sheet_name.Replace("'", "");
            sheet_name = sheet_name.Substring(0, sheet_name.Length - 1);
            try
            {
                ArrayList rows = accdb.array(sheet_name_full, where);
                if (!callback(sheet_name, rows))
                {
                    break;
                }

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

    public static string toCSVRow(Hashtable row, Array fields)
    {
        StringBuilder result = new();
        bool is_first = true;
        foreach (string fld in fields)
        {
            if (!is_first) result.Append(',');

            string str = (string)row[fld] ?? ""; // non-null guard

            // escape double quotes + quote if value has line breaks, double quotes, commas
            // https://www.ietf.org/rfc/rfc4180.txt
            if (str.IndexOf('"') != -1)
            {
                str = "\"" + str.Replace("\"", "\"\"") + "\"";
            }
            else if (str.IndexOfAny(new char[] { ',', '\r', '\n' }) != -1)
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
    public static StringBuilder getCSVExport(string csv_export_headers, string csv_export_fields, ArrayList rows)
    {
        string headers_str = csv_export_headers;
        StringBuilder csv = new();
        string[] fields = null;
        if (csv_export_fields == "" || csv_export_fields == "*")
        {
            // just read field names from first row
            if (rows.Count > 0)
            {
                fields = (rows[0] as Hashtable).Keys.Cast<string>().ToArray();
                headers_str = string.Join(",", fields);
            }
        }
        else
        {
            fields = Utils.qw(csv_export_fields);
        }

        csv.Append(headers_str + "\r\n");
        foreach (Hashtable row in rows)
        {
            csv.Append(Utils.toCSVRow(row, fields) + "\r\n");
        }
        return csv;
    }

    public static void writeCSVExport(HttpResponse response, string filename, string csv_export_headers, string csv_export_fields, ArrayList rows)
    {
        filename = filename.Replace("\"", "'"); // quote doublequotes

        response.Headers.Append("Content-type", "text/csv");
        response.Headers.Append("Content-Disposition", "attachment; filename=\"" + filename + "\"");

        HttpResponseWritingExtensions.WriteAsync(response, Utils.getCSVExport(csv_export_headers, csv_export_fields, rows).ToString()).Wait();
    }

    /// <summary>
    /// export to XLS based on /common/list/export templates
    /// </summary>
    /// <param name="fw"></param>
    /// <param name="filename"></param>
    /// <param name="csv_export_headers">comma-separated names for headers in specific order</param>
    /// <param name="csv_export_fields">qw-string(space separated) list of fields to match headers</param>
    /// <param name="rows">db array of rows</param>
    /// <param name="tpl_dir">template directory</param>
    public static void writeXLSExport(FW fw, string filename, string csv_export_headers, string csv_export_fields, ArrayList rows, string tpl_dir = "/common/list/export")
    {
        Hashtable ps = new();

        ArrayList headers = new();
        foreach (string str in csv_export_headers.Split(","))
        {
            Hashtable h = new();
            h["iname"] = str;
            headers.Add(h);
        }
        ps["headers"] = headers;

        filename = filename.Replace("\"", "_");

        fw.response.Headers.Append("Content-type", "application/vnd.ms-excel");
        fw.response.Headers.Append("Content-Disposition", "attachment; filename=\"" + filename + "\"");

        //output headers
        ParsePage parser = new(fw);
        var filedata = parser.parse_page(tpl_dir, "xls_head.html", ps);
        fw.responseWrite(filedata);

        //output rows in chunks to save memory and keep connection alive
        string[] fields = Utils.qw(csv_export_fields);
        //ps["rows"] = rows;
        var buffer = new ArrayList();
        var psbuffer = new Hashtable() { { "rows", buffer } };
        foreach (Hashtable row in rows)
        {
            var rowcopy = new Hashtable();
            Utils.mergeHash(rowcopy, row);

            ArrayList cell = new();
            foreach (string f in fields)
            {
                Hashtable h = new();
                h["value"] = rowcopy[f];
                cell.Add(h);
            }
            rowcopy["cell"] = cell;
            buffer.Add(rowcopy);

            //write to output every 10000 rows
            if (buffer.Count >= 10000)
            {
                filedata = parser.parse_page(tpl_dir, "xls_rows.html", psbuffer);
                fw.responseWrite(filedata);
                buffer.Clear();
            }
        }

        //output if something left
        if (buffer.Count > 0)
        {
            filedata = parser.parse_page(tpl_dir, "xls_rows.html", psbuffer);
            fw.responseWrite(filedata);
        }

        //output footer
        filedata = parser.parse_page(tpl_dir, "xls_foot.html", ps);
        fw.responseWrite(filedata);

        //simpler but uses more memory and for large results browser might give up waiting results from connection
        //ParsePage parser = new(fw);
        //string tpl_dir = "/common/list/export";
        //string page = parser.parse_page(tpl_dir, "xls.html", ps);
        //await HttpResponseWritingExtensions.WriteAsync(fw.response, page);
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
        FileStream stream = new(from_file, FileMode.Open, FileAccess.Read);

        // Create new image.
        System.Drawing.Image image = System.Drawing.Image.FromStream(stream);

        // Detect orientation and auto-rotate correctly
        rotateImage(image);

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
        Bitmap bitmap = new(w, h, PixelFormat.Format24bppRgb);
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
        EncoderParameters EncoderParameters = null;
        ImageCodecInfo ImageCodecInfo = null;

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
        ImageCodecInfo[] encoders;
        encoders = ImageCodecInfo.GetImageEncoders();

        int j = 0;
        while (j < encoders.Length)
        {
            if (encoders[j].FormatID == format.Guid) return encoders[j];
            j += 1;
        }
        return null;

    } // GetEncoderInfo

    /// <summary>
    /// return file size by path, if file not exists or not accessible - return 0
    /// </summary>
    /// <param name="filepath"></param>
    /// <returns></returns>
    public static long fileSize(string filepath)
    {
        long result = 0;
        try
        {
            FileInfo fi = new(filepath);
            result = fi.Length;
        }
        catch (Exception)
        {
            //ignore errors
        }
        return result;
    }

    // extract just file name (with ext) from file path
    public static string fileName(string filepath)
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
    public static void mergeHash(Hashtable hash1, Hashtable hash2)
    {
        if (hash2 != null)
        {
            // make static copy of hash2.keys, so even if hash2.keys changing (ex: hash1 is same as hash2) it will not affect the loop
            foreach (string key in hash2.Keys)
            {
                hash1[key] = hash2[key];
            }
        }
    }

    public static void mergeHash(DBRow hash1, Hashtable hash2)
    {
        if (hash2 != null)
        {
            // make static copy of hash2.keys, so even if hash2.keys changing (ex: hash1 is same as hash2) it will not affect the loop
            foreach (string key in hash2.Keys)
            {
                hash1[key] = Utils.toStr(hash2[key]);
            }
        }
    }
    public static void mergeHash(DBRow hash1, DBRow hash2)
    {
        if (hash2 != null)
        {
            // make static copy of hash2.keys, so even if hash2.keys changing (ex: hash1 is same as hash2) it will not affect the loop
            foreach (string key in hash2.Keys)
            {
                hash1[key] = Utils.toStr(hash2[key]);
            }
        }
    }

    // deep hash merge, i.e. if hash2 contains values that is hash value - go in it and copy such values to hash2 at same place accordingly
    // recursive
    public static void mergeHashDeep(ref Hashtable hash1, ref Hashtable hash2)
    {
        if (hash2 != null)
        {
            ArrayList keys = new(hash2.Keys);
            foreach (string key in keys)
            {
                if (hash2[key] is Hashtable ht)
                {
                    if (!(hash1[key] is Hashtable))
                        hash1[key] = new Hashtable();
                    Hashtable _hash1 = (Hashtable)hash1[key];
                    Hashtable _hash2 = ht;
                    mergeHashDeep(ref _hash1, ref _hash2);
                }
                else
                    hash1[key] = hash2[key];
            }
        }
    }

    public static string bytes2str(long b)
    {
        string result = b.ToString();

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
        return JsonSerializer.Serialize(data, data.GetType(), options);
    }

    //overload alias for jsonDecode(string)
    public static object jsonDecode(object str)
    {
        return jsonDecode((string)str);
    }

    /* <summary>
    * convert JSON string into data structure
    * </summary>
    * <param name="str">JSON string</param>
    * <returns>value or Hashtable (objects) or ArrayList (arrays) or null if cannot be converted</returns>
    * <remarks></remarks>
    */
    public static object jsonDecode(string str)
    {
        if (string.IsNullOrEmpty(str))
            return null;

        ReadOnlySpan<byte> jsonUtf8 = Encoding.UTF8.GetBytes(str);
        var options = new JsonReaderOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };

        object result;
        try
        {
            var reader = new Utf8JsonReader(jsonUtf8, options);
            reader.Read(); //initial read

            result = jsonDecodeRead(ref reader);
        }
        catch (Exception)
        {
            //ignore json errors, just return null, uncomment and log error for debug
            throw;
        }
        return result;
    }

    private static object jsonDecodeRead(ref Utf8JsonReader reader)
    {
        //rw("jsonDecodeRead init: " + reader.TokenType.ToString());
        object result;
        if (reader.TokenType == JsonTokenType.StartObject)
            result = new Hashtable();
        else if (reader.TokenType == JsonTokenType.StartArray)
            result = new ArrayList();
        else
            //single value
            return jsonDecodeValue(ref reader);

        while (reader.Read())
        {
            //rw("jsonDecodeRead: " + reader.TokenType.ToString());
            if (reader.TokenType == JsonTokenType.None || reader.TokenType == JsonTokenType.Comment)
                //skip no value and comments
                continue;
            else if (reader.TokenType == JsonTokenType.EndArray || reader.TokenType == JsonTokenType.EndObject)
                break; //return result

            if (result is Hashtable ht)
            {
                //for Hashtble if no key name yet - expect it to appear now
                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("PropertyName expected");

                string keyName = reader.GetString();
                if (string.IsNullOrWhiteSpace(keyName))
                    throw new JsonException("Got Empty PropertyName");

                //rw("keyName=" + keyName);
                reader.Read();
                ht[keyName] = jsonDecodeValue(ref reader);
            }
            else if (result is ArrayList al)
            {
                al.Add(jsonDecodeValue(ref reader));
            }

            //rw(".");
        }
        return result;
    }

    // RECURSIVE
    private static object jsonDecodeValue(ref Utf8JsonReader reader)
    {
        //rw("jsonDecodeValue: " + reader.TokenType.ToString());
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                return jsonDecodeRead(ref reader);
            case JsonTokenType.StartArray:
                return jsonDecodeRead(ref reader);
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var intres))
                    return intres;
                else
                    return reader.GetDecimal();
            default:
                throw new JsonException("Not supported TokenType: " + reader.TokenType);
        }
    }

    // convert all values in hierarchical Hashtable/ArrayList json structure to strings
    // returns new object
    // RECURSIVE
    public static object jsonStringifyValues(object json)
    {
        if (json is Hashtable ht)
        {
            var result = new Hashtable();
            foreach (string key in ht.Keys)
                result[key] = jsonStringifyValues(ht[key]);
            return result;
        }
        else if (json is ArrayList al)
        {
            var result = new ArrayList();
            for (int i = 0; i < al.Count; i++)
                result.Add(jsonStringifyValues(al[i]));
            return result;
        }
        else if (json is string str)
        {
            return str;
        }
        else if (json is bool b)
        {
            return b ? "true" : "false";
        }
        else if (json is null)
        {
            return ""; // null is empty string
        }
        else
        {
            return json.ToString();
        }
    }

    // serialize using BinaryFormatter.Serialize
    // return as base64 string
    public static string serialize(object data)
    {
        return jsonEncode(data);
    }

    // deserialize base64 string serialized with Utils.serialize
    // return object or Nothing (if error)
    public static object deserialize(string str)
    {
        return jsonDecode(str);
    }

    // return Hashtable keys as an array
    public static string[] hashKeys(Hashtable h)
    {
        return h.Keys.Cast<string>().ToArray();
    }

    // capitalize first word in string
    // if mode='all' - capitalize all words
    // EXAMPLE: mode="" : sample string => Sample string
    // mode="all" : sample STRING => Sample String
    public static string capitalize(string str, string mode = "")
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
    public static string strRepeat(string str, int num)
    {
        StringBuilder result = new();
        for (int i = 0; i < num; i++)
        {
            result.Append(str);
        }
        return result.ToString();
    }

    // return UUID (3.4 x 10^38 unique IDs)
    public static string uuid()
    {
        return System.Guid.NewGuid().ToString();
    }

    //return nanoID (2.1 trillion unique IDs)
    public static string nanoid(int size = 21)
    {

        using (var rng = RandomNumberGenerator.Create())
        {
            var bytes = new byte[size];
            rng.GetBytes(bytes);
            var chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz-".ToCharArray();
            var result = new char[size];
            for (int i = 0; i < size; i++)
            {
                result[i] = chars[bytes[i] % chars.Length];
            }
            return new string(result);
        }
    }

    public static string getTmpDir(string prefix = TMP_PREFIX)
    {
        var systemTmp = Path.GetTempPath();
        string appTmp = Path.Combine(systemTmp, prefix);
        if (!Directory.Exists(appTmp))
            Directory.CreateDirectory(appTmp); // create if not exists
        return appTmp;
    }

    // return path to tmp filename WITHOUT extension
    public static string getTmpFilename(string prefix = TMP_PREFIX)
    {
        return Utils.getTmpDir(prefix) + "\\" + Utils.uuid();
    }

    // scan tmp directory, find all tmp files created by website and delete older than 1 hour
    public static void cleanupTmpFiles(string prefix = TMP_PREFIX)
    {
        string[] files = Directory.GetFiles(Utils.getTmpDir(prefix), "*");
        foreach (string file in files)
        {
            FileInfo fi = new(file);
            TimeSpan ts = DateTime.Now - fi.CreationTime;
            if (ts.TotalMinutes > 60)
            {
                try
                {
                    fi.Delete();
                }
                catch (Exception)
                {
                    //throw; //ignore errors as it just cleanup, should not affect main logic, could be access denied
                }
            }
        }
    }

    // return md5 hash (hexadecimals) for a string
    public static string md5(string str)
    {
        // convert string to bytes
        UTF8Encoding ustr = new();
        byte[] bstr = ustr.GetBytes(str);

#pragma warning disable SCS0006 // Weak hashing function
        MD5 md5hasher = MD5.Create();
#pragma warning restore SCS0006 // Weak hashing function
        byte[] bhash = md5hasher.ComputeHash(bstr);

        // convert hash value to hex string
        StringBuilder sb = new();
        foreach (byte one_byte in bhash)
        {
            sb.Append(one_byte.ToString("x2").ToUpper());
        }

        return sb.ToString().ToLower();
    }

    // 1 => 01
    // 10 => 10
    public static string toXX(string str)
    {
        if (str.Length < 2) str = "0" + str;
        return str;
    }

    public static string num2ordinal(int num)
    {
        if (num <= 0) return num.ToString();

        return (num % 100) switch
        {
            11 or 12 or 13 => num + "th",
            _ => (num % 10) switch
            {
                1 => num + "st",
                2 => num + "nd",
                3 => num + "rd",
                _ => num + "th",
            },
        };
    }

    // for num (within total) and total - return string "+XXX%" or "-XXX%" depends if num is bigger or smaller than previous period (num-total)
    public static string percentChange(long num, long total)
    {
        string result = "";

        long prev_num = total - num;
        if (prev_num == 0)
            return (num == 0) ? "0%" : "+100%";

        double percent = ((double)num - prev_num) / prev_num * 100;
        if (percent >= 0)
            result = "+";

        return result + Math.Round(percent, 2) + "%";
    }

    // truncate  - This truncates a variable to a character length, the default is 80.
    // trchar    - As an optional second parameter, you can specify a string of text to display at the end if the variable was truncated.
    // The characters in the string are included with the original truncation length.
    // trword    - 0/1. By default, truncate will attempt to cut off at a word boundary =1.
    // trend     - 0/1. If you want to cut off at the exact character length, pass the optional third parameter of 1.
    //<~tag truncate="80" trchar="..." trword="1" trend="1">
    public static string str2truncate(string str, Hashtable hattrs)
    {
        int trlen = 80;
        string trchar = "...";
        int trword = 1;
        int trend = 1;  // if trend=0 trword - ignored

        if (hattrs["truncate"].ToString().Length > 0)
        {
            int trlen1 = toInt(hattrs["truncate"]);
            if (trlen1 > 0) trlen = trlen1;
        }
        if (hattrs.ContainsKey("trchar")) trchar = (string)hattrs["trchar"];
        if (hattrs.ContainsKey("trend")) trend = toInt(hattrs["trend"]);
        if (hattrs.ContainsKey("trword")) trword = toInt(hattrs["trword"]);

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
    public static string orderbyApplySortdir(string orderby, string sortdir)
    {
        string result = orderby;

        if (sortdir == "desc")
        {
            // TODO - move this to fw utils
            ArrayList order_fields = new();
            foreach (string fld in orderby.Split(","))
            {
                string _fld = fld;
                // if fld contains asc or desc - change to opposite
                if (_fld.Contains(" asc"))
                {
                    _fld = _fld.Replace(" asc", " desc");
                }
                else if (_fld.Contains("desc"))
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
            result = string.Join(", ", order_fields.ToArray());  // net 4
        }

        return result;
    }

    public static string html2text(string str)
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
    public static Hashtable commastr2hash(string sel_ids, string value = null)
    {
        Hashtable result = new();
        ArrayList ids = new(sel_ids.Split(","));
        for (int i = 0; i < ids.Count; i++)
        {
            string v = (string)ids[i];
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
    public static string commastr2nlstr(string str)
    {
        return (str ?? "").Replace(",", "\r\n");
    }

    // newline-delimited str to comma-delimited str
    public static string nlstr2commastr(string str)
    {
        return Regex.Replace(str ?? "", @"[\n\r]+", ",");
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
    public static string urlescape(string str)
    {
        return HttpUtility.UrlEncode(str);
    }

    /* <summary>
    *  unescapes/decodes escaped/encoded string back
    *  </summary>
    *  <param name="str"></param>
    *  <returns></returns>
    */
    public static string urlunescape(string str)
    {
        return HttpUtility.UrlDecode(str);
    }

    /// <summary>
    /// load content from url
    /// </summary>
    /// <param name="url">url to get data from</param>
    /// <param name="parameters">optional, name/value params if set - post will be used, instead of get</param>
    /// <param name="headers">optional, name/value headers to add to request</param>
    /// <returns>content received. empty string if error</returns>
    public static string loadUrl(string url, Hashtable parameters = null, Hashtable headers = null)
    {
        string content;
        using (HttpClient client = new())
        {
            if (headers != null)
                foreach (string hkey in headers.Keys)
                    client.DefaultRequestHeaders.Add(hkey, (string)headers[hkey]);


            if (parameters != null)
            {
                //POST
                var nv = new Dictionary<string, string>();
                foreach (string key in parameters.Keys)
                    nv.Add(key, (string)parameters[key]);

                using (HttpContent form = new FormUrlEncodedContent(nv))
                {
                    using (HttpResponseMessage response = client.PostAsync(url, form).Result)
                    {
                        //response.EnsureSuccessStatusCode(); //uncomment if exception wanted in case remote request unsuccessful
                        content = response.Content.ReadAsStringAsync().Result;
                    }
                }
            }
            else
            {
                //GET
                using (HttpResponseMessage response = client.GetAsync(url).Result)
                {
                    //response.EnsureSuccessStatusCode(); //uncomment if exception wanted in case remote request unsuccessful
                    content = response.Content.ReadAsStringAsync().Result;
                }
            }
        }

        return content;
    }

    /// <summary>
    /// sent multipart/form-data POST request to remote URL with files and formFields
    /// </summary>
    /// <param name="url"></param>
    /// <param name="files">key=fieldname, value=filepath</param>
    /// <param name="formFields">optional, key=fieldname, value=value</param>
    /// <param name="cert">optional, certificate</param>
    /// <returns></returns>
    /// TODO - combine this method with loadUrl() ?
    public static string sendFileToUrl(
        string url,
        Hashtable files,
        System.Collections.Specialized.NameValueCollection formFields = null,
        string cert_path = null)
    {
        string result = "";
        HttpClient client;

        //add certificate if requested
        if (cert_path != null)
        {
            var handler = new HttpClientHandler
            {
                SslProtocols = SslProtocols.Tls12
            };
            handler.ClientCertificates.Add(new X509Certificate2(cert_path));

            client = new(handler);
        }
        else
        {
            client = new();
        }

        using (var form = new MultipartFormDataContent())
        {
            //add form fields
            foreach (string name in formFields.Keys)
                form.Add(new StringContent(formFields[name]), name);

            //add files
            foreach (string fileField in files.Keys)
            {
                var filepath = (string)files[fileField];
                using (var fs = new FileStream(filepath, FileMode.Open))
                {
                    //TODO use some mime mapping class
                    string mimeType = "application/octet-stream";
                    if (Path.GetExtension(filepath) == ".xml") mimeType = "text/xml";

                    var part = new StreamContent(fs);
                    part.Headers.ContentType = MediaTypeHeaderValue.Parse(mimeType);
                    form.Add(part, "file", "test.txt");
                }
            }

            HttpResponseMessage response = client.PostAsync(url, form).Result;
            result = response.Content.ReadAsStringAsync().Result;
        }

        return result;
    }

    // convert/normalize external table/field name to fw standard name
    // "SomeCrazy/Name" => "some_crazy_name"
    public static string name2fw(string str)
    {
        string result = str;
        result = Regex.Replace(result, @"^tbl|dbo", "", RegexOptions.IgnoreCase); // remove tbl,dbo prefixes if any
        result = Regex.Replace(result, @"([A-Z]+)", "_$1"); // split CamelCase to underscore, but keep abbrs together ZIP/Code -> zip_code

        result = Regex.Replace(result, @"\W+", "_"); // replace all non-alphanum to underscore
        result = Regex.Replace(result, @"_+", "_"); // deduplicate underscore
        result = Regex.Replace(result, @"^_+|_+$", ""); // remove first and last _ if any
        result = result.ToLower(); // and finally to lowercase
        result = result.Trim();
        return result;
    }


    // convert some system name to human-friendly name'
    // "system_name_id" => "System Name ID"
    public static string name2human(string str)
    {
        string str_lc = str.ToLower();
        if (str_lc == "icode") return "Code";
        if (str_lc == "iname") return "Name";
        if (str_lc == "idesc") return "Description";
        if (str_lc == "idate") return "Date";
        if (str_lc == "itype") return "Type";
        if (str_lc == "iyear") return "Year";
        if (str_lc == "id") return "ID";
        if (str_lc == "fname") return "First Name";
        if (str_lc == "lname") return "Last Name";
        if (str_lc == "midname") return "Middle Name";

        string result = str;
        result = Regex.Replace(result, @"^tbl|dbo", "", RegexOptions.IgnoreCase); // remove tbl prefix if any
        result = Regex.Replace(result, @"_+", " "); // underscores to spaces
        result = Regex.Replace(result, @"([a-z ])([A-Z]+)", "$1 $2"); // split CamelCase words
        result = Regex.Replace(result, @" +", " "); // deduplicate spaces
        result = Utils.capitalize(result, "all"); // Title Case
        result = result.Trim();

        if (Regex.IsMatch(result, @"\bid\b", RegexOptions.IgnoreCase))
        {
            // if contains id/ID - remove it and make singular
            result = Regex.Replace(result, @"\s*\bid\b", "", RegexOptions.IgnoreCase);
            // singularize TODO use external lib to handle all cases
            result = Regex.Replace(result, @"(\S)(?:ies)\s*$", "$1y", RegexOptions.IgnoreCase); // -ies -> -y
            result = Regex.Replace(result, @"(\S)(?:es)\s*$", "$1e", RegexOptions.IgnoreCase); // -es -> -e
            result = Regex.Replace(result, @"(\S)(?:s)\s*$", "$1", RegexOptions.IgnoreCase); // remove -s at the end
        }

        result = result.Trim();
        return result;
    }

    // convert c/snake style name to CamelCase
    // system_name => SystemName
    public static string nameCamelCase(string str)
    {
        string result = str;
        result = Regex.Replace(result, @"\W+", " "); // non-alphanum chars to spaces
        result = Utils.capitalize(result);
        result = Regex.Replace(result, " +", ""); // remove spaces
        return result;
    }

    public static string Right(string str, int len)
    {
        if (string.IsNullOrEmpty(str)) return "";
        if (str.Length <= len)
            return str;
        else
            return str.Substring(str.Length - len);
    }

    //from https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
    public static void CopyDirectory(string sourceDirName, string destDirName, bool isCopyRecursive)
    {
        // Get the subdirectories for the specified directory.
        DirectoryInfo dir = new(sourceDirName);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException(
                "Source directory does not exist or could not be found: "
                + sourceDirName);
        }

        // If the destination directory doesn't exist, create it.
        Directory.CreateDirectory(destDirName);

        // Get the files in the directory and copy them to the new location.
        foreach (FileInfo file in dir.GetFiles())
        {
            string tempPath = Path.Combine(destDirName, file.Name);
            // Copy only not existing files to prevent overwriting and prevent exception by copying config,json which is already generated
            if (!File.Exists(tempPath))
                file.CopyTo(tempPath, false);
        }

        // If copying subdirectories, copy them and their contents to new location.
        if (isCopyRecursive)
        {
            foreach (DirectoryInfo subdir in dir.GetDirectories())
            {
                string tempPath = Path.Combine(destDirName, subdir.Name);
                CopyDirectory(subdir.FullName, tempPath, isCopyRecursive);
            }
        }
    }

    /// work with Cookies
    public static void createCookie(FW fw, string name, string value, long exp_sec)
    {
        var options = new CookieOptions()
        {
            Path = "/",
            Expires = new DateTimeOffset(DateTime.Now.AddSeconds(exp_sec))
        };
        fw.response.Cookies.Append(name, value, options);
    }

    public static string getCookie(FW fw, string name)
    {
        return fw.request.Cookies[name];
    }

    public static void deleteCookie(FW fw, string name)
    {
        fw.response.Cookies.Delete(name);
    }

}
