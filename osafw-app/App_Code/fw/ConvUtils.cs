using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

// see also http://stackoverflow.com/questions/1331926/calling-wkhtmltopdf-to-generate-pdf-from-html/1698839#1698839

namespace osafw;

public class ConvUtils
{
    // parse template and generate pdf
    // Note: set IS_PRINT_MODE=True hf var which is become available in templates
    // if out_filename ="" or doesn't contain "\" or "/" - output pdf file to browser
    // if out_filename cotains "\" or "/" - save pdf file to this path
    // options:
    // landscape = True - will produce landscape output
    public static string parsePagePdf(FW fw, string bdir, string tpl_name, Hashtable ps, string out_filename = "", Hashtable options = null)
    {
        if (options == null)
        {
            options = new Hashtable();
        }
        if (!options.ContainsKey("disposition"))
        {
            options["disposition"] = "attachment";
        }

        ParsePage parser = new(fw);
        ps["IS_PRINT_MODE"] = true;
        string html_data = parser.parse_page(bdir, tpl_name, ps);

        html_data = _replace_specials(html_data);

        string html_file = Utils.getTmpFilename() + ".html";
        string pdf_file = Utils.getTmpFilename() + ".pdf";
        // fw.logger("INFO", "html file = " & html_file)
        // fw.logger("INFO", "pdf file = " & pdf_file)

        // remove_old_files()
        FW.setFileContent(html_file, ref html_data);

        if (string.IsNullOrEmpty(out_filename) || !Regex.IsMatch(out_filename, @"[\/\\]"))
        {
            html2pdf(fw, html_file, pdf_file, options);

            if (string.IsNullOrEmpty(out_filename))
            {
                out_filename = "output";
            }
            fw.fileResponse(pdf_file, out_filename + ".pdf", "application/pdf", (string)options["disposition"]);
            Utils.cleanupTmpFiles(); // this will cleanup temporary .pdf, can't delete immediately as file_response may not yet finish transferring file
        }
        else
            html2pdf(fw, html_file, out_filename, options);
        // remove tmp html file
        File.Delete(html_file);

        return html_data;
    }

    // !uses CONF var FW.config("pdf_converter") for converted command line
    // !and FW.config("pdf_converter_args") - MUST include %IN %OUT which will be replaced by input and output file paths accordingly
    // TODO: example: FW.config("html_converter_args")=" -po Landscape" - for landscape mode
    // all params for TotalHTMLConverter: http://www.coolutils.com/help/TotalHTMLConverter/Commandlineparameters.php
    // all params for WkHTMLtoPDF: http://wkhtmltopdf.org/usage/wkhtmltopdf.txt
    // options:
    // landscape = True - will produce landscape output
    public static void html2pdf(FW fw, string htmlfile, string filename, Hashtable options = null)
    {
        if (htmlfile.Length < 1 | filename.Length < 1)
            throw new ApplicationException("Wrong filename");
        System.Diagnostics.ProcessStartInfo info = new();
        System.Diagnostics.Process process = new();

        string cmdline = (string)FwConfig.settings["pdf_converter_args"];
        cmdline = cmdline.Replace("%IN", "\"" + htmlfile + "\"");
        cmdline = cmdline.Replace("%OUT", "\"" + filename + "\"");
        if (options != null && Utils.toBool(options["landscape"]) == true)
        {
            cmdline = " -O Landscape " + cmdline;
        }
        if (options != null && options.ContainsKey("cmd"))
        {
            cmdline = options["cmd"] + " " + cmdline;
        }
        info.FileName = (string)FwConfig.settings["pdf_converter"];
        info.Arguments = cmdline;

        fw.logger(LogLevel.DEBUG, "exec: ", info.FileName, " ", info.Arguments);
        process.StartInfo = info;
        process.Start();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            fw.logger(LogLevel.ERROR, "Exit code:", process.ExitCode);
        }
        process.Close();
    }

    // TODO - currently it just parse html and save it under .doc extension (Word capable opening it), but need redo with real converter
    // parse template and generate doc
    // if out_filename ="" or doesn't contain "\" or "/" - output pdf file to browser
    // if out_filename cotains "\" or "/" - save pdf file to this path
    public static string parsePageDoc(FW fw, ref string bdir, ref string tpl_name, ref Hashtable ps, string out_filename = "")
    {
        ParsePage parser = new(fw);
        string html_data = parser.parse_page(bdir, tpl_name, ps);

        html_data = _replace_specials(html_data);

        string html_file = Utils.getTmpFilename() + ".html";
        string doc_file = Utils.getTmpFilename() + ".doc";
        // fw.logger("INFO", "html file = " & html_file)
        // fw.logger("INFO", "doc file = " & doc_file)

        // remove_old_files()
        // TODO fw.set_file_content(html_file, html_data)
        // TEMPORARY - store html right to .doc file
        FW.setFileContent(doc_file, ref html_data);

        if (string.IsNullOrEmpty(out_filename) || !Regex.IsMatch(out_filename, @"[\/]"))
        {
            // TODO html2doc(fw, html_file, doc_file)

            if (string.IsNullOrEmpty(out_filename))
            {
                out_filename = "output";
            }
            fw.fileResponse(doc_file, out_filename + ".doc");
            Utils.cleanupTmpFiles(); // this will cleanup temporary .pdf, can't delete immediately as file_response may not yet finish transferring file
        }
        else
        {
            // TODO html2doc(fw, html_file, out_filename)
            File.Delete(out_filename);
            File.Move(doc_file, out_filename);
        }
        // remove tmp html file
        File.Delete(html_file);

        return html_data;
    }

    // using http://www.coolutils.com/TotalHTMLConverterX
    // params http://www.coolutils.com/help/TotalHTMLConverter/Commandlineparameters.php
    public static void html2xls(FW fw, string htmlfile, string xlsfile)
    {
        if (htmlfile.Length < 1 | xlsfile.Length < 1)
            throw new ApplicationException("Wrong filename");
        System.Diagnostics.ProcessStartInfo info = new();
        System.Diagnostics.Process process = new();

        info.FileName = (string)fw.config()["html_converter"];
        info.Arguments = "\"" + htmlfile + "\" \"" + xlsfile + "\" -c xls -AutoSize";
        process.StartInfo = info;
        process.Start();
        process.WaitForExit();
        process.Close();
    }

    // parse template and generate xls
    // Note: set IS_PRINT_MODE=True hf var which is become available in templates
    // if out_filename ="" or doesn't contain "\" or "/" - output pdf file to browser
    // if out_filename cotains "\" or "/" - save pdf file to this path
    public static string parsePageExcel(FW fw, ref string bdir, ref string tpl_name, ref Hashtable ps, string out_filename = "")
    {
        ParsePage parser = new(fw);
        ps["IS_PRINT_MODE"] = true;
        string html_data = parser.parse_page(bdir, tpl_name, ps);

        html_data = _replace_specials(html_data);

        string html_file = Utils.getTmpFilename() + ".html";
        string xls_file = Utils.getTmpFilename() + ".xls";
        fw.logger(LogLevel.DEBUG, "html file = ", html_file);
        fw.logger(LogLevel.DEBUG, "xls file = ", xls_file);

        // remove_old_files()
        FW.setFileContent(html_file, ref html_data);

        if (string.IsNullOrEmpty(out_filename) || !Regex.IsMatch(out_filename, @"[\/\\]"))
        {
            html2xls(fw, html_file, xls_file);

            if (string.IsNullOrEmpty(out_filename))
            {
                out_filename = "output";
            }
            fw.fileResponse(xls_file, out_filename + ".xls", "application/vnd.ms-excel");
            Utils.cleanupTmpFiles(); // this will cleanup temporary .pdf, can't delete immediately as file_response may not yet finish transferring file
        }
        else
        {
            html2xls(fw, html_file, out_filename);
        }
        // remove tmp html file
        File.Delete(html_file);

        return html_data;
    }

    // simple version of parse_page_xls - i.e. it's usual html file, just output as xls (Excel opens it successfully, however displays a warning)
    public static string parsePageExcelSimple(FW fw, string bdir, string tpl_name, Hashtable ps, string out_filename = "")
    {
        ParsePage parser = new(fw);
        ps["IS_PRINT_MODE"] = true;
        string html_data = parser.parse_page(bdir, tpl_name, ps);

        html_data = _replace_specials(html_data);

        if (string.IsNullOrEmpty(out_filename) || !Regex.IsMatch(out_filename, @"[\/\\]"))
        {
            if (string.IsNullOrEmpty(out_filename))
            {
                out_filename = "output";
            }
            // out to browser
            fw.response.Headers.Append("Content-type", "application/vnd.ms-excel");
            fw.response.Headers.Append("Content-Disposition", "attachment; filename=\"" + out_filename + ".xls\"");
            fw.responseWrite(html_data);
        }
        else
        {
            FW.setFileContent(out_filename, ref html_data);
        }

        return html_data;
    }

    // replace couple special chars
    private static string _replace_specials(string html_data)
    {
        html_data = html_data.Replace(((char)(153)).ToString(), "<sup><small>TM</small></sup>");
        html_data = html_data.Replace(((char)(174)).ToString(), "<sup><small>R</small></sup>");
        return html_data;
    }

    public static Dictionary<string, int> GetMaxCharacterWidth(ArrayList rows, List<string> headers)
    {
        var maxColWidth = new Dictionary<string, int>();

        foreach (string header in headers)
        {
            var cell = header;
            var cellValue = header;

            maxColWidth.Add(cell, cell.Length < 10 ? 10 : cell.Length);
        }

        foreach (Hashtable cells in rows)
        {
            foreach (string cell in cells.Keys)
            {
                var cellValue = Utils.toStr(cells[cell]);
                var cellTextLength = cellValue.Length;

                if (!maxColWidth.ContainsKey(cell))
                {
                    maxColWidth.Add(cell, cell.Length == 0 ? 50 : cell.Length);
                }

                if (cellTextLength > maxColWidth[cell])
                {
                    maxColWidth[cell] = cellTextLength;
                }
            }
        }

        return maxColWidth;
    }

    public static DocumentFormat.OpenXml.Spreadsheet.Columns AutoSizeCells(ArrayList rows, List<string> headers)
    {
        var maxColWidth = GetMaxCharacterWidth(rows, headers);
        var columns = new DocumentFormat.OpenXml.Spreadsheet.Columns();
        double maxWidth = 10;

        UInt32Value iter = 1;
        foreach (string item in headers)
        {
            var val = maxColWidth[item];
            var width = ((val * maxWidth + 5) / maxWidth * 256) / 256;
            DocumentFormat.OpenXml.Spreadsheet.Column col = new DocumentFormat.OpenXml.Spreadsheet.Column();
            col.BestFit = true;
            col.Min = iter;
            col.Max = iter;
            col.CustomWidth = true;
            col.Width = (double)width;
            columns.Append(col);
            iter += 1;
        }
        return columns;
    }

    public static Stylesheet GetStylesheet()
    {
        var _Fonts = new DocumentFormat.OpenXml.Spreadsheet.Fonts();
        _Fonts.Append(new DocumentFormat.OpenXml.Spreadsheet.Font());
        _Fonts.Append(new DocumentFormat.OpenXml.Spreadsheet.Font(new DocumentFormat.OpenXml.Spreadsheet.Bold()));
        _Fonts.Append(new DocumentFormat.OpenXml.Spreadsheet.Font(new DocumentFormat.OpenXml.Spreadsheet.Bold()));

        var _Fills = new DocumentFormat.OpenXml.Spreadsheet.Fills(new DocumentFormat.OpenXml.Spreadsheet.Fill());
        var _Borders = new DocumentFormat.OpenXml.Spreadsheet.Borders(new DocumentFormat.OpenXml.Spreadsheet.Border());
        _Borders.Append(new DocumentFormat.OpenXml.Spreadsheet.Border());

        var border = new DocumentFormat.OpenXml.Spreadsheet.Border();
        var topBorder = new DocumentFormat.OpenXml.Spreadsheet.TopBorder();
        topBorder.Style = BorderStyleValues.Medium;
        border.TopBorder = topBorder;
        _Borders.Append(border);

        var _CellFormats = new DocumentFormat.OpenXml.Spreadsheet.CellFormats();
        _CellFormats.Append(new DocumentFormat.OpenXml.Spreadsheet.CellFormat());

        var cellFormat = new DocumentFormat.OpenXml.Spreadsheet.CellFormat();
        cellFormat.FontId = 1;
        cellFormat.FillId = 0;
        cellFormat.BorderId = 1;
        _CellFormats.Append(cellFormat); // header

        cellFormat = new DocumentFormat.OpenXml.Spreadsheet.CellFormat();
        cellFormat.FontId = 1;
        cellFormat.FillId = 0;
        cellFormat.BorderId = 2;
        _CellFormats.Append(cellFormat); // footer

        return new Stylesheet(_Fonts, _Fills, _Borders, _CellFormats);
    }


    /// <summary>
    /// Create xls file form hashtable where key is sheet name and value is rows array of hashtables with table data
    /// * <param name="xls_export_headers">XLS headers row, comma-separated format</param>
    /// * <param name = "xls_export_fields" > empty, * or Utils.qw format</param>
    /// * <param name = "rows" > DB array</param>
    /// </summary>
    /// <param name="sheetsData"></param>
    public static string parsePageNativeExcel(FW fw, string xls_export_headers, string xls_export_fields, ArrayList rows, string out_filename = "")
    {
        SpreadsheetDocument spreadSheet = null;
        var columns = new List<string>();

        var fileName = System.IO.Path.GetTempPath() + Utils.uuid() + ".xlsx";

        try
        {
            // create the workbook
            spreadSheet = SpreadsheetDocument.Create(fileName, SpreadsheetDocumentType.Workbook);
            spreadSheet.AddWorkbookPart();
            spreadSheet.WorkbookPart.Workbook = new Workbook();
            // create the worksheet to workbook relation
            Sheets sheets = spreadSheet.WorkbookPart.Workbook.AppendChild<Sheets>(new Sheets());

            var sheetsOrder = new ArrayList();
            sheetsOrder.Add("sheet1");

            UInt32Value sheetNumber = 0;
            foreach (string sheetName in sheetsOrder)
            {
                List<String> headers = xls_export_headers.Split(',').ToList<string>();
                List<string> fields = xls_export_fields.Split(' ').ToList<string>();
                sheetNumber += 1;

                var _SheetData = new SheetData();
                var _WorksheetPart = spreadSheet.WorkbookPart.AddNewPart<WorksheetPart>();

                Sheet s = new Sheet();
                s.Id = spreadSheet.WorkbookPart.GetIdOfPart(_WorksheetPart);
                s.SheetId = sheetNumber;
                s.Name = sheetName;
                sheets.AppendChild(s);

                var headerRow = new Row();
                var i = 0;
                // create header row
                foreach (string ColumnName in fields)
                {
                    columns.Add(ColumnName);
                    var cell = new Cell();
                    cell.StyleIndex = 1;
                    cell.DataType = CellValues.String;
                    cell.CellValue = new CellValue(headers[i]);
                    headerRow.AppendChild(cell);
                    i++;
                }
                _SheetData.AppendChild(headerRow);

                // create data rows
                foreach (Hashtable row in rows)
                {
                    var newRow = new Row();
                    foreach (string col in fields)
                    {
                        var cell = new Cell();
                        cell.StyleIndex = 0;
                        // Set style index 2 for bold text, can be using to highlight totals, etc
                        //    cell.StyleIndex = 2;
                        cell.DataType = CellValues.String;
                        if (row.ContainsKey(col))
                        {
                            cell.CellValue = new CellValue(Utils.toStr(row[col]));
                        }

                        newRow.AppendChild(cell);
                    }
                    _SheetData.AppendChild(newRow);
                }

                _WorksheetPart.Worksheet = new Worksheet();
                _WorksheetPart.Worksheet.Append(AutoSizeCells(rows, fields));
                _WorksheetPart.Worksheet.Append(_SheetData);
            }

            var _StylePart = spreadSheet.WorkbookPart.AddNewPart<WorkbookStylesPart>();
            _StylePart.Stylesheet = GetStylesheet();
            _StylePart.Stylesheet.Save();

            // save workbook
            spreadSheet.WorkbookPart.Workbook.Save();
        }
        catch (Exception ex)
        {
            throw new UserException(ex.Message);
        }
        finally
        {
            spreadSheet.Dispose();
        }

        return fileName;
    }
}
