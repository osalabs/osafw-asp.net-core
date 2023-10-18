// Reports Base class
//
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Text.RegularExpressions;

namespace osafw;

public class FwReports
{
    protected FW fw;
    protected DB db;
    public string report_code;
    public string format; // report format, if empty - html, other options: html, csv, pdf, xls
    public Hashtable f; // report filters/options
                        // render options for html to pdf/xls/etc... convertor
    public Hashtable render_options = new()
    {
        {"cmd", "--page-size Letter --print-media-type"},
        {"landscape", true}
    };

    public static string cleanupRepcode(string repcode)
    {
        return Regex.Replace(repcode, @"[^\w-]", "").ToLower();
    }
    /// <summary>
    /// Convert report code into class name
    /// </summary>
    /// <param name="repcode">pax-something-summary</param>
    /// <returns>ReportPaxSomethingSummary</returns>
    /// <remarks></remarks>
    public static string repcodeToClass(string repcode)
    {
        string result = "";
        string[] pieces = repcode.Split("-");
        foreach (string piece in pieces)
            result += Utils.capitalize(piece);
        return "Report" + result;
    }

    /// <summary>
    /// Create instance of report class by repcode
    /// </summary>
    /// <param name="repcode">cleaned report code</param>
    /// <param name="f">filters passed from request</param>
    /// <returns></returns>
    public static FwReports createInstance(FW fw, string repcode, Hashtable f)
    {
        string report_class_name = repcodeToClass(repcode);
        if (string.IsNullOrEmpty(report_class_name))
            throw new UserException("Wrong Report Code");

        FwReports report = (FwReports)Activator.CreateInstance(Type.GetType(FW.FW_NAMESPACE_PREFIX + report_class_name, true));
        report.init(fw, repcode, f);
        return report;
    }

    public FwReports()
    {
    }

    public virtual void init(FW fw, string report_code, Hashtable f)
    {
        this.fw = fw;
        this.db = fw.db;
        this.report_code = report_code;
        this.f = f;
        this.format = (string)f["format"];
    }

    public virtual Hashtable getReportData()
    {
        Hashtable ps = new();
        return ps;
    }

    public virtual Hashtable getReportFilters()
    {
        Hashtable result = new();
        // result("select_something")=fw.model(of Something).listSelectOptions()
        return result;
    }

    public virtual bool saveChanges()
    {
        return false;
    }

    // render report according to format
    public virtual void render(Hashtable ps)
    {
        string base_dir = "/admin/reports/" + this.report_code;
        switch (this.format)
        {
            case "pdf":
                {
                    ((Hashtable)ps["f"])["edit"] = false; // force any edit modes off
                    ps["IS_EXPORT_PDF"] = true; //use as <~PARSEPAGE.TOP[IS_EXPORT_PDF]> in templates
                    ConvUtils.parsePagePdf(fw, base_dir, (string)fw.config("PAGE_LAYOUT_PRINT"), ps, report_code, render_options);
                    break;
                }

            case "xls":
                {
                    ps["IS_EXPORT_XLS"] = true; //use as <~PARSEPAGE.TOP[IS_EXPORT_XLS]> in templates
                    ConvUtils.parsePageExcelSimple(fw, base_dir, "/admin/reports/common/xls.html", ps, report_code);
                    break;
                }

            case "csv":
                {
                    var rep = (Hashtable)ps["rep"];
                    Utils.writeCSVExport(fw.response, report_code + ".csv", "", "", (ArrayList)rep["rows"]);
                    break;
                }

            default:
                {
                    // html
                    // show report using templates from related report dir
                    fw.parser(base_dir, ps);
                    break;
                }
        }
    }

    // REPORT HELPERS

    // add "perc" value for each row (percentage of row's "ctr" from sum of all ctr)
    protected int _calcPerc(ArrayList rows)
    {
        int total_ctr = 0;
        foreach (Hashtable row in rows)
            total_ctr += Utils.f2int(row["ctr"]);
        if (total_ctr > 0)
        {
            foreach (Hashtable row in rows)
                row["perc"] = Utils.f2int(row["ctr"]) / (double)total_ctr * 100;
        }
        return total_ctr;
    }
}