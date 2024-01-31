using Microsoft.AspNetCore.Http;
using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;

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
        if (options != null && Utils.f2bool(options["landscape"]) == true)
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
}
