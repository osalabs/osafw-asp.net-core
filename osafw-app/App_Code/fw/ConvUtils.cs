using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Playwright;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace osafw;

public class ConvUtils
{
    private static bool playwrightInstalled = false; // to avoid multiple installs in parallel requests
    private static readonly object playwrightLock = new();

    public static void ensurePlaywrightInstalled(FW fw)
    {
        if (playwrightInstalled) return;
        lock (playwrightLock)
        {
            if (playwrightInstalled) return;
            // Read PLAYWRIGHT_BROWSERS_PATH from config
            string browsersPath = fw.config("PLAYWRIGHT_BROWSERS_PATH").toStr();
            if (string.IsNullOrEmpty(browsersPath))
            {
                Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsersPath);
            }

            try
            {
                Microsoft.Playwright.Program.Main([
                    "install",
                    "chromium",
                    "--with-deps",
                    "--no-shell"
                ]);
                playwrightInstalled = true;
            }
            catch (Exception ex)
            {
                // Optionally log error
                fw.logger(LogLevel.ERROR, "Failed to install Playwright: ", ex.Message);
            }
        }
    }



    // parse template and generate pdf
    // bdir - base directory for templates, can contain:
    //    - tpl_name file for layout template
    //    - "pdf_header.html" and "pdf_footer.html" files for header and footer (note, these fields should contain their own css styles)
    // Note: set IS_PRINT_MODE=True hf var which is become available in templates
    // if out_filename ="" or doesn't contain "\" or "/" - output pdf file to browser
    // if out_filename cotains "\" or "/" - save pdf file to this path
    // options:
    // see html2pdf() method for options
    public static string parsePagePdf(FW fw, string bdir, string tpl_name, FwDict ps, string out_filename = "", FwDict? options = null)
    {
        ensurePlaywrightInstalled(fw);

        options ??= [];
        if (!options.TryGetValue("disposition", out object? value))
        {
            value = "attachment";
            options["disposition"] = value;
        }

        ps["IS_PRINT_MODE"] = true;
        string html_data = fw.parsePage(bdir, tpl_name, ps);
        options["header"] = fw.parsePage(bdir, "pdf_header.html", ps);
        options["footer"] = fw.parsePage(bdir, "pdf_footer.html", ps);

        html_data = _replace_specials(html_data);

        string pdf_file = Utils.getTmpFilename() + ".pdf";
        // fw.logger("INFO", "pdf file = " & pdf_file)

        if (string.IsNullOrEmpty(out_filename) || !Regex.IsMatch(out_filename, @"[\/\\]"))
        {
            html2pdf(fw, html_data, pdf_file, options).GetAwaiter().GetResult();

            if (string.IsNullOrEmpty(out_filename))
            {
                out_filename = "output";
            }
            fw.fileResponse(pdf_file, out_filename + ".pdf", "application/pdf", value.toStr());
            Utils.cleanupTmpFiles(); // this will cleanup temporary .pdf, can't delete immediately as file_response may not yet finish transferring file
        }
        else
        {
            html2pdf(fw, html_data, out_filename, options).GetAwaiter().GetResult();
        }

        return html_data;
    }

    // options:
    // landscape = True - will produce landscape output
    // header = HTML string to be used as header
    // footer = HTML string to be used as footer
    // scale = float value to scale the output (default 0.8)
    // set margins:
    // margin_top = "5mm"
    // margin_right = "10mm"
    // margin_bottom = "5mm"
    // margin_left = "10mm"
    public static async Task html2pdf(FW fw, string html_data, string filename, FwDict? options = null)
    {
        if (filename.Length < 1)
        {
            throw new ApplicationException("Wrong filename");
        }

        options ??= [];

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Channel = "chromium"
            });

            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();

            await page.SetContentAsync(html_data);

            var pdfOptions = new PagePdfOptions
            {
                Path = filename,
                Format = "Letter",
                PrintBackground = true,
                Margin = new Margin
                {
                    Top = options.TryGetValue("margin_top", out object? value) ? value.toStr() : "5mm",
                    Right = options.TryGetValue("margin_right", out object? value1) ? value1.toStr() : "10mm",
                    Bottom = options.TryGetValue("margin_bottom", out object? value2) ? value2.toStr() : "5mm",
                    Left = options.TryGetValue("margin_left", out object? value3) ? value3.toStr() : "10mm"
                },
                Landscape = options["landscape"].toBool(),
                Scale = options.TryGetValue("scale", out object? value4) ? value4.toFloat() : 0.8f,
                DisplayHeaderFooter = !String.IsNullOrEmpty(options["footer"].toStr()) || !String.IsNullOrEmpty(options["header"].toStr()),
                HeaderTemplate = options["header"].toStr(),
                FooterTemplate = options["footer"].toStr(),
                PreferCSSPageSize = false,
                Tagged = true
            };

            await page.PdfAsync(pdfOptions);
        }
        catch (Exception ex)
        {
            fw.logger(LogLevel.ERROR, "PDF generation failed: ", ex.Message);
            throw;
        }
    }

    // TODO - currently it just parse html and save it under .doc extension (Word capable opening it), but need redo with real converter
    // parse template and generate doc
    // if out_filename ="" or doesn't contain "\" or "/" - output pdf file to browser
    // if out_filename cotains "\" or "/" - save pdf file to this path
    public static string parsePageDoc(FW fw, ref string bdir, ref string tpl_name, ref FwDict ps, string out_filename = "")
    {
        string html_data = fw.parsePage(bdir, tpl_name, ps);

        html_data = _replace_specials(html_data);

        string html_file = Utils.getTmpFilename() + ".html";
        string doc_file = Utils.getTmpFilename() + ".doc";
        // fw.logger("INFO", "html file = " & html_file)
        // fw.logger("INFO", "doc file = " & doc_file)

        // remove_old_files()
        // TODO fw.set_file_content(html_file, html_data)
        // TEMPORARY - store html right to .doc file
        Utils.setFileContent(doc_file, ref html_data);

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

        var converterPath = fw.config("html_converter").toStr();
        if (string.IsNullOrEmpty(converterPath))
            throw new ApplicationException("html_converter path is not configured");

        info.FileName = converterPath;
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
    public static string parsePageExcel(FW fw, ref string bdir, ref string tpl_name, ref FwDict ps, string out_filename = "")
    {
        ps["IS_PRINT_MODE"] = true;
        string html_data = fw.parsePage(bdir, tpl_name, ps);

        html_data = _replace_specials(html_data);

        string html_file = Utils.getTmpFilename() + ".html";
        string xls_file = Utils.getTmpFilename() + ".xls";
        fw.logger(LogLevel.DEBUG, "html file = ", html_file);
        fw.logger(LogLevel.DEBUG, "xls file = ", xls_file);

        // remove_old_files()
        Utils.setFileContent(html_file, ref html_data);

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
    public static string parsePageExcelSimple(FW fw, string bdir, string tpl_name, FwDict ps, string out_filename = "")
    {
        ps["IS_PRINT_MODE"] = true;
        string html_data = fw.parsePage(bdir, tpl_name, ps);

        html_data = _replace_specials(html_data);

        if (string.IsNullOrEmpty(out_filename) || !Regex.IsMatch(out_filename, @"[\/\\]"))
        {
            if (string.IsNullOrEmpty(out_filename))
            {
                out_filename = "output";
            }
            // out to browser
            var response = fw.response ?? throw new UserException("Response is not available");
            response.Headers.ContentType = "application/vnd.ms-excel";
            response.Headers.ContentDisposition = $"attachment; filename=\"{out_filename}.xls\"";
            fw.responseWrite(html_data);
        }
        else
        {
            Utils.setFileContent(out_filename, ref html_data);
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

    private static Dictionary<string, int> xlsxGetMaxCharacterWidth(FwList rows, IEnumerable<string> headers)
    {
        var maxColWidth = new Dictionary<string, int>();

        foreach (string header in headers)
        {
            maxColWidth.Add(header, header.Length < 10 ? 10 : header.Length);
        }

        foreach (FwDict cells in rows)
        {
            foreach (string cell in cells.Keys)
            {
                var cellValue = cells[cell].toStr();
                var cellTextLength = cellValue.Length;

                if (!maxColWidth.ContainsKey(cell))
                    maxColWidth.Add(cell, cell.Length == 0 ? 50 : cell.Length);

                if (cellTextLength > maxColWidth[cell])
                    maxColWidth[cell] = cellTextLength;
            }
        }

        return maxColWidth;
    }

    private static Columns xlxsAutoSizeCells(FwList rows, IEnumerable<string> headers)
    {
        var maxColWidth = xlsxGetMaxCharacterWidth(rows, headers);
        var columns = new Columns();
        double maxWidth = 10;

        UInt32Value iter = 1;
        foreach (string item in headers)
        {
            var val = maxColWidth[item];
            var width = ((val * maxWidth + 5) / maxWidth * 256) / 256;
            Column col = new Column
            {
                BestFit = true,
                Min = iter,
                Max = iter,
                CustomWidth = true,
                Width = (double)width
            };
            columns.Append(col);
            iter += 1;
        }
        return columns;
    }

    private static Stylesheet xlsxStylesheet()
    {
        var _Fonts = new Fonts();
        _Fonts.Append(new Font());
        _Fonts.Append(new Font(new Bold()));
        _Fonts.Append(new Font(new Bold()));

        var _Fills = new Fills(new Fill());
        var _Borders = new Borders(new Border());
        _Borders.Append(new Border());

        var border = new Border();
        var topBorder = new TopBorder
        {
            Style = BorderStyleValues.Medium
        };
        border.TopBorder = topBorder;
        _Borders.Append(border);

        var _CellFormats = new CellFormats();
        _CellFormats.Append(new CellFormat());

        var cellFormat = new CellFormat
        {
            FontId = 1,
            FillId = 0,
            BorderId = 1
        };
        _CellFormats.Append(cellFormat); // header

        cellFormat = new CellFormat
        {
            FontId = 1,
            FillId = 0,
            BorderId = 2
        };
        _CellFormats.Append(cellFormat); // footer

        return new Stylesheet(_Fonts, _Fills, _Borders, _CellFormats);
    }

    /// <summary>
    /// Create xls file form hashtable where key is sheet name and value is rows array of hashtables with table data
    /// * <param name = "headers">XLS headers row, comma-separated format</param>
    /// * <param name = "xls_export_fields" > empty, * or Utils.qw format</param>
    /// * <param name = "rows" > DB array</param>
    /// </summary>
    /// <param name="sheetsData"></param>


    /// <summary>
    /// Create native xlsx file 
    /// </summary>
    /// <param name="fw"></param>
    /// <param name="headers"></param>
    /// <param name="fields"></param>
    /// <param name="rows"></param>
    /// <param name="out_filename">if empty or set to just filename (no path) - write to browser, if path - write to file</param>
    /// <returns></returns>
    /// <exception cref="UserException"></exception>
    public static void exportNativeExcel(FW fw, IList<string> headers, IEnumerable<string> fields, FwList rows, string out_filename = "")
    {
        var is_browser = false;
        if (string.IsNullOrEmpty(out_filename) || !Regex.IsMatch(out_filename, @"[\/\\]"))
        {
            is_browser = true;
            if (string.IsNullOrEmpty(out_filename))
                out_filename = "output";
        }

        var fileName = is_browser ? Path.GetTempPath() + Utils.uuid() + ".xlsx" : out_filename;

        // create the workbook
        using (var doc = SpreadsheetDocument.Create(fileName, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            // create the worksheet to workbook relation
            Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());

            var sheetsOrder = new StrList();
            sheetsOrder.Add("Sheet1");

            UInt32Value sheetNumber = 0;
            foreach (string sheetName in sheetsOrder)
            {
                sheetNumber += 1;

                var _SheetData = new SheetData();
                var _WorksheetPart = workbookPart.AddNewPart<WorksheetPart>();

                    var s = new Sheet
                    {
                        Id = workbookPart.GetIdOfPart(_WorksheetPart),
                        SheetId = sheetNumber,
                        Name = sheetName
                    };
                sheets.AppendChild(s);

                var headerRow = new Row();
                var i = 0;
                // create header row
                foreach (string ColumnName in fields)
                {
                    var cell = new Cell
                    {
                        StyleIndex = 1,
                        DataType = CellValues.String,
                        CellValue = new CellValue(headers[i])
                    };
                    headerRow.AppendChild(cell);
                    i++;
                }
                _SheetData.AppendChild(headerRow);

                // create data rows
                foreach (FwDict row in rows)
                {
                    var newRow = new Row();
                    foreach (string col in fields)
                    {
                        var cell = new Cell
                        {
                            StyleIndex = 0,
                            // Set style index 2 for bold text, can be using to highlight totals, etc
                            //    .StyleIndex = 2;
                            DataType = CellValues.String
                        };
                        if (row.TryGetValue(col, out object? value))
                        {
                            cell.CellValue = new CellValue(value.toStr());
                        }

                        newRow.AppendChild(cell);
                    }
                    _SheetData.AppendChild(newRow);
                }

                _WorksheetPart.Worksheet = new Worksheet();
                _WorksheetPart.Worksheet.Append(xlxsAutoSizeCells(rows, fields));
                _WorksheetPart.Worksheet.Append(_SheetData);
            }

                var _StylePart = workbookPart.AddNewPart<WorkbookStylesPart>();
                _StylePart.Stylesheet = xlsxStylesheet();
                _StylePart.Stylesheet.Save();

                // save workbook
                workbookPart.Workbook.Save();
            }

        if (is_browser)
        {
            fw.fileResponse(fileName, out_filename + ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "attachment");
            Utils.cleanupTmpFiles();
        }

    }
}
