using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace osafw_asp.net_core.fw
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
        public static Hashtable qh(string str, object default_value = null) {
            Hashtable result = new Hashtable();
            if (str != null && str != "") {
                string[] arr = Regex.Split(str, @"\s+");
                foreach (string value in arr) {
                    string v = value.Replace("&nbsp;", " ");
                    string[] avoid = v.Split("|", 2);
                    string val = (string)default_value;
                    if (avoid.Length > 1) {
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
            foreach (String key in sh)
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
            if (!Regex.IsMatch(str, "^\w+://")) {
                str = "http://" + str;
            }
            return str;
        }

        public static String ConvertStreamToBase64(Stream fs) {
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

        public static String sTrim(String str, int size) {
            if (str.Length > size) str = str.Substring(0, size) + "...";
            return str;
        }

        public static String getRandStr(int size) {
            StringBuilder result = new StringBuilder();
            String[] chars = qw("A B C D E F a b c d e f 0 1 2 3 4 5 6 7 8 9");

            Random _random = new Random();
            for (int i = 1; i < size; i++)
            {
                result.Append(chars[_random.Next(0, chars.Length - 1)]);
            }

            return result.ToString();
        }

        //''' <summary>
        //''' helper for importing csv files. Example:
        //'''    Utils.importCSV(fw, AddressOf importer, "c:\import.csv")
        //'''    void importer(row as Hashtable)
        //'''       ...your custom import code
        //'''    End void
        //''' </summary>
        //''' <param name="fw">fw instance</param>
        //''' <param name="callback">callback to custom code, accept one row of fields(as Hashtable)</param>
        //''' <param name="filepath">.csv file name to import</param>
        //public static void importCSV(fw As FW, callback As Action(Of Hashtable), filepath As String, Optional is_header As Boolean = True)
        //    Dim dir = Path.GetDirectoryName(filepath)
        //    Dim filename = Path.GetFileName(filepath)

        //    Dim ConnectionString As String = "Provider=" & OLEDB_PROVIDER & ";" +
        //                            "Data Source=" & dir & ";" &
        //                            "Extended Properties=""Text;HDR=" & IIf(is_header, "Yes", "No") & ";IMEX=1;FORMAT=Delimited"";"

        //    Using cn As New Data.OleDb.OleDbConnection(ConnectionString)
        //        cn.Open()

        //        Dim WorkSheetName = filename
        //        'quote as table name
        //        WorkSheetName = Replace(WorkSheetName, "[", "")
        //        WorkSheetName = Replace(WorkSheetName, "]", "")

        //        Dim sql = "select * from [" & WorkSheetName & "]"
        //        Dim dbcomm = New Data.OleDb.OleDbCommand(sql, cn)
        //        Dim dbread As Data.Common.DbDataReader = dbcomm.ExecuteReader()

        //        While dbread.Read()
        //            Dim row As New Hashtable
        //            For i = 0 To dbread.FieldCount - 1
        //                Dim value As String = dbread(i).ToString()
        //                Dim name As String = dbread.GetName(i).ToString()
        //                row.Add(name, value)
        //            Next

        //            'logger(h)
        //            callback(row)
        //        End While
        //    End Using
        //End void

        //''' <summary>
        //''' helper for importing Excel files. Example:
        //'''    Utils.importExcel(fw, AddressOf importer, "c:\import.xlsx")
        //'''    void importer(sheet_name as String, rows as ArrayList)
        //'''       ...your custom import code
        //'''    End void
        //''' </summary>
        //''' <param name="fw">fw instance</param>
        //''' <param name="callback">callback to custom code, accept worksheet name and all rows(as ArrayList of Hashtables)</param>
        //''' <param name="filepath">.xlsx file name to import</param>
        //''' <param name="is_header"></param>
        //''' <returns></returns>
        //public static Function importExcel(fw As FW, callback As Action(Of String, ArrayList), filepath As String, Optional is_header As Boolean = True) As Hashtable
        //    Dim result As New Hashtable()
        //    Dim conf As New Hashtable From
        //    {
        //            { "type", "OLE"},
        //            { "connection_string", "Provider=" & OLEDB_PROVIDER & ";Data Source=" & filepath & ";Extended Properties=""Excel 12.0 Xml;HDR=" & IIf(is_header, "Yes", "No") & ";ReadOnly=True;IMEX=1"""}
        //    }
        //    Dim accdb = New DB(fw, conf)
        //    Dim conn As System.Data.OleDb.OleDbConnection = accdb.connect()
        //    Dim schema = conn.GetOleDbSchemaTable(Data.OleDb.OleDbSchemaGuid.Tables, Nothing)
        //    If schema Is Nothing OrElse schema.Rows.Count< 1 Then
        //        Throw New ApplicationException("No worksheets found in the Excel file")
        //    End If

        //    Dim where As New Hashtable
        //    For i As Integer = 0 To schema.Rows.Count - 1
        //        Dim sheet_name_full = schema.Rows(i)("TABLE_NAME").ToString()
        //        Dim sheet_name = sheet_name_full.Replace("""", "")
        //        sheet_name = sheet_name.Replace("'", "")
        //        sheet_name = sheet_name.voidstring(0, sheet_name.Length - 1)
        //        Try
        //            Dim rows = accdb.array(sheet_name_full, where)
        //            callback(sheet_name, rows)
        //        Catch ex As Exception
        //            Throw New ApplicationException("Error while reading data from [" & sheet_name & "] sheet: " & ex.Message())
        //        End Try
        //    Next
        //    ' close connection to release the file
        //    accdb.disconnect()

        //    Return result
        //}


        //public static Function toCSVRow(row As Hashtable, fields As Array) As String
        //    Dim result As New StringBuilder
        //    Dim is_first = True
        //    For Each fld As String In fields
        //        If Not is_first Then result.Append(",")

        //        Dim str As String = Regex.Replace(row(fld) & "", "[\n\r]+", " ")
        //        str = Replace(str, """", """""")
        //        'check if string need to be quoted (if it contains " or ,)
        //        If InStr(str, """") > 0 OrElse InStr(str, ",") > 0 Then
        //            str = """" & str & """"
        //        End If
        //        result.Append(str)
        //        is_first = False
        //    Next
        //    Return result.ToString()
        //}

        //''' <summary>
        //''' standard function for exporting to csv
        //''' </summary>
        //''' <param name="csv_export_headers">CSV headers row, comma-separated format</param>
        //''' <param name="csv_export_fields">empty, * or Utils.qw format</param>
        //''' <param name="rows">DB array</param>
        //''' <returns></returns>
        //public static Function getCSVExport(csv_export_headers As String, csv_export_fields As String, rows As ArrayList) As StringBuilder
        //    Dim headers_str As String = csv_export_headers
        //    Dim csv As New StringBuilder
        //    Dim fields() As String = Nothing
        //    If csv_export_fields = "" Or csv_export_fields = "*" Then
        //        'just read field names from first row
        //        If rows.Count > "" Then
        //            fields = New ArrayList(DirectCast(rows(0).Keys(), ICollection)).ToArray()
        //            headers_str = Join(fields, ",")
        //        End If
        //    Else
        //        fields = Utils.qw(csv_export_fields)
        //    End If

        //    csv.Append(headers_str & vbLf)
        //    For Each row As Hashtable In rows
        //        csv.Append(Utils.toCSVRow(row, fields) & vbLf)
        //    Next
        //    Return csv
        //}

        //public static Function writeCSVExport(response As HttpResponse, filename As String, csv_export_headers As String, csv_export_fields As String, rows As ArrayList) As Boolean
        //    filename = Replace(filename, """", "'") 'quote doublequotes

        //    response.AppendHeader("Content-type", "text/csv")
        //    response.AppendHeader("Content-Disposition", "attachment; filename=""" & filename & """")

        //    response.Write(Utils.getCSVExport(csv_export_headers, csv_export_fields, rows))
        //    Return True
        //}

        //public static Function writeXLSExport(fw As FW, filename As String, csv_export_headers As String, csv_export_fields As String, rows As ArrayList) As Boolean
        //    Dim ps As New Hashtable
        //    ps("rows") = rows

        //    Dim headers As New ArrayList
        //    For Each str As String In csv_export_headers.Split(",")
        //        Dim h As New Hashtable
        //        h("iname") = str
        //        headers.Add(h)
        //    Next
        //    ps("headers") = headers

        //    Dim fields() As String = Utils.qw(csv_export_fields)
        //    For Each row As Hashtable In rows
        //        Dim cell As New ArrayList
        //        For Each f As String In fields
        //            Dim h As New Hashtable
        //            h("value") = row(f)
        //            cell.Add(h)
        //        Next
        //        row("cell") = cell
        //    Next

        //    'parse and out document
        //    'TODO ConvUtils.parse_page_xls(fw, LCase(fw.cur_controller_path & "/index/export"), "xls.html", hf, "filename")

        //    Dim parser As ParsePage = New ParsePage(fw)
        //    'Dim tpl_dir = LCase(fw.cur_controller_path & "/index/export")
        //    Dim tpl_dir = "/common/list/export"
        //    Dim page As String = parser.parse_page(tpl_dir, "xls.html", ps)

        //    filename = filename.Replace("""", "_")

        //    fw.resp.AddHeader("Content-type", "application/vnd.ms-excel")
        //    fw.resp.AddHeader("Content-Disposition", "attachment; filename=""" & filename & """")
        //    fw.resp.Write(page)
        //}

        //'Detect orientation and auto-rotate correctly
        //public static Function rotateImage(Image As Image) As Boolean
        //    Dim result = False
        //    Dim rot = RotateFlipType.RotateNoneFlipNone
        //    Dim props = Image.PropertyItems()

        //    For Each p In props
        //        If p.Id = 274 Then
        //            Select Case BitConverter.ToInt16(p.Value, 0)
        //                Case 1
        //                    rot = RotateFlipType.RotateNoneFlipNone
        //                Case 3
        //                    rot = RotateFlipType.Rotate180FlipNone
        //                Case 6
        //                    rot = RotateFlipType.Rotate90FlipNone
        //                Case 8
        //                    rot = RotateFlipType.Rotate270FlipNone
        //            End Select
        //        End If
        //    Next
        //    If rot<> RotateFlipType.RotateNoneFlipNone Then
        //        Image.RotateFlip(rot)
        //        result = True
        //    End If
        //    Return result
        //}

        //'resize image in from_file to w/h and save to to_file
        //'(optional)w and h - mean max weight and max height (i.e. image will not be upsized if it's smaller than max w/h)
        //'if no w/h passed - then no resizing occurs, just conversion (based on destination extension)
        //'return false if no resize performed (if image already smaller than necessary). Note if to_file is not same as from_file - to_file will have a copy of the from_file
        //public static Function resizeImage(ByVal from_file As String, ByVal to_file As String, Optional ByVal w As Long = -1, Optional ByVal h As Long = -1) As Boolean
        //    Dim stream As New FileStream(from_file, FileMode.Open, FileAccess.Read)

        //    ' Create new image.
        //    Dim image As System.Drawing.Image = System.Drawing.Image.FromStream(stream)

        //    'Detect orientation and auto-rotate correctly
        //    Dim is_rotated = rotateImage(image)

        //    ' Calculate proportional max width and height.
        //    Dim oldWidth As Integer = image.Width
        //    Dim oldHeight As Integer = image.Height

        //    If w = -1 Then w = oldWidth
        //    If h = -1 Then h = oldHeight

        //    If oldWidth / w >= 1 Or oldHeight / h >= 1 Then
        //        'downsizing
        //    Else
        //        'image already smaller no resize required - keep sizes same
        //        image.Dispose()
        //        stream.Close()
        //        If to_file<> from_file Then
        //            'but if destination file is different - make a copy
        //            File.Copy(from_file, to_file)
        //        End If
        //        Return False
        //    End If

        //    If (CDec(oldWidth) / CDec(oldHeight)) > (CDec(w) / CDec(h)) Then
        //        Dim ratio As Decimal = CDec(w) / oldWidth
        //        h = CInt(oldHeight * ratio)
        //    Else
        //        Dim ratio As Decimal = CDec(h) / oldHeight
        //        w = CInt(oldWidth * ratio)
        //    End If

        //    ' Create a new bitmap with the same resolution as the original image.
        //    Dim bitmap As New Bitmap(w, h, PixelFormat.Format24bppRgb)
        //    bitmap.SetResolution(image.HorizontalResolution, image.VerticalResolution)

        //    ' Create a new graphic.
        //    Dim gr As Graphics = Graphics.FromImage(bitmap)
        //    gr.Clear(Color.White)
        //    gr.InterpolationMode = InterpolationMode.HighQualityBicubic
        //    gr.SmoothingMode = SmoothingMode.HighQuality
        //    gr.PixelOffsetMode = PixelOffsetMode.HighQuality
        //    gr.CompositingQuality = CompositingQuality.HighQuality

        //    ' Create a scaled image based on the original.
        //    gr.DrawImage(image, New Rectangle(0, 0, w, h), New Rectangle(0, 0, oldWidth, oldHeight), GraphicsUnit.Pixel)
        //    gr.Dispose()

        //    ' Save the scaled image.
        //    Dim ext As String = UploadUtils.getUploadFileExt(to_file)
        //    Dim out_format As ImageFormat = image.RawFormat
        //    Dim EncoderParameters As EncoderParameters = Nothing
        //    Dim ImageCodecInfo As ImageCodecInfo = Nothing

        //    If ext = ".gif" Then
        //        out_format = ImageFormat.Gif
        //    ElseIf ext = ".jpg" Then
        //        out_format = ImageFormat.Jpeg
        //        'set jpeg quality to 80
        //        ImageCodecInfo = GetEncoderInfo(out_format)
        //        Dim Encoder As Encoder = Encoder.Quality
        //        EncoderParameters = New EncoderParameters(1)
        //        EncoderParameters.Param(0) = New EncoderParameter(Encoder, CType(80L, Int32))
        //    ElseIf ext = ".png" Then
        //        out_format = ImageFormat.Png
        //    End If

        //    'close read stream before writing as to_file might be same as from_file
        //    image.Dispose()
        //    stream.Close()

        //    If EncoderParameters Is Nothing Then
        //        bitmap.Save(to_file, out_format) 'image.RawFormat
        //    Else
        //        bitmap.Save(to_file, ImageCodecInfo, EncoderParameters)
        //    End If
        //    bitmap.Dispose()

        //    'if( contentType == "image/gif" )
        //    '{
        //    '            Using (thumbnail)
        //    '    {
        //    '        OctreeQuantizer quantizer = new OctreeQuantizer ( 255 , 8 ) ;
        //    '        using ( Bitmap quantized = quantizer.Quantize ( bitmap ) )
        //    '        {
        //    '            Response.ContentType = "image/gif";
        //    '            quantized.Save ( Response.OutputStream , ImageFormat.Gif ) ;
        //    '        }
        //    '    }
        //    '}

        //    Return True
        //}

        //Private Shared Function GetEncoderInfo(ByVal format As ImageFormat) As ImageCodecInfo
        //    Dim j As Integer
        //    Dim encoders() As ImageCodecInfo
        //    encoders = ImageCodecInfo.GetImageEncoders()

        //    j = 0
        //    While j<encoders.Length
        //        If encoders(j).FormatID = format.Guid Then
        //            Return encoders(j)
        //        End If
        //        j += 1
        //    End While
        //    Return Nothing

        //} 'GetEncoderInfo

        //public static Function fileSize(filepath As String) As Long
        //    Dim fi As FileInfo = New FileInfo(filepath)
        //    Return fi.Length
        //}

        //'extract just file name (with ext) from file path
        //public static Function fileName(filepath As String) As String
        //    Return System.IO.Path.GetFileName(filepath)
        //}


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

        //'deep hash merge, i.e. if hash2 contains values that is hash value - go in it and copy such values to hash2 at same place accordingly
        //'recursive
        //public static void mergeHashDeep(ByRef hash1 As Hashtable, ByRef hash2 As Hashtable)
        //    If hash2 IsNot Nothing Then
        //        Dim keys As New ArrayList(hash2.Keys)
        //        For Each key As String In keys
        //            If TypeOf hash2(key) Is Hashtable Then
        //                If Not(TypeOf hash1(key) Is Hashtable) Then
        //                   hash1(key) = New Hashtable
        //                End If
        //                mergeHashDeep(hash1(key), hash2(key))
        //            Else
        //                hash1(key) = hash2(key)
        //            End If
        //        Next
        //    End If
        //End void

        //public static Function bytes2str(b As Integer) As String
        //    Dim result As String = b

        //    If b< 1024 Then
        //        result &= " B"
        //    ElseIf b < 1048576 Then
        //        result = (Math.Floor(b / 1024 * 100) / 100) & " KiB"
        //    ElseIf b < 1073741824 Then
        //        result = (Math.Floor(b / 1048576 * 100) / 100) & " MiB"
        //    Else
        //        result = (Math.Floor(b / 1073741824 * 100) / 100) & " GiB"
        //    End If
        //    Return result
        //}

        //''' <summary>
        //''' convert data structure to JSON string
        //''' </summary>
        //''' <param name="data">any data like single value, arraylist, hashtable, etc..</param>
        //''' <returns></returns>
        //public static Function jsonEncode(data As Object) As String
        //    Return New Script.Serialization.JavaScriptSerializer().Serialize(data)
        //}

        //''' <summary>
        //''' convert JSON string into data structure
        //''' </summary>
        //''' <param name="str">JSON string</param>
        //''' <returns>single value, arraylist, hashtable, etc.. or Nothing if cannot be converted</returns>
        //''' <remarks>Note, JavaScriptSerializer.MaxJsonLength is about 4MB unicode</remarks>
        //public static Function jsonDecode(str As String) As Object
        //    Dim result As Object
        //    Try
        //        Dim des = New Script.Serialization.JavaScriptSerializer()
        //        result = des.DeserializeObject(str)
        //        result = cast2std(result)

        //    Catch ex As Exception
        //        'if error during conversion - return Nothing
        //        FW.Current.logger(ex.Message)
        //        result = Nothing
        //    End Try

        //    Return result
        //}

        //''' <summary>
        //''' depp convert data structure to standard framework's Hashtable/Arraylist
        //''' </summary>
        //''' <param name="data"></param>
        //''' <remarks>RECURSIVE!</remarks>
        //public static Function cast2std(data As Object) As Object
        //    Dim result As Object = data

        //    If TypeOf result Is IDictionary Then
        //        'convert dictionary to Hashtable
        //        Dim result2 = New Hashtable 'because we can't iterate hashtable and change it
        //        For Each key In CType(result, IDictionary).Keys
        //            result2(key) = cast2std(result(key))
        //        Next
        //        result = result2

        //    ElseIf TypeOf result Is IList Then
        //        'convert arrays to ArrayList
        //        result = New ArrayList(CType(result, IList))
        //        For i = 0 To result.Count - 1
        //            result(i) = cast2std(result(i))
        //        Next
        //    End If

        //    Return result
        //}

        //'serialize using BinaryFormatter.Serialize
        //'return as base64 string
        //public static Function serialize(data As Object) As String
        //    Dim xstream As New IO.MemoryStream
        //    Dim xformatter As New BinaryFormatter

        //    xformatter.Serialize(xstream, data)

        //    Return Convert.ToBase64String(xstream.ToArray())
        //}

        //'deserialize base64 string serialized with Utils.serialize
        //'return object or Nothing (if error)
        //public static Function deserialize(ByRef str As String) As Object
        //    Dim data As Object
        //    Try
        //        Dim xstream As MemoryStream
        //        xstream = New MemoryStream(Convert.FromBase64String(str))
        //        Dim xformatter As New BinaryFormatter
        //        data = xformatter.Deserialize(xstream)
        //    Catch ex As Exception
        //        data = Nothing
        //    End Try

        //    Return data
        //}

        //'return Hashtable keys as an array
        //public static Function hashKeys(h As Hashtable) As String()
        //    Return New ArrayList(h.Keys).ToArray(GetType(String))
        //}

        //'capitalize first word in string
        //'if mode='all' - capitalize all words
        //'EXAMPLE: mode="" : sample string => Sample string
        //'mode="all" : sample STRING => Sample String
        //Shared Function capitalize(str As String, Optional mode As String = "") As Object
        //    If mode = "all" Then
        //        str = LCase(str)
        //        str = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str)
        //    Else
        //        str = str.voidstring(0, 1).ToUpper() & str.voidstring(1)
        //    End If

        //    Return str
        //}

        //'repeat string num times
        //Shared Function strRepeat(str As String, num As Integer) As String
        //    Dim result As New StringBuilder
        //    For i As Integer = 1 To num
        //        result.Append(str)
        //    Next
        //    Return result.ToString
        //}

        // return unique file name in form UUID (without extension)
        public static String uuid()
        {
            return System.Guid.NewGuid().ToString();
        }

        //'return path to tmp filename WITHOUT extension
        //public static Function getTmpFilename(Optional prefix As String = "osafw") As String
        //    Return Path.GetTempPath & "\" & prefix & Utils.uuid()
        //}

        //'scan tmp directory, find all tmp files created by website and delete older than 1 hour
        //public static void cleanupTmpFiles(Optional prefix As String = "osafw")
        //    Dim files As String() = Directory.GetFiles(Path.GetTempPath(), prefix & "*")
        //    For Each file As String In files
        //        Dim fi As FileInfo = New FileInfo(file)
        //        If DateDiff(DateInterval.Minute, fi.CreationTime, Now()) > 60 Then
        //            fi.Delete()
        //        End If
        //    Next
        //End void

        //'return md5 hash (hexadecimals) for a string
        //public static Function md5(str As String) As String
        //    'convert string to bytes
        //    Dim ustr As New UTF8Encoding
        //    Dim bstr() As Byte = ustr.GetBytes(str)

        //    Dim md5hasher As MD5 = MD5CryptoServiceProvider.Create()
        //    Dim bhash() As Byte = md5hasher.ComputeHash(bstr)

        //    'convert hash value to hex string
        //    Dim sb As New System.Text.StringBuilder
        //    For Each one_byte As Byte In bhash
        //        sb.Append(one_byte.ToString("x2").ToUpper)
        //    Next

        //    Return sb.ToString().ToLower()
        //}

        //'1 => 01
        //'10 => 10
        //Shared Function toXX(str As String) As String
        //    If Len(str) < 2 Then str = "0" & str
        //    Return str
        //}

        //Shared Function num2ordinal(num As Integer) As String
        //    If num <= 0 Then Return num.ToString()

        //    Select Case num Mod 100
        //        Case 11
        //        Case 12
        //        Case 13
        //            Return num & "th"
        //    End Select

        //    Select Case num Mod 10
        //        Case 1
        //            Return num & "st"
        //        Case 2
        //            Return num & "nd"
        //        Case 3
        //            Return num & "rd"
        //        Case Else
        //            Return num & "th"
        //    End Select
        //}

        //' truncate  - This truncates a variable to a character length, the default is 80.
        //' trchar    - As an optional second parameter, you can specify a string of text to display at the end if the variable was truncated.
        //' The characters in the string are included with the original truncation length.
        //' trword    - 0/1. By default, truncate will attempt to cut off at a word boundary =1.
        //' trend     - 0/1. If you want to cut off at the exact character length, pass the optional third parameter of 1.
        //'<~tag truncate="80" trchar="..." trword="1" trend="1">
        //Shared Function str2truncate(str As String, hattrs As Hashtable) As Object
        //    Dim trlen As Integer = 80
        //    Dim trchar As String = "..."
        //    Dim trword As Integer = 1
        //    Dim trend As Integer = 1  'if trend=0 trword - ignored

        //    If hattrs("truncate") > "" Then
        //        Dim trlen1 As Integer = f2int(hattrs("truncate"))
        //        If trlen1 > 0 Then trlen = trlen1
        //    End If
        //    If hattrs.ContainsKey("trchar") Then trchar = hattrs("trchar")
        //    If hattrs.ContainsKey("trend") Then trend = hattrs("trend")
        //    If hattrs.ContainsKey("trword") Then trword = hattrs("trword")

        //    Dim orig_len As Integer = Len(str)
        //    If orig_len<trlen Then Return str 'no need truncate

        //    If trend = 1 Then
        //        If trword = 1 Then
        //            str = Regex.Replace(str, "^(.{" & trlen & ",}?)[\n \t\.\,\!\?]+(.*)$", "$1", RegexOptions.Singleline)
        //            If Len(str) < orig_len Then str &= trchar
        //        Else
        //            str = Left(str, trlen) & trchar
        //        End If
        //    Else
        //        str = Left(str, trlen / 2) & trchar & Mid(str, trlen / 2 + 1)
        //    End If
        //    Return str
        //}

        //'IN: orderby string for default asc sorting, ex: "id", "id desc", "prio desc, id"
        //'OUT: orderby or inversed orderby (if sortdir="desc"), ex: "id desc", "id asc", "prio asc, id desc"
        //Shared Function orderbyApplySortdir(orderby As String, sortdir As String) As String
        //    Dim result As String = orderby

        //    If sortdir = "desc" Then
        //        'TODO - move this to fw utils
        //        Dim order_fields As New ArrayList
        //        For Each fld As String In orderby.Split(",")
        //            'if fld contains asc or desc - change to opposite
        //            If InStr(fld, " asc") Then
        //                fld = Replace(fld, " asc", " desc")
        //            ElseIf InStr(fld, "desc") Then
        //                fld = Replace(fld, " desc", " asc")
        //            Else
        //                'if no asc/desc - just add desc at the end
        //                fld &= " desc"
        //            End If
        //            order_fields.Add(fld)
        //        Next
        //        'result = String.Join(", ", order_fields.ToArray(GetType(String))) 'net 2
        //        result = Join(New ArrayList(order_fields).ToArray(), ", ") 'net 4
        //    End If

        //    Return result
        //}

        //Shared Function html2text(str As String) As String
        //    str = Regex.Replace(str, "\n+", " ")
        //    str = Regex.Replace(str, "<br\s*\/?>", vbLf)
        //    str = Regex.Replace(str, "(?:<[^>]*>)+", " ")
        //    Return str
        //}

        //'sel_ids - comma-separated ids
        //'value:
        //'     nothing - use id value from input
        //'     "123..."  - use index (by order)
        //'     "other value" - use this value
        //'return hash: id => id
        //Shared Function commastr2hash(sel_ids As String, Optional value As String = Nothing) As Hashtable
        //    Dim ids As New ArrayList(Split(sel_ids, ","))
        //    Dim result As New Hashtable
        //    For i = 0 To ids.Count - 1
        //        Dim v As String = ids(i)
        //        result(v) = IIf(IsNothing(value), v, IIf(value = "123...", i, value))
        //    Next
        //    Return result
        //}

        //'comma-delimited str to newline-delimited str
        //public static Function commastr2nlstr(str As String) As String
        //    Return Replace(str, ",", vbCrLf)
        //}

        //'newline-delimited str to comma-delimited str
        //public static Function nlstr2commastr(str As String) As String
        //    Return Regex.Replace(str & "", "[\n\r]+", ",")
        //}

        /* <summary>
        * for each row in rows add keys/values to this row (by ref)
        * </summary>
        * <param name="rows">db array</param>
        * <param name="fields">keys/values to add</param>
        */
        public static void arrayInject(ArrayList rows, Hashtable fields) {
            foreach (Hashtable row in rows)
            {
                // array merge
                foreach (var key in fields.Keys)
                {
                    row[key] = fields[key];
                }
            }
        }

        //''' <summary>
        //''' escapes/encodes string so it can be passed as part of the url
        //''' </summary>
        //''' <param name="str"></param>
        //''' <returns></returns>
        //public static Function urlescape(str As String) As String
        //    Return HttpUtility.UrlEncode(str)
        //}

        //'sent multipart/form-data POST request to remote URL with files (key=fieldname, value=filepath) and formFields
        //Shared Function UploadFilesToRemoteUrl(ByVal url As String, ByVal files As Hashtable, ByVal Optional formFields As NameValueCollection = Nothing, Optional cert As X509Certificates.X509Certificate2 = Nothing) As String
        //    Dim boundary As String = "----------------------------" & DateTime.Now.Ticks.ToString("x")
        //    Dim request As HttpWebRequest = CType(WebRequest.Create(url), HttpWebRequest)
        //    request.ContentType = "multipart/form-data; boundary=" & boundary
        //    request.Method = "POST"
        //    request.KeepAlive = True
        //    If cert IsNot Nothing Then request.ClientCertificates.Add(cert)

        //    Dim memStream As New System.IO.MemoryStream()
        //    Dim boundarybytes = System.Text.Encoding.ASCII.GetBytes(vbCrLf & "--" & boundary & vbCrLf)
        //    Dim endBoundaryBytes = System.Text.Encoding.ASCII.GetBytes(vbCrLf & "--" & boundary & "--")

        //    'Dim formdataTemplate As String = vbCrLf & "--" & boundary & vbCrLf & "Content-Disposition: form-data; name=""{0}"";" & vbCrLf & vbCrLf & "{1}"
        //    Dim formdataTemplate As String = "--" & boundary & vbCrLf & "Content-Disposition: form-data; name=""{0}"";" & vbCrLf & vbCrLf & "{1}" & vbCrLf
        //    If formFields IsNot Nothing Then
        //        For Each key As String In formFields.Keys
        //            Dim formitem As String = String.Format(formdataTemplate, key, formFields(key))

        //            If memStream.Length > 0 Then formitem = vbCrLf & formitem 'add crlf before the string only for second and further lines

        //            Dim formitembytes As Byte() = System.Text.Encoding.UTF8.GetBytes(formitem)
        //            memStream.Write(formitembytes, 0, formitembytes.Length)
        //        Next
        //    End If

        //    Dim headerTemplate As String = "Content-Disposition: form-data; name=""{0}""; filename=""{1}""" & vbCrLf & "Content-Type: {2}" & vbCrLf & vbCrLf
        //    For Each fileField As String In files.Keys
        //        memStream.Write(boundarybytes, 0, boundarybytes.Length)

        //        'mime (TODO use System.Web.MimeMapping.GetMimeMapping() for .net 4.5+)
        //        Dim mimeType = "application/octet-stream"
        //        If System.IO.Path.GetExtension(files(fileField)) = ".xml" Then mimeType = "text/xml"

        //        Dim header = String.Format(headerTemplate, fileField, System.IO.Path.GetFileName(files(fileField)), mimeType)
        //        Dim headerbytes = System.Text.Encoding.UTF8.GetBytes(header)
        //        memStream.Write(headerbytes, 0, headerbytes.Length)

        //        Using fileStream = New FileStream(files(fileField), FileMode.Open, FileAccess.Read)
        //            Dim buffer = New Byte(1023) { }
        //    Dim bytesRead = fileStream.Read(buffer, 0, buffer.Length)
        //            While bytesRead<> 0
        //                memStream.Write(buffer, 0, bytesRead)
        //                bytesRead = fileStream.Read(buffer, 0, buffer.Length)
        //            End While
        //        End Using
        //    Next

        //    memStream.Write(endBoundaryBytes, 0, endBoundaryBytes.Length)
        //    'Diagnostics.Debug.WriteLine("***")
        //    'Diagnostics.Debug.WriteLine(Encoding.ASCII.GetString(memStream.ToArray()))
        //    'Diagnostics.Debug.WriteLine("***")

        //    request.ContentLength = memStream.Length
        //    Using requestStream = request.GetRequestStream()
        //        memStream.Position = 0

        //        Dim tempBuffer As Byte() = New Byte(memStream.Length - 1)
        //    { }
        //    memStream.Read(tempBuffer, 0, tempBuffer.Length)
        //        memStream.Close()
        //        requestStream.Write(tempBuffer, 0, tempBuffer.Length)
        //    End Using

        //    Using response = request.GetResponse()
        //        Dim stream2 = response.GetResponseStream()
        //        Dim reader2 As New StreamReader(stream2)
        //        Return reader2.ReadToEnd()
        //    End Using

        //}


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
    }
}
